use crate::models::run_submission_records::queries as submission_queries;
use crate::models::run_submission_records::Entity as Records;
use crate::services::auth;
use axum::{
    extract::{Path, Query, State},
    http::HeaderMap,
};
use loco_rs::prelude::*;
use serde::Deserialize;
use uuid::Uuid;

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
) -> Result<Response> {
    let identity = auth::identity(&ctx, &headers).await?;
    let owner = identity.owner_id;
    submission_queries::one(&ctx.db, &owner, id).await?;
    let run = crate::models::run_sessions::Model::find_by_pid(&ctx.db, id).await?;
    Records::request_authorized(&ctx.db, run.id, &owner, Some(identity.grant_id)).await?;
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
