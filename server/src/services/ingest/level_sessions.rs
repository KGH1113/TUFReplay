use super::{IngestError, RunIngestStore};
use uuid::Uuid;

impl RunIngestStore {
    /// Small connection leases only: idle sockets never allocate a run or evidence stream.
    pub async fn level_session_lease(&self, owner: &str, id: Uuid) -> Result<(), IngestError> {
        let accepted: i64 = redis::Script::new(
            "local now=tonumber(redis.call('TIME')[1]); redis.call('ZREMRANGEBYSCORE',KEYS[1],'-inf',now); \
             if not redis.call('ZSCORE',KEYS[1],ARGV[1]) and redis.call('ZCARD',KEYS[1])>=4 then return 0 end; \
             redis.call('ZADD',KEYS[1],now+60,ARGV[1]); redis.call('EXPIRE',KEYS[1],60); return 1",
        ).key(self.key(format!("tufreplay:level-connections:{owner}")))
            .arg(id.to_string()).invoke_async(&mut self.connection.clone()).await?;
        if accepted == 1 {
            Ok(())
        } else {
            Err(IngestError::AccountLimit)
        }
    }

    pub async fn release_level_session(&self, owner: &str, id: Uuid) -> Result<(), IngestError> {
        redis::cmd("ZREM")
            .arg(self.key(format!("tufreplay:level-connections:{owner}")))
            .arg(id.to_string())
            .query_async::<i64>(&mut self.connection.clone())
            .await?;
        Ok(())
    }

    pub async fn allow_run_start(&self, owner: &str) -> Result<(), IngestError> {
        let count: i64 = redis::Script::new(
            "local n=redis.call('INCR',KEYS[1]); if n==1 then redis.call('EXPIRE',KEYS[1],60) end; return n",
        ).key(self.key(format!("tufreplay:run-start-rate:{owner}")))
            .invoke_async(&mut self.connection.clone()).await?;
        if count <= 120 {
            Ok(())
        } else {
            Err(IngestError::AccountLimit)
        }
    }
}
