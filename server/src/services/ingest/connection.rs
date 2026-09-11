use super::RunIngestStore;
use loco_rs::prelude::*;

/// CLI tasks receive AppContext without running HTTP/worker initializers.
pub async fn get(ctx: &AppContext) -> Result<RunIngestStore> {
    if let Some(store) = ctx.shared_store.get::<RunIngestStore>() {
        return Ok(store);
    }
    let settings = crate::settings::Settings::get(ctx)?.auto_submission.ingest;
    let store = RunIngestStore::connect(settings)
        .await
        .map_err(|error| Error::Message(format!("cannot connect to ingest Redis: {error}")))?;
    ctx.shared_store.insert(store.clone());
    Ok(store)
}
