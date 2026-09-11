use super::types::TufMetadata;
use super::{CatalogError, TufCatalogRuntime};
use std::path::Path;
use tokio::io::AsyncWriteExt;

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
        let difficulty = value
            .get("difficulty")
            .ok_or(CatalogError::IneligibleDifficulty)?;
        if !crate::domain::is_eligible_difficulty(
            difficulty
                .get("type")
                .and_then(|v| v.as_str())
                .unwrap_or(""),
            difficulty
                .get("name")
                .and_then(|v| v.as_str())
                .unwrap_or(""),
        ) {
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
            file_id,
            download_url,
        })
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
