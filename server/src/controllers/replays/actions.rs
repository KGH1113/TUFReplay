use super::dtos::ReplayManifestResponse;
use crate::services::replays::{self, PublishedReplay, ReplayFile};
use axum::{
    extract::{Path, State},
    http::{
        header::{CACHE_CONTROL, CONTENT_DISPOSITION, CONTENT_LENGTH, CONTENT_TYPE, ETAG},
        HeaderValue,
    },
};
use loco_rs::prelude::*;
use uuid::Uuid;

pub async fn manifest(State(ctx): State<AppContext>, Path(run_id): Path<Uuid>) -> Result<Response> {
    let replay = PublishedReplay::load(&ctx.db, run_id).await?;
    let mut response = format::json(ReplayManifestResponse::from_replay(&replay))?;
    response.headers_mut().insert(
        CACHE_CONTROL,
        HeaderValue::from_static("public, max-age=31536000, immutable"),
    );
    Ok(response)
}

pub async fn file(
    State(ctx): State<AppContext>,
    Path((run_id, file_name)): Path<(Uuid, String)>,
) -> Result<Response> {
    let requested = ReplayFile::from_name(&file_name).ok_or(Error::NotFound)?;
    let replay = PublishedReplay::load(&ctx.db, run_id).await?;
    let (asset, stream) = replays::download(&ctx, &replay, requested).await?;
    let mut response = Response::new(stream.into_body());
    let headers = response.headers_mut();
    headers.insert(
        CONTENT_TYPE,
        HeaderValue::from_static(requested.media_type()),
    );
    headers.insert(
        CONTENT_DISPOSITION,
        HeaderValue::from_str(&format!("attachment; filename=\"{}\"", requested.name()))
            .map_err(|_| Error::Message("invalid replay filename".into()))?,
    );
    headers.insert(
        CONTENT_LENGTH,
        HeaderValue::from_str(&asset.bytes.to_string())
            .map_err(|_| Error::Message("invalid replay asset size".into()))?,
    );
    headers.insert(
        ETAG,
        HeaderValue::from_str(&format!("\"{}\"", asset.sha256))
            .map_err(|_| Error::Message("invalid replay asset digest".into()))?,
    );
    headers.insert(
        CACHE_CONTROL,
        HeaderValue::from_static("public, max-age=31536000, immutable"),
    );
    Ok(response)
}
