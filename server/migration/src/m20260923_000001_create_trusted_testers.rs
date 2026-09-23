use sea_orm_migration::prelude::*;

#[derive(DeriveMigrationName)]
pub struct Migration;

#[async_trait::async_trait]
impl MigrationTrait for Migration {
    async fn up(&self, manager: &SchemaManager) -> Result<(), DbErr> {
        manager.get_connection().execute_unprepared(
            "CREATE TABLE trusted_testers (
                user_id UUID PRIMARY KEY,
                label TEXT NOT NULL,
                active BOOLEAN NOT NULL,
                updated_at TIMESTAMPTZ NOT NULL,
                updated_by TEXT NOT NULL
             );
             CREATE TABLE trusted_tester_events (
                id BIGSERIAL PRIMARY KEY,
                user_id UUID NOT NULL REFERENCES trusted_testers(user_id),
                action TEXT NOT NULL CHECK (action IN ('grant', 'revoke')),
                actor TEXT NOT NULL,
                reason TEXT NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
             );
             CREATE INDEX trusted_tester_events_user_id_id ON trusted_tester_events(user_id, id DESC);"
        ).await.map(|_| ())
    }

    async fn down(&self, _manager: &SchemaManager) -> Result<(), DbErr> {
        Err(DbErr::Custom(
            "trusted tester membership and audit history must be retained".into(),
        ))
    }
}
