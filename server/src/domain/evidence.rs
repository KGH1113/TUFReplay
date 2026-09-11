use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct EvidenceStream {
    pub kind: u8,
    pub storage_key: String,
    pub sha256: String,
    pub bytes: u64,
    pub records: u64,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct EvidenceManifest {
    pub protocol_version: u32,
    pub final_sequence: i64,
    pub digest: String,
    pub streams: Vec<EvidenceStream>,
}
