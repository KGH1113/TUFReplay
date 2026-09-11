use super::errors::ws_ingest_error;
use crate::protocol::run_stream::ServerControl;
use crate::services::ingest::lifecycle::StreamError;
use axum::extract::ws::{Message, WebSocket};
use std::time::Duration;

pub(super) async fn receive(socket: &mut WebSocket) -> Option<Result<Message, axum::Error>> {
    tokio::time::timeout(Duration::from_secs(45), socket.recv())
        .await
        .ok()
        .flatten()
}

pub(super) async fn send(socket: &mut WebSocket, message: Message) -> Result<(), ()> {
    tokio::time::timeout(Duration::from_secs(10), socket.send(message))
        .await
        .map_err(|_| ())?
        .map_err(|_| ())
}

pub(super) async fn send_control(
    socket: &mut WebSocket,
    control: &ServerControl,
) -> Result<(), ()> {
    send(
        socket,
        Message::Text(serde_json::to_string(control).map_err(|_| ())?.into()),
    )
    .await
}

pub(super) async fn send_error(
    socket: &mut WebSocket,
    code: &'static str,
    terminal: bool,
) -> Result<(), ()> {
    send_control(socket, &ServerControl::Error { code, terminal }).await
}

pub(super) async fn send_stream_error(socket: &mut WebSocket, error: &StreamError) -> bool {
    let (code, terminal) = match error {
        StreamError::Ingest(error) => ws_ingest_error(error),
        StreamError::Lifecycle => ("lifecycle_update_failed", true),
        StreamError::Authorization => ("submission_authorization_revoked", true),
        StreamError::AuthorizationUnavailable => ("authorization_unavailable", false),
    };
    send_error(socket, code, terminal).await.is_ok() && !terminal
}
