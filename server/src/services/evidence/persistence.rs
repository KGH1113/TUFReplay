use crate::models::run_sessions::Model;
use crate::models::run_submission_records::Entity as Records;
use crate::services::ingest::RunIngestStore;
use loco_rs::prelude::*;
use uuid::Uuid;

pub async fn persist(ctx: &AppContext, run_id: Uuid) -> Result<()> {
    let run = Model::find_by_pid(&ctx.db, run_id).await?;
    let record = Records::record(&ctx.db, run.id).await?;
    if record.manifest.is_some() {
        return super::ingest_release::release(ctx, run_id, run.id).await;
    }
    if record.state == "deleted" {
        return Ok(());
    }
    let lease = Uuid::new_v4();
    if !Records::lease(&ctx.db, run.id, lease).await? {
        return Ok(());
    }
    let result = async {
        let store = ctx
            .shared_store
            .get::<RunIngestStore>()
            .ok_or_else(|| Error::Message("ingest store unavailable".into()))?;
        let manifest = tokio::time::timeout(
            std::time::Duration::from_secs(240),
            super::assembler::assemble(&store, &ctx.storage, run_id),
        )
        .await
        .map_err(|_| Error::Message("evidence persistence timed out".into()))??;
        Records::publish(&ctx.db, run.id, lease, serde_json::to_value(manifest)?).await?;
        Ok(())
    }
    .await;
    if matches!(result, Err(Error::BadRequest(_))) {
        Records::transition(
            &ctx.db,
            run.id,
            lease,
            crate::models::run_submission_records::Transition {
                from: "uploading",
                to: "evidence_invalid",
                reason: Some("evidence_invalid"),
                validation: None,
                pass: None,
            },
        )
        .await?;
    }
    Records::release(&ctx.db, run.id, lease).await?;
    if result.is_ok() {
        // Redis failure must not undo a committed manifest or mark evidence invalid.
        if let Err(error) = super::ingest_release::release(ctx, run_id, run.id).await {
            tracing::warn!(%run_id, %error, "ingest release deferred after persistence");
        }
    }
    result
}
