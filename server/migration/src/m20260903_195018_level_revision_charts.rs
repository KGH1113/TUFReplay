use loco_rs::schema::*;
use sea_orm_migration::prelude::*;

#[derive(DeriveMigrationName)]
pub struct Migration;

#[async_trait::async_trait]
impl MigrationTrait for Migration {
    async fn up(&self, m: &SchemaManager) -> Result<(), DbErr> {
        create_table(
            m,
            "level_revision_charts",
            &[
                ("id", ColType::PkAuto),
                ("relative_path", ColType::String),
                ("canonical_chart_hash", ColType::BinaryLen(32)),
                ("chart_hash_version", ColType::BigInteger),
                ("artifact_storage_key", ColType::String),
                ("level_revision_id", ColType::BigInteger),
            ],
            &[],
        )
        .await?;
        m.create_foreign_key(
            ForeignKey::create()
                .name("fk-level-revision-charts-revision")
                .from(
                    Alias::new("level_revision_charts"),
                    Alias::new("level_revision_id"),
                )
                .to(Alias::new("level_revisions"), Alias::new("id"))
                .on_update(ForeignKeyAction::Cascade)
                .on_delete(ForeignKeyAction::Restrict)
                .to_owned(),
        )
        .await?;
        m.create_index(
            Index::create()
                .name("idx-level-revision-charts-revision-path")
                .table(Alias::new("level_revision_charts"))
                .col(Alias::new("level_revision_id"))
                .col(Alias::new("relative_path"))
                .unique()
                .to_owned(),
        )
        .await
    }

    async fn down(&self, m: &SchemaManager) -> Result<(), DbErr> {
        drop_table(m, "level_revision_charts").await
    }
}
