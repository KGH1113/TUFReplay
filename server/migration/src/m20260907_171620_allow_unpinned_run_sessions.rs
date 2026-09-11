use sea_orm_migration::prelude::*;

#[derive(DeriveMigrationName)]
pub struct Migration;

#[async_trait::async_trait]
impl MigrationTrait for Migration {
    async fn up(&self, m: &SchemaManager) -> Result<(), DbErr> {
        m.get_connection().execute_unprepared(
            "ALTER TABLE run_sessions ALTER COLUMN level_revision_id DROP NOT NULL,
                ALTER COLUMN level_revision_chart_id DROP NOT NULL,
                ADD COLUMN client_tuf_file_id TEXT NOT NULL DEFAULT '',
                ADD COLUMN client_level_relative_path TEXT NOT NULL DEFAULT '';
             UPDATE run_sessions r SET client_tuf_file_id=v.tuf_file_id,
                client_level_relative_path=c.relative_path FROM level_revisions v,level_revision_charts c
                WHERE r.level_revision_id=v.id AND r.level_revision_chart_id=c.id;
             ALTER TABLE run_submission_records
                ADD COLUMN oauth_grant_id UUID NULL,
                ADD COLUMN evidence_expires_at TIMESTAMPTZ NULL,
                ADD COLUMN retry_count INTEGER NOT NULL DEFAULT 0,
                ADD COLUMN next_attempt_at TIMESTAMPTZ NULL;
             UPDATE run_submission_records SET evidence_expires_at=updated_at+INTERVAL '7 days'
                WHERE manifest IS NOT NULL AND state NOT IN ('submitted','deleted');
             CREATE INDEX idx_submission_pending ON run_submission_records(state,next_attempt_at,updated_at);"
        ).await?;
        Ok(())
    }

    async fn down(&self, m: &SchemaManager) -> Result<(), DbErr> {
        // Fails safely if unpinned runs exist: an old binary cannot represent them.
        m.get_connection().execute_unprepared(
            "ALTER TABLE run_sessions ALTER COLUMN level_revision_id SET NOT NULL,
                ALTER COLUMN level_revision_chart_id SET NOT NULL;
             ALTER TABLE run_sessions DROP COLUMN client_tuf_file_id, DROP COLUMN client_level_relative_path;
             DROP INDEX idx_submission_pending;
             ALTER TABLE run_submission_records DROP COLUMN oauth_grant_id,
                DROP COLUMN evidence_expires_at,DROP COLUMN retry_count,DROP COLUMN next_attempt_at;"
        ).await?;
        Ok(())
    }
}
