use async_trait::async_trait;
use loco_rs::prelude::*;
use std::sync::Arc;
use tuf_replay_server::services::auth::{AccountIdentity, IdentityProvider, IdentityService};
use uuid::Uuid;

pub const GRANT: Uuid = Uuid::from_u128(42);

struct TestIdentity {
    owner: String,
    token: String,
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
        })
    }
    async fn authorize(&self, owner: &str, grant: Uuid) -> Result<()> {
        if owner != self.owner || grant != GRANT {
            return Err(Error::Unauthorized("fixture_grant_invalid".into()));
        }
        Ok(())
    }
}

pub fn authenticate(ctx: &AppContext, owner: &str) -> String {
    let token = Uuid::new_v4().to_string();
    ctx.shared_store
        .insert(IdentityService(Arc::new(TestIdentity {
            owner: owner.into(),
            token: token.clone(),
        })));
    token
}
