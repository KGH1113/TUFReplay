use crate::services::replays::{PublishedReplay, ReplayFile};
use serde::Serialize;
use uuid::Uuid;

#[derive(Serialize)]
pub struct ReplayManifestResponse {
    pub format_version: u32,
    pub run_id: Uuid,
    pub tuf_level_id: i64,
    pub chart_path: String,
    pub external_pass_id: i64,
    pub official_file_id: String,
    pub chart_sha256: String,
    pub gameplay_hash_version: u32,
    pub gameplay_hash: String,
    pub evidence_digest: String,
    pub recorded_speed: f64,
    pub files: Vec<ReplayFileResponse>,
}

#[derive(Serialize)]
pub struct ReplayFileResponse {
    pub name: &'static str,
    pub kind: &'static str,
    pub media_type: &'static str,
    pub url: String,
    pub sha256: String,
    pub bytes: u64,
    pub records: u64,
}

impl ReplayManifestResponse {
    #[must_use]
    pub fn from_replay(replay: &PublishedReplay) -> Self {
        Self {
            format_version: 2,
            run_id: replay.run_id,
            tuf_level_id: replay.tuf_level_id,
            chart_path: replay.chart_path.clone(),
            external_pass_id: replay.external_pass_id,
            official_file_id: replay.validation.official_file_id.clone(),
            chart_sha256: replay.validation.chart_sha256.clone(),
            gameplay_hash_version: replay.validation.gameplay_hash_version,
            gameplay_hash: replay.validation.gameplay_hash.clone(),
            evidence_digest: replay.manifest.digest.clone(),
            recorded_speed: replay.validation.speed,
            files: replay
                .files()
                .into_iter()
                .map(|(file, stream)| ReplayFileResponse {
                    name: file.name(),
                    kind: file_kind(file),
                    media_type: file.media_type(),
                    url: format!("/api/v1/replays/{}/files/{}", replay.run_id, file.name()),
                    sha256: stream.sha256.clone(),
                    bytes: stream.bytes,
                    records: stream.records,
                })
                .collect(),
        }
    }
}

const fn file_kind(file: ReplayFile) -> &'static str {
    match file {
        ReplayFile::Inputs => "inputs",
        ReplayFile::Hits => "hits",
        ReplayFile::Metadata => "metadata",
        ReplayFile::Lifecycle => "lifecycle",
        ReplayFile::Settings => "settings",
        ReplayFile::Health => "health",
    }
}
