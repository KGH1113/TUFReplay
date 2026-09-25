use super::{
    level_protocol::{self, Control},
    stream_socket, support,
};
use crate::{
    models::{
        run_sessions::{self, Model},
        run_submission_records::Entity as Records,
    },
    protocol::run_stream::{parse_data_chunk, BINARY_HEADER_BYTES},
    services::{
        auth::{self, AccountIdentity},
        ingest::{lifecycle::RunStream, AppendOutcome, RunIngestStore},
        submission::issuance::{self, RunClaims},
    },
};
use axum::{
    extract::{
        ws::{Message, WebSocket, WebSocketUpgrade},
        Path,
    },
    http::HeaderMap,
    response::IntoResponse,
};
use loco_rs::prelude::*;
use sea_orm::{ColumnTrait, EntityTrait, QueryFilter};
use serde_json::{json, Value};
use std::time::{Duration, Instant};
use uuid::Uuid;

pub async fn upgrade(
    Path(level_id): Path<i64>,
    State(ctx): State<AppContext>,
    headers: HeaderMap,
    upgrade: WebSocketUpgrade,
) -> Result<Response> {
    let identity = auth::identity(&ctx, &headers).await?;
    identity.require_can_submit()?;
    if level_id <= 0 {
        return Err(Error::BadRequest("invalid_level_id".into()));
    }
    let connection_id = Uuid::new_v4();
    let store = support::ingest_store(&ctx)?.for_connection(connection_id);
    store
        .level_session_lease(&identity.owner_id, connection_id)
        .await
        .map_err(super::errors::http_ingest_error)?;
    let limit =
        store.settings().max_chunk_bytes + BINARY_HEADER_BYTES + level_protocol::ENVELOPE_BYTES;
    Ok(upgrade
        .max_message_size(limit)
        .max_frame_size(limit)
        .on_upgrade(move |socket| async move {
            serve(socket, &ctx, &store, &identity, level_id, connection_id).await;
            let _ = store
                .release_level_session(&identity.owner_id, connection_id)
                .await;
        })
        .into_response())
}

async fn reply(socket: &mut WebSocket, value: Value) -> bool {
    stream_socket::send(socket, Message::Text(value.to_string().into()))
        .await
        .is_ok()
}

fn error(id: Option<Uuid>, code: &'static str) -> Value {
    json!({"type":"error", "run_id":id, "code":code, "terminal":true})
}

