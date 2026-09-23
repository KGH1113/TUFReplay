use crate::models::run_submission_records::queries as submission_queries;
use crate::models::run_submission_records::Entity as Records;
use crate::models::run_visual_selections::VisualSelection;
use crate::services::auth;
use axum::{
    body::Bytes,
    extract::{Path, Query, State},
    http::HeaderMap,
};
use loco_rs::prelude::*;
use serde::Deserialize;
use uuid::Uuid;

use super::dtos::SubmitRequest;

#[derive(Deserialize)]
pub struct Page {
    before: Option<i64>,
}

pub async fn list(
    State(ctx): State<AppContext>,
    headers: HeaderMap,
    Query(page): Query<Page>,
) -> Result<Response> {
    let owner = auth::owner(&ctx, &headers).await?;
    let mut runs =
        submission_queries::list(&ctx.db, &owner, page.before.unwrap_or(i64::MAX)).await?;
    let more = runs.len() > 50;
    runs.truncate(50);
    let next = if more {
        runs.last().map(|r| r.cursor)
    } else {
        None
    };
    format::json(serde_json::json!({"runs":runs,"next_cursor":next}))
}

pub async fn get_run(
    State(ctx): State<AppContext>,
    headers: HeaderMap,
    Path(id): Path<Uuid>,
) -> Result<Response> {
    let owner = auth::owner(&ctx, &headers).await?;
    format::json(submission_queries::one(&ctx.db, &owner, id).await?)
}

pub async fn submit(
    State(ctx): State<AppContext>,
    headers: HeaderMap,
    Path(id): Path<Uuid>,
    body: Bytes,
) -> Result<Response> {
    let identity = auth::identity(&ctx, &headers).await?;
    identity.require_can_submit()?;
    let owner = identity.owner_id;
    let requested_selection = parse_selection(&body)?;
    submission_queries::one(&ctx.db, &owner, id).await?;
    let run = crate::models::run_sessions::Model::find_by_pid(&ctx.db, id).await?;
    Records::request_authorized_with_selection(
        &ctx.db,
        run.id,
        &owner,
        Some(identity.grant_id),
        requested_selection,
    )
    .await?;
    if let Err(error) = crate::workers::submission::Worker::perform_later(
        &ctx,
        crate::workers::submission::WorkerArgs { run_id: id },
    )
    .await
    {
        tracing::warn!(run_id=%id, %error, "submission dispatch deferred to reconciliation");
    }
    format::json(submission_queries::one(&ctx.db, &owner, id).await?)
}

fn parse_selection(body: &Bytes) -> Result<Option<VisualSelection>> {
    if body.is_empty() {
        return Ok(None);
    }
    if body.len() > 1024 * 1024 {
        return Err(Error::BadRequest("visual_payload_too_large".into()));
    }
    let request: SubmitRequest = serde_json::from_slice(body)
        .map_err(|_| Error::BadRequest("visual_selection_conflict".into()))?;
    Ok(request.presentation.map(|presentation| VisualSelection {
        keyviewer_id: presentation.keyviewer_id,
        overlay_id: presentation.overlay_id,
    }))
}

pub async fn delete_run(
    State(ctx): State<AppContext>,
    headers: HeaderMap,
    Path(id): Path<Uuid>,
) -> Result<Response> {
    let owner = auth::owner(&ctx, &headers).await?;
    let run = crate::models::run_sessions::Model::find_by_pid(&ctx.db, id).await?;
    if !Records::delete(&ctx.db, run.id, &owner).await? {
        return Err(Error::BadRequest("run_cannot_be_deleted".into()));
    }
    format::json(serde_json::json!({"deleted":true}))
}

pub async fn receipt(
    State(ctx): State<AppContext>,
    headers: HeaderMap,
    Path(id): Path<Uuid>,
) -> Result<Response> {
    use sha2::{Digest, Sha256};
    let token = auth::bearer(&headers)?;
    let hash = hex::encode(Sha256::digest(token.as_bytes()));
    let store = super::support::ingest_store(&ctx)?;
    let receipt = store
        .receipt(id, Some(&hash))
        .await
        .map_err(super::errors::http_ingest_error)?;
    format::json(receipt)
}
