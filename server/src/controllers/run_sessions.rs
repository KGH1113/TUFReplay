use axum::{
    extract::{
        ws::{Message, WebSocket, WebSocketUpgrade},
        Path, State,
    },
    http::{HeaderMap, StatusCode},
    response::IntoResponse,
    Json,
};
use base64::{engine::general_purpose::URL_SAFE_NO_PAD, Engine};
use chrono::{Duration, Utc};
use loco_rs::prelude::*;
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use uuid::Uuid;
use validator::Validate;

use crate::models::{
    level_revisions::{CatalogError, RunLevelClaims, TufCatalogRuntime},
    run_ingest::{AppendOutcome, IngestError, RunIngestStore},
    run_sessions::{Model, NewRunSession},
};

const PROTOCOL_VERSION: i64 = 1;
const BINARY_HEADER_BYTES: usize = 20;
const BINARY_MAGIC: [u8; 4] = *b"TUFR";

#[derive(Debug, Deserialize, Validate)]
#[serde(deny_unknown_fields)]
pub struct CreateRunSessionRequest {
    #[validate(range(min = 1, max = 1))]
    protocol_version: i64,
    #[validate(length(min = 1, max = 64))]
    client_game_version: String,
    #[validate(length(min = 1, max = 64))]
    client_mod_version: String,
    #[validate(range(min = 1))]
    tuf_level_id: i64,
    #[validate(length(min = 1, max = 256))]
    client_tuf_file_id: String,
    #[validate(length(equal = 64))]
    client_installed_payload_hash_hex: String,
    #[validate(range(min = 1, max = 1))]
    client_payload_hash_version: i64,
    #[validate(length(min = 1, max = 1024))]
    client_level_relative_path: String,
}

#[derive(Debug, Serialize)]
struct CreateRunSessionResponse {
    run_id: Uuid,
    upload_token: String,
    websocket_url: String,
    lease_expires_at: chrono::DateTime<chrono::FixedOffset>,
    max_chunk_bytes: usize,
    heartbeat_interval_ms: u64,
}

#[derive(Debug, Serialize)]
struct ErrorResponse {
    code: &'static str,
}

#[derive(Debug, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case")]
enum ClientControl {
    Hello {
        protocol_version: i64,
        last_acknowledged_sequence: i64,
    },
    Heartbeat,
    Complete {
        final_sequence: i64,
        input_count: u64,
        hit_context_count: u64,
    },
    Fail {
        #[serde(default)]
        reason: Option<String>,
    },
}

#[derive(Debug, Serialize)]
#[serde(tag = "type", rename_all = "snake_case")]
enum ServerControl {
    Ready {
        acknowledged_sequence: i64,
        heartbeat_interval_ms: u64,
        max_chunk_bytes: usize,
    },
    Ack {
        acknowledged_sequence: i64,
    },
    Nack {
        expected_sequence: i64,
    },
    Sealed {
        acknowledged_sequence: i64,
    },
    Error {
        code: &'static str,
        terminal: bool,
    },
}

#[derive(Debug)]
struct DataChunk<'a> {
    kind: u8,
    sequence: u64,
    payload: &'a [u8],
}

