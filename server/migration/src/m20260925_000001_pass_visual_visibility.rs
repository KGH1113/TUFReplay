use sea_orm_migration::prelude::*;

#[derive(DeriveMigrationName)]
pub struct Migration;

#[async_trait::async_trait]
impl MigrationTrait for Migration {
    async fn up(&self, m: &SchemaManager) -> Result<(), DbErr> {
        m.get_connection()
            .execute_unprepared("ALTER TABLE visual_presets ADD COLUMN hidden_at TIMESTAMPTZ NULL;")
            .await
            .map(|_| ())
    }
    async fn down(&self, m: &SchemaManager) -> Result<(), DbErr> {
        m.get_connection()
            .execute_unprepared("ALTER TABLE visual_presets DROP COLUMN hidden_at;")
            .await
            .map(|_| ())
    }
}
