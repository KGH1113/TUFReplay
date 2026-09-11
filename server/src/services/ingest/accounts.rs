use super::{IngestError, RunIngestStore};
impl RunIngestStore {
    pub async fn create_owned_session(
        &self,
        run_id: uuid::Uuid,
        token_hash: &str,
        hard_expires_at: i64,
        owner: &str,
    ) -> Result<(), IngestError> {
        let created: i64 = redis::Script::new(include_str!("scripts/create_session_script.lua"))
            .key(self.key(super::store::meta_key(run_id)))
            .key(self.key(format!("tufreplay:active:{owner}")))
            .arg(token_hash)
            .arg(hard_expires_at)
            .arg(self.settings().active_ttl_seconds)
            .arg(owner)
            .invoke_async(&mut self.connection.clone())
            .await?;
        match created {
            1 => Ok(()),
            -7 => Err(IngestError::AccountLimit),
            _ => Err(IngestError::AlreadyExists),
        }
    }
}
