use crate::services::ingest::RunIngestStore;
use crate::services::tuf::catalog::TufCatalogRuntime;
use axum::http::HeaderMap;
use loco_rs::prelude::*;
pub(super) fn ingest_store(ctx: &AppContext) -> Result<RunIngestStore> {
    ctx.shared_store
        .get::<RunIngestStore>()
        .ok_or_else(|| Error::Message("run ingest store is not initialized".to_owned()))
}

pub(super) fn catalog_runtime(ctx: &AppContext) -> Result<TufCatalogRuntime> {
    ctx.shared_store
        .get::<TufCatalogRuntime>()
        .ok_or_else(|| Error::Message("TUF catalog is not initialized".to_owned()))
}

pub(super) fn bearer_token(headers: &HeaderMap) -> Result<&str> {
    crate::services::auth::bearer(headers)
}
