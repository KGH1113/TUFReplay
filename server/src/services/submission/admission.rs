//! Check the loaded chart before creating a run or accepting evidence.
use super::issuance::RunClaims;
use crate::domain::OfficialChartProvider;
use crate::services::tuf::catalog::{CatalogError, TufCatalogRuntime};
use loco_rs::prelude::*;
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
pub struct ChartAdmission {
    pub file_id: String,
    pub hash_version: i64,
    pub gameplay_hash: String,
}

impl ChartAdmission {
    pub fn matches(&self, claims: &RunClaims) -> bool {
        self.file_id == claims.file_id
            && self.hash_version == claims.submission_hash_version
            && self.gameplay_hash == claims.submission_hash
    }
}

pub fn valid_hash(version: i64, hash: &str) -> bool {
    version == 1
        && hash.len() == 64
        && hash
            .bytes()
            .all(|b| b.is_ascii_hexdigit() && !b.is_ascii_uppercase())
}

pub async fn check(ctx: &AppContext, claims: &RunClaims) -> Result<ChartAdmission, &'static str> {
    if !valid_hash(claims.submission_hash_version, &claims.submission_hash) {
        return Err("submission_chart_identity_missing_or_unsupported");
    }
    let catalog = ctx
        .shared_store
        .get::<TufCatalogRuntime>()
        .ok_or("catalog_unavailable")?;
    catalog
        .require_eligible(claims.level_id)
        .await
        .map_err(|e| match e {
            CatalogError::IneligibleDifficulty => "level_not_eligible",
            _ => "catalog_unavailable",
        })?;
    let chart = catalog
        .acquire(claims.level_id, &claims.chart_path)
        .await
        .map_err(|e| match e.as_str() {
            "official_chart_ambiguous" => "official_chart_ambiguous",
            "official_chart_unsupported" => "official_chart_unsupported",
            _ => "official_chart_unavailable",
        })?;
    if chart.file_id != claims.file_id {
        return Err("level_revision_outdated");
    }
    if chart.submission_gameplay_hash != claims.submission_hash {
        return Err("submission_chart_gameplay_mismatch");
    }
    Ok(ChartAdmission {
        file_id: chart.file_id,
        hash_version: claims.submission_hash_version,
        gameplay_hash: chart.submission_gameplay_hash,
    })
}

pub fn verify_evidence(
    admission: Option<&serde_json::Value>,
    metadata: &serde_json::Value,
) -> Result<(), &'static str> {
    // Already uploaded legacy records remain subject to final official-chart validation.
    let Some(value) = admission else {
        return Ok(());
    };
    let admitted: ChartAdmission =
        serde_json::from_value(value.clone()).map_err(|_| "submission_chart_admission_invalid")?;
    if metadata["submissionGameplayHashVersion"].as_i64() != Some(admitted.hash_version)
        || metadata["submissionGameplayHashHex"].as_str() != Some(admitted.gameplay_hash.as_str())
    {
        return Err("submission_chart_admission_mismatch");
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    #[test]
    fn admission_rejects_changed_missing_and_unsupported_evidence_identity() {
        let admission = json!({"file_id":"file", "hash_version":1, "gameplay_hash":"a".repeat(64)});
        let mut metadata =
            json!({"submissionGameplayHashVersion":1,"submissionGameplayHashHex":"a".repeat(64)});
        assert!(verify_evidence(Some(&admission), &metadata).is_ok());
        metadata["submissionGameplayHashHex"] = json!("b".repeat(64));
        assert_eq!(
            verify_evidence(Some(&admission), &metadata),
            Err("submission_chart_admission_mismatch")
        );
        assert!(verify_evidence(Some(&admission), &json!({})).is_err());
        assert!(verify_evidence(None, &json!({})).is_ok()); // Legacy records still undergo final validation.
    }

    #[test]
    fn admission_hash_has_one_canonical_wire_representation() {
        assert!(valid_hash(1, &"a".repeat(64)));
        for (version, hash) in [
            (0, "a".repeat(64)),
            (2, "a".repeat(64)),
            (1, "A".repeat(64)),
            (1, "g".repeat(64)),
            (1, "a".repeat(63)),
        ] {
            assert!(!valid_hash(version, &hash));
        }
    }
}