async fn serve(
    mut socket: WebSocket,
    ctx: &AppContext,
    store: &RunIngestStore,
    identity: &AccountIdentity,
    level_id: i64,
    connection_id: Uuid,
) {
    let Some(Ok(Message::Text(text))) = stream_socket::receive(&mut socket).await else {
        return;
    };
    if !matches!(
        serde_json::from_str::<Control>(&text),
        Ok(Control::SessionHello {
            protocol_version: level_protocol::VERSION
        })
    ) {
        let _ = reply(&mut socket, error(None, "unsupported_protocol")).await;
        return;
    }
    if !reply(
        &mut socket,
        json!({"type":"session_ready", "protocol_version":level_protocol::VERSION}),
    )
    .await
    {
        return;
    }
    let mut active: Option<Model> = None;
    // Warm only public official-chart bytes, never a player's run or evidence.
    // A cold archive can finish while the first attempt's bounded check expires.
    if let Ok(catalog) = support::catalog_runtime(ctx) {
        tokio::spawn(async move {
            use crate::domain::OfficialChartProvider;
            let _ = catalog.acquire(level_id, "").await;
        });
    }
    let mut ticks = tokio::time::interval(Duration::from_secs(5));
    ticks.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
    let mut last_message = Instant::now();
    let mut budget_at = Instant::now();
    let mut messages = 0;
    loop {
        let message = tokio::select! {
            value = socket.recv() => match value { Some(Ok(value)) => value, _ => return },
            _ = ticks.tick() => {
                if last_message.elapsed() > Duration::from_secs(45) { return; }
                if let Err(cause) = auth::authorize_grant(ctx, &identity.owner_id, Some(identity.grant_id)).await {
                    if matches!(cause, Error::Unauthorized(_)) {
                        if let Some(model) = &active { let _ = run(ctx, store, model, &hex::encode(&model.upload_token_hash)).fail().await; }
                    }
                    let _ = reply(&mut socket, error(None, "submission_authorization_unavailable")).await;
                    return;
                }
                if store.level_session_lease(&identity.owner_id, connection_id).await.is_err() { return; }
                continue;
            }
        };
        last_message = Instant::now();
        if budget_at.elapsed() >= Duration::from_secs(1) {
            messages = 0;
            budget_at = Instant::now();
        }
        messages += 1;
        if messages > 256 {
            let _ = reply(&mut socket, error(None, "message_rate_exceeded")).await;
            return;
        }
        let response = match message {
            Message::Text(text) => match serde_json::from_str::<Control>(&text) {
                Ok(Control::Heartbeat) => json!({"type":"heartbeat"}),
                Ok(Control::RunStart {
                    run_id,
                    client_game_version,
                    client_mod_version,
                    client_tuf_file_id,
                    client_level_relative_path,
                    submission_gameplay_hash_version,
                    submission_gameplay_hash_hex,
                    last_acknowledged_sequence,
                }) => {
                    if active.as_ref().is_some_and(|run| run.pid != run_id) {
                        error(Some(run_id), "previous_run_active")
                    } else if last_acknowledged_sequence < -1 {
                        error(Some(run_id), "invalid_acknowledgement")
                    } else {
                        let claims = RunClaims {
                            protocol_version: level_protocol::VERSION,
                            game_version: client_game_version,
                            mod_version: client_mod_version,
                            level_id,
                            file_id: client_tuf_file_id,
                            chart_path: client_level_relative_path,
                            submission_hash_version: submission_gameplay_hash_version,
                            submission_hash: submission_gameplay_hash_hex,
                        };
                        match start(ctx, identity, run_id, claims).await {
                            Ok(model) => {
                                let hash = hex::encode(&model.upload_token_hash);
                                if model.status == "failed" {
                                    error(Some(run_id), "run_failed")
                                } else if let Ok(receipt) = store.receipt(run_id, Some(&hash)).await
                                {
                                    if receipt.status == "sealed" {
                                        active = None;
                                        json!({"type":"sealed", "run_id":run_id, "acknowledged_sequence":receipt.acknowledged_sequence})
                                    } else {
                                        match run(ctx, store, &model, &hash).open().await {
                                            Ok(ack) => {
                                                active = Some(model);
                                                json!({"type":"ready", "chart_verified":true, "run_id":run_id, "acknowledged_sequence":ack, "max_chunk_bytes":store.settings().max_chunk_bytes, "heartbeat_interval_ms":store.settings().heartbeat_interval_ms})
                                            }
                                            Err(_) => error(Some(run_id), "run_start_unavailable"),
                                        }
                                    }
                                } else {
                                    error(Some(run_id), "run_evidence_expired")
                                }
                            }
                            Err(code) => error(Some(run_id), code),
                        }
                    }
                }
                Ok(Control::RunHeartbeat { run_id }) => match &active {
                    Some(model) if model.pid == run_id => match store
                        .heartbeat(run_id, &hex::encode(&model.upload_token_hash))
                        .await
                    {
                        Ok(ack) => {
                            json!({"type":"ack", "run_id":run_id, "acknowledged_sequence":ack})
                        }
                        Err(_) => error(Some(run_id), "run_evidence_expired"),
                    },
                    _ => error(Some(run_id), "run_not_active"),
                },
                Ok(Control::RunFail { run_id }) => {
                    let result =
                        terminal(ctx, store, identity, level_id, &active, run_id, None).await;
                    if (result["type"] == "failed" || result["type"] == "sealed")
                        && active.as_ref().is_some_and(|model| model.pid == run_id)
                    {
                        active = None;
                    }
                    result
                }
                Ok(Control::RunComplete {
                    run_id,
                    final_sequence,
                    input_count,
                    hit_context_count,
                }) => {
                    let result = terminal(
                        ctx,
                        store,
                        identity,
                        level_id,
                        &active,
                        run_id,
                        Some((final_sequence, input_count, hit_context_count)),
                    )
                    .await;
                    if result["type"] == "sealed"
                        && active.as_ref().is_some_and(|model| model.pid == run_id)
                    {
                        active = None;
                    }
                    result
                }
                _ => error(None, "malformed_control"),
            },
            Message::Binary(bytes) => match level_protocol::parse_binary(&bytes) {
                Ok((id, payload)) => match &active {
                    Some(model) if model.pid == id => {
                        match parse_data_chunk(payload, store.settings().max_chunk_bytes) {
                            Ok(chunk) => match store
                                .append(
                                    id,
                                    &hex::encode(&model.upload_token_hash),
                                    chunk.sequence,
                                    chunk.kind,
                                    chunk.payload,
                                )
                                .await
                            {
                                Ok(
                                    AppendOutcome::Accepted {
                                        acknowledged_sequence: ack,
                                    }
                                    | AppendOutcome::Duplicate {
                                        acknowledged_sequence: ack,
                                    },
                                ) => {
                                    json!({"type":"ack", "run_id":id, "acknowledged_sequence":ack})
                                }
                                Ok(AppendOutcome::Gap { expected_sequence }) => {
                                    json!({"type":"nack", "run_id":id, "expected_sequence":expected_sequence})
                                }
                                Err(cause) => {
                                    error(Some(id), super::errors::ws_ingest_error(&cause).0)
                                }
                            },
                            Err(code) => error(Some(id), code),
                        }
                    }
                    _ => error(Some(id), "run_not_active"),
                },
                Err(code) => error(None, code),
            },
            Message::Ping(value) => {
                if stream_socket::send(&mut socket, Message::Pong(value))
                    .await
                    .is_err()
                {
                    return;
                }
                continue;
            }
            Message::Pong(_) => continue,
            Message::Close(_) => return,
        };
        if !reply(&mut socket, response).await {
            return;
        }
    }
}

