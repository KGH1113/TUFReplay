use crate::domain::submission_gameplay_hash::SUBMISSION_GAMEPLAY_HASH_VERSION;
use crate::{
    domain::{
        EvidenceManifest, OfficialChartProvider, ResultProvenance, ValidatedResult,
        ValidationOutcome, ValidationStatus,
    },
    models::run_sessions::{Model, RunStatus},
};
use futures_util::StreamExt;
use loco_rs::prelude::*;
use serde::Deserialize;
use sha2::{Digest, Sha256};
use std::path::Path;

const MAX_METADATA_BYTES: u64 = 262_144;
const MAX_RECORDED_JUDGMENTS: u64 = 1_000_000;
const MAX_CLEAR_TIME_US: i64 = 21_600_000_000;

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct SubmissionResultSnapshot {
    version: u8,
    judgments: [u64; 9],
    perfect_minus: u64,
    perfect_plus: u64,
    adofai_version: u8,
    is_x_perfect_mode: bool,
    is_no_hold_tap: bool,
}

struct RecordedClaims {
    speed: f64,
    key_count: u8,
    adofai_version: u8,
    is_x_perfect_mode: bool,
    is_no_hold_tap: bool,
}

/// Converts bounded game-recorded result metadata into the explicit v2
/// trusted-tester result. This path performs structural and eligibility checks;
/// it never claims that gameplay was simulated.
pub async fn validate(
    ctx: &AppContext,
    run: &Model,
    manifest: &EvidenceManifest,
    charts: &dyn OfficialChartProvider,
) -> Result<ValidationOutcome> {
    if !valid_manifest(run, manifest) {
        return Ok(ValidationOutcome::Rejected("evidence_manifest_invalid"));
    }
    let Some(metadata_stream) = metadata_stream(run, manifest) else {
        return Ok(ValidationOutcome::Rejected("submission_metadata_missing"));
    };
    let Some(metadata_bytes) = read_metadata(ctx, metadata_stream).await? else {
        return Ok(ValidationOutcome::Rejected("submission_metadata_too_large"));
    };
    if metadata_bytes.len() as u64 != metadata_stream.bytes
        || hex::encode(Sha256::digest(&metadata_bytes)) != metadata_stream.sha256
    {
        return Ok(ValidationOutcome::Rejected(
            "submission_metadata_integrity_failed",
        ));
    }

    let metadata = match serde_json::from_slice::<serde_json::Value>(&metadata_bytes) {
        Ok(metadata) if metadata.is_object() => metadata,
        _ => return Ok(ValidationOutcome::Rejected("submission_metadata_invalid")),
    };
    let snapshot = match metadata.get("submissionResult") {
        Some(value) => match serde_json::from_value::<SubmissionResultSnapshot>(value.clone()) {
            Ok(snapshot) => snapshot,
            Err(_) => return Ok(ValidationOutcome::Rejected("recorded_result_invalid")),
        },
        None => return Ok(ValidationOutcome::Rejected("recorded_result_missing")),
    };
    let Some(claims) = recorded_claims(
        run.pid,
        &run.status,
        &run.client_game_version,
        &metadata,
        &snapshot,
    ) else {
        return Ok(ValidationOutcome::Rejected("recorded_result_ineligible"));
    };

    // Acquisition resolves current TUF metadata and the current official chart
    // archive. Any upstream failure remains retryable through the worker path.
    let chart = match charts
        .acquire(run.tuf_level_id, &run.client_level_relative_path)
        .await
    {
        Ok(chart) => chart,
        Err(reason) if reason == "official_chart_ambiguous" => {
            return Ok(ValidationOutcome::Rejected("official_chart_ambiguous"))
        }
        Err(reason) if reason == "official_chart_unsupported" => {
            return Ok(ValidationOutcome::Rejected("official_chart_unsupported"))
        }
        Err(_) => return Err(Error::Message("official_chart_unavailable".into())),
    };

    if let Err(reason) = verify_submission_chart(&metadata, &chart.submission_gameplay_hash) {
        return Ok(ValidationOutcome::Rejected(reason));
    }

    Ok(ValidationOutcome::Accepted(Box::new(build_result(
        &manifest.digest,
        chart,
        snapshot,
        claims,
    ))))
}

