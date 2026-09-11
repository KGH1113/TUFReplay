use crate::models::{
    run_sessions::{Model, NewRunSession},
    run_submission_records::Entity as Records,
};
use crate::services::{
    auth::AccountIdentity,
    ingest::{IngestError, RunIngestStore},
};
use base64::{engine::general_purpose::URL_SAFE_NO_PAD, Engine};
use chrono::{Duration, Utc};
use loco_rs::prelude::*;
use sea_orm::TransactionTrait;
use sha2::{Digest, Sha256};
use uuid::Uuid;

pub struct RunClaims {
    pub protocol_version: i64,
    pub game_version: String,
    pub mod_version: String,
    pub level_id: i64,
    pub file_id: String,
    pub chart_path: String,
}

pub struct IssuedRun {
    pub run: Model,
    pub upload_token: String,
    pub lease_duration_ms: u64,
    pub max_chunk_bytes: usize,
    pub heartbeat_interval_ms: u64,
}

#[derive(Debug, thiserror::Error)]
pub enum IssuanceError {
    #[error(transparent)]
    Internal(#[from] loco_rs::Error),
    #[error(transparent)]
    Model(#[from] loco_rs::model::ModelError),
    #[error(transparent)]
    Database(#[from] sea_orm::DbErr),
    #[error(transparent)]
    Ingest(#[from] IngestError),
}

pub async fn issue(
    ctx: &AppContext,
    identity: AccountIdentity,
    claims: RunClaims,
) -> Result<IssuedRun, IssuanceError> {
    let store = ctx
        .shared_store
        .get::<RunIngestStore>()
        .ok_or_else(|| Error::Message("ingest unavailable".into()))?;
    let mut bytes = [0_u8; 32];
    getrandom::fill(&mut bytes).map_err(|_| Error::Message("random source unavailable".into()))?;
    let token = URL_SAFE_NO_PAD.encode(bytes);
    let hash = Sha256::digest(token.as_bytes()).to_vec();
    let id = Uuid::new_v4();
    let now = Utc::now();
    let settings = store.settings();
    let transaction = ctx.db.begin().await?;
    let run = Model::create(
        &transaction,
        NewRunSession {
            pid: id,
            protocol_version: claims.protocol_version,
            client_game_version: claims.game_version,
            client_mod_version: claims.mod_version,
            tuf_level_id: claims.level_id,
            client_tuf_file_id: claims.file_id,
            client_level_relative_path: claims.chart_path,
            level_revision_id: None,
            level_revision_chart_id: None,
            upload_token_hash: hash.clone(),
            lease_expires_at: (now + Duration::seconds(settings.active_ttl_seconds as i64))
                .fixed_offset(),
            hard_expires_at: (now + Duration::seconds(settings.hard_duration_seconds as i64))
                .fixed_offset(),
        },
    )
    .await?;
    Records::create_authorized(
        &transaction,
        run.id,
        &identity.owner_id,
        Some(identity.grant_id),
    )
    .await?;
    transaction.commit().await?;
    if let Err(error) = store
        .create_owned_session(
            id,
            &hex::encode(hash),
            run.hard_expires_at.timestamp(),
            &identity.owner_id,
        )
        .await
    {
        if let Err(cleanup) = store.discard(id).await {
            tracing::error!(run_id=%id, %cleanup, "Redis issuance compensation failed");
        }
        if let Err(cleanup) = run.delete_record(&ctx.db).await {
            tracing::error!(run_id=%id, %cleanup, "DB issuance compensation failed");
        }
        return Err(error.into());
    }
    Ok(IssuedRun {
        run,
        upload_token: token,
        lease_duration_ms: settings.active_ttl_seconds.saturating_mul(1_000),
        max_chunk_bytes: settings.max_chunk_bytes,
        heartbeat_interval_ms: settings.heartbeat_interval_ms,
    })
}
