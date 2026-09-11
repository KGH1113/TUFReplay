use super::{IngestError, RunIngestStore};
use redis::streams::StreamRangeReply;
use serde::Serialize;
use uuid::Uuid;

#[derive(Clone, Debug, Serialize)]
pub struct SealReceipt {
    pub status: String,
    pub acknowledged_sequence: i64,
    pub input_count: u64,
    pub hit_context_count: u64,
}

pub struct IngestChunk {
    pub id: String,
    pub sequence: u64,
    pub kind: u8,
    pub payload: Vec<u8>,
}

impl RunIngestStore {
    pub async fn receipt(
        &self,
        run_id: Uuid,
        token: Option<&str>,
    ) -> Result<SealReceipt, IngestError> {
        let mut conn = self.connection.clone();
        let values: std::collections::HashMap<String, String> = redis::cmd("HGETALL")
            .arg(self.key(super::store::meta_key(run_id)))
            .query_async(&mut conn)
            .await?;
        if values.is_empty() {
            return Err(IngestError::NotFound);
        }
        if token.is_some_and(|token| values.get("token_hash").map(String::as_str) != Some(token)) {
            return Err(IngestError::Unauthorized);
        }
        let number = |key| {
            values
                .get(key)
                .and_then(|s| s.parse::<u64>().ok())
                .unwrap_or(0)
        };
        Ok(SealReceipt {
            status: values
                .get("status")
                .cloned()
                .ok_or(IngestError::InvalidState)?,
            acknowledged_sequence: number("next_sequence") as i64 - 1,
            input_count: number("input_count"),
            hit_context_count: number("hit_context_count"),
        })
    }

    pub async fn read_page(
        &self,
        run_id: Uuid,
        after: Option<&str>,
    ) -> Result<Vec<IngestChunk>, IngestError> {
        let mut conn = self.connection.clone();
        let start = after.map_or_else(|| "-".to_owned(), |id| format!("({id}"));
        let page: StreamRangeReply = redis::cmd("XRANGE")
            .arg(self.key(super::store::chunks_key(run_id)))
            .arg(start)
            .arg("+")
            .arg("COUNT")
            .arg(16)
            .query_async(&mut conn)
            .await?;
        page.ids
            .into_iter()
            .map(|entry| {
                Ok(IngestChunk {
                    sequence: entry.get("sequence").ok_or(IngestError::InvalidState)?,
                    kind: entry.get("kind").ok_or(IngestError::InvalidState)?,
                    payload: entry.get("payload").ok_or(IngestError::InvalidState)?,
                    id: entry.id,
                })
            })
            .collect()
    }
}
