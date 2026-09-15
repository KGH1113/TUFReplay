use crate::services::{ingest::RunIngestSettings, tuf::catalog::TufCatalogSettings};
use loco_rs::prelude::*;
use serde::Deserialize;

#[derive(Clone, Deserialize)]
pub struct Settings {
    pub auto_submission: SubmissionSettings,
}

#[derive(Clone, Deserialize)]
pub struct SubmissionSettings {
    #[serde(flatten)]
    pub ingest: RunIngestSettings,
    #[serde(flatten)]
    pub catalog: TufCatalogSettings,
    #[serde(default)]
    pub validation_mode: SubmissionValidationMode,
}

#[derive(Clone, Copy, Debug, Default, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum SubmissionValidationMode {
    #[default]
    Unavailable,
    TrustedTester,
}

impl Settings {
    pub fn parse(config: &loco_rs::config::Config) -> Result<Self> {
        let settings: Self = serde_json::from_value(
            config
                .settings
                .clone()
                .ok_or_else(|| Error::Message("settings.auto_submission is required".into()))?,
        )?;
        let ingest = &settings.auto_submission.ingest;
        let catalog = &settings.auto_submission.catalog;
        if ingest.redis_url.trim().is_empty()
            || ingest.active_ttl_seconds == 0
            || ingest.sealed_ttl_seconds == 0
            || ingest.hard_duration_seconds == 0
            || ingest.max_chunk_bytes == 0
            || ingest.max_session_bytes == 0
            || ingest.heartbeat_interval_ms == 0
            || ingest.max_chunk_bytes as u64 > ingest.max_session_bytes
            || ingest.active_ttl_seconds >= ingest.hard_duration_seconds
            || ingest.hard_duration_seconds > i64::MAX as u64
            || catalog.artifact_root.trim().is_empty()
            || catalog.tuf_api_base_url.trim().is_empty()
            || catalog.artifact_max_download_bytes == 0
            || catalog.artifact_max_extracted_bytes == 0
            || catalog.artifact_max_files == 0
            || catalog.artifact_hydration_timeout_seconds == 0
            || catalog.artifact_max_concurrent_hydrations == 0
        {
            return Err(Error::Message("invalid auto submission settings".into()));
        }
        Ok(settings)
    }

    pub fn get(ctx: &AppContext) -> Result<Self> {
        ctx.shared_store
            .get::<Self>()
            .ok_or_else(|| Error::Message("submission settings unavailable".into()))
    }
}

#[cfg(test)]
mod tests {
    use super::{SubmissionSettings, SubmissionValidationMode};

    #[test]
    fn validation_mode_defaults_to_unavailable_and_rejects_unknown_values() {
        assert_eq!(
            serde_json::from_value::<SubmissionValidationMode>(serde_json::json!("unavailable"))
                .unwrap(),
            SubmissionValidationMode::Unavailable
        );
        assert_eq!(
            serde_json::from_value::<SubmissionValidationMode>(serde_json::json!("trusted_tester"))
                .unwrap(),
            SubmissionValidationMode::TrustedTester
        );
        assert!(
            serde_json::from_value::<SubmissionValidationMode>(serde_json::json!("fixture_accept"))
                .is_err()
        );

        let settings: SubmissionSettings = serde_json::from_value(serde_json::json!({
            "redis_url": "redis://localhost/1",
            "active_ttl_seconds": 60,
            "sealed_ttl_seconds": 120,
            "hard_duration_seconds": 300,
            "max_chunk_bytes": 1024,
            "max_session_bytes": 4096,
            "heartbeat_interval_ms": 100,
            "tuf_api_base_url": "http://localhost",
            "artifact_root": "/tmp/tuf-replay-test",
            "artifact_max_download_bytes": 1024,
            "artifact_max_extracted_bytes": 2048,
            "artifact_max_files": 4,
            "artifact_hydration_timeout_seconds": 5,
            "artifact_max_concurrent_hydrations": 1
        }))
        .unwrap();
        assert_eq!(
            settings.validation_mode,
            SubmissionValidationMode::Unavailable
        );
    }
}
