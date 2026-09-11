use super::{dtos::*, errors::catalog_error_response, support::catalog_runtime};
use crate::services::{
    auth,
    ingest::IngestError,
    submission::issuance::{self, IssuanceError, RunClaims},
};
use axum::{
    http::{HeaderMap, StatusCode},
    response::IntoResponse,
};
use loco_rs::prelude::*;

#[debug_handler]
pub async fn issue_run_session(
    State(ctx): State<AppContext>,
    headers: HeaderMap,
    JsonValidateWithMessage(params): JsonValidateWithMessage<CreateRunSessionRequest>,
) -> Result<Response> {
    let identity = auth::identity(&ctx, &headers).await?;
    if let Err(error) = catalog_runtime(&ctx)?
        .require_eligible(params.tuf_level_id)
        .await
    {
        return Ok(catalog_error_response(error));
    }
    let issued = match issuance::issue(
        &ctx,
        identity,
        RunClaims {
            protocol_version: params.protocol_version,
            game_version: params.client_game_version,
            mod_version: params.client_mod_version,
            level_id: params.tuf_level_id,
            file_id: params.client_tuf_file_id,
            chart_path: params.client_level_relative_path,
        },
    )
    .await
    {
        Ok(issued) => issued,
        Err(IssuanceError::Ingest(IngestError::AccountLimit)) => {
            return Ok((
                StatusCode::TOO_MANY_REQUESTS,
                Json(ErrorResponse {
                    code: "account_upload_limit",
                }),
            )
                .into_response())
        }
        Err(error) => {
            return Err(match error {
                IssuanceError::Internal(error) => error,
                IssuanceError::Model(error) => error.into(),
                IssuanceError::Database(error) => error.into(),
                IssuanceError::Ingest(error) => Error::Message(error.to_string()),
            })
        }
    };
    Ok((
        StatusCode::CREATED,
        Json(CreateRunSessionResponse {
            run_id: issued.run.pid,
            upload_token: issued.upload_token,
            websocket_url: format!("/api/v1/runs/{}/stream", issued.run.pid),
            lease_expires_at: issued.run.lease_expires_at,
            lease_duration_ms: issued.lease_duration_ms,
            max_chunk_bytes: issued.max_chunk_bytes,
            heartbeat_interval_ms: issued.heartbeat_interval_ms,
        }),
    )
        .into_response())
}
