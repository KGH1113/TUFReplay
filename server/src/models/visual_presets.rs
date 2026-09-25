//! Query models for immutable visual snapshots.
//!
//! These rows use raw SQL because the repository's generated Sea-ORM entities
//! are managed by the Loco generator; keeping the result shapes here avoids
//! hand-editing generated files while the generator schema catches up.

use chrono::{DateTime, FixedOffset};
use loco_rs::prelude::*;
use sea_orm::{DbBackend, FromQueryResult, SqlErr, Statement, Value};
use serde::Serialize;
use uuid::Uuid;

const MAX_BUNDLE_BYTES: usize = 64 * 1024 * 1024;

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum VisualKind {
    Keyviewer,
    Overlay,
}

impl VisualKind {
    #[must_use]
    pub const fn as_str(self) -> &'static str {
        match self {
            Self::Keyviewer => "keyviewer",
            Self::Overlay => "overlay",
        }
    }

    #[must_use]
    pub fn parse(value: &str) -> Option<Self> {
        match value {
            "keyviewer" => Some(Self::Keyviewer),
            "overlay" => Some(Self::Overlay),
            _ => None,
        }
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum VisualSource {
    JipperResourcePack,
    Dmnote,
    ImplDmnote,
    JipperKeyviewer,
    ImplResourcePack,
}

impl VisualSource {
    #[must_use]
    pub const fn as_str(self) -> &'static str {
        match self {
            Self::JipperResourcePack => "jipper-resourcepack",
            Self::Dmnote => "dmnote",
            Self::ImplDmnote => "impl-dmnote",
            Self::JipperKeyviewer => "jipper-keyviewer",
            Self::ImplResourcePack => "impl-resourcepack",
        }
    }

    #[must_use]
    pub const fn is_dmnote(self) -> bool {
        matches!(self, Self::Dmnote | Self::ImplDmnote)
    }

    pub const fn supports(self, kind: VisualKind) -> bool {
        match self {
            Self::Dmnote | Self::ImplDmnote | Self::JipperKeyviewer => {
                matches!(kind, VisualKind::Keyviewer)
            }
            Self::ImplResourcePack => matches!(kind, VisualKind::Overlay),
            Self::JipperResourcePack => true,
        }
    }

    pub fn parse(value: &str) -> Option<Self> {
        match value {
            "jipper-resourcepack" => Some(Self::JipperResourcePack),
            "dmnote" => Some(Self::Dmnote),
            "impl-dmnote" => Some(Self::ImplDmnote),
            "jipper-keyviewer" => Some(Self::JipperKeyviewer),
            "impl-resourcepack" => Some(Self::ImplResourcePack),
            _ => None,
        }
    }
}

#[derive(Clone, Debug, Serialize, FromQueryResult)]
pub struct VisualPresetMetadata {
    pub id: Uuid,
    pub name: String,
    pub kind: String,
    pub source: String,
    pub source_version: String,
    pub created_at: DateTime<FixedOffset>,
}

#[derive(Clone, Debug, FromQueryResult)]
pub struct VisualPresetBundle {
    pub id: Uuid,
    pub name: String,
    pub kind: String,
    pub source: String,
    pub source_version: String,
    pub bundle: Vec<u8>,
    pub sha256: String,
    pub bytes: i64,
}

pub async fn list(db: &DatabaseConnection, owner_id: &str) -> Result<Vec<VisualPresetMetadata>> {
    Ok(VisualPresetMetadata::find_by_statement(statement(
        "SELECT id,name,kind,source,source_version,created_at
         FROM visual_presets
         WHERE owner_id=$1 AND deleted_at IS NULL
         ORDER BY created_at DESC,id DESC",
        vec![owner_id.into()],
    ))
    .all(db)
    .await?)
}

#[allow(clippy::too_many_arguments)]
pub async fn create(
    db: &DatabaseConnection,
    owner_id: &str,
    name: &str,
    kind: VisualKind,
    source: VisualSource,
    source_version: &str,
    bundle: Vec<u8>,
    sha256: &str,
) -> Result<VisualPresetMetadata> {
    if bundle.is_empty() || bundle.len() > MAX_BUNDLE_BYTES {
        return Err(Error::BadRequest("visual_payload_too_large".into()));
    }
    if !source.supports(kind) {
        return Err(Error::BadRequest("visual_source_unsupported".into()));
    }

    let bundle_bytes = bundle.len() as i64;
    let row = VisualPresetMetadata::find_by_statement(statement(
        "INSERT INTO visual_presets
            (id,owner_id,name,kind,source,source_version,bundle,sha256,bytes)
         VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)
         ON CONFLICT DO NOTHING
         RETURNING id,name,kind,source,source_version,created_at",
        vec![
            Uuid::new_v4().into(),
            owner_id.into(),
            name.into(),
            kind.as_str().into(),
            source.as_str().into(),
            source_version.into(),
            bundle.into(),
            sha256.into(),
            bundle_bytes.into(),
        ],
    ))
    .one(db)
    .await?;

    row.ok_or_else(|| Error::BadRequest("visual_name_taken".into()))
}

