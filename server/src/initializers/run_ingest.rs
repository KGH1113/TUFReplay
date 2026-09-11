use async_trait::async_trait;
use loco_rs::{
    app::{AppContext, Initializer},
    doctor::{Check, CheckStatus},
    Result,
};

use crate::services::ingest::RunIngestSettings;
use crate::services::ingest::RunIngestStore;

pub struct RunIngestInitializer;

#[async_trait]
impl Initializer for RunIngestInitializer {
    fn name(&self) -> String {
        "run-ingest".to_owned()
    }

    async fn before_run(&self, ctx: &AppContext) -> Result<()> {
        crate::services::ingest::connection::get(ctx).await?;
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
    Ok(crate::settings::Settings::get(ctx)?.auto_submission.ingest)
}
