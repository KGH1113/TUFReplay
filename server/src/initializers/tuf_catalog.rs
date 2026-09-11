use async_trait::async_trait;
use loco_rs::{
    app::{AppContext, Initializer},
    doctor::{Check, CheckStatus},
    Error, Result,
};

use crate::services::tuf::catalog::TufCatalogRuntime;
use crate::services::tuf::catalog::TufCatalogSettings;

pub struct TufCatalogInitializer;

#[async_trait]
impl Initializer for TufCatalogInitializer {
    fn name(&self) -> String {
        "tuf-catalog".to_owned()
    }

    async fn before_run(&self, ctx: &AppContext) -> Result<()> {
        let runtime = TufCatalogRuntime::new(load_settings(ctx)?)
            .map_err(|error| Error::Message(format!("cannot initialize TUF catalog: {error}")))?;
        ctx.shared_store.insert(runtime);
        Ok(())
    }

    async fn check(&self, ctx: &AppContext) -> Result<Option<Check>> {
        let result = load_settings(ctx).and_then(|settings| {
            if settings.artifact_root.trim().is_empty() {
                Err(Error::Message("artifact_root is empty".to_owned()))
            } else {
                Ok(())
            }
        });
        Ok(Some(match result {
            Ok(()) => Check {
                status: CheckStatus::Ok,
                message: "TUF catalog configuration: valid".to_owned(),
                description: None,
            },
            Err(error) => Check {
                status: CheckStatus::NotOk,
                message: "TUF catalog configuration: invalid".to_owned(),
                description: Some(error.to_string()),
            },
        }))
    }
}

fn load_settings(ctx: &AppContext) -> Result<TufCatalogSettings> {
    Ok(crate::settings::Settings::get(ctx)?.auto_submission.catalog)
}
