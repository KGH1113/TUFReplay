use crate::models::run_submission_records::{queries, recovery};
use crate::services::ingest::{connection, IngestError};
use crate::workers::{evidence_persistence, submission};
use loco_rs::prelude::*;

/// The DB is the durable source of pending work across DB/Redis/queue crash windows.
/// Duplicate delivery is safe: workers claim the run lease before processing it.
pub async fn reconcile(ctx: &AppContext) -> Result<()> {
    let store = connection::get(ctx).await?;
    crate::services::evidence::ingest_release::reconcile(ctx).await?;
    for candidate in queries::pending(&ctx.db).await? {
        let result = if candidate.state == "uploading" {
            match store.receipt(candidate.run_id, None).await {
                Ok(receipt) if receipt.status == "sealed" => {
                    evidence_persistence::Worker::perform_later(
                        ctx,
                        evidence_persistence::WorkerArgs {
                            run_id: candidate.run_id,
                        },
                    )
                    .await
                    .map(|_| ())
                }
                Err(IngestError::NotFound) => {
                    recovery::expire_upload(&ctx.db, candidate.run_id).await
                }
                Ok(_) => recovery::touch_upload(&ctx.db, candidate.run_id).await,
                Err(error) => Err(Error::Message(error.to_string())),
            }
        } else {
            submission::Worker::perform_later(
                ctx,
                submission::WorkerArgs {
                    run_id: candidate.run_id,
                },
            )
            .await
            .map(|_| ())
        };
        if let Err(error) = result {
            tracing::warn!(run_id=%candidate.run_id, %error, "run dispatch deferred");
        }
    }
    Ok(())
}
