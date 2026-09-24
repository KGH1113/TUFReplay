use crate::domain::{EvidenceManifest, EvidenceStream, ValidatedResult};
use crate::models::run_submission_records::queries;
use crate::models::visual_presets::{self, VisualKind, VisualPresetBundle};
use loco_rs::prelude::*;
use serde::Serialize;
use sha2::{Digest, Sha256};
use std::path::Path;
use uuid::Uuid;

const REQUIRED_REPLAY_KINDS: [u8; 3] = [0, 1, 2];

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum ReplayFile {
    Inputs,
    Hits,
    Metadata,
    Lifecycle,
    Settings,
    Health,
}

impl ReplayFile {
    pub const ALL: [Self; 6] = [
        Self::Inputs,
        Self::Hits,
        Self::Metadata,
        Self::Lifecycle,
        Self::Settings,
        Self::Health,
    ];

    #[must_use]
    pub fn from_name(name: &str) -> Option<Self> {
        Self::ALL.into_iter().find(|file| file.name() == name)
    }

    #[must_use]
    pub const fn kind(self) -> u8 {
        match self {
            Self::Inputs => 0,
            Self::Hits => 1,
            Self::Metadata => 2,
            Self::Lifecycle => 3,
            Self::Settings => 4,
            Self::Health => 5,
        }
    }

    #[must_use]
    pub const fn name(self) -> &'static str {
        match self {
            Self::Inputs => "inputs.csv",
            Self::Hits => "hits.csv",
            Self::Metadata => "metadata.json",
            Self::Lifecycle => "lifecycle.jsonl",
            Self::Settings => "settings.jsonl",
            Self::Health => "health.jsonl",
        }
    }

    #[must_use]
    pub const fn media_type(self) -> &'static str {
        match self {
            Self::Inputs | Self::Hits => "text/csv; charset=utf-8",
            Self::Metadata => "application/json",
            Self::Lifecycle | Self::Settings | Self::Health => "application/x-ndjson",
        }
    }
}

pub struct PublishedReplay {
    pub run_id: Uuid,
    pub tuf_level_id: i64,
    pub chart_path: String,
    pub external_pass_id: i64,
    pub manifest: EvidenceManifest,
    pub validation: ValidatedResult,
}

#[derive(Clone, Debug, Serialize)]
pub struct VisualDescriptor {
    pub preset_id: Uuid,
    pub name: String,
    pub source: String,
    pub source_version: String,
    pub url: String,
    pub sha256: String,
    pub bytes: u64,
}

#[derive(Clone, Debug)]
pub struct PublishedVisuals {
    pub keyviewer: Option<(VisualDescriptor, VisualPresetBundle)>,
    pub overlay: Option<(VisualDescriptor, VisualPresetBundle)>,
}

pub async fn published_visuals(
    ctx: &AppContext,
    run_id: Uuid,
    objects: bool,
) -> Result<PublishedVisuals> {
    Ok(PublishedVisuals {
        keyviewer: visual_for_run(ctx, run_id, VisualKind::Keyviewer, objects).await?,
        overlay: visual_for_run(ctx, run_id, VisualKind::Overlay, objects).await?,
    })
}

pub async fn published_visual(
    ctx: &AppContext,
    run_id: Uuid,
    kind: VisualKind,
    objects: bool,
) -> Result<(VisualDescriptor, VisualPresetBundle)> {
    visual_for_run(ctx, run_id, kind, objects)
        .await?
        .ok_or(Error::NotFound)
}

