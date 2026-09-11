use async_trait::async_trait;
use loco_rs::prelude::*;
use std::sync::Arc;
use uuid::Uuid;

#[derive(Clone)]
pub struct AccountIdentity {
    pub owner_id: String,
    pub grant_id: Uuid,
}

#[async_trait]
pub trait IdentityProvider: Send + Sync {
    async fn identify(&self, token: &str) -> Result<AccountIdentity>;
    async fn authorize(&self, owner: &str, grant: Uuid) -> Result<()>;
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
