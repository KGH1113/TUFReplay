use super::types::TufMetadata;
use super::{CatalogError, TufCatalogRuntime};
use std::path::Path;
use tokio::io::AsyncWriteExt;

#[derive(serde::Deserialize)]
struct Difficulty {
    id: i64,
    #[serde(rename = "type")]
    kind: String,
    name: String,
}

impl TufCatalogRuntime {
    pub(super) async fn fetch_metadata(
        &self,
        tuf_level_id: i64,
    ) -> Result<TufMetadata, CatalogError> {
        let url = format!(
            "{}/v2/database/levels/byId/{tuf_level_id}",
            self.settings.tuf_api_base_url.trim_end_matches('/')
        );
        let response = self
            .client
            .get(url)
            .header(reqwest::header::ACCEPT, "application/json")
            .send()
            .await
            .and_then(reqwest::Response::error_for_status)
            .map_err(|_| CatalogError::UpstreamUnavailable)?;
        let mut value: serde_json::Value = response
            .json()
            .await
            .map_err(|_| CatalogError::UpstreamUnavailable)?;
        if let Some(inner) = value.get("data").or_else(|| value.get("result")).cloned() {
            value = inner;
        }
        // Level responses expose an opaque diffId, not an embedded difficulty.
        // Resolve the authoritative catalog entry before applying the P/G policy.
        let difficulty_id = value
            .get("diffId")
            .and_then(serde_json::Value::as_i64)
            .ok_or(CatalogError::UpstreamUnavailable)?;
        let difficulties: Vec<Difficulty> = self
            .client
            .get(format!(
                "{}/v2/database/difficulties",
                self.settings.tuf_api_base_url.trim_end_matches('/')
            ))
            .header(reqwest::header::ACCEPT, "application/json")
            .send()
            .await
            .and_then(reqwest::Response::error_for_status)
            .map_err(|_| CatalogError::UpstreamUnavailable)?
            .json()
            .await
            .map_err(|_| CatalogError::UpstreamUnavailable)?;
        let mut matches = difficulties
            .iter()
            .filter(|entry| entry.id == difficulty_id);
        let difficulty = matches.next().ok_or(CatalogError::UpstreamUnavailable)?;
        if matches.next().is_some()
            || difficulty.kind.trim().is_empty()
            || difficulty.name.trim().is_empty()
        {
            return Err(CatalogError::UpstreamUnavailable);
        }
        if !crate::domain::is_eligible_difficulty(&difficulty.kind, &difficulty.name) {
            return Err(CatalogError::IneligibleDifficulty);
        }
        if value.get("isDeleted").and_then(|v| v.as_bool()) == Some(true)
            || value.get("isHidden").and_then(|v| v.as_bool()) == Some(true)
        {
            return Err(CatalogError::IneligibleDifficulty);
        }
        let level_id = json_i64(&value, &["id", "Id"]).ok_or(CatalogError::UpstreamUnavailable)?;
        let file_id = json_string(&value, &["fileId", "FileId"])
            .filter(|value| !value.trim().is_empty())
            .ok_or(CatalogError::UpstreamUnavailable)?;
        let download_url = json_string(&value, &["dlLink", "DownloadLink"])
            .filter(|value| !value.trim().is_empty())
            .ok_or(CatalogError::UpstreamUnavailable)?;
        if level_id != tuf_level_id {
            return Err(CatalogError::UpstreamUnavailable);
        }
        Ok(TufMetadata {
            target_chart_path: self.fetch_target_chart_path(&file_id).await?,
            file_id,
            download_url,
        })
    }

    async fn fetch_target_chart_path(&self, file_id: &str) -> Result<Option<String>, CatalogError> {
        let mut url = reqwest::Url::parse(&format!(
            "{}/cdn/",
            self.settings
                .tuf_metadata_base_url
                .as_deref()
                .unwrap_or(&self.settings.tuf_api_base_url)
                .trim_end_matches('/')
        ))
        .map_err(|_| CatalogError::UpstreamUnavailable)?;
        url.path_segments_mut()
            .map_err(|_| CatalogError::UpstreamUnavailable)?
            .pop_if_empty()
            .push(file_id)
            .push("metadata");
        let response = self
            .client
            .get(url)
            .send()
            .await
            .map_err(|_| CatalogError::UpstreamUnavailable)?;
        // Older catalogs may lack metadata. Only semantic unanimity may be used then.
        if response.status() == reqwest::StatusCode::NOT_FOUND {
            return Ok(None);
        }
        let value: serde_json::Value = response
            .error_for_status()
            .map_err(|_| CatalogError::UpstreamUnavailable)?
            .json()
            .await
            .map_err(|_| CatalogError::UpstreamUnavailable)?;
        target_chart_path(&value["metadata"])
    }

