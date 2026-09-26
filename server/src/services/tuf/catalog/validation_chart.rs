use super::archives::process_original_archive;
use super::types::{ProcessedRevision, TufMetadata};
use super::{CatalogError, TufCatalogRuntime};
use crate::domain::submission_gameplay_hash::compute_submission_gameplay_hash;
use crate::domain::{
    compute_gameplay_hash, OfficialChart, OfficialChartProvider, GAMEPLAY_HASH_VERSION,
};
use sha2::{Digest, Sha256};
use std::{fs::File, io::Read, time::Duration};

impl TufCatalogRuntime {
    /// Check public eligibility; admission separately acquires the official chart.
    pub async fn require_eligible(&self, level_id: i64) -> Result<(), CatalogError> {
        self.fetch_metadata(level_id).await.map(|_| ())
    }

    async fn temporary_chart(
        &self,
        level_id: i64,
        _relative_path: &str,
    ) -> Result<OfficialChart, CatalogError> {
        let _slot = self
            .hydration_slots
            .acquire()
            .await
            .map_err(|_| CatalogError::Busy)?;
        let metadata = self.fetch_metadata(level_id).await?;
        // A small bounded cache holds at most four 32 MiB charts. Metadata is
        // re-resolved on every acquisition, including administrator path edits.
        // The hash contract is process-constant; deployment invalidates the cache.
        if let Some((_, _, chart)) = self
            .chart_cache
            .lock()
            .await
            .iter()
            .find(|(id, cached, _)| *id == level_id && cached == &metadata)
        {
            return Ok(chart.clone());
        }
        let temporary = tempfile::tempdir().map_err(|e| CatalogError::Storage(e.to_string()))?;
        let archive = temporary.path().join("official.zip");
        self.download_archive(&metadata.download_url, &archive)
            .await?;
        let settings = self.settings.clone();
        let selection = metadata.clone();
        // The blocking task owns the directory, so cancellation cannot remove
        // it during extraction or leave it behind after extraction finishes.
        let chart = tokio::task::spawn_blocking(move || {
            let revision =
                process_original_archive(&archive, &temporary.path().join("charts"), &settings)?;
            select_official_chart(revision, &selection)
        })
        .await
        .map_err(|_| CatalogError::UnsafeArchive)??;
        if self.fetch_metadata(level_id).await? != metadata {
            return Err(CatalogError::CatalogUnstable);
        }
        let mut cache = self.chart_cache.lock().await;
        cache.retain(|(id, _, _)| *id != level_id);
        if cache.len() >= 4 {
            cache.remove(0);
        }
        cache.push((level_id, metadata, chart.clone()));
        Ok(chart)
    }
}

pub(super) fn select_official_chart(
    revision: ProcessedRevision,
    metadata: &TufMetadata,
) -> Result<OfficialChart, CatalogError> {
    let mut selected: Option<(String, Vec<u8>)> = None;
    for chart in revision.charts {
        if metadata
            .confirmed_chart_path
            .as_ref()
            .is_some_and(|path| path != &chart.relative_path)
        {
            continue;
        }
        let input = File::open(&chart.file_path).map_err(|_| CatalogError::UnsafeArchive)?;
        let mut bytes = Vec::new();
        input
            .take(32 * 1024 * 1024 + 1)
            .read_to_end(&mut bytes)
            .map_err(|_| CatalogError::UnsafeArchive)?;
        if bytes.len() > 32 * 1024 * 1024 {
            return Err(CatalogError::ArtifactTooLarge);
        }
        let hash =
            compute_submission_gameplay_hash(&bytes).map_err(|_| CatalogError::UnsupportedChart)?;
        match &selected {
            Some((previous, _)) if previous != &hash => return Err(CatalogError::AmbiguousChart),
            Some(_) => {}
            None => selected = Some((hash, bytes)),
        }
    }
    let (submission_gameplay_hash, bytes) = selected.ok_or(CatalogError::ChartNotFound)?;
    Ok(OfficialChart {
        file_id: metadata.file_id.clone(),
        sha256: hex::encode(Sha256::digest(&bytes)),
        gameplay_hash_version: GAMEPLAY_HASH_VERSION,
        gameplay_hash: compute_gameplay_hash(&bytes).map_err(|_| CatalogError::UnsupportedChart)?,
        submission_gameplay_hash,
        bytes,
    })
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
        .map_err(|error| {
            tracing::warn!(level_id, error = %error, "official chart acquisition failed");
            error.to_string()
        })
    }
}
