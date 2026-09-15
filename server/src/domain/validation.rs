use super::evidence::EvidenceManifest;
use async_trait::async_trait;
use serde::{Deserialize, Deserializer, Serialize};

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ValidationStatus {
    SkippedTrustedTester,
    #[default]
    Validated,
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ResultProvenance {
    RecordedGameResult,
    #[default]
    GameplayValidator,
}

#[derive(Clone, Debug, Serialize)]
pub struct ValidatedResult {
    pub validation_contract_version: u32,
    pub validation_status: ValidationStatus,
    pub result_provenance: ResultProvenance,
    pub validator_version: String,
    pub rules_version: String,
    pub evidence_digest: String,
    pub official_file_id: String,
    pub chart_sha256: String,
    pub gameplay_hash_version: u32,
    pub gameplay_hash: String,
    pub speed: f64,
    pub judgments: [u64; 9],
    pub perfect_minus: u64,
    pub perfect_plus: u64,
    pub key_count: u8,
    pub is_no_hold_tap: bool,
    pub is_adofai_v2: bool,
    pub adofai_version: u8,
    pub is_x_perfect_mode: bool,
}

impl ValidatedResult {
    #[must_use]
    pub fn has_valid_v2_contract(&self) -> bool {
        self.validation_contract_version == 2
            && matches!(
                (self.validation_status, self.result_provenance),
                (
                    ValidationStatus::SkippedTrustedTester,
                    ResultProvenance::RecordedGameResult
                ) | (
                    ValidationStatus::Validated,
                    ResultProvenance::GameplayValidator
                )
            )
            && (1..=3).contains(&self.adofai_version)
            && self.is_adofai_v2 == (self.adofai_version == 1)
            && (!self.is_x_perfect_mode || self.adofai_version == 3)
            && (self.is_x_perfect_mode || (self.perfect_minus == 0 && self.perfect_plus == 0))
    }
}

// Public replay rows from before contract v2 remain readable. They are never
// produced by the trusted-tester adapter; missing markers identify legacy rows.
impl<'de> Deserialize<'de> for ValidatedResult {
    fn deserialize<D>(deserializer: D) -> std::result::Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        #[derive(Deserialize)]
        struct LegacyCompatibleResult {
            #[serde(default)]
            validation_contract_version: Option<u32>,
            #[serde(default)]
            validation_status: Option<ValidationStatus>,
            #[serde(default)]
            result_provenance: Option<ResultProvenance>,
            validator_version: String,
            rules_version: String,
            evidence_digest: String,
            official_file_id: String,
            chart_sha256: String,
            gameplay_hash_version: u32,
            gameplay_hash: String,
            speed: f64,
            judgments: [u64; 9],
            #[serde(default)]
            perfect_minus: u64,
            #[serde(default)]
            perfect_plus: u64,
            key_count: u8,
            is_no_hold_tap: bool,
            is_adofai_v2: bool,
            #[serde(default)]
            adofai_version: Option<u8>,
            #[serde(default)]
            is_x_perfect_mode: bool,
        }

        let legacy = LegacyCompatibleResult::deserialize(deserializer)?;
        Ok(Self {
            validation_contract_version: legacy.validation_contract_version.unwrap_or(1),
            validation_status: legacy.validation_status.unwrap_or_default(),
            result_provenance: legacy.result_provenance.unwrap_or_default(),
            validator_version: legacy.validator_version,
            rules_version: legacy.rules_version,
            evidence_digest: legacy.evidence_digest,
            official_file_id: legacy.official_file_id,
            chart_sha256: legacy.chart_sha256,
            gameplay_hash_version: legacy.gameplay_hash_version,
            gameplay_hash: legacy.gameplay_hash,
            speed: legacy.speed,
            judgments: legacy.judgments,
            perfect_minus: legacy.perfect_minus,
            perfect_plus: legacy.perfect_plus,
            key_count: legacy.key_count,
            is_no_hold_tap: legacy.is_no_hold_tap,
            is_adofai_v2: legacy.is_adofai_v2,
            adofai_version: legacy.adofai_version.unwrap_or(if legacy.is_adofai_v2 {
                1
            } else {
                2
            }),
            is_x_perfect_mode: legacy.is_x_perfect_mode,
        })
    }
}

pub enum ValidationOutcome {
    Accepted(Box<ValidatedResult>),
    Rejected(&'static str),
    Unavailable,
}

/// The gameplay-validation path only accepts results produced by semantic
/// replay validation. The separately configured trusted-tester path may also
/// produce an accepted result, but tags it as skipped semantic validation with
/// recorded-game-result provenance. Ingest and structural validation alone do
/// not imply that a play qualifies for submission.
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

#[cfg(test)]
mod tests {
    use super::ValidatedResult;

    #[test]
    fn persisted_v1_results_remain_deserializable_as_legacy_values() {
        let result: ValidatedResult = serde_json::from_value(serde_json::json!({
            "validator_version": "legacy-v1",
            "rules_version": "legacy-rules-v1",
            "evidence_digest": "a".repeat(64),
            "official_file_id": "legacy-file",
            "chart_sha256": "b".repeat(64),
            "gameplay_hash_version": 1,
            "gameplay_hash": "c".repeat(64),
            "speed": 1.0,
            "judgments": [0, 0, 0, 0, 1, 0, 0, 0, 0],
            "key_count": 4,
            "is_no_hold_tap": false,
            "is_adofai_v2": true
        }))
        .unwrap();

        assert_eq!(result.validation_contract_version, 1);
        assert_eq!(result.adofai_version, 1);
        assert!(!result.has_valid_v2_contract());
    }
}