async fn visual_for_run(
    ctx: &AppContext,
    run_id: Uuid,
    kind: VisualKind,
    objects: bool,
) -> Result<Option<(VisualDescriptor, VisualPresetBundle)>> {
    let Some(mut bundle) =
        visual_presets::active_bundle_for_published_run(&ctx.db, run_id, kind).await?
    else {
        return Ok(None);
    };
    let bytes = u64::try_from(bundle.bytes)
        .map_err(|_| Error::Message("visual bundle size is invalid".into()))?;
    if bytes != bundle.bundle.len() as u64
        || hex::encode(Sha256::digest(&bundle.bundle)) != bundle.sha256
    {
        return Err(Error::Message("visual bundle integrity is invalid".into()));
    }
    if !objects {
        bundle.bundle = super::visual_assets::inline_legacy(ctx, &bundle.bundle).await?;
    } else {
        let mut value: serde_json::Value = serde_json::from_slice(&bundle.bundle)?;
        if value["schema_version"] == 2 {
            super::visual_assets::public_routes(ctx, &mut value).await?;
            bundle.bundle = serde_json::to_vec(&value)?;
        }
    }
    bundle.bytes = bundle.bundle.len() as i64;
    bundle.sha256 = hex::encode(Sha256::digest(&bundle.bundle));
    let descriptor = VisualDescriptor {
        preset_id: bundle.id,
        name: bundle.name.clone(),
        source: bundle.source.clone(),
        source_version: bundle.source_version.clone(),
        url: format!(
            "/api/v1/replays/{run_id}/visuals/{}{}",
            kind.as_str(),
            if objects { "?asset_mode=objects" } else { "" }
        ),
        sha256: bundle.sha256.clone(),
        bytes: bundle.bytes as u64,
    };
    Ok(Some((descriptor, bundle)))
}

impl PublishedReplay {
    pub async fn load(db: &DatabaseConnection, run_id: Uuid) -> Result<Self> {
        let record = queries::published_replay(db, run_id).await?;
        let manifest: EvidenceManifest = serde_json::from_value(record.manifest)
            .map_err(|_| Error::Message("published replay manifest is invalid".into()))?;
        let validation: ValidatedResult = serde_json::from_value(record.validation)
            .map_err(|_| Error::Message("published replay validation is invalid".into()))?;
        if manifest.digest != validation.evidence_digest || !valid_manifest(run_id, &manifest) {
            return Err(Error::Message(
                "published replay evidence is inconsistent".into(),
            ));
        }
        Ok(Self {
            run_id: record.run_id,
            tuf_level_id: record.tuf_level_id,
            chart_path: record.chart_path,
            external_pass_id: record.external_pass_id,
            manifest,
            validation,
        })
    }

    #[must_use]
    pub fn files(&self) -> Vec<(ReplayFile, &EvidenceStream)> {
        ReplayFile::ALL
            .into_iter()
            .filter_map(|file| self.file(file).map(|stream| (file, stream)))
            .collect()
    }

    #[must_use]
    pub fn file(&self, file: ReplayFile) -> Option<&EvidenceStream> {
        stream_for_kind(&self.manifest, file.kind()).filter(|stream| {
            stream
                .storage_key
                .starts_with(&format!("evidence/{}/", self.run_id))
        })
    }
}

pub async fn download(
    ctx: &AppContext,
    replay: &PublishedReplay,
    file: ReplayFile,
) -> Result<(EvidenceStream, loco_rs::storage::stream::BytesStream)> {
    let stream = replay.file(file).cloned().ok_or(Error::NotFound)?;
    let body = ctx
        .storage
        .download_stream(Path::new(&stream.storage_key))
        .await
        .map_err(|error| {
            tracing::error!(run_id=%replay.run_id, file=file.name(), %error, "published replay asset unavailable");
            Error::Message("published replay asset unavailable".into())
        })?;
    Ok((stream, body))
}

fn stream_for_kind(manifest: &EvidenceManifest, kind: u8) -> Option<&EvidenceStream> {
    let mut streams = manifest.streams.iter().filter(|stream| stream.kind == kind);
    let stream = streams.next()?;
    streams.next().is_none().then_some(stream)
}

fn valid_manifest(run_id: Uuid, manifest: &EvidenceManifest) -> bool {
    let prefix = format!("evidence/{run_id}/");
    let replay_streams = manifest.streams.iter().filter(|stream| stream.kind <= 5);
    if replay_streams
        .clone()
        .any(|stream| !stream.storage_key.starts_with(&prefix))
    {
        return false;
    }
    if ReplayFile::ALL.into_iter().any(|file| {
        manifest
            .streams
            .iter()
            .filter(|stream| stream.kind == file.kind())
            .count()
            > 1
    }) {
        return false;
    }
    REQUIRED_REPLAY_KINDS
        .iter()
        .all(|kind| stream_for_kind(manifest, *kind).is_some())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn replay_file_names_are_a_closed_mapping() {
        for file in ReplayFile::ALL {
            assert_eq!(ReplayFile::from_name(file.name()), Some(file));
        }
        assert_eq!(ReplayFile::from_name("../inputs.csv"), None);
        assert_eq!(ReplayFile::from_name("server-timing.csv"), None);
    }
}
