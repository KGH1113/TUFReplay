use crate::domain::*;
use crate::models::run_sessions::Model;
use crate::models::run_submission_records::Entity as Records;
use crate::services::submission::SubmissionRuntime;
use loco_rs::prelude::*;
use uuid::Uuid;

pub async fn validate(
    ctx: &AppContext,
    run: &Model,
    lease: Uuid,
    manifest: &EvidenceManifest,
    runtime: &SubmissionRuntime,
) -> Result<()> {
    let validator = runtime.validator.clone();
    #[cfg(feature = "e2e")]
    let validator = if ctx.environment.to_string() == "e2e" {
        crate::e2e::validator(ctx, manifest).await?
    } else {
        validator
    };
    let (state, reason, result) = if !validator.available() {
        ("validator_unavailable", Some("validator_unavailable"), None)
    } else {
        let chart = runtime
            .charts
            .acquire(run.tuf_level_id, &run.client_level_relative_path)
            .await
            .map_err(|_| Error::Message("official_chart_unavailable".into()))?;
        match validator.validate(&chart, manifest).await {
            Ok(ValidationOutcome::Accepted(result))
                if result.evidence_digest == manifest.digest
                    && result.chart_sha256 == chart.sha256
                    && result.official_file_id == chart.file_id
                    && result.gameplay_hash_version == chart.gameplay_hash_version
                    && result.gameplay_hash == chart.gameplay_hash
                    && result.speed.is_finite()
                    && (1.0..=100.0).contains(&result.speed)
                    && (1..=64).contains(&result.key_count)
                    && !result.validator_version.is_empty()
                    && !result.rules_version.is_empty() =>
            {
                ("registering", None, Some(serde_json::to_value(result)?))
            }
            Ok(ValidationOutcome::Rejected(reason)) => ("validation_rejected", Some(reason), None),
            Ok(ValidationOutcome::Unavailable) => {
                ("validator_unavailable", Some("validator_unavailable"), None)
            }
            _ => ("validation_error", Some("validation_error"), None),
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
