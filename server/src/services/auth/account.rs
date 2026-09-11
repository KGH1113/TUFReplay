use super::{internal::InternalTokens, AccountIdentity, IdentityService};
use axum::http::HeaderMap;
use loco_rs::prelude::*;

pub fn bearer(headers: &HeaderMap) -> Result<&str> {
    headers
        .get("authorization")
        .and_then(|h| h.to_str().ok())
        .and_then(|v| v.strip_prefix("Bearer "))
        .filter(|s| !s.is_empty() && s.len() <= 8192)
        .ok_or_else(|| Error::Unauthorized("authentication_required".into()))
}

pub fn require_service(ctx: &AppContext, headers: &HeaderMap) -> Result<()> {
    ctx.shared_store
        .get::<InternalTokens>()
        .ok_or_else(|| Error::Unauthorized("internal_auth_not_configured".into()))?
        .authorize(headers)
}

pub async fn identity(ctx: &AppContext, headers: &HeaderMap) -> Result<AccountIdentity> {
    IdentityService::get(ctx)?
        .0
        .identify(bearer(headers)?)
        .await
}

pub async fn owner(ctx: &AppContext, headers: &HeaderMap) -> Result<String> {
    Ok(identity(ctx, headers).await?.owner_id)
}

pub async fn authorize_grant(
    ctx: &AppContext,
    owner: &str,
    grant: Option<uuid::Uuid>,
) -> Result<()> {
    let grant = grant.ok_or_else(|| Error::Unauthorized("oauth_grant_required".into()))?;
    IdentityService::get(ctx)?.0.authorize(owner, grant).await
}
