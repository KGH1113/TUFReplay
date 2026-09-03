use loco_rs::schema::*;
use sea_orm_migration::prelude::*;

#[derive(DeriveMigrationName)]
pub struct Migration;

#[async_trait::async_trait]
impl MigrationTrait for Migration {
    async fn up(&self, m: &SchemaManager) -> Result<(), DbErr> {
        create_table(
            m,
            "run_sessions",
            &[
                ("id", ColType::PkAuto),
                ("pid", ColType::UuidUniq),
                ("status", ColType::String),
                ("protocol_version", ColType::BigInteger),
                ("client_game_version", ColType::String),
                ("client_mod_version", ColType::String),
                ("tuf_level_id", ColType::BigInteger),
                ("upload_token_hash", ColType::BinaryLen(32)),
                ("lease_expires_at", ColType::TimestampWithTimeZone),
                ("hard_expires_at", ColType::TimestampWithTimeZone),
                ("sealed_at", ColType::TimestampWithTimeZoneNull),
                ("level_revision_id", ColType::BigInteger),
                ("level_revision_chart_id", ColType::BigInteger),
            ],
            &[],
        )
        .await?;
        m.create_foreign_key(
            ForeignKey::create()
                .name("fk-run-sessions-level-revision")
                .from(Alias::new("run_sessions"), Alias::new("level_revision_id"))
                .to(Alias::new("level_revisions"), Alias::new("id"))
                .on_update(ForeignKeyAction::Cascade)
                .on_delete(ForeignKeyAction::Restrict)
                .to_owned(),
        )
        .await?;
        m.create_foreign_key(
            ForeignKey::create()
                .name("fk-run-sessions-level-revision-chart")
                .from(
                    Alias::new("run_sessions"),
                    Alias::new("level_revision_chart_id"),
                )
                .to(Alias::new("level_revision_charts"), Alias::new("id"))
                .on_update(ForeignKeyAction::Cascade)
                .on_delete(ForeignKeyAction::Restrict)
                .to_owned(),
        )
        .await
    }

    async fn down(&self, m: &SchemaManager) -> Result<(), DbErr> {
        drop_table(m, "run_sessions").await
    }
}
