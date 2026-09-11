//! Compiled only into the explicitly selected local E2E build.
use crate::domain::*;
use loco_rs::prelude::*;
use std::{path::Path, sync::Arc};

#[derive(serde::Deserialize)]
struct FixtureResult {
    mode: String,
    judgments: [u64; 9],
    key_count: u8,
    speed: f64,
    is_no_hold_tap: bool,
    is_adofai_v2: bool,
}

pub async fn validator(
    ctx: &AppContext,
    manifest: &EvidenceManifest,
) -> Result<Arc<dyn GameplayValidator>> {
    let stream = manifest
        .streams
        .iter()
        .find(|s| s.kind == 2)
        .ok_or_else(|| Error::BadRequest("e2e_metadata_missing".into()))?;
    let bytes: Vec<u8> = ctx.storage.download(Path::new(&stream.storage_key)).await?;
    let meta: serde_json::Value = serde_json::from_slice(&bytes)?;
    let fixture: FixtureResult = serde_json::from_value(
        meta.get("e2e")
            .cloned()
            .ok_or_else(|| Error::BadRequest("e2e_fixture_missing".into()))?,
    )?;
    match fixture.mode.as_str() {
        "unavailable" => Ok(Arc::new(UnavailableValidator)),
        "accepted" => Ok(Arc::new(FixtureValidator(fixture))),
        _ => Err(Error::BadRequest("e2e_mode_invalid".into())),
    }
}

struct FixtureValidator(FixtureResult);

#[async_trait::async_trait]
impl GameplayValidator for FixtureValidator {
    async fn validate(
        &self,
        chart: &OfficialChart,
        evidence: &EvidenceManifest,
    ) -> std::result::Result<ValidationOutcome, String> {
        Ok(ValidationOutcome::Accepted(Box::new(ValidatedResult {
            validator_version: "e2e-fixture-NOT-gameplay-validation-v1".into(),
            rules_version: "e2e-fixture-v1".into(),
            evidence_digest: evidence.digest.clone(),
            official_file_id: chart.file_id.clone(),
            chart_sha256: chart.sha256.clone(),
            gameplay_hash_version: chart.gameplay_hash_version,
            gameplay_hash: chart.gameplay_hash.clone(),
            speed: self.0.speed,
            judgments: self.0.judgments,
            key_count: self.0.key_count,
            is_no_hold_tap: self.0.is_no_hold_tap,
            is_adofai_v2: self.0.is_adofai_v2,
        })))
    }
}
