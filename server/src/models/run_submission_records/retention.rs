use super::sql;
use loco_rs::prelude::*;
use sea_orm::{ConnectionTrait, FromQueryResult};

#[derive(FromQueryResult)]
pub struct DeletedEvidence {
    pub run_session_id: i64,
    pub run_id: uuid::Uuid,
}

pub async fn pending(db: &DatabaseConnection) -> Result<Vec<DeletedEvidence>> {
    // Atomically close submission admission before storage deletion begins.
    // A registration with an unknown remote outcome must retain its evidence.
    db.execute_raw(sql(
        "UPDATE run_submission_records SET state='expired',updated_at=NOW()
        WHERE evidence_expires_at<=NOW() AND external_pass_id IS NULL
        AND state IN ('evidence_ready','validator_unavailable','validation_error','validation_rejected','registration_rejected')
        AND (lease_until IS NULL OR lease_until<NOW())",
        vec![],
    )).await?;
    Ok(DeletedEvidence::find_by_statement(sql(
        "SELECT s.run_session_id,r.pid AS run_id FROM run_submission_records s
        JOIN run_sessions r ON r.id=s.run_session_id
        WHERE (s.reason IS NULL OR s.reason<>'evidence_cleaned')
        AND (s.lease_until IS NULL OR s.lease_until<NOW())
        AND (s.state='deleted' OR (s.state='expired' AND s.manifest IS NOT NULL)
            OR (s.state IN ('expired','evidence_invalid')
            AND s.manifest IS NULL AND s.updated_at<NOW()-INTERVAL '24 hours'))
        ORDER BY s.updated_at LIMIT 16",
        vec![],
    ))
    .all(db)
    .await?)
}

pub async fn complete(db: &DatabaseConnection, run_id: i64) -> Result<()> {
    db.execute_raw(sql(
        "UPDATE run_submission_records SET manifest=NULL,validation=NULL,reason='evidence_cleaned',updated_at=NOW()
        WHERE run_session_id=$1 AND state IN ('deleted','expired','evidence_invalid')",
        vec![run_id.into()],
    ))
    .await?;
    Ok(())
}
