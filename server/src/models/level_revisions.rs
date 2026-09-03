use std::{
    collections::HashSet,
    fs::{self, File},
    io::{self, Read},
    path::{Path, PathBuf},
    sync::Arc,
    time::{Duration as StdDuration, Instant},
};

use chrono::{DateTime, FixedOffset, Utc};
use loco_rs::{
    prelude::*,
    storage::{stream::BytesStream, Storage},
};
use reqwest::Client;
use sea_orm::{
    ActiveModelTrait, ActiveValue::Set, ColumnTrait, EntityTrait, QueryFilter, TransactionTrait,
};
use serde::Deserialize;
use sha2::{Digest, Sha256};
use tokio::{io::AsyncWriteExt, sync::Semaphore};
use tokio_util::io::ReaderStream;
use uuid::Uuid;
use zip::ZipArchive;

pub use super::_entities::level_revisions::{ActiveModel, Column, Entity, Model};
use super::{level_revision_charts, run_ingest::RunIngestStore};
pub type LevelRevisions = Entity;

const PAYLOAD_HASH_VERSION: i64 = 1;
const CHART_HASH_VERSION: i64 = 1;
const MANIFEST_FILE_NAME: &str = ".tufhelperlite-level.json";

#[async_trait::async_trait]
impl ActiveModelBehavior for ActiveModel {
    async fn before_save<C>(self, _db: &C, insert: bool) -> std::result::Result<Self, DbErr>
    where
        C: ConnectionTrait,
    {
        if insert {
            Ok(self)
        } else {
            Err(DbErr::Custom("level revisions are immutable".to_owned()))
        }
    }
}

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
    client: Client,
    settings: Arc<TufCatalogSettings>,
    hydration_slots: Arc<Semaphore>,
    ingest: RunIngestStore,
}

#[derive(Debug)]
pub struct RunLevelClaims {
    pub tuf_level_id: i64,
    pub client_tuf_file_id: String,
    pub client_installed_payload_hash: Vec<u8>,
    pub client_payload_hash_version: i64,
    pub client_level_relative_path: String,
}

#[derive(Debug)]
pub struct PinnedLevel {
    pub revision: Model,
    pub chart: level_revision_charts::Model,
}

#[derive(Debug, thiserror::Error)]
pub enum CatalogError {
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
struct TufMetadata {
    level_id: i64,
    file_id: String,
    download_url: String,
    updated_at: Option<DateTime<FixedOffset>>,
}

#[derive(Debug)]
struct ProcessedChart {
    relative_path: String,
    canonical_hash: Vec<u8>,
    file_path: PathBuf,
}

#[derive(Debug)]
struct ProcessedRevision {
    payload_hash: Vec<u8>,
    charts: Vec<ProcessedChart>,
}

impl TufCatalogRuntime {
    pub fn new(settings: TufCatalogSettings, ingest: RunIngestStore) -> Result<Self, CatalogError> {
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
        let timeout = StdDuration::from_secs(settings.artifact_hydration_timeout_seconds);
        let client = Client::builder()
            .connect_timeout(StdDuration::from_secs(5))
            .timeout(timeout)
            .redirect(reqwest::redirect::Policy::limited(5))
            .build()
            .map_err(|error| CatalogError::Storage(error.to_string()))?;
        let slots = settings.artifact_max_concurrent_hydrations;
        Ok(Self {
            client,
            settings: Arc::new(settings),
            hydration_slots: Arc::new(Semaphore::new(slots)),
            ingest,
        })
    }

