use std::sync::Arc;

use redis::{aio::ConnectionManager, Script};
use serde::Deserialize;
use uuid::Uuid;

const CREATE_SESSION_SCRIPT: &str = r#"
if redis.call('EXISTS', KEYS[1]) == 1 then
  return 0
end
redis.call('HSET', KEYS[1],
  'status', 'created',
  'token_hash', ARGV[1],
  'next_sequence', 0,
  'total_bytes', 0,
  'hard_expires_at', ARGV[2])
redis.call('EXPIRE', KEYS[1], ARGV[3])
return 1
"#;

const AUTHORIZE_SCRIPT: &str = r#"
if redis.call('EXISTS', KEYS[1]) == 0 then return -1 end
if redis.call('HGET', KEYS[1], 'token_hash') ~= ARGV[1] then return -2 end
local status = redis.call('HGET', KEYS[1], 'status')
if status ~= 'created' and status ~= 'streaming' then return -3 end
local now = tonumber(redis.call('TIME')[1])
if now >= tonumber(redis.call('HGET', KEYS[1], 'hard_expires_at')) then return -4 end
return 1
"#;

const OPEN_SCRIPT: &str = r#"
if redis.call('EXISTS', KEYS[1]) == 0 then return {-1, -1} end
if redis.call('HGET', KEYS[1], 'token_hash') ~= ARGV[1] then return {-2, -1} end
local status = redis.call('HGET', KEYS[1], 'status')
if status ~= 'created' and status ~= 'streaming' then return {-3, -1} end
local now = tonumber(redis.call('TIME')[1])
if now >= tonumber(redis.call('HGET', KEYS[1], 'hard_expires_at')) then return {-4, -1} end
redis.call('HSET', KEYS[1], 'status', 'streaming')
redis.call('EXPIRE', KEYS[1], ARGV[2])
if redis.call('EXISTS', KEYS[2]) == 1 then redis.call('EXPIRE', KEYS[2], ARGV[2]) end
return {1, tonumber(redis.call('HGET', KEYS[1], 'next_sequence')) - 1}
"#;

const APPEND_SCRIPT: &str = r#"
if redis.call('EXISTS', KEYS[1]) == 0 then return {-1, -1} end
if redis.call('HGET', KEYS[1], 'token_hash') ~= ARGV[1] then return {-2, -1} end
if redis.call('HGET', KEYS[1], 'status') ~= 'streaming' then return {-3, -1} end
local now = tonumber(redis.call('TIME')[1])
if now >= tonumber(redis.call('HGET', KEYS[1], 'hard_expires_at')) then return {-4, -1} end
local expected = tonumber(redis.call('HGET', KEYS[1], 'next_sequence'))
local sequence = tonumber(ARGV[2])
if sequence < expected then return {2, expected - 1} end
if sequence > expected then return {3, expected} end
local total = tonumber(redis.call('HGET', KEYS[1], 'total_bytes')) + tonumber(ARGV[5])
if total > tonumber(ARGV[6]) then return {-5, -1} end
redis.call('XADD', KEYS[2], '*', 'sequence', ARGV[2], 'kind', ARGV[3], 'payload', ARGV[4])
redis.call('HSET', KEYS[1], 'next_sequence', expected + 1, 'total_bytes', total)
redis.call('EXPIRE', KEYS[1], ARGV[7])
redis.call('EXPIRE', KEYS[2], ARGV[7])
return {1, sequence}
"#;

const HEARTBEAT_SCRIPT: &str = r#"
if redis.call('EXISTS', KEYS[1]) == 0 then return {-1, -1} end
if redis.call('HGET', KEYS[1], 'token_hash') ~= ARGV[1] then return {-2, -1} end
if redis.call('HGET', KEYS[1], 'status') ~= 'streaming' then return {-3, -1} end
local now = tonumber(redis.call('TIME')[1])
if now >= tonumber(redis.call('HGET', KEYS[1], 'hard_expires_at')) then return {-4, -1} end
redis.call('EXPIRE', KEYS[1], ARGV[2])
if redis.call('EXISTS', KEYS[2]) == 1 then redis.call('EXPIRE', KEYS[2], ARGV[2]) end
return {1, tonumber(redis.call('HGET', KEYS[1], 'next_sequence')) - 1}
"#;

