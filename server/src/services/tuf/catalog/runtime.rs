use super::{CatalogError, TufCatalogRuntime, TufCatalogSettings};
use reqwest::Client;
use std::{sync::Arc, time::Duration};
use tokio::sync::Semaphore;

impl TufCatalogRuntime {
    pub fn new(settings: TufCatalogSettings) -> Result<Self, CatalogError> {
        if settings.tuf_api_base_url.trim().is_empty()
            || settings.artifact_root.trim().is_empty()
            || settings.artifact_max_download_bytes == 0
            || settings.artifact_max_extracted_bytes == 0
            || settings.artifact_max_files == 0
            || settings.artifact_hydration_timeout_seconds == 0
            || settings.artifact_max_concurrent_hydrations == 0
        {
            return Err(CatalogError::Storage(
                "catalog settings must be non-empty and non-zero".to_owned(),
            ));
        }
        let timeout = Duration::from_secs(settings.artifact_hydration_timeout_seconds);
        let client = Client::builder()
            .connect_timeout(Duration::from_secs(5))
            .timeout(timeout)
            .redirect(reqwest::redirect::Policy::limited(5))
            .build()
            .map_err(|error| CatalogError::Storage(error.to_string()))?;
        let slots = settings.artifact_max_concurrent_hydrations;
        Ok(Self {
            client,
            settings: Arc::new(settings),
            hydration_slots: Arc::new(Semaphore::new(slots)),
        })
    }
}
