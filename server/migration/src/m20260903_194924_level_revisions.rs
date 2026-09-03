use loco_rs::schema::*;
use sea_orm_migration::prelude::*;

#[derive(DeriveMigrationName)]
pub struct Migration;

#[async_trait::async_trait]
impl MigrationTrait for Migration {
    async fn up(&self, m: &SchemaManager) -> Result<(), DbErr> {
        create_table(
            m,
            "level_revisions",
            &[
                ("id", ColType::PkAuto),
                ("tuf_level_id", ColType::BigInteger),
                ("tuf_file_id", ColType::String),
                ("canonical_payload_hash", ColType::BinaryLen(32)),
                ("payload_hash_version", ColType::BigInteger),
                ("archive_storage_key", ColType::String),
                ("source_updated_at", ColType::TimestampWithTimeZoneNull),
                ("fetched_at", ColType::TimestampWithTimeZone),
            ],
            &[],
        )
        .await?;
        m.create_index(
            Index::create()
                .name("idx-level-revisions-level-file")
                .table(Alias::new("level_revisions"))
                .col(Alias::new("tuf_level_id"))
                .col(Alias::new("tuf_file_id"))
                .unique()
                .to_owned(),
        )
        .await
    }

    async fn down(&self, m: &SchemaManager) -> Result<(), DbErr> {
        drop_table(m, "level_revisions").await
    }
}