fn verify_submission_chart(
    metadata: &serde_json::Value,
    expected: &str,
) -> std::result::Result<(), &'static str> {
    if metadata
        .get("submissionGameplayHashVersion")
        .and_then(|v| v.as_u64())
        != Some(SUBMISSION_GAMEPLAY_HASH_VERSION.into())
    {
        return Err("submission_chart_identity_missing_or_unsupported");
    }
    let claimed = metadata
        .get("submissionGameplayHashHex")
        .and_then(|v| v.as_str())
        .filter(|s| {
            s.len() == 64
                && s.bytes()
                    .all(|c| c.is_ascii_digit() || (b'a'..=b'f').contains(&c))
        })
        .ok_or("submission_chart_identity_invalid")?;
    if claimed != expected {
        return Err("submission_chart_gameplay_mismatch");
    }
    Ok(())
}

fn build_result(
    evidence_digest: &str,
    chart: crate::domain::OfficialChart,
    snapshot: SubmissionResultSnapshot,
    claims: RecordedClaims,
) -> ValidatedResult {
    let perfect_minus = if claims.is_x_perfect_mode {
        snapshot.perfect_minus
    } else {
        0
    };
    let perfect_plus = if claims.is_x_perfect_mode {
        snapshot.perfect_plus
    } else {
        0
    };
    ValidatedResult {
        validation_contract_version: 2,
        validation_status: ValidationStatus::SkippedTrustedTester,
        result_provenance: ResultProvenance::RecordedGameResult,
        validator_version: "trusted-tester-adapter-v2".into(),
        rules_version: "recorded-game-result-submission-chart-v1".into(),
        evidence_digest: evidence_digest.into(),
        official_file_id: chart.file_id,
        chart_sha256: chart.sha256,
        gameplay_hash_version: chart.gameplay_hash_version,
        gameplay_hash: chart.gameplay_hash,
        speed: claims.speed,
        judgments: snapshot.judgments,
        perfect_minus,
        perfect_plus,
        key_count: claims.key_count,
        is_no_hold_tap: claims.is_no_hold_tap,
        is_adofai_v2: claims.adofai_version == 1,
        adofai_version: claims.adofai_version,
        is_x_perfect_mode: claims.is_x_perfect_mode,
    }
}

fn valid_manifest(run: &Model, manifest: &EvidenceManifest) -> bool {
    if manifest.protocol_version != 1 || manifest.final_sequence < 0 {
        return false;
    }
    let Ok(serialized_streams) = serde_json::to_vec(&manifest.streams) else {
        return false;
    };
    if hex::encode(Sha256::digest(serialized_streams)) != manifest.digest {
        return false;
    }

    let prefix = format!("evidence/{}/", run.pid);
    let mut seen = [false; 7];
    for stream in &manifest.streams {
        let kind = usize::from(stream.kind);
        if kind >= seen.len()
            || seen[kind]
            || stream.bytes == 0
            || stream.records == 0
            || !stream.storage_key.starts_with(&prefix)
            || hex::decode(&stream.sha256).map_or(true, |digest| digest.len() != 32)
        {
            return false;
        }
        seen[kind] = true;
    }

    seen[0] && seen[1] && seen[2] && seen[6]
}

fn metadata_stream<'a>(
    run: &Model,
    manifest: &'a EvidenceManifest,
) -> Option<&'a crate::domain::EvidenceStream> {
    let mut matches = manifest.streams.iter().filter(|stream| stream.kind == 2);
    let stream = matches.next()?;
    if matches.next().is_some()
        || stream.records != 1
        || stream.bytes == 0
        || stream.bytes > MAX_METADATA_BYTES
        || !stream
            .storage_key
            .starts_with(&format!("evidence/{}/", run.pid))
    {
        return None;
    }
    Some(stream)
}

