use async_trait::async_trait;
use loco_rs::{
    app::{AppContext, Initializer},
    doctor::{Check, CheckStatus},
    Error, Result,
};
use serde::Deserialize;

use crate::models::run_ingest::{RunIngestSettings, RunIngestStore};

#[derive(Debug, Deserialize)]
struct Settings {
    auto_submission: RunIngestSettings,
}

pub struct RunIngestInitializer;

#[async_trait]
impl Initializer for RunIngestInitializer {
    fn name(&self) -> String {
        "run-ingest".to_owned()
    }

    async fn before_run(&self, ctx: &AppContext) -> Result<()> {
        let store = RunIngestStore::connect(load_settings(ctx)?)
            .await
            .map_err(|error| Error::Message(format!("cannot connect to ingest Redis: {error}")))?;
        ctx.shared_store.insert(store);
        Ok(())
    }

    async fn check(&self, ctx: &AppContext) -> Result<Option<Check>> {
        let result = match load_settings(ctx) {
            Ok(settings) => RunIngestStore::connect(settings)
                .await
                .map(|_| ())
                .map_err(|error| error.to_string()),
            Err(error) => Err(error.to_string()),
        };
        Ok(Some(match result {
            Ok(()) => Check {
                status: CheckStatus::Ok,
                message: "Redis ingest connection: success".to_owned(),
                description: None,
            },
            Err(error) => Check {
                status: CheckStatus::NotOk,
                message: "Redis ingest connection: failed".to_owned(),
                description: Some(error),
            },
        }))
    }
}

fn load_settings(ctx: &AppContext) -> Result<RunIngestSettings> {
    let value = ctx
        .config
        .settings
        .clone()
        .ok_or_else(|| Error::Message("settings.auto_submission is required".to_owned()))?;
    let settings: Settings = serde_json::from_value(value)
        .map_err(|error| Error::Message(format!("invalid settings.auto_submission: {error}")))?;
    validate_settings(&settings.auto_submission)?;
    Ok(settings.auto_submission)
}

fn validate_settings(settings: &RunIngestSettings) -> Result<()> {
    if settings.redis_url.trim().is_empty()
        || settings.active_ttl_seconds == 0
        || settings.sealed_ttl_seconds == 0
        || settings.hard_duration_seconds == 0
        || settings.max_chunk_bytes == 0
        || settings.max_session_bytes == 0
        || settings.heartbeat_interval_ms == 0
    {
        return Err(Error::Message(
            "auto submission limits and Redis URL must be non-empty and non-zero".to_owned(),
        ));
    }
    if settings.max_chunk_bytes as u64 > settings.max_session_bytes {
        return Err(Error::Message(
            "auto submission max_chunk_bytes must not exceed max_session_bytes".to_owned(),
        ));
    }
    Ok(())
}
