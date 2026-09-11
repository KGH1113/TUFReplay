use crate::models::run_submission_records::{ingest_release as records, Entity};
use crate::services::ingest::{connection, RunIngestStore};
use loco_rs::prelude::*;
use uuid::Uuid;

pub async fn release(ctx: &AppContext, run_id: Uuid, record_id: i64) -> Result<()> {
    let record = Entity::record(&ctx.db, record_id).await?;
    if record.manifest.is_none() || record.ingest_released_at.is_some() {
        return Ok(());
    }
    let store = connection::get(ctx).await?;
    release_with_store(ctx, &store, run_id, record_id).await
}

async fn release_with_store(
    ctx: &AppContext,
    store: &RunIngestStore,
    run_id: Uuid,
    record_id: i64,
) -> Result<()> {
    store
        .release_chunks(run_id)
        .await
        .map_err(|error| Error::Message(error.to_string()))?;
    records::complete(&ctx.db, record_id).await
}

pub async fn reconcile(ctx: &AppContext) -> Result<()> {
    let store = connection::get(ctx).await?;
    for record in records::pending(&ctx.db).await? {
        if let Err(error) =
            release_with_store(ctx, &store, record.run_id, record.run_session_id).await
        {
            tracing::warn!(run_id=%record.run_id, %error, "ingest release deferred");
        }
    }
    Ok(())
}
