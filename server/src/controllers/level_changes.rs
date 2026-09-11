use crate::services::auth;
use crate::services::ingest::RunIngestStore;
use axum::{
    extract::{
        ws::{Message, WebSocket, WebSocketUpgrade},
        Path, State,
    },
    http::HeaderMap,
    response::IntoResponse,
    Json,
};
use loco_rs::prelude::*;
use serde::Deserialize;
use std::time::Duration;

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Change {
    event_id: String,
}

async fn changed(
    Path(id): Path<i64>,
    State(ctx): State<AppContext>,
    headers: HeaderMap,
    Json(change): Json<Change>,
) -> Result<Response> {
    auth::require_service(&ctx, &headers)?;
    if id <= 0 || change.event_id.is_empty() || change.event_id.len() > 128 {
        return Err(Error::BadRequest("invalid_level_change".into()));
    }
    store(&ctx)?
        .record_level_change(id, &change.event_id)
        .await
        .map_err(|_| Error::Message("notification_unavailable".into()))?;
    format::json(serde_json::json!({"received":true}))
}

async fn watch(
    Path(id): Path<i64>,
    State(ctx): State<AppContext>,
    headers: HeaderMap,
    upgrade: WebSocketUpgrade,
) -> Result<Response> {
    auth::owner(&ctx, &headers).await?;
    if id <= 0 {
        return Err(Error::BadRequest("invalid_level_id".into()));
    }
    let ingest = store(&ctx)?;
    Ok(upgrade
        .max_message_size(1024)
        .on_upgrade(move |socket| watch_socket(socket, ctx, headers, ingest, id))
        .into_response())
}

fn store(ctx: &AppContext) -> Result<RunIngestStore> {
    ctx.shared_store
        .get::<RunIngestStore>()
        .ok_or_else(|| Error::Message("ingest unavailable".into()))
}

async fn watch_socket(
    mut socket: WebSocket,
    ctx: AppContext,
    headers: HeaderMap,
    store: RunIngestStore,
    id: i64,
) {
    let mut ticks = tokio::time::interval(Duration::from_secs(5));
    ticks.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
    loop {
        tokio::select! {
            message = socket.recv() => if !matches!(message, Some(Ok(Message::Pong(_)))) { return; },
            _ = ticks.tick() => {
                if auth::owner(&ctx, &headers).await.is_err() { return; }
                let Ok(generation) = store.level_generation(id).await else { return; };
                let text = serde_json::json!({"type":"level_state","level_id":id,"generation":generation}).to_string();
                if !matches!(tokio::time::timeout(Duration::from_secs(10), socket.send(Message::Text(text.into()))).await, Ok(Ok(()))) { return; }
            }
        }
    }
}

pub fn routes() -> Routes {
    Routes::new()
        .prefix("/api/v1/levels")
        .add("/{id}/changes", get(watch))
}

pub fn internal_routes() -> Routes {
    Routes::new()
        .prefix("/internal/tuf/levels")
        .add("/{id}/changed", post(changed))
}
