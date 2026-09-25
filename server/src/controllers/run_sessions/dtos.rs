use serde::{Deserialize, Serialize};
use uuid::Uuid;
use validator::Validate;
#[derive(Debug, Deserialize, Validate)]
#[serde(deny_unknown_fields)]
pub struct CreateRunSessionRequest {
    #[validate(range(min = 1, max = 1))]
    pub protocol_version: i64,
    #[validate(length(min = 1, max = 64))]
    pub client_game_version: String,
    #[validate(length(min = 1, max = 64))]
    pub client_mod_version: String,
    #[validate(range(min = 1))]
    pub tuf_level_id: i64,
    #[validate(length(min = 1, max = 256))]
    pub client_tuf_file_id: String,
    #[validate(length(equal = 64))]
    pub client_installed_payload_hash_hex: String,
    #[validate(range(min = 1, max = 1))]
    pub client_payload_hash_version: i64,
    #[validate(length(min = 1, max = 1024))]
    pub client_level_relative_path: String,
    #[serde(default)]
    pub submission_gameplay_hash_version: i64,
    #[serde(default)]
    pub submission_gameplay_hash_hex: String,
}

#[derive(Debug, Serialize)]
pub(super) struct CreateRunSessionResponse {
    pub run_id: Uuid,
    pub upload_token: String,
    pub websocket_url: String,
    pub lease_expires_at: chrono::DateTime<chrono::FixedOffset>,
    pub lease_duration_ms: u64,
    pub max_chunk_bytes: usize,
    pub heartbeat_interval_ms: u64,
}

#[derive(Debug, Serialize)]
pub(super) struct ErrorResponse {
    pub code: &'static str,
}

#[derive(Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SubmitRequest {
    #[serde(default)]
    pub presentation: Option<SubmitPresentation>,
}

#[derive(Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SubmitPresentation {
    #[serde(default)]
    pub keyviewer_id: Option<Uuid>,
    #[serde(default)]
    pub overlay_id: Option<Uuid>,
}
