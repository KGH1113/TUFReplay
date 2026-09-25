use crate::{
    models::pass_visuals,
    services::{auth, replays::PublishedReplay},
};
use axum::{
    extract::{Path, Query, State},
    http::{header::CACHE_CONTROL, HeaderMap, HeaderValue},
    Json,
};
use loco_rs::prelude::*;
use serde::Deserialize;
use uuid::Uuid;

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Owner {
    owner_id: Uuid,
    pass_id: i64,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Update {
    owner_id: Uuid,
    pass_id: i64,
    defaults: pass_visuals::Defaults,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Visibility {
    owner_id: Uuid,
    pass_id: i64,
    hidden: bool,
}

fn response(options: pass_visuals::Options) -> Result<Response> {
    let mut response = format::json(options)?;
    response
        .headers_mut()
        .insert(CACHE_CONTROL, HeaderValue::from_static("no-store"));
    Ok(response)
}
async fn public(State(ctx): State<AppContext>, Path(run): Path<Uuid>) -> Result<Response> {
    PublishedReplay::load(&ctx.db, run).await?;
    response(pass_visuals::public_options(&ctx.db, run).await?)
}
async fn owner(
    State(ctx): State<AppContext>,
    Path(run): Path<Uuid>,
    headers: HeaderMap,
    Query(q): Query<Owner>,
) -> Result<Response> {
    auth::require_service(&ctx, &headers)?;
    response(pass_visuals::owner_options(&ctx.db, run, q.pass_id, &q.owner_id.to_string()).await?)
}
async fn update(
    State(ctx): State<AppContext>,
    Path(run): Path<Uuid>,
    headers: HeaderMap,
    Json(q): Json<Update>,
) -> Result<Response> {
    auth::require_service(&ctx, &headers)?;
    response(
        pass_visuals::save_defaults(
            &ctx.db,
            run,
            q.pass_id,
            &q.owner_id.to_string(),
            &q.defaults,
        )
        .await?,
    )
}
async fn visibility(
    State(ctx): State<AppContext>,
    Path((run, preset)): Path<(Uuid, Uuid)>,
    headers: HeaderMap,
    Json(q): Json<Visibility>,
) -> Result<Response> {
    auth::require_service(&ctx, &headers)?;
    response(
        pass_visuals::set_hidden(
            &ctx.db,
            run,
            q.pass_id,
            &q.owner_id.to_string(),
            preset,
            q.hidden,
        )
        .await?,
    )
}
pub fn routes() -> Routes {
    Routes::new()
        .prefix("/api/v1/replays")
        .add("/{run}/visual-options", get(public))
}
pub fn internal_routes() -> Routes {
    Routes::new()
        .prefix("/internal/tuf/replays")
        .add("/{run}/visuals", get(owner).put(update))
        .add("/{run}/visuals/{preset}/visibility", put(visibility))
}
