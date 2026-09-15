use async_trait::async_trait;
use loco_rs::prelude::*;
use std::sync::Arc;
use tuf_replay_server::services::auth::{
    AccountIdentity, IdentityProvider, IdentityService, SubmissionDenialReason,
};
use uuid::Uuid;

pub const GRANT: Uuid = Uuid::from_u128(42);

struct TestIdentity {
    owner: String,
    token: String,
    can_submit: bool,
}

#[async_trait]
impl IdentityProvider for TestIdentity {
    async fn identify(&self, token: &str) -> Result<AccountIdentity> {
        if token != self.token {
            return Err(Error::Unauthorized("fixture_token_invalid".into()));
        }
        Ok(AccountIdentity {
            owner_id: self.owner.clone(),
            grant_id: GRANT,
            client_id: "fixture-client".into(),
            username: "fixture-user".into(),
            nickname: Some("Fixture User".into()),
            can_submit: self.can_submit,
            denial_reason: (!self.can_submit)
                .then_some(SubmissionDenialReason::AutoSubmissionTesterRequired),
        })
    }
    async fn authorize(&self, owner: &str, grant: Uuid) -> Result<AccountIdentity> {
        if owner != self.owner || grant != GRANT {
            return Err(Error::Unauthorized("fixture_grant_invalid".into()));
        }
        Ok(AccountIdentity {
            owner_id: self.owner.clone(),
            grant_id: GRANT,
            client_id: "fixture-client".into(),
            username: "fixture-user".into(),
            nickname: Some("Fixture User".into()),
            can_submit: self.can_submit,
            denial_reason: (!self.can_submit)
                .then_some(SubmissionDenialReason::AutoSubmissionTesterRequired),
        })
    }
}

pub fn authenticate(ctx: &AppContext, owner: &str) -> String {
    authenticate_with_policy(ctx, owner, true)
}

pub fn authenticate_with_policy(ctx: &AppContext, owner: &str, can_submit: bool) -> String {
    let token = Uuid::new_v4().to_string();
    ctx.shared_store
        .insert(IdentityService(Arc::new(TestIdentity {
            owner: owner.into(),
            token: token.clone(),
            can_submit,
        })));
    token
}
