use super::dtos::ReplayManifestResponse;
use crate::models::visual_presets::VisualKind;
use crate::services::replays::{self, PublishedReplay, ReplayFile};
use axum::{
    extract::{Path, Query, State},
    http::{
        header::{CACHE_CONTROL, CONTENT_DISPOSITION, CONTENT_LENGTH, CONTENT_TYPE, ETAG},
        HeaderValue,
    },
};
use loco_rs::prelude::*;
use serde::Deserialize;
use sha2::{Digest, Sha256};
use uuid::Uuid;

#[derive(Debug, Deserialize)]
pub struct ManifestQuery {
    pub format: Option<u32>,
    pub asset_mode: Option<String>,
}

pub async fn manifest(
    State(ctx): State<AppContext>,
    Path(run_id): Path<Uuid>,
    Query(query): Query<ManifestQuery>,
) -> Result<Response> {
    let replay = PublishedReplay::load(&ctx.db, run_id).await?;
    let is_v3 = query.format == Some(3);
    let mut response = if is_v3 {
        let visuals = replays::published_visuals(
            &ctx,
            run_id,
            query.asset_mode.as_deref() == Some("objects"),
        )
        .await?;
        format::json(ReplayManifestResponse::from_replay_v3(&replay, &visuals))?
    } else {
        format::json(ReplayManifestResponse::from_replay(&replay))?
    };
    response.headers_mut().insert(
        CACHE_CONTROL,
        if is_v3 {
            HeaderValue::from_static("no-store")
        } else {
            HeaderValue::from_static("public, max-age=31536000, immutable")
        },
    );
    Ok(response)
}

pub async fn visual(
    State(ctx): State<AppContext>,
    Path((run_id, kind)): Path<(Uuid, String)>,
    Query(query): Query<ManifestQuery>,
) -> Result<Response> {
    let kind = VisualKind::parse(&kind).ok_or(Error::NotFound)?;
    // Revalidate the published replay before resolving its frozen visual
    // selection. This keeps a bundle endpoint subject to the same evidence
    // and publication checks as the manifest endpoint.
    PublishedReplay::load(&ctx.db, run_id).await?;
    let (_descriptor, bundle) = replays::published_visual(
        &ctx,
        run_id,
        kind,
        query.asset_mode.as_deref() == Some("objects"),
    )
    .await?;
    let bytes = u64::try_from(bundle.bytes)
        .map_err(|_| Error::Message("visual bundle size is invalid".into()))?;
    if bytes != bundle.bundle.len() as u64
        || hex::encode(Sha256::digest(&bundle.bundle)) != bundle.sha256
    {
        return Err(Error::Message("visual bundle integrity is invalid".into()));
    }
    let mut response = Response::new(axum::body::Body::from(bundle.bundle));
    let headers = response.headers_mut();
    headers.insert(CONTENT_TYPE, HeaderValue::from_static("application/json"));
    headers.insert(
        CONTENT_LENGTH,
        HeaderValue::from_str(&bytes.to_string())
            .map_err(|_| Error::Message("invalid visual bundle size".into()))?,
    );
    headers.insert(
        ETAG,
        HeaderValue::from_str(&format!("\"{}\"", bundle.sha256))
            .map_err(|_| Error::Message("invalid visual bundle digest".into()))?,
    );
    headers.insert(CACHE_CONTROL, HeaderValue::from_static("no-store"));
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

pub async fn visual_asset(
    State(ctx): State<AppContext>,
    Path((run_id, kind, sha256)): Path<(Uuid, String, String)>,
) -> Result<Response> {
    let kind = VisualKind::parse(&kind).ok_or(Error::NotFound)?;
    PublishedReplay::load(&ctx.db, run_id).await?;
    let bundle =
        crate::models::visual_presets::active_bundle_for_published_run(&ctx.db, run_id, kind)
            .await?
            .ok_or(Error::NotFound)?;
    if bundle.bytes != bundle.bundle.len() as i64
        || hex::encode(Sha256::digest(&bundle.bundle)) != bundle.sha256
    {
        return Err(Error::Message("visual bundle integrity is invalid".into()));
    }
    let value: serde_json::Value = serde_json::from_slice(&bundle.bundle)?;
    let reference = value["assets"]
        .as_array()
        .and_then(|items| items.iter().find(|a| a["sha256"].as_str() == Some(&sha256)))
        .ok_or(Error::NotFound)?;
    let reference = crate::services::visual_assets::reference_from_value(reference)?;
    let asset = crate::services::visual_assets::find(&ctx.db, &sha256)
        .await?
        .ok_or(Error::NotFound)?;
    if asset.bytes != reference.bytes || asset.media_type != reference.media_type {
        return Err(Error::NotFound);
    }
    // Published-run access does not grant global public/CDN access to the asset.
    crate::controllers::visual_assets::download(&ctx, asset, false).await
}
