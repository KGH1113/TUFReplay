use super::{errors::*, stream_socket::*, support::*};
use crate::models::run_sessions::Model;
use crate::protocol::run_stream::*;
use crate::services::ingest::lifecycle::RunStream;
use crate::services::ingest::RunIngestStore;
use axum::{
    extract::{
        ws::{Message, WebSocket, WebSocketUpgrade},
        Path, State,
    },
    http::HeaderMap,
    response::IntoResponse,
};
use loco_rs::prelude::*;
use sha2::{Digest, Sha256};
use uuid::Uuid;

#[debug_handler]
pub async fn stream_run_session(
    Path(run_id): Path<Uuid>,
    State(ctx): State<AppContext>,
    headers: HeaderMap,
    websocket: WebSocketUpgrade,
) -> Result<Response> {
    let token_hash = hex::encode(Sha256::digest(bearer_token(&headers)?.as_bytes()));
    let store = ingest_store(&ctx)?.for_connection(Uuid::new_v4());
    store
        .authorize(run_id, &token_hash)
        .await
        .map_err(http_ingest_error)?;
    Model::find_by_pid(&ctx.db, run_id).await?;
    let limit = store.settings().max_chunk_bytes + BINARY_HEADER_BYTES;
    Ok(websocket
        .max_message_size(limit)
        .max_frame_size(limit)
        .on_upgrade(move |socket| handle_socket(socket, ctx, store, run_id, token_hash))
        .into_response())
}

async fn handle_socket(
    mut socket: WebSocket,
    ctx: AppContext,
    store: RunIngestStore,
    run_id: Uuid,
    token_hash: String,
) {
    let Some(Ok(Message::Text(text))) = receive(&mut socket).await else {
        let _ = send_error(&mut socket, "hello_required", true).await;
        return;
    };
    let Ok(ClientControl::Hello {
        protocol_version,
        last_acknowledged_sequence,
    }) = serde_json::from_str(&text)
    else {
        let _ = send_error(&mut socket, "invalid_hello", true).await;
        return;
    };
    if protocol_version != PROTOCOL_VERSION {
        let _ = send_error(&mut socket, "unsupported_protocol", true).await;
        return;
    }
    if last_acknowledged_sequence < -1 {
        let _ = send_error(&mut socket, "invalid_hello", true).await;
        return;
    }
    if let Ok(receipt) = store.receipt(run_id, Some(&token_hash)).await {
        if receipt.status == "sealed" {
            let _ = send_control(
                &mut socket,
                &ServerControl::Sealed {
                    acknowledged_sequence: receipt.acknowledged_sequence,
                },
            )
            .await;
            return;
        }
    }
    let run = RunStream {
        ctx: &ctx,
        store: &store,
        run_id,
        token_hash: &token_hash,
    };
    let acknowledged_sequence = match run.open().await {
        Ok(value) => value,
        Err(error) => {
            let _ = send_stream_error(&mut socket, &error).await;
            return;
        }
    };
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
    let mut authorization = tokio::time::interval(std::time::Duration::from_secs(5));
    authorization.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
    authorization.tick().await;
    loop {
        let message = tokio::select! {
            message = receive(&mut socket) => match message { Some(Ok(message)) => message, _ => return },
            _ = authorization.tick() => {
                if let Err(error) = run.authorize().await {
                    if matches!(error, crate::services::ingest::lifecycle::StreamError::Authorization) {
                        let _ = run.fail().await;
                    }
                    let _ = send_stream_error(&mut socket, &error).await;
                    return;
                }
                continue;
            }
        };
        let keep_open = match message {
            Message::Binary(bytes) => {
                super::stream_messages::binary(&mut socket, &run, &bytes).await
            }
            Message::Text(text) => super::stream_messages::control(&mut socket, &run, &text).await,
            Message::Ping(payload) => send(&mut socket, Message::Pong(payload)).await.is_ok(),
            Message::Pong(_) => true,
            Message::Close(_) => false,
        };
        if !keep_open {
            return;
        }
    }
}
