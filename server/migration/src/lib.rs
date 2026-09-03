#![allow(elided_lifetimes_in_paths)]
#![allow(clippy::wildcard_imports)]

pub use sea_orm_migration::prelude::*;

mod m20260903_194924_level_revisions;
mod m20260903_195018_level_revision_charts;
mod m20260903_195205_run_sessions;
pub struct Migrator;

#[async_trait::async_trait]
impl MigratorTrait for Migrator {
    fn migrations() -> Vec<Box<dyn MigrationTrait>> {
        vec![
            Box::new(m20260903_194924_level_revisions::Migration),
            Box::new(m20260903_195018_level_revision_charts::Migration),
            Box::new(m20260903_195205_run_sessions::Migration),
            // inject-above (do not remove this comment)
        ]
    }
}
