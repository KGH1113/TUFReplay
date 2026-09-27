use super::validation::ValidatedResult;
use async_trait::async_trait;

pub struct PassRegistration<'a> {
    pub run_id: uuid::Uuid,
    pub owner_id: &'a str,
    pub grant_id: uuid::Uuid,
    pub level_id: i64,
    pub current_file_id: &'a str,
    pub feeling_rating: Option<&'a str>,
}

#[async_trait]
pub trait PassRegistrar: Send + Sync {
    async fn lookup(
        &self,
        run_id: uuid::Uuid,
        owner_id: &str,
        evidence_digest: &str,
    ) -> Result<Option<i64>, String>;
    /// The remote service must uniquely index run_id, including after an
    /// accepted request whose response was lost.
    async fn register(
        &self,
        request: PassRegistration<'_>,
        result: &ValidatedResult,
    ) -> Result<i64, String>;
}
