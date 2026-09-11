use super::{errors::ws_ingest_error, stream_socket::*};
use crate::protocol::run_stream::*;
use crate::services::ingest::lifecycle::RunStream;
use crate::services::ingest::AppendOutcome;
use axum::extract::ws::WebSocket;

pub(super) async fn binary(socket: &mut WebSocket, run: &RunStream<'_>, bytes: &[u8]) -> bool {
    let chunk = match parse_data_chunk(bytes, run.store.settings().max_chunk_bytes) {
        Ok(chunk) => chunk,
        Err(code) => return send_error(socket, code, false).await.is_ok(),
    };
    let control = match run
        .store
        .append(
            run.run_id,
            run.token_hash,
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
        Ok(AppendOutcome::Gap { expected_sequence }) => ServerControl::Nack { expected_sequence },
        Err(error) => {
            let (code, terminal) = ws_ingest_error(&error);
            return send_error(socket, code, terminal).await.is_ok() && !terminal;
        }
    };
    send_control(socket, &control).await.is_ok()
}

pub(super) async fn control(socket: &mut WebSocket, run: &RunStream<'_>, text: &str) -> bool {
    let control = match serde_json::from_str::<ClientControl>(text) {
        Ok(control) => control,
        Err(_) => return send_error(socket, "malformed_control", false).await.is_ok(),
    };
    match control {
        ClientControl::Hello { .. } => send_error(socket, "duplicate_hello", false).await.is_ok(),
        ClientControl::Heartbeat => match run.store.heartbeat(run.run_id, run.token_hash).await {
            Ok(acknowledged_sequence) => send_control(
                socket,
                &ServerControl::Ack {
                    acknowledged_sequence,
                },
            )
            .await
            .is_ok(),
            Err(error) => send_stream_error(socket, &error.into()).await,
        },
        ClientControl::Complete {
            final_sequence,
            input_count,
            hit_context_count,
        } => {
            match run
                .seal(final_sequence, input_count, hit_context_count)
                .await
            {
                Ok(receipt) => {
                    let _ = send_control(
                        socket,
                        &ServerControl::Sealed {
                            acknowledged_sequence: receipt.acknowledged_sequence,
                        },
                    )
                    .await;
                    false
                }
                Err(error) => send_stream_error(socket, &error).await,
            }
        }
        ClientControl::Fail { reason } => {
            if reason.is_some_and(|value| value.len() > 256) {
                let _ = send_error(socket, "invalid_failure_reason", true).await;
                return false;
            }
            match run.fail().await {
                Ok(()) => false,
                Err(error) => send_stream_error(socket, &error).await,
            }
        }
    }
}
