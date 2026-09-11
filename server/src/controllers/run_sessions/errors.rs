use super::dtos::ErrorResponse;
use crate::services::ingest::IngestError;
use crate::services::tuf::catalog::CatalogError;
use axum::{http::StatusCode, response::IntoResponse, Json};
use loco_rs::prelude::*;
pub(super) fn catalog_error_response(error: CatalogError) -> Response {
    let (status, code) = match error {
        CatalogError::IneligibleDifficulty => {
            (StatusCode::UNPROCESSABLE_ENTITY, "level_not_eligible")
        }
        CatalogError::LevelRevisionOutdated => (StatusCode::CONFLICT, "level_revision_outdated"),
        CatalogError::LevelInstallationMismatch => {
            (StatusCode::CONFLICT, "level_installation_mismatch")
        }
        CatalogError::ChartNotFound => (StatusCode::UNPROCESSABLE_ENTITY, "chart_not_found"),
        CatalogError::CatalogUnstable => (StatusCode::SERVICE_UNAVAILABLE, "catalog_unstable"),
        CatalogError::UpstreamUnavailable => {
            (StatusCode::SERVICE_UNAVAILABLE, "catalog_unavailable")
        }
        CatalogError::Busy => (StatusCode::SERVICE_UNAVAILABLE, "catalog_busy"),
        CatalogError::UnsafeArchive => (StatusCode::BAD_GATEWAY, "unsafe_level_archive"),
        CatalogError::ArtifactTooLarge => (StatusCode::BAD_GATEWAY, "level_archive_too_large"),
        CatalogError::NoCharts => (StatusCode::BAD_GATEWAY, "level_archive_has_no_charts"),
        CatalogError::CatalogRevisionConflict => (
            StatusCode::INTERNAL_SERVER_ERROR,
            "catalog_revision_conflict",
        ),
        CatalogError::Storage(_) => (StatusCode::INTERNAL_SERVER_ERROR, "artifact_storage_failed"),
        CatalogError::Database(_) => (StatusCode::INTERNAL_SERVER_ERROR, "catalog_database_failed"),
    };
    tracing::warn!(%error, code, "run session eligibility failed");
    (status, Json(ErrorResponse { code })).into_response()
}

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
