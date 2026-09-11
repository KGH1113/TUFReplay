use crate::domain::*;
use crate::models::run_sessions::Model;
use crate::models::run_submission_records::Entity as Records;
use loco_rs::prelude::*;
use std::sync::Arc;
use uuid::Uuid;

pub struct SubmissionRuntime {
    pub validator: Arc<dyn GameplayValidator>,
    pub charts: Arc<dyn OfficialChartProvider>,
    pub registrar: Arc<dyn PassRegistrar>,
    pub evidence_slots: tokio::sync::Semaphore,
}

pub async fn process(ctx: &AppContext, id: Uuid, runtime: &SubmissionRuntime) -> Result<()> {
    let run = Model::find_by_pid(&ctx.db, id).await?;
    let lease = Uuid::new_v4();
    if !Records::lease(&ctx.db, run.id, lease).await? {
        return Ok(());
    }
    let result = tokio::time::timeout(
        std::time::Duration::from_secs(240),
        process_leased(ctx, &run, lease, runtime),
    )
    .await
    .unwrap_or_else(|_| Err(Error::Message("submission_timeout".into())));
    if matches!(&result, Err(Error::Unauthorized(_))) {
        let record = Records::record(&ctx.db, run.id).await?;
        let target = if record.state == "registering" {
            "registration_error"
        } else {
            "validation_error"
        };
        Records::transition(
            &ctx.db,
            run.id,
            lease,
            crate::models::run_submission_records::Transition {
                from: &record.state,
                to: target,
                reason: Some("submission_authorization_required"),
                validation: None,
                pass: None,
            },
        )
        .await?;
    } else if result.is_err() {
        crate::models::run_submission_records::retry::defer(&ctx.db, run.id, lease).await?;
    }
    Records::release(&ctx.db, run.id, lease).await?;
    result
}

async fn process_leased(
    ctx: &AppContext,
    run: &Model,
    lease: Uuid,
    runtime: &SubmissionRuntime,
) -> Result<()> {
    let record = Records::record(&ctx.db, run.id).await?;
    let Some(manifest) = record.manifest else {
        return Ok(());
    };
    let manifest: EvidenceManifest = serde_json::from_value(manifest)?;
    if record.state == "validation_pending" {
        crate::services::auth::authorize_grant(ctx, &record.owner_id, record.oauth_grant_id)
            .await?;
        super::validation::validate(ctx, run, lease, &manifest, runtime).await?;
    }
    super::registration::register(ctx, run, lease, runtime).await
}
