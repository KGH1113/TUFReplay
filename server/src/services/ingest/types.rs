use serde::Deserialize;

#[derive(Clone, Debug, Deserialize)]
pub struct RunIngestSettings {
    pub redis_url: String,
    #[serde(default)]
    pub redis_key_prefix: String,
    pub active_ttl_seconds: u64,
    pub sealed_ttl_seconds: u64,
    pub hard_duration_seconds: u64,
    pub max_chunk_bytes: usize,
    pub max_session_bytes: u64,
    pub heartbeat_interval_ms: u64,
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
    #[error("account upload quota exceeded")]
    AccountLimit,
    #[error("conflicting duplicate or stale connection")]
    Conflict,
    #[error("final sequence does not match; expected {expected_sequence}")]
    FinalSequenceMismatch { expected_sequence: i64 },
    #[error("run ingest session already exists")]
    AlreadyExists,
    #[error(transparent)]
    Redis(#[from] redis::RedisError),
}
