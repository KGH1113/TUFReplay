use super::sql;
use loco_rs::prelude::*;
use sea_orm::FromQueryResult;
use serde::Serialize;
use uuid::Uuid;

#[derive(Debug, Serialize, FromQueryResult)]
pub struct RunView {
    pub cursor: i64,
    pub run_id: Uuid,
    pub tuf_level_id: i64,
    pub chart_path: String,
    pub status: String,
    pub reason: Option<String>,
    pub external_pass_id: Option<i64>,
    pub created_at: chrono::DateTime<chrono::FixedOffset>,
    pub evidence_expires_at: Option<chrono::DateTime<chrono::FixedOffset>>,
}

const SELECT: &str = "SELECT s.id AS cursor,r.pid AS run_id,r.tuf_level_id,
    r.client_level_relative_path AS chart_path,
    CASE WHEN s.state='uploading' THEN r.status ELSE s.state END AS status,
    s.reason,s.external_pass_id,r.created_at,s.evidence_expires_at
    FROM run_submission_records s JOIN run_sessions r ON r.id=s.run_session_id";

pub async fn list(db: &DatabaseConnection, owner: &str, before: i64) -> Result<Vec<RunView>> {
    Ok(RunView::find_by_statement(sql(
        &format!(
            "{SELECT}
        WHERE s.owner_id=$1 AND s.id<$2 AND s.state<>'deleted'
        AND (s.manifest IS NOT NULL OR r.status IN ('streaming','sealed')) ORDER BY s.id DESC LIMIT 51"
        ),
        vec![owner.into(), before.into()],
    ))
    .all(db)
    .await?)
}

pub async fn one(db: &DatabaseConnection, owner: &str, run_id: Uuid) -> Result<RunView> {
    RunView::find_by_statement(sql(
        &format!(
            "{SELECT}
        WHERE s.owner_id=$1 AND r.pid=$2 AND s.state<>'deleted'"
        ),
        vec![owner.into(), run_id.into()],
    ))
    .one(db)
    .await?
    .ok_or(Error::NotFound)
}

#[derive(FromQueryResult)]
pub struct PublishedReplay {
    pub run_id: Uuid,
    pub tuf_level_id: i64,
    pub chart_path: String,
    pub external_pass_id: i64,
    pub manifest: serde_json::Value,
    pub validation: serde_json::Value,
}

pub async fn published_replay(db: &DatabaseConnection, run_id: Uuid) -> Result<PublishedReplay> {
    PublishedReplay::find_by_statement(sql(
        "SELECT r.pid AS run_id,r.tuf_level_id,
        r.client_level_relative_path AS chart_path,s.external_pass_id,s.manifest,s.validation
        FROM run_submission_records s JOIN run_sessions r ON r.id=s.run_session_id
        WHERE r.pid=$1 AND s.state='submitted' AND s.external_pass_id IS NOT NULL
        AND s.manifest IS NOT NULL AND s.validation IS NOT NULL",
        vec![run_id.into()],
    ))
    .one(db)
    .await?
    .ok_or(Error::NotFound)
}

#[derive(FromQueryResult)]
pub struct PendingRun {
    pub run_id: Uuid,
    pub state: String,
}

pub async fn pending(db: &DatabaseConnection) -> Result<Vec<PendingRun>> {
    Ok(PendingRun::find_by_statement(sql(
        "SELECT r.pid AS run_id,s.state
        FROM run_submission_records s JOIN run_sessions r ON s.run_session_id=r.id
        WHERE s.state IN ('uploading','validation_pending','registering')
        AND (s.lease_until IS NULL OR s.lease_until<NOW())
        AND (s.next_attempt_at IS NULL OR s.next_attempt_at<=NOW())
        ORDER BY s.updated_at LIMIT 32",
        vec![],
    ))
    .all(db)
    .await?)
}
