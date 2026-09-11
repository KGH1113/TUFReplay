use super::sql;
use loco_rs::prelude::*;
use sea_orm::{ConnectionTrait, FromQueryResult};
use uuid::Uuid;

#[derive(FromQueryResult)]
pub struct PendingRelease {
    pub run_session_id: i64,
    pub run_id: Uuid,
}

pub async fn pending(db: &DatabaseConnection) -> Result<Vec<PendingRelease>> {
    Ok(PendingRelease::find_by_statement(sql(
        "SELECT s.run_session_id,r.pid AS run_id FROM run_submission_records s
        JOIN run_sessions r ON r.id=s.run_session_id
        WHERE s.manifest IS NOT NULL AND s.ingest_released_at IS NULL
        ORDER BY s.id LIMIT 32",
        vec![],
    ))
    .all(db)
    .await?)
}

pub async fn complete(db: &impl ConnectionTrait, run_id: i64) -> Result<()> {
    db.execute_raw(sql(
        "UPDATE run_submission_records SET ingest_released_at=NOW()
        WHERE run_session_id=$1 AND manifest IS NOT NULL AND ingest_released_at IS NULL",
        vec![run_id.into()],
    ))
    .await?;
    Ok(())
}
