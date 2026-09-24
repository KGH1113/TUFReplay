//! Resumable conversion of legacy inline presets. IDs and selections do not change.
use crate::services::{visual_assets, visuals};
use loco_rs::prelude::*;
use sea_orm::FromQueryResult;
use sha2::{Digest, Sha256};
use uuid::Uuid;

pub struct MigrateVisualAssets;

#[derive(FromQueryResult)]
struct LegacyPreset {
    id: Uuid,
    owner_id: String,
    bundle: Vec<u8>,
    sha256: String,
    bytes: i64,
}

#[async_trait]
impl Task for MigrateVisualAssets {
    fn task(&self) -> TaskInfo {
        TaskInfo {
            name: "migrate_visual_assets".into(),
            detail:
                "Verify and move inline preset assets into object storage, preserving preset IDs"
                    .into(),
        }
    }

    async fn run(&self, ctx: &AppContext, _vars: &task::Vars) -> Result<()> {
        let mut after = Uuid::nil();
        let mut migrated = 0;
        // One preset at a time bounds memory, including when legacy rows contain large fonts.
        loop {
            let row = LegacyPreset::find_by_statement(visual_assets::statement(
                "SELECT id,owner_id,bundle,sha256,bytes FROM visual_presets WHERE id>$1 ORDER BY id LIMIT 1",
                vec![after.into()],
            )).one(&ctx.db).await?;
            let Some(row) = row else { break };
            after = row.id;
            if row.bytes != row.bundle.len() as i64
                || hex::encode(Sha256::digest(&row.bundle)) != row.sha256
            {
                return Err(Error::Message(format!(
                    "preset integrity mismatch: {}",
                    row.id
                )));
            }
            let value: serde_json::Value = serde_json::from_slice(&row.bundle)?;
            if value["schema_version"] == 2 {
                continue;
            }
            let mut bundle = visuals::validate_bundle(value)?;
            visual_assets::externalize(ctx, &row.owner_id, &mut bundle).await?;
            // Publish metadata only after every referenced object has been verified.
            let result = ctx.db.execute_raw(visual_assets::statement(
                "UPDATE visual_presets SET bundle=$1,sha256=$2,bytes=$3 WHERE id=$4 AND sha256=$5",
                vec![bundle.bytes.clone().into(), bundle.sha256.into(), (bundle.bytes.len() as i64).into(), row.id.into(), row.sha256.into()],
            )).await?;
            if result.rows_affected() != 1 {
                return Err(Error::Message(format!(
                    "preset changed during migration: {}",
                    row.id
                )));
            }
            migrated += 1;
            tracing::info!(preset_id=%row.id, "visual assets migrated and verified");
        }
        tracing::info!(migrated, "visual asset migration finished");
        Ok(())
    }
}
