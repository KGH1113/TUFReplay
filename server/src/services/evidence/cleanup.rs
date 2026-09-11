use crate::models::run_submission_records::retention as repository;
use loco_rs::prelude::*;
use std::path::Path;

/// Explicit deletions and day-old incomplete failures are eligible. Submitted evidence is retained.
/// Deterministic staging names bound each run prefix; retries repair interrupted deletion.
pub async fn cleanup(ctx: &AppContext) -> Result<()> {
    for record in repository::pending(&ctx.db).await? {
        let result = async {
            super::ingest_release::release(ctx, record.run_id, record.run_session_id).await?;
            let prefix = format!("evidence/{}/", record.run_id);
            for entry in ctx.storage.list(Path::new(&prefix), true).await? {
                if entry.is_dir || !entry.path.starts_with(&prefix) {
                    continue;
                }
                let path = Path::new(&entry.path);
                if ctx.storage.exists(path).await? {
                    ctx.storage.delete(path).await?;
                }
            }
            repository::complete(&ctx.db, record.run_session_id).await
        }
        .await;
        if let Err(error) = result {
            tracing::warn!(run_session_id=record.run_session_id, %error, "evidence cleanup deferred");
        }
    }
    Ok(())
}