const SEAL_SCRIPT: &str = r#"
if redis.call('EXISTS', KEYS[1]) == 0 then return {-1, -1, -1} end
if redis.call('HGET', KEYS[1], 'token_hash') ~= ARGV[1] then return {-2, -1, -1} end
local status = redis.call('HGET', KEYS[1], 'status')
local ack = tonumber(redis.call('HGET', KEYS[1], 'next_sequence')) - 1
local total = tonumber(redis.call('HGET', KEYS[1], 'total_bytes'))
if status == 'sealed' then return {2, ack, total} end
if status ~= 'streaming' then return {-3, -1, -1} end
if tonumber(ARGV[2]) ~= ack then return {3, ack + 1, total} end
redis.call('HSET', KEYS[1], 'status', 'sealed', 'input_count', ARGV[3], 'hit_context_count', ARGV[4])
redis.call('EXPIRE', KEYS[1], ARGV[5])
if redis.call('EXISTS', KEYS[2]) == 1 then redis.call('EXPIRE', KEYS[2], ARGV[5]) end
return {1, ack, total}
"#;

const FAIL_SCRIPT: &str = r#"
if redis.call('EXISTS', KEYS[1]) == 0 then return 0 end
if redis.call('HGET', KEYS[1], 'token_hash') ~= ARGV[1] then return -1 end
local status = redis.call('HGET', KEYS[1], 'status')
if status ~= 'created' and status ~= 'streaming' then return -3 end
redis.call('DEL', KEYS[1], KEYS[2])
return 1
"#;

const RELEASE_LOCK_SCRIPT: &str = r#"
if redis.call('GET', KEYS[1]) == ARGV[1] then
  return redis.call('DEL', KEYS[1])
end
return 0
"#;

#[derive(Clone, Debug, Deserialize)]
pub struct RunIngestSettings {
    pub redis_url: String,
    pub active_ttl_seconds: u64,
    pub sealed_ttl_seconds: u64,
    pub hard_duration_seconds: u64,
    pub max_chunk_bytes: usize,
    pub max_session_bytes: u64,
    pub heartbeat_interval_ms: u64,
}