#[debug_handler]
pub async fn issue_run_session(
    State(ctx): State<AppContext>,
    JsonValidateWithMessage(params): JsonValidateWithMessage<CreateRunSessionRequest>,
) -> Result<Response> {
    let installed_payload_hash = hex::decode(&params.client_installed_payload_hash_hex)
        .map_err(|_| Error::BadRequest("client payload hash must be hexadecimal".to_owned()))?;
    if installed_payload_hash.len() != 32 {
        return Err(Error::BadRequest(
            "client payload hash must encode exactly 32 bytes".to_owned(),
        ));
    }

    let catalog = catalog_runtime(&ctx)?;
    let pinned = match catalog
        .resolve_for_run(
            &ctx,
            RunLevelClaims {
                tuf_level_id: params.tuf_level_id,
                client_tuf_file_id: params.client_tuf_file_id,
                client_installed_payload_hash: installed_payload_hash,
                client_payload_hash_version: params.client_payload_hash_version,
                client_level_relative_path: params.client_level_relative_path,
            },
        )
        .await
    {
        Ok(pinned) => pinned,
        Err(error) => return Ok(catalog_error_response(error)),
    };

    let store = ingest_store(&ctx)?;
    let run_id = Uuid::new_v4();
    let upload_token = generate_upload_token()?;
    let upload_token_hash = Sha256::digest(upload_token.as_bytes()).to_vec();
    let upload_token_hash_hex = hex::encode(&upload_token_hash);
    let now = Utc::now();
    let hard_expires_at = now
        + Duration::seconds(
            i64::try_from(store.settings().hard_duration_seconds).map_err(|_| {
                Error::Message("hard_duration_seconds exceeds the supported range".to_owned())
            })?,
        );
    let lease_expires_at =
        now + Duration::seconds(i64::try_from(store.settings().active_ttl_seconds).map_err(
            |_| Error::Message("active_ttl_seconds exceeds the supported range".to_owned()),
        )?);

    let run = Model::create(
        &ctx.db,
        NewRunSession {
            pid: run_id,
            protocol_version: params.protocol_version,
            client_game_version: params.client_game_version,
            client_mod_version: params.client_mod_version,
            tuf_level_id: pinned.revision.tuf_level_id,
            level_revision_id: pinned.revision.id,
            level_revision_chart_id: pinned.chart.id,
            upload_token_hash,
            lease_expires_at: lease_expires_at.fixed_offset(),
            hard_expires_at: hard_expires_at.fixed_offset(),
        },
    )
    .await?;

    if let Err(error) = store
        .create_session(run_id, &upload_token_hash_hex, hard_expires_at.timestamp())
        .await
    {
        if let Err(cleanup_error) = store.discard(run_id).await {
            tracing::error!(%run_id, %cleanup_error, "failed to compensate Redis run session");
        }
        if let Err(cleanup_error) = run.delete_record(&ctx.db).await {
            tracing::error!(%run_id, %cleanup_error, "failed to compensate PostgreSQL run session");
        }
        return Err(Error::Message(format!(
            "failed to create run ingest session: {error}"
        )));
    }

    Ok((
        StatusCode::CREATED,
        Json(CreateRunSessionResponse {
            run_id,
            upload_token,
            websocket_url: format!("/api/v1/runs/{run_id}/stream"),
            lease_expires_at: lease_expires_at.fixed_offset(),
            max_chunk_bytes: store.settings().max_chunk_bytes,
            heartbeat_interval_ms: store.settings().heartbeat_interval_ms,
        }),
    )
        .into_response())
}

#[debug_handler]
pub async fn stream_run_session(
    Path(run_id): Path<Uuid>,
    State(ctx): State<AppContext>,
    headers: HeaderMap,
    websocket: WebSocketUpgrade,
) -> Result<Response> {
    let upload_token = bearer_token(&headers)?;
    let token_hash_hex = hex::encode(Sha256::digest(upload_token.as_bytes()));
    let store = ingest_store(&ctx)?;
    store
        .authorize(run_id, &token_hash_hex)
        .await
        .map_err(http_ingest_error)?;
    Model::find_by_pid(&ctx.db, run_id).await?;

    let maximum_message_bytes = store.settings().max_chunk_bytes + BINARY_HEADER_BYTES;
    Ok(websocket
        .max_message_size(maximum_message_bytes)
        .max_frame_size(maximum_message_bytes)
        .on_upgrade(move |socket| handle_socket(socket, ctx, store, run_id, token_hash_hex))
        .into_response())
}

