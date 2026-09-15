use axum::{extract::State, http::HeaderMap};
use loco_rs::prelude::*;

pub fn routes() -> Routes {
    Routes::new().prefix("/api/v1").add("/account", get(show))
}

pub async fn show(State(ctx): State<AppContext>, headers: HeaderMap) -> Result<Response> {
    format::json(crate::services::auth::identity(&ctx, &headers).await?)
}
