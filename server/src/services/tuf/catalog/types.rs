use reqwest::Client;
use serde::Deserialize;
use std::{path::PathBuf, sync::Arc};
use tokio::sync::Semaphore;

#[derive(Clone, Debug, Deserialize)]
pub struct TufCatalogSettings {
    pub tuf_api_base_url: String,
    pub artifact_root: String,
    pub artifact_max_download_bytes: u64,
    pub artifact_max_extracted_bytes: u64,
    pub artifact_max_files: usize,
    pub artifact_hydration_timeout_seconds: u64,
    pub artifact_max_concurrent_hydrations: usize,
}

#[derive(Clone)]
pub struct TufCatalogRuntime {
    pub(super) client: Client,
    pub(super) settings: Arc<TufCatalogSettings>,
    pub(super) hydration_slots: Arc<Semaphore>,
}

#[derive(Debug, thiserror::Error)]
pub enum CatalogError {
    #[error("only P and G levels support automatic submission")]
    IneligibleDifficulty,
    #[error("TUF catalog is unavailable")]
    UpstreamUnavailable,
    #[error("the installed TUF level is not the latest revision")]
    LevelRevisionOutdated,
    #[error("the installed TUF level does not match the canonical revision")]
    LevelInstallationMismatch,
    #[error("the selected chart does not exist in the canonical revision")]
    ChartNotFound,
    #[error("the upstream TUF revision changed repeatedly during hydration")]
    CatalogUnstable,
    #[error("the same TUF revision identity resolved to conflicting content")]
    CatalogRevisionConflict,
    #[error("the TUF archive is unsafe or malformed")]
    UnsafeArchive,
    #[error("the TUF archive exceeds configured resource limits")]
    ArtifactTooLarge,
    #[error("the TUF archive contains no .adofai chart")]
    NoCharts,
    #[error("canonical revision hydration is busy")]
    Busy,
    #[error("canonical artifact storage failed: {0}")]
    Storage(String),
    #[error("canonical revision database operation failed: {0}")]
    Database(String),
}

#[derive(Clone, Debug)]
pub(super) struct TufMetadata {
    pub(super) file_id: String,
    pub(super) download_url: String,
}

#[derive(Debug)]
pub(super) struct ProcessedChart {
    pub(super) relative_path: String,
    pub(super) file_path: PathBuf,
}

#[derive(Debug)]
pub(super) struct ProcessedRevision {
    pub(super) charts: Vec<ProcessedChart>,
}