    pub async fn resolve_for_run(
        &self,
        ctx: &AppContext,
        claims: RunLevelClaims,
    ) -> Result<PinnedLevel, CatalogError> {
        if claims.client_payload_hash_version != PAYLOAD_HASH_VERSION
            || claims.client_installed_payload_hash.len() != 32
        {
            return Err(CatalogError::LevelInstallationMismatch);
        }
        let relative_path = level_revision_charts::normalize_relative_chart_path(
            &claims.client_level_relative_path,
        )
        .map_err(|_| CatalogError::ChartNotFound)?;
        let metadata = self.fetch_metadata(claims.tuf_level_id).await?;
        if metadata.file_id != claims.client_tuf_file_id {
            return Err(CatalogError::LevelRevisionOutdated);
        }

        if let Some(revision) = find_revision(&ctx.db, metadata.level_id, &metadata.file_id).await?
        {
            return verify_and_select(
                &ctx.db,
                &ctx.storage,
                revision,
                &claims.client_installed_payload_hash,
                &relative_path,
            )
            .await;
        }

        let _slot = self
            .hydration_slots
            .acquire()
            .await
            .map_err(|_| CatalogError::Busy)?;
        let owner = Uuid::new_v4().to_string();
        let started = Instant::now();
        let timeout = StdDuration::from_secs(self.settings.artifact_hydration_timeout_seconds);
        loop {
            if self
                .ingest
                .try_acquire_revision_lock(
                    metadata.level_id,
                    &metadata.file_id,
                    &owner,
                    self.settings.artifact_hydration_timeout_seconds + 30,
                )
                .await
                .map_err(|error| CatalogError::Storage(error.to_string()))?
            {
                break;
            }
            if let Some(revision) =
                find_revision(&ctx.db, metadata.level_id, &metadata.file_id).await?
            {
                return verify_and_select(
                    &ctx.db,
                    &ctx.storage,
                    revision,
                    &claims.client_installed_payload_hash,
                    &relative_path,
                )
                .await;
            }
            if started.elapsed() >= timeout {
                return Err(CatalogError::Busy);
            }
            tokio::time::sleep(StdDuration::from_millis(100)).await;
        }

        let result = self
            .hydrate_locked(
                ctx,
                &metadata,
                &claims.client_installed_payload_hash,
                &relative_path,
            )
            .await;
        if let Err(error) = self
            .ingest
            .release_revision_lock(metadata.level_id, &metadata.file_id, &owner)
            .await
        {
            tracing::warn!(%error, "failed to release TUF revision hydration lock");
        }
        result
    }

