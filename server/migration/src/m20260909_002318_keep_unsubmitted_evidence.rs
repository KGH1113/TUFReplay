use sea_orm_migration::prelude::*;

#[derive(DeriveMigrationName)]
pub struct Migration;

#[async_trait::async_trait]
impl MigrationTrait for Migration {
    async fn up(&self, m: &SchemaManager) -> Result<(), DbErr> {
        m.get_connection().execute_unprepared(
            "UPDATE run_submission_records SET evidence_expires_at=NULL
            WHERE manifest IS NOT NULL AND state IN
            ('evidence_ready','validation_pending','validator_unavailable','validation_error',
             'validation_rejected','registering','registration_error','registration_rejected','submitted')"
        ).await?;
        Ok(())
    }

    async fn down(&self, _m: &SchemaManager) -> Result<(), DbErr> {
        Err(DbErr::Custom(
            "Removed evidence deadlines cannot be reconstructed safely".into(),
        ))
    }
}