async fn read_metadata(
    ctx: &AppContext,
    stream: &crate::domain::EvidenceStream,
) -> Result<Option<Vec<u8>>> {
    let mut body = ctx
        .storage
        .download_stream(Path::new(&stream.storage_key))
        .await?;
    let mut bytes = Vec::with_capacity(stream.bytes.min(MAX_METADATA_BYTES) as usize);
    while let Some(chunk) = body.next().await {
        let chunk = chunk.map_err(|_| Error::Message("submission_metadata_unavailable".into()))?;
        if bytes.len().saturating_add(chunk.len()) as u64 > MAX_METADATA_BYTES {
            return Ok(None);
        }
        bytes.extend_from_slice(&chunk);
    }
    Ok(Some(bytes))
}

fn recorded_claims(
    run_id: uuid::Uuid,
    run_status: &str,
    issued_game_version: &str,
    metadata: &serde_json::Value,
    snapshot: &SubmissionResultSnapshot,
) -> Option<RecordedClaims> {
    if run_status != RunStatus::Sealed.as_str()
        || metadata.get("metadataVersion").and_then(|v| v.as_u64()) != Some(1)
        || metadata.get("submissionRunId").and_then(|v| v.as_str())? != run_id.to_string()
        || metadata.get("wonTimeUs").and_then(|v| v.as_i64())? <= 0
        || metadata.get("wonTimeUs").and_then(|v| v.as_i64())? > MAX_CLEAR_TIME_US
        || metadata.get("noFailMode").and_then(|v| v.as_bool()) != Some(false)
        || metadata.get("judgmentDifficulty").and_then(|v| v.as_i64()) != Some(2)
        || !all_recorder_failures_clear(metadata)
        || snapshot.version != 1
        || snapshot
            .judgments
            .iter()
            .any(|count| *count > MAX_RECORDED_JUDGMENTS)
        || snapshot.perfect_minus > MAX_RECORDED_JUDGMENTS
        || snapshot.perfect_plus > MAX_RECORDED_JUDGMENTS
        || snapshot.judgments[0] != 0
        || snapshot.judgments[8] != 0
    {
        return None;
    }

    let key_count = metadata
        .get("keyCount")
        .and_then(|v| v.as_u64())
        .filter(|count| (1..=64).contains(count))? as u8;
    let speed = metadata
        .get("effectivePitch")
        .and_then(|v| v.as_f64())
        .filter(|speed| speed.is_finite() && (1.0..=100.0).contains(speed))?;
    let game_version = metadata.get("gameVersion").and_then(|v| v.as_str())?;
    if game_version != issued_game_version {
        return None;
    }
    let adofai_version = parse_adofai_version(game_version)?;
    let judgment_system = metadata.get("judgmentSystem").and_then(|v| v.as_str())?;
    let is_x_perfect_mode = match judgment_system {
        "Legacy" | "ModernClassic" => false,
        "ModernCompetitive" => true,
        _ => return None,
    };
    let is_no_hold_tap = match metadata.get("holdBehavior").and_then(|v| v.as_i64())? {
        0 => false,
        1 | 2 => true,
        _ => return None,
    };

    if snapshot.adofai_version != adofai_version
        || snapshot.is_x_perfect_mode != is_x_perfect_mode
        || snapshot.is_no_hold_tap != is_no_hold_tap
        || (is_x_perfect_mode && adofai_version != 3)
        || (!is_x_perfect_mode && (snapshot.perfect_minus != 0 || snapshot.perfect_plus != 0))
    {
        return None;
    }

    Some(RecordedClaims {
        speed,
        key_count,
        adofai_version,
        is_x_perfect_mode,
        is_no_hold_tap,
    })
}

