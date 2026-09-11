use async_trait::async_trait;
use loco_rs::prelude::BackgroundWorker;
use loco_rs::{
    app::{AppContext, Hooks, Initializer},
    bgworker::Queue,
    boot::{create_app, BootResult, StartMode},
    config::Config,
    controller::AppRoutes,
    environment::Environment,
    storage::{self, Storage},
    task::Tasks,
    Error, Result,
};
use migration::Migrator;
use std::path::Path;

use crate::controllers;
use crate::initializers;
use crate::models;
use crate::tasks;

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
        create_app::<Self, Migrator>(mode, environment, config).await
    }

    async fn after_context(ctx: AppContext) -> Result<AppContext> {
        if ctx.environment.to_string() == "e2e" {
            #[cfg(not(feature = "e2e"))]
            return Err(Error::Message("E2E requires the e2e Cargo feature".into()));
            #[cfg(feature = "e2e")]
            crate::e2e::check_context(&ctx)?;
        }
        let settings = crate::settings::Settings::parse(&ctx.config)?;
        ctx.shared_store.insert(settings.clone());
        if let Ok(tokens) = crate::services::auth::internal::InternalTokens::from_env() {
            ctx.shared_store.insert(tokens);
        }
        let driver = if matches!(ctx.environment, Environment::Test) {
            storage::drivers::mem::new()
        } else {
            let root = settings.auto_submission.catalog.artifact_root;
            std::fs::create_dir_all(&root).map_err(|error| {
                Error::Message(format!("cannot create artifact root {root}: {error}"))
            })?;
            storage::drivers::local::new_with_prefix(&root)?
        };
        Ok(ctx
            .into_builder()
            .storage(Storage::single(driver).into())
            .build())
    }

    async fn initializers(_ctx: &AppContext) -> Result<Vec<Box<dyn Initializer>>> {
        Ok(vec![
            Box::new(initializers::run_ingest::RunIngestInitializer),
            Box::new(initializers::tuf_catalog::TufCatalogInitializer),
            Box::new(initializers::submission::SubmissionInitializer),
            // inject-above (do not remove)
        ])
    }

    fn routes(ctx: &AppContext) -> AppRoutes {
        let routes = AppRoutes::with_default_routes().add_route(controllers::replays::routes());
        if ingest_routes_enabled(&ctx.environment)
            || ctx
                .shared_store
                .get::<crate::services::auth::internal::InternalTokens>()
                .is_some()
        {
            routes
                .add_route(controllers::run_sessions::routes())
                .add_route(controllers::level_changes::routes())
                .add_route(controllers::level_changes::internal_routes())
        } else {
            routes
        }
    }

    async fn connect_workers(ctx: &AppContext, queue: &Queue) -> Result<()> {
        queue
            .register(crate::workers::submission::Worker::build(ctx))
            .await?;
        queue
            .register(crate::workers::evidence_persistence::Worker::build(ctx))
            .await?;
        Ok(())
    }

    fn register_tasks(tasks: &mut Tasks) {
        tasks.register(tasks::reconcile_submissions::ReconcileSubmissions);
        tasks.register(tasks::cleanup_evidence::CleanupEvidence);
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