async fn handle_socket(
    mut socket: WebSocket,
    ctx: AppContext,
    store: RunIngestStore,
    run_id: Uuid,
    token_hash_hex: String,
) {
    let Some(Ok(Message::Text(text))) = socket.recv().await else {
        let _ = send_error(&mut socket, "hello_required", true).await;
        return;
    };
    let Ok(ClientControl::Hello {
        protocol_version,
        last_acknowledged_sequence,
    }) = serde_json::from_str::<ClientControl>(&text)
    else {
        let _ = send_error(&mut socket, "invalid_hello", true).await;
        return;
    };
    if protocol_version != PROTOCOL_VERSION {
        let _ = send_error(&mut socket, "unsupported_protocol", true).await;
        return;
    }

    let acknowledged_sequence = match store.open(run_id, &token_hash_hex).await {
        Ok(sequence) => sequence,
        Err(error) => {
            let (code, terminal) = ws_ingest_error(&error);
            let _ = send_error(&mut socket, code, terminal).await;
            return;
        }
    };
    match Model::find_by_pid(&ctx.db, run_id).await {
        Ok(run) => {
            if let Err(error) = run.mark_streaming(&ctx.db).await {
                tracing::error!(%run_id, %error, "failed to mark opened run as streaming");
                let _ = send_error(&mut socket, "lifecycle_update_failed", true).await;
                return;
            }
        }
        Err(_) => {
            let _ = send_error(&mut socket, "run_not_found", true).await;
            return;
        }
    }
    if last_acknowledged_sequence != acknowledged_sequence {
        tracing::debug!(%run_id, client_ack = last_acknowledged_sequence, server_ack = acknowledged_sequence, "client and authoritative ingest acknowledgements differ");
    }
    if send_control(
        &mut socket,
        &ServerControl::Ready {
            acknowledged_sequence,
            heartbeat_interval_ms: store.settings().heartbeat_interval_ms,
            max_chunk_bytes: store.settings().max_chunk_bytes,
        },
    )
    .await
    .is_err()
    {
        return;
    }

    while let Some(message) = socket.recv().await {
        let Ok(message) = message else { return };
        match message {
            Message::Binary(bytes) => {
                let chunk = match parse_data_chunk(&bytes, store.settings().max_chunk_bytes) {
                    Ok(chunk) => chunk,
                    Err(code) => {
                        if send_error(&mut socket, code, false).await.is_err() {
                            return;
                        }
                        continue;
                    }
                };
                let response = match store
                    .append(
                        run_id,
                        &token_hash_hex,
                        chunk.sequence,
                        chunk.kind,
                        chunk.payload,
                    )
                    .await
                {
                    Ok(
                        AppendOutcome::Accepted {
                            acknowledged_sequence,
                        }
                        | AppendOutcome::Duplicate {
                            acknowledged_sequence,
                        },
                    ) => ServerControl::Ack {
                        acknowledged_sequence,
                    },
                    Ok(AppendOutcome::Gap { expected_sequence }) => {
                        ServerControl::Nack { expected_sequence }
                    }
                    Err(error) => {
                        let (code, terminal) = ws_ingest_error(&error);
                        if send_error(&mut socket, code, terminal).await.is_err() || terminal {
                            return;
                        }
                        continue;
                    }
                };
                if send_control(&mut socket, &response).await.is_err() {
                    return;
                }
            }
            Message::Text(text) => {
                let control = match serde_json::from_str::<ClientControl>(&text) {
                    Ok(control) => control,
                    Err(_) => {
                        if send_error(&mut socket, "malformed_control", false)
                            .await
                            .is_err()
                        {
                            return;
                        }
                        continue;
                    }
                };
                match control {
                    ClientControl::Hello { .. } => {
                        if send_error(&mut socket, "duplicate_hello", false)
                            .await
                            .is_err()
                        {
                            return;
                        }
                    }
                    ClientControl::Heartbeat => {
                        match store.heartbeat(run_id, &token_hash_hex).await {
                            Ok(acknowledged_sequence) => {
                                if send_control(
                                    &mut socket,
                                    &ServerControl::Ack {
                                        acknowledged_sequence,
                                    },
                                )
                                .await
                                .is_err()
                                {
                                    return;
                                }
                            }
                            Err(error) => {
                                let (code, terminal) = ws_ingest_error(&error);
                                let _ = send_error(&mut socket, code, terminal).await;
                                if terminal {
                                    return;
                                }
                            }
                        }
                    }
                    ClientControl::Complete {
                        final_sequence,
                        input_count,
                        hit_context_count,
                    } => match store
                        .seal(
                            run_id,
                            &token_hash_hex,
                            final_sequence,
                            input_count,
                            hit_context_count,
                        )
                        .await
                    {
                        Ok(seal) => {
                            let run = match Model::find_by_pid(&ctx.db, run_id).await {
                                Ok(run) => run,
                                Err(error) => {
                                    tracing::error!(%run_id, %error, "sealed run is missing in PostgreSQL");
                                    let _ =
                                        send_error(&mut socket, "lifecycle_update_failed", true)
                                            .await;
                                    return;
                                }
                            };
                            if let Err(error) = run.mark_sealed(&ctx.db).await {
                                tracing::error!(%run_id, %error, "Redis sealed but PostgreSQL lifecycle update failed");
                                let _ =
                                    send_error(&mut socket, "lifecycle_update_failed", true).await;
                                return;
                            }
                            let _ = send_control(
                                &mut socket,
                                &ServerControl::Sealed {
                                    acknowledged_sequence: seal.acknowledged_sequence,
                                },
                            )
                            .await;
                            return;
                        }
                        Err(error) => {
                            let (code, terminal) = ws_ingest_error(&error);
                            let _ = send_error(&mut socket, code, terminal).await;
                            if terminal {
                                return;
                            }
                        }
                    },
                    ClientControl::Fail { reason } => {
                        if let Some(reason) = reason {
                            tracing::info!(%run_id, %reason, "client reported failed run");
                        }
                        match store.fail(run_id, &token_hash_hex).await {
                            Ok(()) => {
                                if let Ok(run) = Model::find_by_pid(&ctx.db, run_id).await {
                                    if let Err(error) = run.mark_failed(&ctx.db).await {
                                        tracing::error!(%run_id, %error, "failed to update failed run lifecycle");
                                    }
                                }
                                return;
                            }
                            Err(error) => {
                                let (code, terminal) = ws_ingest_error(&error);
                                let _ = send_error(&mut socket, code, terminal).await;
                                if terminal {
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            Message::Close(_) => return,
            Message::Ping(payload) => {
                if socket.send(Message::Pong(payload)).await.is_err() {
                    return;
                }
            }
            Message::Pong(_) => {}
        }
    }
}

fn parse_data_chunk(
    bytes: &[u8],
    max_chunk_bytes: usize,
) -> std::result::Result<DataChunk<'_>, &'static str> {
    if bytes.len() < BINARY_HEADER_BYTES {
        return Err("malformed_chunk_header");
    }
    if bytes[0..4] != BINARY_MAGIC || bytes[4] != PROTOCOL_VERSION as u8 {
        return Err("unsupported_chunk_header");
    }
    let kind = bytes[5];
    if kind > 5 || u16::from_be_bytes([bytes[6], bytes[7]]) != 0 {
        return Err("unsupported_chunk_header");
    }
    let sequence = u64::from_be_bytes(bytes[8..16].try_into().expect("fixed header length"));
    let payload_length =
        u32::from_be_bytes(bytes[16..20].try_into().expect("fixed header length")) as usize;
    if payload_length > max_chunk_bytes {
        return Err("chunk_too_large");
    }
    if bytes.len() != BINARY_HEADER_BYTES + payload_length {
        return Err("payload_length_mismatch");
    }
    Ok(DataChunk {
        kind,
        sequence,
        payload: &bytes[BINARY_HEADER_BYTES..],
    })
}

fn ingest_store(ctx: &AppContext) -> Result<RunIngestStore> {
    ctx.shared_store
        .get::<RunIngestStore>()
        .ok_or_else(|| Error::Message("run ingest store is not initialized".to_owned()))
}

fn catalog_runtime(ctx: &AppContext) -> Result<TufCatalogRuntime> {
    ctx.shared_store
        .get::<TufCatalogRuntime>()
        .ok_or_else(|| Error::Message("TUF catalog is not initialized".to_owned()))
}

fn bearer_token(headers: &HeaderMap) -> Result<&str> {
    let value = headers
        .get(axum::http::header::AUTHORIZATION)
        .and_then(|value| value.to_str().ok())
        .ok_or_else(|| Error::Unauthorized("upload token is required".to_owned()))?;
    value
        .strip_prefix("Bearer ")
        .filter(|token| !token.is_empty())
        .ok_or_else(|| Error::Unauthorized("invalid upload token".to_owned()))
}

fn generate_upload_token() -> Result<String> {
    let mut bytes = [0_u8; 32];
    getrandom::fill(&mut bytes)
        .map_err(|error| Error::Message(format!("cannot generate upload token: {error}")))?;
    Ok(URL_SAFE_NO_PAD.encode(bytes))
}

fn catalog_error_response(error: CatalogError) -> Response {
    let (status, code) = match error {
        CatalogError::LevelRevisionOutdated => (StatusCode::CONFLICT, "level_revision_outdated"),
        CatalogError::LevelInstallationMismatch => {
            (StatusCode::CONFLICT, "level_installation_mismatch")
        }
        CatalogError::ChartNotFound => (StatusCode::UNPROCESSABLE_ENTITY, "chart_not_found"),
        CatalogError::CatalogUnstable => (StatusCode::SERVICE_UNAVAILABLE, "catalog_unstable"),
        CatalogError::UpstreamUnavailable => {
            (StatusCode::SERVICE_UNAVAILABLE, "catalog_unavailable")
        }
        CatalogError::Busy => (StatusCode::SERVICE_UNAVAILABLE, "catalog_busy"),
        CatalogError::UnsafeArchive => (StatusCode::BAD_GATEWAY, "unsafe_level_archive"),
        CatalogError::ArtifactTooLarge => (StatusCode::BAD_GATEWAY, "level_archive_too_large"),
        CatalogError::NoCharts => (StatusCode::BAD_GATEWAY, "level_archive_has_no_charts"),
        CatalogError::CatalogRevisionConflict => (
            StatusCode::INTERNAL_SERVER_ERROR,
            "catalog_revision_conflict",
        ),
        CatalogError::Storage(_) => (StatusCode::INTERNAL_SERVER_ERROR, "artifact_storage_failed"),
        CatalogError::Database(_) => (StatusCode::INTERNAL_SERVER_ERROR, "catalog_database_failed"),
    };
    tracing::warn!(%error, code, "run session eligibility failed");
    (status, Json(ErrorResponse { code })).into_response()
}

fn http_ingest_error(error: IngestError) -> Error {
    match error {
        IngestError::Unauthorized => Error::Unauthorized("invalid upload token".to_owned()),
        IngestError::NotFound => Error::NotFound,
        IngestError::Expired => Error::BadRequest("run ingest session expired".to_owned()),
        IngestError::InvalidState => {
            Error::BadRequest("run ingest session is not active".to_owned())
        }
        other => Error::Message(format!("run ingest failure: {other}")),
    }
}

fn ws_ingest_error(error: &IngestError) -> (&'static str, bool) {
    match error {
        IngestError::NotFound => ("run_not_found", true),
        IngestError::Unauthorized => ("unauthorized", true),
        IngestError::InvalidState => ("invalid_state", true),
        IngestError::Expired => ("session_expired", true),
        IngestError::SessionTooLarge => ("session_too_large", true),
        IngestError::FinalSequenceMismatch { .. } => ("final_sequence_mismatch", false),
        IngestError::AlreadyExists => ("session_already_exists", true),
        IngestError::Redis(_) => ("ingest_unavailable", true),
    }
}

async fn send_control(
    socket: &mut WebSocket,
    message: &ServerControl,
) -> std::result::Result<(), ()> {
    let text = serde_json::to_string(message).map_err(|_| ())?;
    socket
        .send(Message::Text(text.into()))
        .await
        .map_err(|_| ())
}

async fn send_error(
    socket: &mut WebSocket,
    code: &'static str,
    terminal: bool,
) -> std::result::Result<(), ()> {
    send_control(socket, &ServerControl::Error { code, terminal }).await
}

pub fn routes() -> Routes {
    Routes::new()
        .prefix("/api/v1/runs")
        .add("/", post(issue_run_session))
        .add("/{run_id}/stream", get(stream_run_session))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_binary_chunk() {
        let mut bytes = Vec::from(BINARY_MAGIC);
        bytes.extend([1, 0, 0, 0]);
        bytes.extend(7_u64.to_be_bytes());
        bytes.extend(3_u32.to_be_bytes());
        bytes.extend(b"abc");
        let chunk = parse_data_chunk(&bytes, 64).expect("valid chunk");
        assert_eq!(chunk.kind, 0);
        assert_eq!(chunk.sequence, 7);
        assert_eq!(chunk.payload, b"abc");
    }

    #[test]
    fn rejects_payload_length_mismatch() {
        let mut bytes = Vec::from(BINARY_MAGIC);
        bytes.extend([1, 0, 0, 0]);
        bytes.extend(0_u64.to_be_bytes());
        bytes.extend(4_u32.to_be_bytes());
        bytes.extend(b"abc");
        assert_eq!(
            parse_data_chunk(&bytes, 64).expect_err("invalid chunk"),
            "payload_length_mismatch"
        );
    }
}
