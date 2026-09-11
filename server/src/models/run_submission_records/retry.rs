use super::sql;
use loco_rs::prelude::*;
use sea_orm::ConnectionTrait;
use uuid::Uuid;

/// Persisted backoff survives process and game exits. After three retries the
/// user can explicitly retry; registration retries retain the receipt identity.
pub async fn defer(db: &impl ConnectionTrait, run_id: i64, lease: Uuid) -> Result<()> {
    db.execute_raw(sql(
        "UPDATE run_submission_records SET
        next_attempt_at=CASE WHEN retry_count<3 THEN NOW()+
            (CASE retry_count WHEN 0 THEN 5 WHEN 1 THEN 30 ELSE 120 END)*INTERVAL '1 second' ELSE NULL END,
        state=CASE WHEN retry_count<3 THEN state
            WHEN state='registering' THEN 'registration_error' ELSE 'validation_error' END,
        reason='submission_temporarily_unavailable',retry_count=retry_count+1,updated_at=NOW()
        WHERE run_session_id=$1 AND lease_owner=$2 AND lease_until>NOW()
        AND state IN ('validation_pending','registering')",
        vec![run_id.into(), lease.into()],
    )).await?;
    Ok(())
}