    async fn hydrate_locked(
        &self,
        ctx: &AppContext,
        metadata: &TufMetadata,
        client_payload_hash: &[u8],
        relative_path: &str,
    ) -> Result<PinnedLevel, CatalogError> {
        if let Some(revision) = find_revision(&ctx.db, metadata.level_id, &metadata.file_id).await?
        {
            return verify_and_select(
                &ctx.db,
                &ctx.storage,
                revision,
                client_payload_hash,
                relative_path,
            )
            .await;
        }

        let temporary =
            tempfile::tempdir().map_err(|error| CatalogError::Storage(error.to_string()))?;
        let archive_path = temporary.path().join("revision.zip");
        self.download_archive(&metadata.download_url, &archive_path)
            .await?;
        let extract_root = temporary.path().join("extracted");
        let settings = Arc::clone(&self.settings);
        let archive_for_worker = archive_path.clone();
        let extract_for_worker = extract_root.clone();
        let processed = tokio::task::spawn_blocking(move || {
            process_archive(&archive_for_worker, &extract_for_worker, &settings)
        })
        .await
        .map_err(|error| CatalogError::Storage(error.to_string()))??;

        let confirmed = self.fetch_metadata(metadata.level_id).await?;
        if confirmed.file_id != metadata.file_id {
            let stable = self.fetch_metadata(metadata.level_id).await?;
            return if stable.file_id == confirmed.file_id {
                Err(CatalogError::LevelRevisionOutdated)
            } else {
                Err(CatalogError::CatalogUnstable)
            };
        }
        if processed.payload_hash != client_payload_hash {
            return Err(CatalogError::LevelInstallationMismatch);
        }

        let archive_key = format!("archives/{}.zip", hex::encode(&processed.payload_hash));
        upload_file_if_missing(&ctx.storage, &archive_key, &archive_path).await?;
        for chart in &processed.charts {
            let key = format!("charts/{}.adofai", hex::encode(&chart.canonical_hash));
            upload_file_if_missing(&ctx.storage, &key, &chart.file_path).await?;
        }

        let transaction = ctx
            .db
            .begin()
            .await
            .map_err(|error| CatalogError::Database(error.to_string()))?;
        let revision = ActiveModel {
            tuf_level_id: Set(metadata.level_id),
            tuf_file_id: Set(metadata.file_id.clone()),
            canonical_payload_hash: Set(processed.payload_hash),
            payload_hash_version: Set(PAYLOAD_HASH_VERSION),
            archive_storage_key: Set(archive_key),
            source_updated_at: Set(metadata.updated_at),
            fetched_at: Set(Utc::now().fixed_offset()),
            ..Default::default()
        }
        .insert(&transaction)
        .await
        .map_err(|error| CatalogError::Database(error.to_string()))?;

        for chart in processed.charts {
            let key = format!("charts/{}.adofai", hex::encode(&chart.canonical_hash));
            level_revision_charts::ActiveModel {
                level_revision_id: Set(revision.id),
                relative_path: Set(chart.relative_path),
                canonical_chart_hash: Set(chart.canonical_hash),
                chart_hash_version: Set(CHART_HASH_VERSION),
                artifact_storage_key: Set(key),
                ..Default::default()
            }
            .insert(&transaction)
            .await
            .map_err(|error| CatalogError::Database(error.to_string()))?;
        }
        transaction
            .commit()
            .await
            .map_err(|error| CatalogError::Database(error.to_string()))?;

        verify_and_select(
            &ctx.db,
            &ctx.storage,
            revision,
            client_payload_hash,
            relative_path,
        )
        .await
    }

    async fn fetch_metadata(&self, tuf_level_id: i64) -> Result<TufMetadata, CatalogError> {
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
        let updated_at = json_string(&value, &["updatedAt", "UpdatedAt"])
            .and_then(|value| DateTime::parse_from_rfc3339(&value).ok());
        Ok(TufMetadata {
            level_id,
            file_id,
            download_url,
            updated_at,
        })
    }

