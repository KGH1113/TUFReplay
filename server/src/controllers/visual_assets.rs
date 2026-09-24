use crate::services::{
    auth,
    visual_assets::{self, AssetReference},
    visuals,
};
use axum::{
    body::Bytes,
    extract::{DefaultBodyLimit, Path, State},
    http::{header, HeaderMap, HeaderValue},
    Json,
};
use loco_rs::prelude::*;
use serde::Deserialize;

pub fn routes() -> Routes {
    Routes::new()
        .prefix("/api/v1/visual-assets")
        .add(
            "/check",
            post(check).layer(DefaultBodyLimit::max(1024 * 1024)),
        )
        .add(
            "/{sha256}",
            put(upload).layer(DefaultBodyLimit::max(visuals::MAX_ASSET_BYTES)),
        )
        .add("/{sha256}", get(owned_download))
        .add("/public/{sha256}", get(public_download))
}

#[derive(Deserialize)]
pub struct CheckRequest {
    pub assets: Vec<AssetReference>,
}

async fn check(
    State(ctx): State<AppContext>,
    headers: HeaderMap,
    Json(request): Json<CheckRequest>,
) -> Result<Response> {
    let owner = auth::owner(&ctx, &headers).await?;
    if request.assets.len() > visuals::MAX_ASSETS {
        return Err(Error::BadRequest("visual_payload_too_large".into()));
    }
    let mut missing = Vec::new();
    for reference in request.assets {
        if !visual_assets::owned(&ctx.db, &owner, &reference).await? {
            missing.push(reference.sha256);
        }
    }
    format::json(serde_json::json!({"missing":missing}))
}

async fn upload(
    State(ctx): State<AppContext>,
    Path(sha256): Path<String>,
    headers: HeaderMap,
    body: Bytes,
) -> Result<Response> {
    let owner = auth::owner(&ctx, &headers).await?;
    let media_type = headers
        .get(header::CONTENT_TYPE)
        .and_then(|v| v.to_str().ok())
        .unwrap_or_default()
        .to_owned();
    let reference = AssetReference {
        sha256,
        bytes: body.len() as i64,
        media_type,
    };
    visual_assets::store(&ctx, &owner, &reference, body).await?;
    format::json(serde_json::json!({"asset":reference}))
}

async fn owned_download(
    State(ctx): State<AppContext>,
    Path(sha256): Path<String>,
    headers: HeaderMap,
) -> Result<Response> {
    let owner = auth::owner(&ctx, &headers).await?;
    let asset = visual_assets::find(&ctx.db, &sha256)
        .await?
        .ok_or(Error::NotFound)?;
    let reference = AssetReference {
        sha256,
        bytes: asset.bytes,
        media_type: asset.media_type.clone(),
    };
    if !visual_assets::owned(&ctx.db, &owner, &reference).await? {
        return Err(Error::NotFound);
    }
    download(&ctx, asset, false).await
}

async fn public_download(
    State(ctx): State<AppContext>,
    Path(sha256): Path<String>,
) -> Result<Response> {
    let asset = visual_assets::find(&ctx.db, &sha256)
        .await?
        .ok_or(Error::NotFound)?;
    if asset.public_license.is_none() {
        return Err(Error::NotFound);
    }
    download(&ctx, asset, true).await
}

pub async fn download(
    ctx: &AppContext,
    asset: visual_assets::Asset,
    public: bool,
) -> Result<Response> {
    let stream = ctx
        .storage
        .download_stream(std::path::Path::new(&asset.storage_key))
        .await?;
    let mut response = Response::new(stream.into_body());
    response.headers_mut().insert(
        header::CONTENT_TYPE,
        HeaderValue::from_str(&asset.media_type)
            .map_err(|_| Error::Message("invalid asset content type".into()))?,
    );
    response.headers_mut().insert(
        header::CONTENT_LENGTH,
        HeaderValue::from_str(&asset.bytes.to_string()).unwrap(),
    );
    response.headers_mut().insert(
        header::ETAG,
        HeaderValue::from_str(&format!("\"{}\"", asset.sha256)).unwrap(),
    );
    response.headers_mut().insert(
        header::CACHE_CONTROL,
        HeaderValue::from_static(if public {
            "public, max-age=31536000, immutable"
        } else {
            "private, no-store"
        }),
    );
    response.headers_mut().insert(
        header::X_CONTENT_TYPE_OPTIONS,
        HeaderValue::from_static("nosniff"),
    );
    Ok(response)
}
