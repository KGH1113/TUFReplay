use super::{IngestError, RunIngestStore};
use uuid::Uuid;

impl RunIngestStore {
    /// Caller must first confirm the durable manifest has been committed.
    /// Retain the small sealed receipt and its existing TTL for reconnects.
    pub async fn release_chunks(&self, run_id: Uuid) -> Result<(), IngestError> {
        redis::cmd("UNLINK")
            .arg(self.key(super::store::chunks_key(run_id)))
            .query_async::<i64>(&mut self.connection.clone())
            .await?;
        Ok(())
    }
}
