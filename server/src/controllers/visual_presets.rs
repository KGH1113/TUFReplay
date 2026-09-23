use crate::models::visual_presets;
use crate::services::{auth, visuals};
use axum::{
    body::Bytes,
    extract::{DefaultBodyLimit, Path, State},
    http::HeaderMap,
};
use loco_rs::prelude::*;
use serde_json::Value;
use uuid::Uuid;

pub fn routes() -> Routes {
    Routes::new()
        .prefix("/api/v1/visual-presets")
        .add("", get(list))
        .add(
            "",
            post(create).layer(DefaultBodyLimit::max(visuals::MAX_REQUEST_BYTES)),
        )
        .add("/{id}", delete(remove))
}

pub async fn list(State(ctx): State<AppContext>, headers: HeaderMap) -> Result<Response> {
    let owner = auth::owner(&ctx, &headers).await?;
    format::json(serde_json::json!({
        "presets": visual_presets::list(&ctx.db, &owner).await?
    }))
}

pub async fn create(
    State(ctx): State<AppContext>,
    headers: HeaderMap,
    body: Bytes,
) -> Result<Response> {
    if body.len() > visuals::MAX_REQUEST_BYTES {
        return Err(Error::BadRequest("visual_payload_too_large".into()));
    }
    let owner = auth::owner(&ctx, &headers).await?;
    let object: Value = serde_json::from_slice(&body)
        .map_err(|_| Error::BadRequest("visual_bundle_invalid".into()))?;
    let object = object
        .as_object()
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
    let name = visuals::validate_name(object.get("name"))?;
    let bundle = object
        .get("bundle")
        .cloned()
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
    let validated = visuals::validate_bundle(bundle)?;
    let visual_kind = validated.kind;
    let visual_source = validated.source;
    let preset = visual_presets::create(
        &ctx.db,
        &owner,
        &name,
        visual_kind,
        visual_source,
        &validated.source_version,
        validated.bytes,
        &validated.sha256,
    )
    .await?;
    format::json(serde_json::json!({"preset": preset}))
}

pub async fn remove(
    State(ctx): State<AppContext>,
    headers: HeaderMap,
    Path(id): Path<String>,
) -> Result<Response> {
    let owner = auth::owner(&ctx, &headers).await?;
    let id = Uuid::parse_str(&id).map_err(|_| visuals::preset_not_found())?;
    if !visual_presets::delete(&ctx.db, &owner, id).await? {
        return Err(visuals::preset_not_found());
    }
    format::json(serde_json::json!({"deleted":true}))
}
