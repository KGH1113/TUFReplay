//! Published defaults are editable only through authenticated TUF service calls.
//! Viewer choices never modify these rows or the recorded gameplay evidence.
use loco_rs::prelude::*;
use sea_orm::{DbBackend, FromQueryResult, Statement, TransactionTrait, Value};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

#[derive(Debug, Clone, Serialize, FromQueryResult)]
pub struct PresetOption {
    pub id: Uuid,
    pub name: String,
    pub kind: String,
    pub source: String,
    pub is_hidden: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize, Default, FromQueryResult)]
#[serde(deny_unknown_fields)]
pub struct Defaults {
    pub keyviewer_id: Option<Uuid>,
    pub overlay_id: Option<Uuid>,
}

#[derive(Serialize)]
pub struct Options {
    pub presets: Vec<PresetOption>,
    pub defaults: Defaults,
}

#[derive(FromQueryResult)]
struct Publication {
    id: i64,
    owner_id: String,
    external_pass_id: i64,
}

async fn publication(db: &impl ConnectionTrait, run_id: Uuid) -> Result<Publication> {
    Publication::find_by_statement(sql(
        "SELECT s.id,s.owner_id,s.external_pass_id FROM run_submission_records s
         JOIN run_sessions r ON r.id=s.run_session_id
         WHERE r.pid=$1 AND s.state='submitted' AND s.external_pass_id IS NOT NULL
         AND s.manifest IS NOT NULL AND s.validation IS NOT NULL",
        vec![run_id.into()],
    ))
    .one(db)
    .await?
    .ok_or(Error::NotFound)
}

async fn owned_publication(
    db: &impl ConnectionTrait,
    run_id: Uuid,
    pass_id: i64,
    owner: &str,
) -> Result<Publication> {
    let record = publication(db, run_id).await?;
    if record.owner_id != owner || record.external_pass_id != pass_id {
        return Err(Error::NotFound);
    }
    Ok(record)
}

async fn options(
    db: &impl ConnectionTrait,
    record: &Publication,
    include_hidden: bool,
) -> Result<Options> {
    let presets = PresetOption::find_by_statement(sql(
        "SELECT id,name,kind,source,(hidden_at IS NOT NULL) AS is_hidden FROM visual_presets
         WHERE owner_id=$1 AND deleted_at IS NULL AND ($2 OR hidden_at IS NULL)
         ORDER BY created_at DESC,id DESC",
        vec![record.owner_id.clone().into(), include_hidden.into()],
    ))
    .all(db)
    .await?;
    let mut defaults = Defaults::find_by_statement(sql(
        "SELECT keyviewer_id,overlay_id FROM run_visual_selections WHERE run_submission_record_id=$1",
        vec![record.id.into()]
    )).one(db).await?.unwrap_or_default();
    for (kind, id) in [
        ("keyviewer", &mut defaults.keyviewer_id),
        ("overlay", &mut defaults.overlay_id),
    ] {
        if !presets
            .iter()
            .any(|p| Some(p.id) == *id && p.kind == kind && !p.is_hidden)
        {
            *id = None;
        }
    }
    Ok(Options { presets, defaults })
}

pub async fn public_options(db: &DatabaseConnection, run_id: Uuid) -> Result<Options> {
    options(db, &publication(db, run_id).await?, false).await
}

pub async fn owner_options(
    db: &DatabaseConnection,
    run_id: Uuid,
    pass_id: i64,
    owner: &str,
) -> Result<Options> {
    options(
        db,
        &owned_publication(db, run_id, pass_id, owner).await?,
        true,
    )
    .await
}

pub async fn save_defaults(
    db: &DatabaseConnection,
    run_id: Uuid,
    pass_id: i64,
    owner: &str,
    defaults: &Defaults,
) -> Result<Options> {
    let txn = db.begin().await?;
    let record = owned_publication(&txn, run_id, pass_id, owner).await?;
    // Lock in UUID order, shared with visibility changes, before touching defaults.
    let mut requested: Vec<_> = [
        (defaults.keyviewer_id, "keyviewer"),
        (defaults.overlay_id, "overlay"),
    ]
    .into_iter()
    .filter_map(|(id, kind)| id.map(|id| (id, kind)))
    .collect();
    requested.sort_by_key(|(id, _)| *id);
    for (id, kind) in requested {
        let found = PresetOption::find_by_statement(sql(
            "SELECT id,name,kind,source,FALSE AS is_hidden FROM visual_presets
             WHERE id=$1 AND owner_id=$2 AND kind=$3 AND hidden_at IS NULL AND deleted_at IS NULL FOR SHARE",
            vec![id.into(),owner.into(),kind.into()]
        )).one(&txn).await?;
        if found.is_none() {
            return Err(Error::BadRequest("visual_preset_not_found".into()));
        }
    }
    txn.execute_raw(sql(
        "INSERT INTO run_visual_selections (run_submission_record_id,keyviewer_id,overlay_id)
         VALUES ($1,$2,$3) ON CONFLICT (run_submission_record_id) DO UPDATE
         SET keyviewer_id=EXCLUDED.keyviewer_id,overlay_id=EXCLUDED.overlay_id",
        vec![
            record.id.into(),
            defaults.keyviewer_id.into(),
            defaults.overlay_id.into(),
        ],
    ))
    .await?;
    let result = options(&txn, &record, true).await?;
    txn.commit().await?;
    Ok(result)
}

pub async fn set_hidden(
    db: &DatabaseConnection,
    run_id: Uuid,
    pass_id: i64,
    owner: &str,
    preset_id: Uuid,
    hidden: bool,
) -> Result<Options> {
    let txn = db.begin().await?;
    let record = owned_publication(&txn, run_id, pass_id, owner).await?;
    let updated = txn
        .execute_raw(sql(
            "UPDATE visual_presets SET hidden_at=CASE WHEN $3 THEN NOW() ELSE NULL END
         WHERE id=$1 AND owner_id=$2 AND deleted_at IS NULL",
            vec![preset_id.into(), owner.into(), hidden.into()],
        ))
        .await?;
    if updated.rows_affected() != 1 {
        return Err(Error::NotFound);
    }
    if hidden {
        txn.execute_raw(sql(
            "UPDATE run_visual_selections v SET
             keyviewer_id=CASE WHEN v.keyviewer_id=$1 THEN NULL ELSE v.keyviewer_id END,
             overlay_id=CASE WHEN v.overlay_id=$1 THEN NULL ELSE v.overlay_id END
             FROM run_submission_records s WHERE s.id=v.run_submission_record_id
             AND s.owner_id=$2 AND s.state='submitted' AND (v.keyviewer_id=$1 OR v.overlay_id=$1)",
            vec![preset_id.into(), owner.into()],
        ))
        .await?;
    }
    let result = options(&txn, &record, true).await?;
    txn.commit().await?;
    Ok(result)
}

fn sql(query: &str, values: Vec<Value>) -> Statement {
    Statement::from_sql_and_values(DbBackend::Postgres, query, values)
}