pub async fn active_owned(
    db: &impl ConnectionTrait,
    owner_id: &str,
    id: Uuid,
) -> Result<Option<VisualPresetMetadata>> {
    Ok(VisualPresetMetadata::find_by_statement(statement(
        "SELECT id,name,kind,source,source_version,created_at
         FROM visual_presets
         WHERE id=$1 AND owner_id=$2 AND deleted_at IS NULL",
        vec![id.into(), owner_id.into()],
    ))
    .one(db)
    .await?)
}

pub async fn active_bundle(
    db: &impl ConnectionTrait,
    id: Uuid,
    kind: VisualKind,
) -> Result<Option<VisualPresetBundle>> {
    Ok(VisualPresetBundle::find_by_statement(statement(
        "SELECT id,name,kind,source,source_version,bundle,sha256,bytes
         FROM visual_presets
         WHERE id=$1 AND kind=$2 AND deleted_at IS NULL",
        vec![id.into(), kind.as_str().into()],
    ))
    .one(db)
    .await?)
}

pub async fn active_bundle_for_published_run(
    db: &DatabaseConnection,
    run_id: Uuid,
    kind: VisualKind,
) -> Result<Option<VisualPresetBundle>> {
    let selected_column = match kind {
        VisualKind::Keyviewer => "v.keyviewer_id",
        VisualKind::Overlay => "v.overlay_id",
    };
    Ok(VisualPresetBundle::find_by_statement(statement(
        &format!(
            "SELECT p.id,p.name,p.kind,p.source,p.source_version,p.bundle,p.sha256,p.bytes
             FROM run_submission_records s
             JOIN run_sessions r ON r.id=s.run_session_id
             JOIN run_visual_selections v ON v.run_submission_record_id=s.id
             JOIN visual_presets p ON p.id={selected_column} AND p.owner_id=s.owner_id
             WHERE r.pid=$1 AND s.state='submitted' AND s.external_pass_id IS NOT NULL
               AND s.manifest IS NOT NULL AND s.validation IS NOT NULL
               AND p.kind=$2 AND p.deleted_at IS NULL",
        ),
        vec![run_id.into(), kind.as_str().into()],
    ))
    .one(db)
    .await?)
}

pub async fn rename(
    db: &DatabaseConnection,
    owner_id: &str,
    id: Uuid,
    name: &str,
) -> Result<Option<VisualPresetMetadata>> {
    let row = VisualPresetMetadata::find_by_statement(statement(
        "UPDATE visual_presets SET name=$3
         WHERE id=$1 AND owner_id=$2 AND deleted_at IS NULL
         RETURNING id,name,kind,source,source_version,created_at",
        vec![id.into(), owner_id.into(), name.into()],
    ))
    .one(db)
    .await
    .map_err(|error| match error.sql_err() {
        Some(SqlErr::UniqueConstraintViolation(_)) => Error::BadRequest("visual_name_taken".into()),
        _ => error.into(),
    })?;
    Ok(row)
}

pub async fn delete(db: &DatabaseConnection, owner_id: &str, id: Uuid) -> Result<bool> {
    let result = db
        .execute_raw(statement(
            "UPDATE visual_presets SET deleted_at=NOW()
             WHERE id=$1 AND owner_id=$2 AND deleted_at IS NULL",
            vec![id.into(), owner_id.into()],
        ))
        .await?;
    Ok(result.rows_affected() == 1)
}

fn statement(query: &str, values: Vec<Value>) -> Statement {
    Statement::from_sql_and_values(DbBackend::Postgres, query, values)
}

#[cfg(test)]
mod source_tests {
    use super::VisualSource;

    #[test]
    fn jipper_mods_have_distinct_explicit_identifiers() {
        assert_eq!(
            VisualSource::parse("jipper-resourcepack"),
            Some(VisualSource::JipperResourcePack)
        );
        assert_eq!(
            VisualSource::parse("jipper-keyviewer"),
            Some(VisualSource::JipperKeyviewer)
        );
        assert_eq!(VisualSource::parse("jipper"), None);
    }
}
