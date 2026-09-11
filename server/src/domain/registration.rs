use super::validation::ValidatedResult;
use async_trait::async_trait;

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
        run_id: uuid::Uuid,
        owner_id: &str,
        grant_id: uuid::Uuid,
        level_id: i64,
        current_file_id: &str,
        result: &ValidatedResult,
    ) -> Result<i64, String>;
}