fn all_recorder_failures_clear(metadata: &serde_json::Value) -> bool {
    [
        "inputOverflowDropped",
        "inputUnmappedEvents",
        "inputReadFailures",
        "inputDegradedEvents",
    ]
    .into_iter()
    .all(|key| metadata.get(key).and_then(|value| value.as_u64()) == Some(0))
}

fn parse_adofai_version(value: &str) -> Option<u8> {
    let version = value.trim().strip_prefix('v').unwrap_or(value.trim());
    let mut parts = version.split('.');
    let major = leading_digits(parts.next()?)?;
    let minor = leading_digits(parts.next()?)?;
    let patch = leading_digits(parts.next()?)?;
    match (major, minor, patch) {
        (2, _, _) => Some(1),
        (major, minor, patch) if (major, minor, patch) >= (3, 4, 0) => Some(3),
        (3, _, _) => Some(2),
        _ => None,
    }
}

fn leading_digits(value: &str) -> Option<u32> {
    let digits = value
        .chars()
        .take_while(char::is_ascii_digit)
        .collect::<String>();
    (!digits.is_empty()).then(|| digits.parse().ok()).flatten()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::domain::submission_gameplay_hash::compute_submission_gameplay_hash;
    use serde_json::json;

    #[test]
    fn submission_identity_is_mandatory_and_distinct_from_activity_hash() {
        let bytes = br#"{"settings":{"version":17,"bpm":120},"angleData":[0,180],"actions":[]}"#;
        let hash = compute_submission_gameplay_hash(bytes).unwrap();
        let mut metadata = json!({"gameplayHashVersion":4,"gameplayHashHex":hash});
        assert_eq!(
            verify_submission_chart(&metadata, &hash),
            Err("submission_chart_identity_missing_or_unsupported")
        );
        metadata["submissionGameplayHashVersion"] = json!(1);
        metadata["submissionGameplayHashHex"] = json!(hash);
        assert_eq!(verify_submission_chart(&metadata, &hash), Ok(()));
        metadata["submissionGameplayHashHex"] = json!("a".repeat(64));
        assert_eq!(
            verify_submission_chart(&metadata, &hash),
            Err("submission_chart_gameplay_mismatch")
        );
        metadata["submissionGameplayHashVersion"] = json!(2);
        assert!(verify_submission_chart(&metadata, &hash).is_err());
    }

    #[test]
    fn version_and_game_mode_come_from_existing_recorded_metadata() {
        assert_eq!(parse_adofai_version("2.8.1"), Some(1));
        assert_eq!(parse_adofai_version("3.3.1"), Some(2));
        assert_eq!(parse_adofai_version("v3.4.0"), Some(3));
        assert_eq!(parse_adofai_version("3.4.1f1"), Some(3));
        assert_eq!(parse_adofai_version("1.9.9"), None);

        assert!(all_recorder_failures_clear(&json!({
            "inputOverflowDropped": 0,
            "inputUnmappedEvents": 0,
            "inputReadFailures": 0,
            "inputDegradedEvents": 0
        })));
        assert!(!all_recorder_failures_clear(&json!({
            "inputOverflowDropped": 1,
            "inputUnmappedEvents": 0,
            "inputReadFailures": 0,
            "inputDegradedEvents": 0
        })));
        assert!(!all_recorder_failures_clear(&json!({
            "inputOverflowDropped": 0,
            "inputUnmappedEvents": 0,
            "inputReadFailures": 0
        })));
    }

    #[test]
    fn e2e_fixture_fields_cannot_replace_the_recorded_result_snapshot() {
        let metadata = json!({
            "e2e": {"mode": "accepted"},
            "effectivePitch": 1.0,
            "keyCount": 4,
            "gameVersion": "3.4.0",
            "judgmentSystem": "ModernCompetitive"
        });
        assert!(metadata.get("submissionResult").is_none());
    }
}
