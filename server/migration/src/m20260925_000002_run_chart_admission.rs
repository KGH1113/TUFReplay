use sea_orm_migration::prelude::*;

#[derive(DeriveMigrationName)]
pub struct Migration;

#[async_trait::async_trait]
impl MigrationTrait for Migration {
    async fn up(&self, m: &SchemaManager) -> Result<(), DbErr> {
        m.get_connection()
            .execute_unprepared("ALTER TABLE run_sessions ADD COLUMN chart_admission JSONB NULL;")
            .await
            .map(|_| ())
    }
    async fn down(&self, m: &SchemaManager) -> Result<(), DbErr> {
        m.get_connection()
            .execute_unprepared("ALTER TABLE run_sessions DROP COLUMN chart_admission;")
            .await
            .map(|_| ())
    }
}