fn run<'a>(
    ctx: &'a AppContext,
    store: &'a RunIngestStore,
    model: &Model,
    hash: &'a str,
) -> RunStream<'a> {
    RunStream {
        ctx,
        store,
        run_id: model.pid,
        token_hash: hash,
    }
}

async fn owned(
    ctx: &AppContext,
    identity: &AccountIdentity,
    id: Uuid,
) -> std::result::Result<Option<Model>, &'static str> {
    let model = run_sessions::Entity::find()
        .filter(run_sessions::Column::Pid.eq(id))
        .one(&ctx.db)
        .await
        .map_err(|_| "run_lookup_unavailable")?;
    if let Some(model) = &model {
        let record = Records::record(&ctx.db, model.id)
            .await
            .map_err(|_| "run_owner_mismatch")?;
        if record.owner_id != identity.owner_id || record.oauth_grant_id != Some(identity.grant_id)
        {
            return Err("run_owner_mismatch");
        }
    }
    Ok(model)
}

async fn start(
    ctx: &AppContext,
    identity: &AccountIdentity,
    id: Uuid,
    claims: RunClaims,
) -> std::result::Result<Model, &'static str> {
    auth::authorize_grant(ctx, &identity.owner_id, Some(identity.grant_id))
        .await
        .map_err(|_| "submission_authorization_unavailable")?;
    if id.is_nil()
        || claims.game_version.is_empty()
        || claims.game_version.len() > 64
        || claims.mod_version.is_empty()
        || claims.mod_version.len() > 64
        || claims.file_id.is_empty()
        || claims.file_id.len() > 256
        || claims.chart_path.is_empty()
        || claims.chart_path.len() > 1024
    {
        return Err("invalid_run_start");
    }
    if let Some(model) = owned(ctx, identity, id).await? {
        if model.protocol_version != level_protocol::VERSION
            || model.tuf_level_id != claims.level_id
            || model.client_tuf_file_id != claims.file_id
            || model.client_level_relative_path != claims.chart_path
            || model.client_game_version != claims.game_version
            || model.client_mod_version != claims.mod_version
            || model.chart_admission.as_ref()
                .and_then(|v| serde_json::from_value::<crate::services::submission::admission::ChartAdmission>(v.clone()).ok())
                .is_none_or(|admission| !admission.matches(&claims))
        {
            return Err("run_start_conflict");
        }
        return Ok(model);
    }
    issuance::issue_with_id(ctx, identity.clone(), claims, id)
        .await
        .map(|issued| issued.run)
        .map_err(|error| match error {
            issuance::IssuanceError::Admission(code) => code,
            _ => "run_start_unavailable",
        })
}

async fn terminal(
    ctx: &AppContext,
    store: &RunIngestStore,
    identity: &AccountIdentity,
    level_id: i64,
    active: &Option<Model>,
    id: Uuid,
    complete: Option<(i64, u64, u64)>,
) -> Value {
    if auth::authorize_grant(ctx, &identity.owner_id, Some(identity.grant_id))
        .await
        .is_err()
    {
        return error(Some(id), "submission_authorization_unavailable");
    }
    let model = match owned(ctx, identity, id).await {
        Ok(Some(model)) => model,
        _ => return error(Some(id), "run_not_found"),
    };
    if model.tuf_level_id != level_id || model.protocol_version != level_protocol::VERSION {
        return error(Some(id), "run_not_found");
    }
    let hash = hex::encode(&model.upload_token_hash);
    // Terminal receipts win races; neither duplicate fail nor late fail can undo a seal.
    if let Ok(receipt) = store.receipt(id, Some(&hash)).await {
        if receipt.status == "sealed" {
            if complete.is_some_and(|(seq, inputs, hits)| {
                seq != receipt.acknowledged_sequence
                    || inputs != receipt.input_count
                    || hits != receipt.hit_context_count
            }) {
                return error(Some(id), "completion_conflict");
            }
            return json!({"type":"sealed", "run_id":id, "acknowledged_sequence":receipt.acknowledged_sequence});
        }
    }
    if model.status == "failed" {
        return if complete.is_none() {
            json!({"type":"failed", "run_id":id})
        } else {
            error(Some(id), "run_failed")
        };
    }
    if active.as_ref().is_none_or(|model| model.pid != id) {
        return error(Some(id), "run_not_active");
    }
    let run = run(ctx, store, &model, &hash);
    match complete {
        Some((seq, inputs, hits)) => match run.seal(seq, inputs, hits).await {
            Ok(receipt) => {
                json!({"type":"sealed", "run_id":id, "acknowledged_sequence":receipt.acknowledged_sequence})
            }
            Err(_) => error(Some(id), "completion_rejected"),
        },
        None => match run.fail().await {
            Ok(()) => json!({"type":"failed", "run_id":id}),
            Err(_) => error(Some(id), "failure_rejected"),
        },
    }
}
