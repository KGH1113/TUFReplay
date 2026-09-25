use crate::services::ingest::IngestError;
use loco_rs::prelude::*;
pub(super) fn http_ingest_error(error: IngestError) -> Error {
    match error {
        IngestError::Unauthorized => Error::Unauthorized("invalid upload token".to_owned()),
        IngestError::NotFound => Error::NotFound,
        IngestError::Expired => Error::BadRequest("run ingest session expired".to_owned()),
        IngestError::InvalidState => {
            Error::BadRequest("run ingest session is not active".to_owned())
        }
        other => Error::Message(format!("run ingest failure: {other}")),
    }
}

pub(super) fn ws_ingest_error(error: &IngestError) -> (&'static str, bool) {
    match error {
        IngestError::NotFound => ("run_not_found", true),
        IngestError::Unauthorized => ("unauthorized", true),
        IngestError::InvalidState => ("invalid_state", true),
        IngestError::Expired => ("session_expired", true),
        IngestError::SessionTooLarge => ("session_too_large", true),
        IngestError::AccountLimit => ("account_upload_limit", true),
        IngestError::FinalSequenceMismatch { .. } => ("final_sequence_mismatch", false),
        IngestError::AlreadyExists => ("session_already_exists", true),
        IngestError::Conflict => ("stream_conflict", true),
        IngestError::Redis(_) => ("ingest_unavailable", true),
    }
}
