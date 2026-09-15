use crate::domain::*;
use crate::models::run_sessions::Model;
use crate::models::run_submission_records::Entity as Records;
use crate::services::submission::SubmissionRuntime;
use crate::settings::SubmissionValidationMode;
use loco_rs::prelude::*;
use uuid::Uuid;

struct ExpectedChartIdentity {
    file_id: String,
    sha256: String,
    gameplay_hash_version: u32,
    gameplay_hash: String,
}

enum ComputedValidation {
    Outcome(ValidationOutcome),
    Error,
}

pub async fn validate(
    ctx: &AppContext,
    run: &Model,
    lease: Uuid,
    manifest: &EvidenceManifest,
    runtime: &SubmissionRuntime,
) -> Result<()> {
    let mode = crate::settings::Settings::get(ctx)?
        .auto_submission
        .validation_mode;
    let mut expected_chart = None;
    let computed = match mode {
        SubmissionValidationMode::TrustedTester => ComputedValidation::Outcome(
            super::trusted_tester::validate(ctx, run, manifest, runtime.charts.as_ref()).await?,
        ),
        SubmissionValidationMode::Unavailable => {
            let validator = runtime.validator.clone();
            #[cfg(feature = "e2e")]
            let validator = if ctx.environment.to_string() == "e2e" {
                crate::e2e::validator(ctx, manifest).await?
            } else {
                validator
            };
            if !validator.available() {
                ComputedValidation::Outcome(ValidationOutcome::Unavailable)
            } else {
                let chart = runtime
                    .charts
                    .acquire(run.tuf_level_id, &run.client_level_relative_path)
                    .await
                    .map_err(|_| Error::Message("official_chart_unavailable".into()))?;
                expected_chart = Some(ExpectedChartIdentity {
                    file_id: chart.file_id.clone(),
                    sha256: chart.sha256.clone(),
                    gameplay_hash_version: chart.gameplay_hash_version,
                    gameplay_hash: chart.gameplay_hash.clone(),
                });
                match validator.validate(&chart, manifest).await {
                    Ok(outcome) => ComputedValidation::Outcome(outcome),
                    Err(_) => ComputedValidation::Error,
                }
            }
        }
    };
    let (state, reason, result) = match computed {
        ComputedValidation::Outcome(ValidationOutcome::Accepted(result))
            if result.has_valid_v2_contract()
                && result.evidence_digest == manifest.digest
                && result.speed.is_finite()
                && (1.0..=100.0).contains(&result.speed)
                && (1..=64).contains(&result.key_count)
                && !result.validator_version.is_empty()
                && !result.rules_version.is_empty()
                && expected_chart.as_ref().is_none_or(|chart| {
                    result.official_file_id == chart.file_id
                        && result.chart_sha256 == chart.sha256
                        && result.gameplay_hash_version == chart.gameplay_hash_version
                        && result.gameplay_hash == chart.gameplay_hash
                }) =>
        {
            ("registering", None, Some(serde_json::to_value(result)?))
        }
        ComputedValidation::Outcome(ValidationOutcome::Rejected(reason)) => {
            ("validation_rejected", Some(reason), None)
        }
        ComputedValidation::Outcome(ValidationOutcome::Unavailable) => {
            ("validator_unavailable", Some("validator_unavailable"), None)
        }
        ComputedValidation::Outcome(ValidationOutcome::Accepted(_)) | ComputedValidation::Error => {
            ("validation_error", Some("validation_error"), None)
        }
    };
    Records::transition(
        &ctx.db,
        run.id,
        lease,
        crate::models::run_submission_records::Transition {
            from: "validation_pending",
            to: state,
            reason,
            validation: result,
            pass: None,
        },
    )
    .await?;
    Ok(())
}
