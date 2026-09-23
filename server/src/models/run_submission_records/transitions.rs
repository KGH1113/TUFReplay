pub struct Transition<'a> {
    pub from: &'a str,
    pub to: &'a str,
    pub reason: Option<&'a str>,
    pub validation: Option<serde_json::Value>,
    pub pass: Option<i64>,
}

use super::{ActiveModel, Column, Entity, Model};
use crate::models::{run_visual_selections, visual_presets};
use loco_rs::prelude::*;
use sea_orm::{ActiveValue::Set, EntityTrait, FromQueryResult, QueryFilter};
use sea_orm::{ConnectionTrait, DbBackend, Statement, TransactionTrait, Value};
use serde_json::Value as Json;
use uuid::Uuid;

#[derive(Debug, FromQueryResult)]
struct SubmissionLock {
    id: i64,
    requested_at: Option<chrono::DateTime<chrono::FixedOffset>>,
    state: String,
}

impl Entity {
    pub async fn create(db: &impl ConnectionTrait, run_id: i64, owner: &str) -> Result<()> {
        Self::create_authorized(db, run_id, owner, None).await
    }

    pub async fn create_authorized(
        db: &impl ConnectionTrait,
        run_id: i64,
        owner: &str,
        grant: Option<Uuid>,
    ) -> Result<()> {
        ActiveModel {
            run_session_id: Set(run_id),
            owner_id: Set(owner.to_owned()),
            oauth_grant_id: Set(grant),
            state: Set("uploading".into()),
            ..Default::default()
        }
        .insert(db)
        .await?;
        Ok(())
    }

    pub async fn record(db: &impl ConnectionTrait, run_id: i64) -> Result<Model> {
        <Self as EntityTrait>::find()
            .filter(Column::RunSessionId.eq(run_id))
            .one(db)
            .await?
            .ok_or(Error::NotFound)
    }

    pub async fn owned(db: &impl ConnectionTrait, run_id: i64, owner: &str) -> Result<Model> {
        <Self as EntityTrait>::find()
            .filter(Column::RunSessionId.eq(run_id))
            .filter(Column::OwnerId.eq(owner))
            .one(db)
            .await?
            .ok_or(Error::NotFound)
    }

    /// A DB lease prevents cleanup/retry workers from publishing over each other.
    pub async fn lease(db: &impl ConnectionTrait, run_id: i64, owner: Uuid) -> Result<bool> {
        let result = db
            .execute_raw(sql(
                "UPDATE run_submission_records
            SET lease_owner=$2,lease_until=NOW()+INTERVAL '5 minutes',updated_at=NOW()
            WHERE run_session_id=$1 AND state IN ('uploading','validation_pending','registering')
            AND (next_attempt_at IS NULL OR next_attempt_at<=NOW())
            AND (lease_until IS NULL OR lease_until<NOW())",
                vec![run_id.into(), owner.into()],
            ))
            .await?;
        Ok(result.rows_affected() == 1)
    }

    pub async fn release(db: &impl ConnectionTrait, run_id: i64, owner: Uuid) -> Result<()> {
        db.execute_raw(sql(
            "UPDATE run_submission_records SET lease_until=NULL,lease_owner=NULL
            WHERE run_session_id=$1 AND lease_owner=$2",
            vec![run_id.into(), owner.into()],
        ))
        .await?;
        Ok(())
    }

    pub async fn publish(
        db: &impl ConnectionTrait,
        run_id: i64,
        owner: Uuid,
        manifest: Json,
    ) -> Result<()> {
        let result = db
            .execute_raw(sql(
                "UPDATE run_submission_records
            SET manifest=$3,state='evidence_ready',reason=NULL,updated_at=NOW(),
                evidence_expires_at=NULL
            WHERE run_session_id=$1 AND lease_owner=$2 AND lease_until>NOW()
            AND manifest IS NULL AND state<>'deleted'",
                vec![run_id.into(), owner.into(), manifest.into()],
            ))
            .await?;
        if result.rows_affected() != 1 {
            return Err(Error::Message("persistence lease lost".into()));
        }
        Ok(())
    }

    pub async fn request(db: &impl ConnectionTrait, run_id: i64, owner: &str) -> Result<()> {
        Self::request_authorized(db, run_id, owner, None).await
    }

    pub async fn request_authorized(
        db: &impl ConnectionTrait,
        run_id: i64,
        owner: &str,
        grant: Option<Uuid>,
    ) -> Result<()> {
        Self::owned(db, run_id, owner).await?;
        let updated = db.execute_raw(sql("UPDATE run_submission_records
            SET state=CASE WHEN state='registration_error' THEN 'registering' ELSE 'validation_pending' END,
            requested_at=COALESCE(requested_at,NOW()),reason=NULL,updated_at=NOW(),retry_count=0,next_attempt_at=NULL,
            oauth_grant_id=COALESCE($3,oauth_grant_id)
            WHERE run_session_id=$1 AND owner_id=$2 AND manifest IS NOT NULL
            AND (evidence_expires_at IS NULL OR evidence_expires_at>NOW() OR state='registration_error')
            AND state IN ('evidence_ready','validation_error','validator_unavailable','registration_error')",
            vec![run_id.into(),owner.into(),grant.into()])).await?;
        if updated.rows_affected() == 0 {
            let record = Self::owned(db, run_id, owner).await?;
            if !matches!(
                record.state.as_str(),
                "validation_pending" | "registering" | "submitted"
            ) {
                return Err(Error::BadRequest("submission_not_available".into()));
            }
        }
        Ok(())
    }