    pub(super) async fn download_archive(
        &self,
        url: &str,
        destination: &Path,
    ) -> Result<(), CatalogError> {
        let url = canonical_download_url(url)?;
        let mut response = self
            .client
            .get(url)
            .send()
            .await
            .and_then(reqwest::Response::error_for_status)
            .map_err(|_| CatalogError::UpstreamUnavailable)?;
        if response
            .content_length()
            .is_some_and(|size| size > self.settings.artifact_max_download_bytes)
        {
            return Err(CatalogError::ArtifactTooLarge);
        }
        let mut output = tokio::fs::File::create(destination)
            .await
            .map_err(|error| CatalogError::Storage(error.to_string()))?;
        let mut total = 0_u64;
        while let Some(chunk) = response
            .chunk()
            .await
            .map_err(|_| CatalogError::UpstreamUnavailable)?
        {
            total = total
                .checked_add(chunk.len() as u64)
                .ok_or(CatalogError::ArtifactTooLarge)?;
            if total > self.settings.artifact_max_download_bytes {
                return Err(CatalogError::ArtifactTooLarge);
            }
            output
                .write_all(&chunk)
                .await
                .map_err(|error| CatalogError::Storage(error.to_string()))?;
        }
        output
            .flush()
            .await
            .map_err(|error| CatalogError::Storage(error.to_string()))?;
        Ok(())
    }
}

pub(super) fn target_chart_path(
    metadata: &serde_json::Value,
) -> Result<Option<String>, CatalogError> {
    let confirmed = metadata.get("pathConfirmed").and_then(|v| v.as_bool()) == Some(true);
    let Some(target) = metadata
        .get("targetLevel")
        .and_then(|v| v.as_str())
        .filter(|path| !path.is_empty())
    else {
        return if confirmed {
            Err(CatalogError::ChartNotFound)
        } else {
            Ok(None)
        };
    };
    let files = metadata
        .get("levelFiles")
        .and_then(|v| v.as_object())
        .ok_or(CatalogError::ChartNotFound)?;
    // Map TUF's target storage path back to its original ZIP entry, even when
    // pathConfirmed is false. Never select by basename or flattened client path.
    let mut matches = files
        .iter()
        .filter(|(_, entry)| entry.get("path").and_then(|v| v.as_str()) == Some(target));
    let (path, _) = matches.next().ok_or(CatalogError::ChartNotFound)?;
    if matches.next().is_some() {
        return Err(CatalogError::AmbiguousChart);
    }
    crate::models::level_revision_charts::normalize_relative_chart_path(path)
        .map(Some)
        .map_err(|_| CatalogError::ChartNotFound)
}

pub(super) fn canonical_download_url(value: &str) -> Result<String, CatalogError> {
    let parsed = reqwest::Url::parse(value).map_err(|_| CatalogError::UpstreamUnavailable)?;
    let loopback_http = parsed.scheme() == "http"
        && matches!(parsed.host_str(), Some("127.0.0.1" | "localhost" | "::1"));
    if parsed.scheme() != "https" && !loopback_http {
        return Err(CatalogError::UpstreamUnavailable);
    }
    if parsed.host_str() == Some("drive.google.com") {
        let id = parsed
            .path()
            .strip_prefix("/file/d/")
            .and_then(|rest| rest.split('/').next())
            .filter(|id| !id.is_empty())
            .map(str::to_owned)
            .or_else(|| {
                parsed
                    .query_pairs()
                    .find(|(key, _)| key == "id")
                    .map(|(_, value)| value.into_owned())
            });
        if let Some(id) = id {
            return Ok(format!(
                "https://drive.usercontent.google.com/download?id={id}&export=download&confirm=t"
            ));
        }
    }
    Ok(parsed.to_string())
}

pub(super) fn json_string(value: &serde_json::Value, keys: &[&str]) -> Option<String> {
    keys.iter()
        .find_map(|key| value.get(*key))
        .and_then(serde_json::Value::as_str)
        .map(str::to_owned)
}

pub(super) fn json_i64(value: &serde_json::Value, keys: &[&str]) -> Option<i64> {
    let value = keys.iter().find_map(|key| value.get(*key))?;
    value
        .as_i64()
        .or_else(|| value.as_str().and_then(|value| value.parse().ok()))
}
