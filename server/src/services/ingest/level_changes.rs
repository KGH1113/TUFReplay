use super::{IngestError, RunIngestStore};
use sha2::{Digest, Sha256};

impl RunIngestStore {
    pub async fn record_level_change(
        &self,
        level_id: i64,
        event_id: &str,
    ) -> Result<(), IngestError> {
        let event = hex::encode(Sha256::digest(event_id.as_bytes()));
        let script = redis::Script::new("if redis.call('SET',KEYS[1],'1','NX','EX',2592000) then redis.call('INCR',KEYS[2]); redis.call('EXPIRE',KEYS[2],2592000); end; return 1");
        let mut connection = self.connection.clone();
        let _: i64 = script
            .key(self.key(format!("tuf:level-change:{level_id}:{event}")))
            .key(self.key(format!("tuf:level-generation:{level_id}")))
            .invoke_async(&mut connection)
            .await?;
        Ok(())
    }

    pub async fn level_generation(&self, level_id: i64) -> Result<i64, IngestError> {
        let mut connection = self.connection.clone();
        let value: Option<i64> = redis::cmd("GET")
            .arg(self.key(format!("tuf:level-generation:{level_id}")))
            .query_async(&mut connection)
            .await?;
        Ok(value.unwrap_or_default())
    }
}