    async fn download_archive(&self, url: &str, destination: &Path) -> Result<(), CatalogError> {
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

async fn find_revision(
    db: &DatabaseConnection,
    tuf_level_id: i64,
    tuf_file_id: &str,
) -> Result<Option<Model>, CatalogError> {
    Entity::find()
        .filter(Column::TufLevelId.eq(tuf_level_id))
        .filter(Column::TufFileId.eq(tuf_file_id))
        .one(db)
        .await
        .map_err(|error| CatalogError::Database(error.to_string()))
}

async fn verify_and_select(
    db: &DatabaseConnection,
    storage: &Storage,
    revision: Model,
    client_payload_hash: &[u8],
    relative_path: &str,
) -> Result<PinnedLevel, CatalogError> {
    if revision.payload_hash_version != PAYLOAD_HASH_VERSION
        || revision.canonical_payload_hash != client_payload_hash
    {
        return Err(CatalogError::LevelInstallationMismatch);
    }
    if !storage
        .exists(Path::new(&revision.archive_storage_key))
        .await
        .map_err(|error| CatalogError::Storage(error.to_string()))?
    {
        return Err(CatalogError::CatalogRevisionConflict);
    }
    let chart = level_revision_charts::Model::find_for_revision(db, revision.id, relative_path)
        .await
        .map_err(|_| CatalogError::ChartNotFound)?;
    if chart.chart_hash_version != CHART_HASH_VERSION
        || !storage
            .exists(Path::new(&chart.artifact_storage_key))
            .await
            .map_err(|error| CatalogError::Storage(error.to_string()))?
    {
        return Err(CatalogError::CatalogRevisionConflict);
    }
    Ok(PinnedLevel { revision, chart })
}

async fn upload_file_if_missing(
    storage: &Storage,
    key: &str,
    source: &Path,
) -> Result<(), CatalogError> {
    let key = Path::new(key);
    if storage
        .exists(key)
        .await
        .map_err(|error| CatalogError::Storage(error.to_string()))?
    {
        return Ok(());
    }
    let file = tokio::fs::File::open(source)
        .await
        .map_err(|error| CatalogError::Storage(error.to_string()))?;
    let stream = BytesStream::from_body_stream(ReaderStream::new(file));
    storage
        .upload_stream(key, stream)
        .await
        .map_err(|error| CatalogError::Storage(error.to_string()))
}

fn process_archive(
    archive_path: &Path,
    extract_root: &Path,
    settings: &TufCatalogSettings,
) -> Result<ProcessedRevision, CatalogError> {
    fs::create_dir_all(extract_root).map_err(|_| CatalogError::UnsafeArchive)?;
    let archive_file = File::open(archive_path).map_err(|_| CatalogError::UnsafeArchive)?;
    let mut archive = ZipArchive::new(archive_file).map_err(|_| CatalogError::UnsafeArchive)?;
    if archive.len() > settings.artifact_max_files {
        return Err(CatalogError::ArtifactTooLarge);
    }
    let mut extracted_bytes = 0_u64;
    let mut extracted_paths = HashSet::new();
    for index in 0..archive.len() {
        let mut entry = archive
            .by_index(index)
            .map_err(|_| CatalogError::UnsafeArchive)?;
        if entry
            .unix_mode()
            .is_some_and(|mode| mode & 0o170_000 == 0o120_000)
        {
            return Err(CatalogError::UnsafeArchive);
        }
        let relative = entry
            .enclosed_name()
            .ok_or(CatalogError::UnsafeArchive)?
            .to_path_buf();
        if relative.to_str().is_none() {
            return Err(CatalogError::UnsafeArchive);
        }
        let collision_key = relative.to_string_lossy().replace('\\', "/").to_lowercase();
        if !extracted_paths.insert(collision_key) {
            return Err(CatalogError::UnsafeArchive);
        }
        let destination = extract_root.join(relative);
        if entry.is_dir() {
            fs::create_dir_all(&destination).map_err(|_| CatalogError::UnsafeArchive)?;
            continue;
        }
        extracted_bytes = extracted_bytes
            .checked_add(entry.size())
            .ok_or(CatalogError::ArtifactTooLarge)?;
        if extracted_bytes > settings.artifact_max_extracted_bytes {
            return Err(CatalogError::ArtifactTooLarge);
        }
        if let Some(parent) = destination.parent() {
            fs::create_dir_all(parent).map_err(|_| CatalogError::UnsafeArchive)?;
        }
        let mut output = File::create(&destination).map_err(|_| CatalogError::UnsafeArchive)?;
        let copied = io::copy(&mut entry, &mut output).map_err(|_| CatalogError::UnsafeArchive)?;
        if copied != entry.size() {
            return Err(CatalogError::UnsafeArchive);
        }
    }

    flatten_leaf_files(extract_root, extract_root)?;
    let payload_hash = calculate_payload_hash(extract_root)?;
    let mut files = collect_files(extract_root)?;
    files.sort_by(|left, right| {
        ordinal_cmp(
            &relative_string(extract_root, left),
            &relative_string(extract_root, right),
        )
    });
    let mut charts = Vec::new();
    for file in files {
        if file
            .extension()
            .and_then(|extension| extension.to_str())
            .is_some_and(|extension| extension.eq_ignore_ascii_case("adofai"))
        {
            let relative_path = level_revision_charts::normalize_relative_chart_path(
                &relative_string(extract_root, &file),
            )
            .map_err(|_| CatalogError::UnsafeArchive)?;
            charts.push(ProcessedChart {
                relative_path,
                canonical_hash: hash_file(&file)?,
                file_path: file,
            });
        }
    }
    if charts.is_empty() {
        return Err(CatalogError::NoCharts);
    }
    Ok(ProcessedRevision {
        payload_hash,
        charts,
    })
}

fn calculate_payload_hash(root: &Path) -> Result<Vec<u8>, CatalogError> {
    let mut files = collect_files(root)?;
    files.retain(|file| {
        file.file_name()
            .and_then(|name| name.to_str())
            .is_none_or(|name| {
                !name.eq_ignore_ascii_case(MANIFEST_FILE_NAME)
                    && !name.eq_ignore_ascii_case(&format!("{MANIFEST_FILE_NAME}.tmp"))
            })
    });
    files.sort_by(|left, right| {
        ordinal_cmp(&relative_string(root, left), &relative_string(root, right))
    });
    let mut hash = Sha256::new();
    let mut buffer = [0_u8; 128 * 1024];
    for file in files {
        let relative = relative_string(root, &file);
        let path_bytes = relative.as_bytes();
        let length = u32::try_from(path_bytes.len()).map_err(|_| CatalogError::UnsafeArchive)?;
        hash.update(length.to_le_bytes());
        hash.update(path_bytes);
        let mut input = File::open(file).map_err(|_| CatalogError::UnsafeArchive)?;
        loop {
            let read = input
                .read(&mut buffer)
                .map_err(|_| CatalogError::UnsafeArchive)?;
            if read == 0 {
                break;
            }
            hash.update(&buffer[..read]);
        }
    }
    Ok(hash.finalize().to_vec())
}

fn hash_file(path: &Path) -> Result<Vec<u8>, CatalogError> {
    let mut file = File::open(path).map_err(|_| CatalogError::UnsafeArchive)?;
    let mut hash = Sha256::new();
    let mut buffer = [0_u8; 128 * 1024];
    loop {
        let read = file
            .read(&mut buffer)
            .map_err(|_| CatalogError::UnsafeArchive)?;
        if read == 0 {
            break;
        }
        hash.update(&buffer[..read]);
    }
    Ok(hash.finalize().to_vec())
}

fn collect_files(root: &Path) -> Result<Vec<PathBuf>, CatalogError> {
    let mut files = Vec::new();
    let mut pending = vec![root.to_path_buf()];
    while let Some(directory) = pending.pop() {
        for entry in fs::read_dir(directory).map_err(|_| CatalogError::UnsafeArchive)? {
            let entry = entry.map_err(|_| CatalogError::UnsafeArchive)?;
            let file_type = entry.file_type().map_err(|_| CatalogError::UnsafeArchive)?;
            if file_type.is_symlink() {
                return Err(CatalogError::UnsafeArchive);
            }
            if file_type.is_dir() {
                pending.push(entry.path());
            } else if file_type.is_file() {
                files.push(entry.path());
            }
        }
    }
    Ok(files)
}

fn flatten_leaf_files(start: &Path, root: &Path) -> Result<(), CatalogError> {
    let mut directories = Vec::new();
    for entry in fs::read_dir(start).map_err(|_| CatalogError::UnsafeArchive)? {
        let entry = entry.map_err(|_| CatalogError::UnsafeArchive)?;
        if entry
            .file_type()
            .map_err(|_| CatalogError::UnsafeArchive)?
            .is_dir()
        {
            directories.push(entry.path());
        }
    }
    if !directories.is_empty() {
        for directory in directories {
            flatten_leaf_files(&directory, root)?;
        }
        return Ok(());
    }
    if start == root {
        return Ok(());
    }
    for entry in fs::read_dir(start).map_err(|_| CatalogError::UnsafeArchive)? {
        let entry = entry.map_err(|_| CatalogError::UnsafeArchive)?;
        if !entry
            .file_type()
            .map_err(|_| CatalogError::UnsafeArchive)?
            .is_file()
        {
            continue;
        }
        let destination = root.join(entry.file_name());
        if !destination.exists() {
            fs::rename(entry.path(), destination).map_err(|_| CatalogError::UnsafeArchive)?;
        }
    }
    Ok(())
}

fn relative_string(root: &Path, path: &Path) -> String {
    path.strip_prefix(root)
        .unwrap_or(path)
        .to_string_lossy()
        .replace('\\', "/")
}

fn ordinal_cmp(left: &str, right: &str) -> std::cmp::Ordering {
    left.encode_utf16().cmp(right.encode_utf16())
}

fn canonical_download_url(value: &str) -> Result<String, CatalogError> {
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

fn json_string(value: &serde_json::Value, keys: &[&str]) -> Option<String> {
    keys.iter()
        .find_map(|key| value.get(*key))
        .and_then(serde_json::Value::as_str)
        .map(str::to_owned)
}

fn json_i64(value: &serde_json::Value, keys: &[&str]) -> Option<i64> {
    let value = keys.iter().find_map(|key| value.get(*key))?;
    value
        .as_i64()
        .or_else(|| value.as_str().and_then(|value| value.parse().ok()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write as _;
    use zip::write::SimpleFileOptions;

    fn test_settings() -> TufCatalogSettings {
        TufCatalogSettings {
            tuf_api_base_url: "http://127.0.0.1:1".to_owned(),
            artifact_root: "unused".to_owned(),
            artifact_max_download_bytes: 1024 * 1024,
            artifact_max_extracted_bytes: 1024 * 1024,
            artifact_max_files: 20,
            artifact_hydration_timeout_seconds: 10,
            artifact_max_concurrent_hydrations: 1,
        }
    }

    fn write_zip(path: &Path, entries: &[(&str, &[u8])]) {
        let file = File::create(path).expect("create ZIP");
        let mut archive = zip::ZipWriter::new(file);
        for (name, contents) in entries {
            archive
                .start_file(*name, SimpleFileOptions::default())
                .expect("start ZIP entry");
            archive.write_all(contents).expect("write ZIP entry");
        }
        archive.finish().expect("finish ZIP");
    }

    #[test]
    fn normalizes_google_drive_file_links() {
        let result = canonical_download_url("https://drive.google.com/file/d/abc123/view")
            .expect("valid URL");
        assert!(result.contains("id=abc123"));
        assert!(result.starts_with("https://drive.usercontent.google.com/download"));
    }

    #[test]
    fn processes_multiple_charts_and_matches_tufhelper_payload_hash() {
        let temporary = tempfile::tempdir().expect("temporary directory");
        let archive = temporary.path().join("level.zip");
        write_zip(&archive, &[("a.adofai", b"A"), ("nested/b.adofai", b"B")]);
        let processed = process_archive(
            &archive,
            &temporary.path().join("extracted"),
            &test_settings(),
        )
        .expect("process archive");
        assert_eq!(
            processed
                .charts
                .iter()
                .map(|chart| chart.relative_path.as_str())
                .collect::<Vec<_>>(),
            ["a.adofai", "b.adofai"]
        );

        let mut expected = Sha256::new();
        for (name, contents) in [("a.adofai", b"A".as_slice()), ("b.adofai", b"B".as_slice())] {
            expected.update(
                u32::try_from(name.len())
                    .expect("path length")
                    .to_le_bytes(),
            );
            expected.update(name.as_bytes());
            expected.update(contents);
        }
        assert_eq!(processed.payload_hash, expected.finalize().to_vec());
    }

    #[test]
    fn rejects_case_colliding_archive_paths() {
        let temporary = tempfile::tempdir().expect("temporary directory");
        let archive = temporary.path().join("level.zip");
        write_zip(&archive, &[("A.adofai", b"A"), ("a.adofai", b"B")]);
        assert!(matches!(
            process_archive(
                &archive,
                &temporary.path().join("extracted"),
                &test_settings(),
            ),
            Err(CatalogError::UnsafeArchive)
        ));
    }
}
