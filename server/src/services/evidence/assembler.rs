use super::records::{invalid, record_count};
use crate::domain::EvidenceManifest;
use crate::domain::EvidenceStream;
use crate::services::ingest::RunIngestStore;
use loco_rs::{
    prelude::*,
    storage::{stream::BytesStream, Storage},
};
use sha2::{Digest, Sha256};
use tokio::io::AsyncWriteExt;
use tokio_util::io::ReaderStream;

pub async fn assemble(
    store: &RunIngestStore,
    storage: &Storage,
    run_id: uuid::Uuid,
) -> Result<EvidenceManifest> {
    let receipt = store.receipt(run_id, None).await.map_err(|_| invalid())?;
    if receipt.status != "sealed" {
        return Err(invalid());
    }
    let temp = tempfile::tempdir()?;
    let mut files = Vec::new();
    for kind in 0..7 {
        files.push(tokio::fs::File::create(temp.path().join(kind.to_string())).await?);
    }
    let mut hashes: Vec<_> = (0..7).map(|_| Sha256::new()).collect();
    let mut counts = [0u64; 7];
    let mut sizes = [0u64; 7];
    let mut next = 0u64;
    let mut after = None;
    let mut meta = Vec::new();
    loop {
        let page = store
            .read_page(run_id, after.as_deref())
            .await
            .map_err(|e| Error::Message(e.to_string()))?;
        if page.is_empty() {
            break;
        }
        for chunk in page {
            if chunk.sequence != next || chunk.kind > 5 {
                return Err(invalid());
            }
            next += 1;
            let kind = usize::from(chunk.kind);
            counts[kind] += record_count(chunk.kind, &chunk.payload)?;
            sizes[kind] += chunk.payload.len() as u64;
            if sizes[..6].iter().sum::<u64>() > store.settings().max_session_bytes {
                return Err(invalid());
            }
            if kind == 2 {
                if sizes[2] > 262_144 {
                    return Err(invalid());
                }
                meta.extend_from_slice(&chunk.payload);
            }
            hashes[kind].update(&chunk.payload);
            files[kind].write_all(&chunk.payload).await?;
            // Preserve server receipt ordering/time independently of client clocks.
            let timing = format!("{},{},{}\n", chunk.sequence, chunk.kind, chunk.id);
            hashes[6].update(timing.as_bytes());
            sizes[6] += timing.len() as u64;
            counts[6] += 1;
            files[6].write_all(timing.as_bytes()).await?;
            after = Some(chunk.id);
        }
    }
    if next as i64 - 1 != receipt.acknowledged_sequence
        || counts[0] != receipt.input_count
        || counts[1] != receipt.hit_context_count
        || counts[0] == 0
        || counts[1] == 0
    {
        return Err(invalid());
    }
    let meta: serde_json::Value = serde_json::from_slice(&meta).map_err(|_| invalid())?;
    if !meta.is_object() {
        return Err(invalid());
    }
    counts[2] = 1;
    for file in &mut files {
        file.flush().await?;
        file.sync_all().await?;
    }
    drop(files);
    let mut streams = Vec::new();
    for (kind, hash) in hashes.into_iter().enumerate() {
        if sizes[kind] == 0 {
            continue;
        }
        let digest = hex::encode(hash.finalize());
        let key = format!("evidence/{run_id}/{digest}");
        // A retry reuses this staging key, including after process termination.
        let staged = format!("evidence/{run_id}/temp-{kind}");
        let file = tokio::fs::File::open(temp.path().join(kind.to_string())).await?;
        let bytes = BytesStream::from_body_stream(ReaderStream::new(file));
        storage
            .upload_stream(std::path::Path::new(&staged), bytes)
            .await?;
        storage
            .rename(std::path::Path::new(&staged), std::path::Path::new(&key))
            .await?;
        streams.push(EvidenceStream {
            kind: kind as u8,
            storage_key: key,
            sha256: digest,
            bytes: sizes[kind],
            records: counts[kind],
        });
    }
    let digest = hex::encode(Sha256::digest(serde_json::to_vec(&streams)?));
    Ok(EvidenceManifest {
        protocol_version: 1,
        final_sequence: receipt.acknowledged_sequence,
        digest,
        streams,
    })
}
