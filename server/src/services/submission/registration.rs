use super::SubmissionRuntime;
use crate::{
    domain::ValidatedResult,
    models::{run_sessions::Model, run_submission_records::Entity as Records},
};
use loco_rs::prelude::*;
use uuid::Uuid;

pub(super) async fn register(
    ctx: &AppContext,
    run: &Model,
    lease: Uuid,
    runtime: &SubmissionRuntime,
) -> Result<()> {
    let record = Records::record(&ctx.db, run.id).await?;
    if record.state == "registering" {
        let result: ValidatedResult = serde_json::from_value(
            record
                .validation
                .ok_or_else(|| Error::Message("missing_validation".into()))?,
        )?;
        // Recover a committed registration before checking mutable catalog/account eligibility.
        // A later chart edit must not hide a pass whose original response was lost.
        if let Some(pass) = runtime
            .registrar
            .lookup(run.pid, &record.owner_id, &result.evidence_digest)
            .await
            .map_err(|_| Error::Message("registration_receipt_unavailable".into()))?
        {
            Records::transition(
                &ctx.db,
                run.id,
                lease,
                crate::models::run_submission_records::Transition {
                    from: "registering",
                    to: "submitted",
                    reason: None,
                    validation: None,
                    pass: Some(pass),
                },
            )
            .await?;
            return Ok(());
        }
        crate::services::auth::authorize_grant(ctx, &record.owner_id, record.oauth_grant_id)
            .await?;
        let registered = runtime
            .registrar
            .register(
                run.pid,
                &record.owner_id,
                record
                    .oauth_grant_id
                    .ok_or_else(|| Error::Unauthorized("oauth_grant_required".into()))?,
                run.tuf_level_id,
                &result.official_file_id,
                &result,
            )
            .await;
        let pass = match registered {
            Ok(pass) => pass,
            Err(reason) if reason == "level_revision_changed" => {
                // A confirmed conflict created no pass. Manual retry must acquire
                // and validate the current chart instead of reusing stale results.
                Records::transition(
                    &ctx.db,
                    run.id,
                    lease,
                    crate::models::run_submission_records::Transition {
                        from: "registering",
                        to: "validation_error",
                        reason: Some("level_revision_changed"),
                        validation: None,
                        pass: None,
                    },
                )
                .await?;
                return Ok(());
            }
            Err(reason) if reason == "registration_rejected" => {
                Records::transition(
                    &ctx.db,
                    run.id,
                    lease,
                    crate::models::run_submission_records::Transition {
                        from: "registering",
                        to: "registration_rejected",
                        reason: Some("registration_rejected"),
                        validation: None,
                        pass: None,
                    },
                )
                .await?;
                return Ok(());
            }
            Err(_) => return Err(Error::Message("registration_unavailable".into())),
        };
        Records::transition(
            &ctx.db,
            run.id,
            lease,
            crate::models::run_submission_records::Transition {
                from: "registering",
                to: "submitted",
                reason: None,
                validation: None,
                pass: Some(pass),
            },
        )
        .await?;
    }
    Ok(())
}
