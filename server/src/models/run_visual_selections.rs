//! Query models for the separately migrated frozen visual selection row.
//!
//! The generated entity tree is intentionally left untouched; this module
//! owns the small SQL result shapes and lock/insert queries for the new table.

use chrono::{DateTime, FixedOffset};
use loco_rs::prelude::*;
use sea_orm::{DbBackend, FromQueryResult, Statement, Value};
use uuid::Uuid;

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct VisualSelection {
    pub keyviewer_id: Option<Uuid>,
    pub overlay_id: Option<Uuid>,
}

#[derive(Clone, Debug, FromQueryResult)]
pub struct FrozenVisualSelection {
    pub run_submission_record_id: i64,
    pub keyviewer_id: Option<Uuid>,
    pub overlay_id: Option<Uuid>,
    pub fixed_at: DateTime<FixedOffset>,
}

impl FrozenVisualSelection {
    #[must_use]
    pub fn selection(&self) -> VisualSelection {
        VisualSelection {
            keyviewer_id: self.keyviewer_id,
            overlay_id: self.overlay_id,
        }
    }
}

pub async fn lock_for_update(
    db: &impl ConnectionTrait,
    run_submission_record_id: i64,
) -> Result<Option<FrozenVisualSelection>> {
    Ok(FrozenVisualSelection::find_by_statement(statement(
        "SELECT run_submission_record_id,keyviewer_id,overlay_id,fixed_at
         FROM run_visual_selections
         WHERE run_submission_record_id=$1
         FOR UPDATE",
        vec![run_submission_record_id.into()],
    ))
    .one(db)
    .await?)
}

pub async fn insert(
    db: &impl ConnectionTrait,
    run_submission_record_id: i64,
    selection: &VisualSelection,
) -> Result<()> {
    db.execute_raw(statement(
        "INSERT INTO run_visual_selections
            (run_submission_record_id,keyviewer_id,overlay_id)
         VALUES ($1,$2,$3)",
        vec![
            run_submission_record_id.into(),
            selection.keyviewer_id.into(),
            selection.overlay_id.into(),
        ],
    ))
    .await?;
    Ok(())
}

pub async fn for_run(
    db: &impl ConnectionTrait,
    run_submission_record_id: i64,
) -> Result<Option<FrozenVisualSelection>> {
    Ok(FrozenVisualSelection::find_by_statement(statement(
        "SELECT run_submission_record_id,keyviewer_id,overlay_id,fixed_at
         FROM run_visual_selections
         WHERE run_submission_record_id=$1",
        vec![run_submission_record_id.into()],
    ))
    .one(db)
    .await?)
}

fn statement(query: &str, values: Vec<Value>) -> Statement {
    Statement::from_sql_and_values(DbBackend::Postgres, query, values)
}