#[derive(Clone)]
pub struct RunIngestStore {
    connection: ConnectionManager,
    settings: Arc<RunIngestSettings>,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum AppendOutcome {
    Accepted { acknowledged_sequence: i64 },
    Duplicate { acknowledged_sequence: i64 },
    Gap { expected_sequence: i64 },
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct SealOutcome {
    pub acknowledged_sequence: i64,
    pub total_payload_bytes: i64,
    pub already_sealed: bool,
}

#[derive(Debug, thiserror::Error)]
pub enum IngestError {
    #[error("run ingest session was not found or expired")]
    NotFound,
    #[error("invalid upload token")]
    Unauthorized,
    #[error("run ingest session is not in the required state")]
    InvalidState,
    #[error("run ingest session reached its hard deadline")]
    Expired,
    #[error("run payload exceeds the configured session limit")]
    SessionTooLarge,
    #[error("final sequence does not match; expected {expected_sequence}")]
    FinalSequenceMismatch { expected_sequence: i64 },
    #[error("run ingest session already exists")]
    AlreadyExists,
    #[error(transparent)]
    Redis(#[from] redis::RedisError),
}

impl RunIngestStore {
    pub async fn connect(settings: RunIngestSettings) -> Result<Self, IngestError> {
        let client = redis::Client::open(settings.redis_url.as_str())?;
        let mut connection = ConnectionManager::new(client).await?;
        redis::cmd("PING")
            .query_async::<String>(&mut connection)
            .await?;
        Ok(Self {
            connection,
            settings: Arc::new(settings),
        })
    }

    #[must_use]
    pub fn settings(&self) -> &RunIngestSettings {
        &self.settings
    }

    pub async fn create_session(
        &self,
        run_id: Uuid,
        token_hash_hex: &str,
        hard_expires_at_unix: i64,
    ) -> Result<(), IngestError> {
        let mut connection = self.connection.clone();
        let created = Script::new(CREATE_SESSION_SCRIPT)
            .key(meta_key(run_id))
            .arg(token_hash_hex)
            .arg(hard_expires_at_unix)
            .arg(self.settings.active_ttl_seconds)
            .invoke_async::<i64>(&mut connection)
            .await?;
        if created == 1 {
            Ok(())
        } else {
            Err(IngestError::AlreadyExists)
        }
    }

    pub async fn open(&self, run_id: Uuid, token_hash_hex: &str) -> Result<i64, IngestError> {
        let values = self
            .invoke_pair(
                OPEN_SCRIPT,
                run_id,
                &[
                    token_hash_hex.to_owned(),
                    self.settings.active_ttl_seconds.to_string(),
                ],
            )
            .await?;
        decode_ack(values)
    }

    pub async fn authorize(&self, run_id: Uuid, token_hash_hex: &str) -> Result<(), IngestError> {
        let mut connection = self.connection.clone();
        let result = Script::new(AUTHORIZE_SCRIPT)
            .key(meta_key(run_id))
            .arg(token_hash_hex)
            .invoke_async::<i64>(&mut connection)
            .await?;
        if result == 1 {
            Ok(())
        } else {
            Err(decode_error(result))
        }
    }

    pub async fn append(
        &self,
        run_id: Uuid,
        token_hash_hex: &str,
        sequence: u64,
        kind: u8,
        payload: &[u8],
    ) -> Result<AppendOutcome, IngestError> {
        if payload.len() > self.settings.max_chunk_bytes {
            return Err(IngestError::SessionTooLarge);
        }
        let sequence = i64::try_from(sequence).map_err(|_| IngestError::InvalidState)?;
        let mut connection = self.connection.clone();
        let values = Script::new(APPEND_SCRIPT)
            .key(meta_key(run_id))
            .key(chunks_key(run_id))
            .arg(token_hash_hex)
            .arg(sequence)
            .arg(kind)
            .arg(payload)
            .arg(payload.len())
            .arg(self.settings.max_session_bytes)
            .arg(self.settings.active_ttl_seconds)
            .invoke_async::<Vec<i64>>(&mut connection)
            .await?;
        match values.as_slice() {
            [1, acknowledged_sequence] => Ok(AppendOutcome::Accepted {
                acknowledged_sequence: *acknowledged_sequence,
            }),
            [2, acknowledged_sequence] => Ok(AppendOutcome::Duplicate {
                acknowledged_sequence: *acknowledged_sequence,
            }),
            [3, expected_sequence] => Ok(AppendOutcome::Gap {
                expected_sequence: *expected_sequence,
            }),
            [code, _] => Err(decode_error(*code)),
            _ => Err(IngestError::InvalidState),
        }
    }

    pub async fn heartbeat(&self, run_id: Uuid, token_hash_hex: &str) -> Result<i64, IngestError> {
        let values = self
            .invoke_pair(
                HEARTBEAT_SCRIPT,
                run_id,
                &[
                    token_hash_hex.to_owned(),
                    self.settings.active_ttl_seconds.to_string(),
                ],
            )
            .await?;
        decode_ack(values)
    }

    pub async fn seal(
        &self,
        run_id: Uuid,
        token_hash_hex: &str,
        final_sequence: i64,
        input_count: u64,
        hit_context_count: u64,
    ) -> Result<SealOutcome, IngestError> {
        let mut connection = self.connection.clone();
        let values = Script::new(SEAL_SCRIPT)
            .key(meta_key(run_id))
            .key(chunks_key(run_id))
            .arg(token_hash_hex)
            .arg(final_sequence)
            .arg(input_count)
            .arg(hit_context_count)
            .arg(self.settings.sealed_ttl_seconds)
            .invoke_async::<Vec<i64>>(&mut connection)
            .await?;
        match values.as_slice() {
            [code @ (1 | 2), acknowledged_sequence, total_payload_bytes] => Ok(SealOutcome {
                acknowledged_sequence: *acknowledged_sequence,
                total_payload_bytes: *total_payload_bytes,
                already_sealed: *code == 2,
            }),
            [3, expected_sequence, _] => Err(IngestError::FinalSequenceMismatch {
                expected_sequence: *expected_sequence,
            }),
            [code, _, _] => Err(decode_error(*code)),
            _ => Err(IngestError::InvalidState),
        }
    }

    pub async fn fail(&self, run_id: Uuid, token_hash_hex: &str) -> Result<(), IngestError> {
        let mut connection = self.connection.clone();
        let result = Script::new(FAIL_SCRIPT)
            .key(meta_key(run_id))
            .key(chunks_key(run_id))
            .arg(token_hash_hex)
            .invoke_async::<i64>(&mut connection)
            .await?;
        match result {
            -1 => Err(IngestError::Unauthorized),
            -3 => Err(IngestError::InvalidState),
            _ => Ok(()),
        }
    }

    pub async fn discard(&self, run_id: Uuid) -> Result<(), IngestError> {
        let mut connection = self.connection.clone();
        redis::cmd("DEL")
            .arg(meta_key(run_id))
            .arg(chunks_key(run_id))
            .query_async::<i64>(&mut connection)
            .await?;
        Ok(())
    }

    pub async fn try_acquire_revision_lock(
        &self,
        tuf_level_id: i64,
        tuf_file_id: &str,
        owner: &str,
        ttl_seconds: u64,
    ) -> Result<bool, IngestError> {
        let mut connection = self.connection.clone();
        let result: Option<String> = redis::cmd("SET")
            .arg(revision_lock_key(tuf_level_id, tuf_file_id))
            .arg(owner)
            .arg("NX")
            .arg("EX")
            .arg(ttl_seconds)
            .query_async(&mut connection)
            .await?;
        Ok(result.is_some())
    }

    pub async fn release_revision_lock(
        &self,
        tuf_level_id: i64,
        tuf_file_id: &str,
        owner: &str,
    ) -> Result<(), IngestError> {
        let mut connection = self.connection.clone();
        Script::new(RELEASE_LOCK_SCRIPT)
            .key(revision_lock_key(tuf_level_id, tuf_file_id))
            .arg(owner)
            .invoke_async::<i64>(&mut connection)
            .await?;
        Ok(())
    }

    async fn invoke_pair(
        &self,
        script: &str,
        run_id: Uuid,
        args: &[String],
    ) -> Result<Vec<i64>, IngestError> {
        let mut connection = self.connection.clone();
        let script = Script::new(script);
        let mut invocation = script.prepare_invoke();
        invocation.key(meta_key(run_id)).key(chunks_key(run_id));
        for arg in args {
            invocation.arg(arg);
        }
        Ok(invocation.invoke_async(&mut connection).await?)
    }
}

fn decode_ack(values: Vec<i64>) -> Result<i64, IngestError> {
    match values.as_slice() {
        [1, acknowledged_sequence] => Ok(*acknowledged_sequence),
        [code, _] => Err(decode_error(*code)),
        _ => Err(IngestError::InvalidState),
    }
}

fn decode_error(code: i64) -> IngestError {
    match code {
        -1 => IngestError::NotFound,
        -2 => IngestError::Unauthorized,
        -3 => IngestError::InvalidState,
        -4 => IngestError::Expired,
        -5 => IngestError::SessionTooLarge,
        _ => IngestError::InvalidState,
    }
}

fn meta_key(run_id: Uuid) -> String {
    format!("tufreplay:run:{run_id}:meta")
}

fn chunks_key(run_id: Uuid) -> String {
    format!("tufreplay:run:{run_id}:chunks")
}

fn revision_lock_key(tuf_level_id: i64, tuf_file_id: &str) -> String {
    use sha2::{Digest, Sha256};
    let file_id_hash = hex::encode(Sha256::digest(tuf_file_id.as_bytes()));
    format!("tufreplay:level-revision:{tuf_level_id}:{file_id_hash}:lock")
}

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use redis::streams::StreamRangeReply;
    use serial_test::serial;

    use super::*;

    fn settings(
        active_ttl_seconds: u64,
        max_chunk_bytes: usize,
        max_session_bytes: u64,
    ) -> RunIngestSettings {
        RunIngestSettings {
            redis_url: "redis://127.0.0.1:6379/1".to_owned(),
            active_ttl_seconds,
            sealed_ttl_seconds: 60,
            hard_duration_seconds: 60,
            max_chunk_bytes,
            max_session_bytes,
            heartbeat_interval_ms: 1_000,
        }
    }

    async fn create_open(store: &RunIngestStore, run_id: Uuid, token: &str) {
        store
            .create_session(run_id, token, chrono::Utc::now().timestamp() + 60)
            .await
            .expect("create ingest session");
        assert_eq!(store.open(run_id, token).await.expect("open session"), -1);
    }

    #[tokio::test]
    #[serial]
    async fn append_is_ordered_idempotent_and_sealable() {
        let store = RunIngestStore::connect(settings(10, 64, 256))
            .await
            .expect("connect Redis");
        let run_id = Uuid::new_v4();
        let token = "token-hash";
        create_open(&store, run_id, token).await;

        assert_eq!(
            store
                .append(run_id, token, 0, 0, b"native-a")
                .await
                .expect("append"),
            AppendOutcome::Accepted {
                acknowledged_sequence: 0
            }
        );
        assert_eq!(
            store
                .append(run_id, token, 0, 0, b"ignored")
                .await
                .expect("duplicate"),
            AppendOutcome::Duplicate {
                acknowledged_sequence: 0
            }
        );
        assert_eq!(
            store
                .append(run_id, token, 2, 1, b"gap")
                .await
                .expect("gap"),
            AppendOutcome::Gap {
                expected_sequence: 1
            }
        );
        store
            .append(run_id, token, 1, 0, b"native-b")
            .await
            .expect("second append");

        assert!(matches!(
            store.seal(run_id, token, 0, 2, 0).await,
            Err(IngestError::FinalSequenceMismatch {
                expected_sequence: 2
            })
        ));
        let sealed = store.seal(run_id, token, 1, 2, 0).await.expect("seal");
        assert_eq!(sealed.acknowledged_sequence, 1);
        assert_eq!(sealed.total_payload_bytes, 16);
        assert!(!sealed.already_sealed);
        assert!(
            store
                .seal(run_id, token, 1, 2, 0)
                .await
                .expect("idempotent seal")
                .already_sealed
        );
        assert!(matches!(
            store.append(run_id, token, 2, 0, b"late").await,
            Err(IngestError::InvalidState)
        ));

        let mut connection = store.connection.clone();
        let stream: StreamRangeReply = redis::cmd("XRANGE")
            .arg(chunks_key(run_id))
            .arg("-")
            .arg("+")
            .query_async(&mut connection)
            .await
            .expect("read stream");
        let restored: Vec<u8> = stream
            .ids
            .iter()
            .filter(|entry| entry.get::<u8>("kind") == Some(0))
            .flat_map(|entry| entry.get::<Vec<u8>>("payload").expect("payload"))
            .collect();
        assert_eq!(restored, b"native-anative-b");

        store.discard(run_id).await.expect("cleanup");
    }

    #[tokio::test]
    #[serial]
    async fn concurrent_duplicate_is_appended_once() {
        let store = RunIngestStore::connect(settings(10, 64, 256))
            .await
            .expect("connect Redis");
        let run_id = Uuid::new_v4();
        let token = "concurrent-token";
        create_open(&store, run_id, token).await;

        let left = store.clone();
        let right = store.clone();
        let (left, right) = tokio::join!(
            left.append(run_id, token, 0, 0, b"same"),
            right.append(run_id, token, 0, 0, b"same")
        );
        assert!(left.is_ok() && right.is_ok());

        let mut connection = store.connection.clone();
        let length: i64 = redis::cmd("XLEN")
            .arg(chunks_key(run_id))
            .query_async(&mut connection)
            .await
            .expect("stream length");
        assert_eq!(length, 1);
        store.discard(run_id).await.expect("cleanup");
    }

    #[tokio::test]
    #[serial]
    async fn enforces_limits_refreshes_ttl_and_expires() {
        let store = RunIngestStore::connect(settings(2, 4, 6))
            .await
            .expect("connect Redis");
        let run_id = Uuid::new_v4();
        let token = "limits-token";
        create_open(&store, run_id, token).await;
        assert!(matches!(
            store.append(run_id, token, 0, 0, b"12345").await,
            Err(IngestError::SessionTooLarge)
        ));
        store
            .append(run_id, token, 0, 0, b"1234")
            .await
            .expect("within chunk limit");
        assert!(matches!(
            store.append(run_id, token, 1, 0, b"123").await,
            Err(IngestError::SessionTooLarge)
        ));

        tokio::time::sleep(Duration::from_millis(1_100)).await;
        store.heartbeat(run_id, token).await.expect("heartbeat");
        let mut connection = store.connection.clone();
        let ttl: i64 = redis::cmd("TTL")
            .arg(meta_key(run_id))
            .query_async(&mut connection)
            .await
            .expect("TTL");
        assert!(ttl >= 1);
        store.discard(run_id).await.expect("cleanup");

        let expiring = RunIngestStore::connect(settings(1, 4, 6))
            .await
            .expect("connect Redis");
        let expired_run_id = Uuid::new_v4();
        expiring
            .create_session(expired_run_id, token, chrono::Utc::now().timestamp() + 60)
            .await
            .expect("create expiring session");
        tokio::time::sleep(Duration::from_millis(1_200)).await;
        assert!(matches!(
            expiring.authorize(expired_run_id, token).await,
            Err(IngestError::NotFound)
        ));
    }
}
