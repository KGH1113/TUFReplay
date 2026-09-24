use sea_orm_migration::prelude::*;

#[derive(DeriveMigrationName)]
pub struct Migration;
#[async_trait::async_trait]
impl MigrationTrait for Migration {
    async fn up(&self, m: &SchemaManager) -> Result<(), DbErr> {
        m.get_connection().execute_unprepared(
            "CREATE TABLE visual_assets (
                sha256 TEXT PRIMARY KEY CHECK (sha256 ~ '^[0-9a-f]{64}$'),
                bytes BIGINT NOT NULL CHECK (bytes > 0 AND bytes <= 50331648),
                media_type TEXT NOT NULL,
                storage_key TEXT NOT NULL UNIQUE,
                public_license TEXT NULL CHECK (public_license IS NULL OR length(trim(public_license)) > 0),
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            CREATE TABLE visual_asset_owners (
                owner_id TEXT NOT NULL,
                sha256 TEXT NOT NULL REFERENCES visual_assets(sha256),
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                PRIMARY KEY (owner_id, sha256)
            );"
        ).await.map(|_| ())
    }
    async fn down(&self, m: &SchemaManager) -> Result<(), DbErr> {
        m.get_connection()
            .execute_unprepared("DROP TABLE visual_asset_owners; DROP TABLE visual_assets;")
            .await
            .map(|_| ())
    }
}
