use super::evidence::EvidenceManifest;
use async_trait::async_trait;
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct ValidatedResult {
    pub validator_version: String,
    pub rules_version: String,
    pub evidence_digest: String,
    pub official_file_id: String,
    pub chart_sha256: String,
    pub gameplay_hash_version: u32,
    pub gameplay_hash: String,
    pub speed: f64,
    pub judgments: [u64; 9],
    pub key_count: u8,
    pub is_no_hold_tap: bool,
    pub is_adofai_v2: bool,
}

pub enum ValidationOutcome {
    Accepted(Box<ValidatedResult>),
    Rejected(&'static str),
    Unavailable,
}

/// Only a semantic validator may construct an accepted outcome. Ingest and
/// structural validation never imply that a play qualifies for submission.
#[async_trait]
pub trait GameplayValidator: Send + Sync {
    fn available(&self) -> bool {
        true
    }

    async fn validate(
        &self,
        chart: &OfficialChart,
        evidence: &EvidenceManifest,
    ) -> Result<ValidationOutcome, String>;
}

pub struct UnavailableValidator;

#[async_trait]
impl GameplayValidator for UnavailableValidator {
    fn available(&self) -> bool {
        false
    }

    async fn validate(
        &self,
        _: &OfficialChart,
        _: &EvidenceManifest,
    ) -> Result<ValidationOutcome, String> {
        Ok(ValidationOutcome::Unavailable)
    }
}

/// Owned only for the duration of validation; never persisted as an artifact.
pub struct OfficialChart {
    pub file_id: String,
    pub sha256: String,
    pub gameplay_hash_version: u32,
    pub gameplay_hash: String,
    pub bytes: Vec<u8>,
}

#[async_trait]
pub trait OfficialChartProvider: Send + Sync {
    async fn acquire(&self, level_id: i64, relative_path: &str) -> Result<OfficialChart, String>;
}
