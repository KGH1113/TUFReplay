use crate::services::artifacts::{digest_stream, verify_stream, ArtifactStores};
use loco_rs::prelude::*;
use loco_rs::storage::drivers::StoreDriver;
use std::path::Path;

pub struct MigrateArtifacts;
#[async_trait]
impl Task for MigrateArtifacts {
    fn task(&self) -> TaskInfo {
        TaskInfo {
            name: "migrate_artifacts".into(),
            detail: "Copy local artifacts to R2 and verify SHA-256; retain local originals".into(),
        }
    }
    async fn run(&self, ctx: &AppContext, _vars: &task::Vars) -> Result<()> {
        let stores = ctx
            .shared_store
            .get::<ArtifactStores>()
            .ok_or_else(|| Error::Message("artifact storage is not configured".into()))?;
        if stores.r2.is_none() {
            return Err(Error::Message("enable R2 before migration".into()));
        }
        let mut verified = 0u64;
        for entry in stores.local.list(Path::new(""), true).await? {
            if entry.is_dir || entry.path.split('/').any(|part| part.starts_with("temp-")) {
                continue;
            }
            let path = Path::new(&entry.path);
            let (sha256, bytes) = digest_stream(stores.local.get_stream(path).await?).await?;
            // Evidence object names are their committed content digest.
            if entry.path.starts_with("evidence/")
                && path.file_name().and_then(|n| n.to_str()) != Some(&sha256)
            {
                return Err(Error::Message(format!(
                    "local evidence digest mismatch: {}",
                    entry.path
                )));
            }
            if !stores.primary.exists(path).await? {
                stores
                    .upload_stream(path, stores.local.get_stream(path).await?)
                    .await?;
            }
            verify_stream(stores.primary.get_stream(path).await?, &sha256, bytes).await?;
            verified += 1;
            tracing::info!(key=%entry.path, bytes, "R2 migration object verified");
        }
        tracing::info!(verified, "R2 migration finished; local originals retained");
        Ok(())
    }
}
