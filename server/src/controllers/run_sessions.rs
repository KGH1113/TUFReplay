use loco_rs::prelude::*;
mod actions;
mod dtos;
mod errors;
mod issuance;
mod stream;
mod stream_messages;
mod stream_socket;
mod support;

pub use issuance::issue_run_session;
pub use stream::stream_run_session;

pub fn routes() -> Routes {
    Routes::new()
        .prefix("/api/v1/runs")
        .add("/", post(issue_run_session))
        .add("/", get(actions::list))
        .add("/{run_id}", get(actions::get_run))
        .add("/{run_id}", delete(actions::delete_run))
        .add("/{run_id}/submit", post(actions::submit))
        .add("/{run_id}/receipt", get(actions::receipt))
        .add("/{run_id}/stream", get(stream_run_session))
}
