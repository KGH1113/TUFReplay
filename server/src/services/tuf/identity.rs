use crate::services::auth::internal::InternalTokens;
use loco_rs::prelude::*;
use serde::Deserialize;
use uuid::Uuid;

#[derive(Clone)]
pub struct TufIdentityClient {
    pub client: reqwest::Client,
    pub base_url: String,
    pub tokens: Option<InternalTokens>,
}

#[derive(Deserialize)]
pub struct TufIdentity {
    pub owner_id: String,
    pub grant_id: Uuid,
    pub client_id: String,
    pub username: String,
    pub nickname: Option<String>,
}

impl TufIdentityClient {
    pub async fn identify(&self, access_token: &str) -> Result<TufIdentity> {
        self.call("identity", serde_json::json!({"access_token":access_token}))
            .await
    }

    pub async fn authorize(&self, owner_id: &str, grant_id: Uuid) -> Result<TufIdentity> {
        let identity = self
            .call(
                "authorization",
                serde_json::json!({
                    "owner_id":owner_id,"grant_id":grant_id,
                }),
            )
            .await?;
        if identity.owner_id != owner_id || identity.grant_id != grant_id {
            return Err(Error::Unauthorized("identity_mismatch".into()));
        }
        Ok(identity)
    }

    async fn call(&self, action: &str, body: serde_json::Value) -> Result<TufIdentity> {
        let tokens = self
            .tokens
            .as_ref()
            .ok_or_else(|| Error::Unauthorized("internal_auth_not_configured".into()))?;
        let response = self
            .client
            .post(format!(
                "{}/v2/internal/auto-submission/{action}",
                self.base_url,
            ))
            .bearer_auth(tokens.outgoing())
            .json(&body)
            .send()
            .await
            .map_err(|_| Error::Message("identity_service_unavailable".into()))?;
        if matches!(response.status().as_u16(), 401 | 403) {
            return Err(Error::Unauthorized(
                "submission_authorization_required".into(),
            ));
        }
        if !response.status().is_success() {
            return Err(Error::Message("identity_service_unavailable".into()));
        }
        response
            .json()
            .await
            .map_err(|_| Error::Message("invalid_identity_response".into()))
    }
}

#[async_trait::async_trait]
impl crate::services::auth::IdentityProvider for TufIdentityClient {
    async fn identify(&self, token: &str) -> Result<crate::services::auth::AccountIdentity> {
        let identity = TufIdentityClient::identify(self, token).await?;
        Ok(crate::services::auth::AccountIdentity {
            owner_id: identity.owner_id,
            grant_id: identity.grant_id,
        })
    }
    async fn authorize(&self, owner: &str, grant: Uuid) -> Result<()> {
        TufIdentityClient::authorize(self, owner, grant)
            .await
            .map(|_| ())
    }
}
