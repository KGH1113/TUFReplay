use super::sql;
use loco_rs::prelude::*;
use sea_orm::ConnectionTrait;
use uuid::Uuid;

pub async fn expire_upload(db: &DatabaseConnection, id: Uuid) -> Result<()> {
    db.execute_raw(sql(
        "UPDATE run_submission_records SET state='expired',updated_at=NOW()
        WHERE run_session_id=(SELECT id FROM run_sessions WHERE pid=$1)
        AND state='uploading' AND manifest IS NULL
        AND (lease_until IS NULL OR lease_until<NOW())",
        vec![id.into()],
    ))
    .await?;
    Ok(())
}

pub async fn touch_upload(db: &DatabaseConnection, id: Uuid) -> Result<()> {
    db.execute_raw(sql(
        "UPDATE run_submission_records SET updated_at=NOW()
        WHERE run_session_id=(SELECT id FROM run_sessions WHERE pid=$1) AND state='uploading'",
        vec![id.into()],
    ))
    .await?;
    Ok(())
}
