use super::types::{AppendOutcome, IngestError, RunIngestSettings, SealOutcome};
use std::sync::Arc;

use redis::{aio::ConnectionManager, Script};
use uuid::Uuid;

const CREATE_SESSION_SCRIPT: &str = include_str!("scripts/create_session_script.lua");

const AUTHORIZE_SCRIPT: &str = include_str!("scripts/authorize_script.lua");

const OPEN_SCRIPT: &str = include_str!("scripts/open_script.lua");

const APPEND_SCRIPT: &str = include_str!("scripts/append_script.lua");

const HEARTBEAT_SCRIPT: &str = include_str!("scripts/heartbeat_script.lua");

const SEAL_SCRIPT: &str = include_str!("scripts/seal_script.lua");

const FAIL_SCRIPT: &str = include_str!("scripts/fail_script.lua");

#[derive(Clone)]
pub struct RunIngestStore {
    connection_id: String,
    pub(super) connection: ConnectionManager,
    settings: Arc<RunIngestSettings>,
}

impl RunIngestStore {
    pub fn for_connection(mut self, id: Uuid) -> Self {
        self.connection_id = id.to_string();
        self
    }

    pub async fn connect(settings: RunIngestSettings) -> Result<Self, IngestError> {
        let client = redis::Client::open(settings.redis_url.as_str())?;
        let mut connection = ConnectionManager::new(client).await?;
        redis::cmd("PING")
            .query_async::<String>(&mut connection)
            .await?;
        Ok(Self {
            connection,
            connection_id: String::new(),
            settings: Arc::new(settings),
        })
    }

    #[must_use]
    pub(super) fn key(&self, key: String) -> String {
        format!("{}{}", self.settings.redis_key_prefix, key)
    }

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
            .key(self.key(meta_key(run_id)))
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
                    self.connection_id.clone(),
                ],
            )
            .await?;
        decode_ack(values)
    }

    pub async fn authorize(&self, run_id: Uuid, token_hash_hex: &str) -> Result<(), IngestError> {
        let mut connection = self.connection.clone();
        let result = Script::new(AUTHORIZE_SCRIPT)
            .key(self.key(meta_key(run_id)))
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
        use sha2::{Digest, Sha256};
        let mut content_hash = Sha256::new();
        content_hash.update([kind]);
        content_hash.update(payload);
        let mut connection = self.connection.clone();
        let values = Script::new(APPEND_SCRIPT)
            .key(self.key(meta_key(run_id)))
            .key(self.key(chunks_key(run_id)))
            .arg(token_hash_hex)
            .arg(sequence)
            .arg(kind)
            .arg(payload)
            .arg(payload.len())
            .arg(self.settings.max_session_bytes)
            .arg(self.settings.active_ttl_seconds)
            .arg(&self.connection_id)
            .arg(hex::encode(content_hash.finalize()))
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
                    self.connection_id.clone(),
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
            .key(self.key(meta_key(run_id)))
            .key(self.key(chunks_key(run_id)))
            .arg(token_hash_hex)
            .arg(final_sequence)
            .arg(input_count)
            .arg(hit_context_count)
            .arg(self.settings.sealed_ttl_seconds)
            .arg(&self.connection_id)
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
            .key(self.key(meta_key(run_id)))
            .key(self.key(chunks_key(run_id)))
            .arg(token_hash_hex)
            .arg(&self.connection_id)
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
            .arg(self.key(meta_key(run_id)))
            .arg(self.key(chunks_key(run_id)))
            .query_async::<i64>(&mut connection)
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
        invocation
            .key(self.key(meta_key(run_id)))
            .key(self.key(chunks_key(run_id)));
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
        -6 => IngestError::Conflict,
        -7 => IngestError::AccountLimit,
        _ => IngestError::InvalidState,
    }
}

pub(super) fn meta_key(run_id: Uuid) -> String {
    format!("tufreplay:run:{run_id}:meta")
}

pub(super) fn chunks_key(run_id: Uuid) -> String {
    format!("tufreplay:run:{run_id}:chunks")
}

#[cfg(test)]
#[path = "tests.rs"]
mod tests;
