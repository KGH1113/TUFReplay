//! Visual snapshots and immutable per-submission selection fixation.
//!
//! The selection backfill intentionally creates an empty fixed row for every
//! already-requested or submitted record so pre-migration retries cannot
//! acquire a presentation after the migration.

use sea_orm_migration::prelude::*;

#[derive(DeriveMigrationName)]
pub struct Migration;

#[async_trait::async_trait]
impl MigrationTrait for Migration {
    async fn up(&self, m: &SchemaManager) -> Result<(), DbErr> {
        m.get_connection()
            .execute_unprepared(
                "CREATE TABLE visual_presets (
                    id UUID PRIMARY KEY,
                    owner_id TEXT NOT NULL,
                    name TEXT NOT NULL,
                    kind TEXT NOT NULL CHECK (kind IN ('keyviewer','overlay')),
                    source TEXT NOT NULL CHECK (source IN ('jipper-resourcepack','dmnote')),
                    CHECK (source <> 'dmnote' OR kind = 'keyviewer'),
                    source_version TEXT NOT NULL,
                    bundle BYTEA NOT NULL,
                    sha256 TEXT NOT NULL CHECK (sha256 ~ '^[0-9a-f]{64}$'),
                    bytes BIGINT NOT NULL CHECK (
                        bytes > 0 AND bytes <= 67108864 AND bytes = octet_length(bundle)
                    ),
                    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                    deleted_at TIMESTAMPTZ NULL
                );
                CREATE UNIQUE INDEX idx_visual_presets_owner_active_name
                    ON visual_presets(owner_id, LOWER(name))
                    WHERE deleted_at IS NULL;
                CREATE INDEX idx_visual_presets_owner_active
                    ON visual_presets(owner_id, created_at DESC)
                    WHERE deleted_at IS NULL;
                CREATE TABLE run_visual_selections (
                    run_submission_record_id BIGINT PRIMARY KEY
                        REFERENCES run_submission_records(id) ON DELETE CASCADE,
                    keyviewer_id UUID NULL,
                    overlay_id UUID NULL,
                    fixed_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );
                INSERT INTO run_visual_selections
                    (run_submission_record_id, keyviewer_id, overlay_id, fixed_at)
                SELECT id, NULL, NULL, COALESCE(requested_at, updated_at)
                FROM run_submission_records
                WHERE requested_at IS NOT NULL OR state = 'submitted'
                ON CONFLICT (run_submission_record_id) DO NOTHING;",
            )
            .await
            .map(|_| ())
    }

    async fn down(&self, m: &SchemaManager) -> Result<(), DbErr> {
        m.get_connection()
            .execute_unprepared(
                "DROP TABLE run_visual_selections;
                 DROP INDEX idx_visual_presets_owner_active;
                 DROP INDEX idx_visual_presets_owner_active_name;
                 DROP TABLE visual_presets;",
            )
            .await
            .map(|_| ())
    }
}
