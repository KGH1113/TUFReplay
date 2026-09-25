#![allow(elided_lifetimes_in_paths)]
#![allow(clippy::wildcard_imports)]

pub use sea_orm_migration::prelude::*;

mod m20260903_194924_level_revisions;
mod m20260903_195018_level_revision_charts;
mod m20260903_195205_run_sessions;
mod m20260907_011819_create_run_submission_records;
mod m20260907_171620_allow_unpinned_run_sessions;
mod m20260908_063527_add_ingest_released_at_to_run_submission_records;
mod m20260909_002318_keep_unsubmitted_evidence;
mod m20260916_000001_create_visual_presets;
mod m20260917_000001_extend_visual_sources;
mod m20260923_000001_create_trusted_testers;
mod m20260924_000001_visual_asset_objects;
mod m20260925_000001_pass_visual_visibility;
pub struct Migrator;

#[async_trait::async_trait]
impl MigratorTrait for Migrator {
    fn migrations() -> Vec<Box<dyn MigrationTrait>> {
        vec![
            Box::new(m20260903_194924_level_revisions::Migration),
            Box::new(m20260903_195018_level_revision_charts::Migration),
            Box::new(m20260903_195205_run_sessions::Migration),
            Box::new(m20260907_011819_create_run_submission_records::Migration),
            Box::new(m20260907_171620_allow_unpinned_run_sessions::Migration),
            Box::new(m20260908_063527_add_ingest_released_at_to_run_submission_records::Migration),
            Box::new(m20260909_002318_keep_unsubmitted_evidence::Migration),
            Box::new(m20260916_000001_create_visual_presets::Migration),
            Box::new(m20260917_000001_extend_visual_sources::Migration),
            Box::new(m20260923_000001_create_trusted_testers::Migration),
            Box::new(m20260924_000001_visual_asset_objects::Migration),
            Box::new(m20260925_000001_pass_visual_visibility::Migration),
            // inject-above (do not remove this comment)
        ]
    }
}
