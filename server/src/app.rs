use async_trait::async_trait;
use loco_rs::{
    app::{AppContext, Hooks, Initializer},
    bgworker::{self, Queue},
    boot::{create_context, run_app, BootResult, StartMode},
    config::Config,
    controller::AppRoutes,
    db,
    environment::Environment,
    storage::{self, Storage},
    task::Tasks,
    Error, Result,
};
use migration::Migrator;
use std::{path::Path, sync::Arc};

use crate::{controllers, initializers, models};

pub struct App;

#[async_trait]
impl Hooks for App {
    fn app_name() -> &'static str {
        env!("CARGO_CRATE_NAME")
    }

    fn app_version() -> String {
        format!(
            "{} ({})",
            env!("CARGO_PKG_VERSION"),
            option_env!("BUILD_SHA")
                .or(option_env!("GITHUB_SHA"))
                .unwrap_or("dev")
        )
    }

    async fn boot(
        mode: StartMode,
        environment: &Environment,
        config: Config,
    ) -> Result<BootResult> {
        let mut ctx = create_context::<Self>(environment, config).await?;
        let driver = if matches!(environment, Environment::Test) {
            storage::drivers::mem::new()
        } else {
            let root = artifact_root(&ctx.config)?;
            std::fs::create_dir_all(&root).map_err(|error| {
                Error::Message(format!("cannot create artifact root {root}: {error}"))
            })?;
            storage::drivers::local::new_with_prefix(&root)?
        };
        ctx.storage = Arc::new(Storage::single(driver));
        db::converge::<Self, Migrator>(&ctx, &ctx.config.database).await?;
        if let (Some(queue), Some(config)) = (&ctx.queue_provider, &ctx.config.queue) {
            bgworker::converge(queue, config).await?;
        }
        run_app::<Self>(&mode, ctx).await
    }

    async fn initializers(_ctx: &AppContext) -> Result<Vec<Box<dyn Initializer>>> {
        Ok(vec![
            Box::new(initializers::run_ingest::RunIngestInitializer),
            Box::new(initializers::tuf_catalog::TufCatalogInitializer),
            // inject-above (do not remove)
        ])
    }

    fn routes(ctx: &AppContext) -> AppRoutes {
        let routes = AppRoutes::with_default_routes();
        if ingest_routes_enabled(&ctx.environment) {
            routes.add_route(controllers::run_sessions::routes())
        } else {
            routes
        }
    }

    async fn connect_workers(_ctx: &AppContext, _queue: &Queue) -> Result<()> {
        Ok(())
    }

    fn register_tasks(_tasks: &mut Tasks) {
        // tasks-inject (do not remove)
    }

    async fn truncate(ctx: &AppContext) -> Result<()> {
        loco_rs::db::truncate_table(&ctx.db, models::run_sessions::Entity).await?;
        loco_rs::db::truncate_table(&ctx.db, models::level_revision_charts::Entity).await?;
        loco_rs::db::truncate_table(&ctx.db, models::level_revisions::Entity).await?;
        Ok(())
    }

    async fn seed(_ctx: &AppContext, _base: &Path) -> Result<()> {
        Ok(())
    }
}

fn artifact_root(config: &Config) -> Result<String> {
    config
        .settings
        .as_ref()
        .and_then(|settings| settings.get("auto_submission"))
        .and_then(|settings| settings.get("artifact_root"))
        .and_then(serde_json::Value::as_str)
        .filter(|root| !root.trim().is_empty())
        .map(str::to_owned)
        .ok_or_else(|| {
            Error::Message("settings.auto_submission.artifact_root is required".to_owned())
        })
}

fn ingest_routes_enabled(environment: &Environment) -> bool {
    matches!(environment, Environment::Development | Environment::Test)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn unauthenticated_ingest_routes_are_not_enabled_in_production() {
        assert!(ingest_routes_enabled(&Environment::Development));
        assert!(ingest_routes_enabled(&Environment::Test));
        assert!(!ingest_routes_enabled(&Environment::Production));
    }
}
