use super::archives::process_archive;
use super::{CatalogError, TufCatalogRuntime};
use crate::domain::{
    compute_gameplay_hash, OfficialChart, OfficialChartProvider, GAMEPLAY_HASH_VERSION,
};
use crate::models::level_revision_charts;
use sha2::{Digest, Sha256};
use std::{fs::File, io::Read, time::Duration};

impl TufCatalogRuntime {
    /// Issuance checks public metadata only. Installed file identity is a claim,
    /// not proof, and does not cause a pre-play archive download.
    pub async fn require_eligible(&self, level_id: i64) -> Result<(), CatalogError> {
        self.fetch_metadata(level_id).await.map(|_| ())
    }

    async fn temporary_chart(
        &self,
        level_id: i64,
        relative_path: &str,
    ) -> Result<OfficialChart, CatalogError> {
        let path = level_revision_charts::normalize_relative_chart_path(relative_path)
            .map_err(|_| CatalogError::ChartNotFound)?;
        let _slot = self
            .hydration_slots
            .acquire()
            .await
            .map_err(|_| CatalogError::Busy)?;
        let metadata = self.fetch_metadata(level_id).await?;
        let temporary = tempfile::tempdir().map_err(|e| CatalogError::Storage(e.to_string()))?;
        let archive = temporary.path().join("official.zip");
        self.download_archive(&metadata.download_url, &archive)
            .await?;
        let settings = self.settings.clone();
        let file_id = metadata.file_id.clone();
        // The blocking task owns the directory, so cancellation cannot remove
        // it during extraction or leave it behind after extraction finishes.
        let chart = tokio::task::spawn_blocking(move || {
            let revision = process_archive(&archive, &temporary.path().join("charts"), &settings)?;
            let chart = revision
                .charts
                .into_iter()
                .find(|chart| chart.relative_path == path)
                .ok_or(CatalogError::ChartNotFound)?;
            let input = File::open(&chart.file_path).map_err(|_| CatalogError::UnsafeArchive)?;
            let mut bytes = Vec::new();
            // Provisional parser allocation bound, not a play/upload quota.
            input
                .take(32 * 1024 * 1024 + 1)
                .read_to_end(&mut bytes)
                .map_err(|_| CatalogError::UnsafeArchive)?;
            if bytes.len() > 32 * 1024 * 1024 {
                return Err(CatalogError::ArtifactTooLarge);
            }
            Ok(OfficialChart {
                file_id,
                sha256: hex::encode(Sha256::digest(&bytes)),
                gameplay_hash_version: GAMEPLAY_HASH_VERSION,
                gameplay_hash: compute_gameplay_hash(&bytes)
                    .map_err(|_| CatalogError::UnsafeArchive)?,
                bytes,
            })
        })
        .await
        .map_err(|_| CatalogError::UnsafeArchive)??;
        if self.fetch_metadata(level_id).await?.file_id != metadata.file_id {
            return Err(CatalogError::CatalogUnstable);
        }
        Ok(chart)
    }
}

#[async_trait::async_trait]
impl OfficialChartProvider for TufCatalogRuntime {
    async fn acquire(&self, level_id: i64, relative_path: &str) -> Result<OfficialChart, String> {
        tokio::time::timeout(
            Duration::from_secs(self.settings.artifact_hydration_timeout_seconds),
            self.temporary_chart(level_id, relative_path),
        )
        .await
        .map_err(|_| "official_chart_timeout".to_owned())?
        .map_err(|error| error.to_string())
    }
}