    /// Freeze the optional visual selection together with the existing
    /// submission state transition. A bodyless retry leaves the frozen value
    /// unchanged, while an explicit retry must match it by preset IDs.
    pub async fn request_authorized_with_selection(
        db: &DatabaseConnection,
        run_id: i64,
        owner: &str,
        grant: Option<Uuid>,
        requested_selection: Option<run_visual_selections::VisualSelection>,
    ) -> Result<()> {
        let transaction = db.begin().await?;
        let locked = SubmissionLock::find_by_statement(sql(
            "SELECT id,requested_at,state
             FROM run_submission_records
             WHERE run_session_id=$1 AND owner_id=$2
             FOR UPDATE",
            vec![run_id.into(), owner.into()],
        ))
        .one(&transaction)
        .await?
        .ok_or(Error::NotFound)?;

        let frozen = run_visual_selections::lock_for_update(&transaction, locked.id).await?;
        match frozen {
            Some(frozen) => {
                if requested_selection
                    .as_ref()
                    .is_some_and(|requested| *requested != frozen.selection())
                {
                    return Err(Error::BadRequest("visual_selection_conflict".into()));
                }
            }
            None => {
                let selection =
                    requested_selection.unwrap_or(run_visual_selections::VisualSelection {
                        keyviewer_id: None,
                        overlay_id: None,
                    });
                // Records that already entered submission before this
                // migration are permanently legacy-empty. The migration
                // backfills these rows, and this guard covers an interrupted
                // or manually repaired database as well.
                if (selection.keyviewer_id.is_some() || selection.overlay_id.is_some())
                    && (locked.requested_at.is_some() || locked.state == "submitted")
                {
                    return Err(Error::BadRequest("visual_selection_conflict".into()));
                }
                if selection.keyviewer_id.is_some() || selection.overlay_id.is_some() {
                    validate_selection(&transaction, owner, &selection).await?;
                }
                run_visual_selections::insert(&transaction, locked.id, &selection).await?;
            }
        }

        Self::request_authorized(&transaction, run_id, owner, grant).await?;
        transaction.commit().await?;
        Ok(())
    }

    pub async fn transition(
        db: &impl ConnectionTrait,
        run_id: i64,
        lease: Uuid,
        change: Transition<'_>,
    ) -> Result<bool> {
        let Transition {
            from,
            to,
            reason,
            validation,
            pass,
        } = change;
        Ok(db.execute_raw(sql("UPDATE run_submission_records SET state=$4,reason=$5,
            validation=COALESCE($6,validation),external_pass_id=COALESCE($7,external_pass_id),updated_at=NOW(),
            evidence_expires_at=CASE WHEN $4='submitted' THEN NULL ELSE evidence_expires_at END,
            retry_count=0,next_attempt_at=NULL
            WHERE run_session_id=$1 AND lease_owner=$2 AND lease_until>NOW() AND state=$3",
            vec![run_id.into(),lease.into(),from.into(),to.into(),reason.into(),validation.into(),pass.into()]))
            .await?.rows_affected() == 1)
    }

    pub async fn delete(db: &impl ConnectionTrait, run_id: i64, owner: &str) -> Result<bool> {
        Self::owned(db, run_id, owner).await?;
        Ok(db.execute_raw(sql("UPDATE run_submission_records SET state='deleted',updated_at=NOW()
            WHERE run_session_id=$1 AND owner_id=$2 AND state IN
            ('evidence_ready','validator_unavailable','validation_error','validation_rejected','registration_rejected','evidence_invalid')
            AND (lease_until IS NULL OR lease_until<NOW())",vec![run_id.into(),owner.into()]))
            .await?.rows_affected()==1)
    }
}

async fn validate_selection(
    db: &impl ConnectionTrait,
    owner: &str,
    selection: &run_visual_selections::VisualSelection,
) -> Result<()> {
    for (id, kind) in [
        (
            selection.keyviewer_id,
            visual_presets::VisualKind::Keyviewer,
        ),
        (selection.overlay_id, visual_presets::VisualKind::Overlay),
    ] {
        let Some(id) = id else { continue };
        let Some(preset) = visual_presets::active_owned(db, owner, id).await? else {
            return Err(crate::services::visuals::preset_not_found());
        };
        if preset.kind != kind.as_str() {
            return Err(Error::BadRequest("visual_selection_conflict".into()));
        }
    }
    Ok(())
}

pub(crate) fn sql(query: &str, values: Vec<Value>) -> Statement {
    Statement::from_sql_and_values(DbBackend::Postgres, query, values)
}
