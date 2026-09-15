use async_trait::async_trait;
use loco_rs::prelude::*;
use serde::{Deserialize, Serialize};
use std::sync::Arc;
use uuid::Uuid;

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all = "snake_case")]
pub struct AccountIdentity {
    pub owner_id: String,
    pub grant_id: Uuid,
    pub client_id: String,
    pub username: String,
    pub nickname: Option<String>,
    #[serde(default)]
    pub can_submit: bool,
    #[serde(default)]
    pub denial_reason: Option<SubmissionDenialReason>,
}

impl AccountIdentity {
    pub fn require_can_submit(&self) -> Result<()> {
        if self.can_submit {
            return Ok(());
        }

        let reason = self
            .denial_reason
            .map(SubmissionDenialReason::as_str)
            .unwrap_or("submission_authorization_required");
        Err(Error::Unauthorized(reason.into()))
    }
}

#[derive(Clone, Copy, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum SubmissionDenialReason {
    AutoSubmissionDisabled,
    AutoSubmissionTesterRequired,
}

impl SubmissionDenialReason {
    pub const fn as_str(self) -> &'static str {
        match self {
            Self::AutoSubmissionDisabled => "auto_submission_disabled",
            Self::AutoSubmissionTesterRequired => "auto_submission_tester_required",
        }
    }
}

#[async_trait]
pub trait IdentityProvider: Send + Sync {
    async fn identify(&self, token: &str) -> Result<AccountIdentity>;
    async fn authorize(&self, owner: &str, grant: Uuid) -> Result<AccountIdentity>;
}

#[derive(Clone)]
pub struct IdentityService(pub Arc<dyn IdentityProvider>);

impl IdentityService {
    pub fn get(ctx: &AppContext) -> Result<Self> {
        ctx.shared_store
            .get::<Self>()
            .ok_or_else(|| Error::Message("identity_service_unavailable".into()))
    }
}
