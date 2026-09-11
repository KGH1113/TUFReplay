use loco_rs::schema::*;
use sea_orm_migration::prelude::*;

#[derive(DeriveMigrationName)]
pub struct Migration;

#[async_trait::async_trait]
impl MigrationTrait for Migration {
    async fn up(&self, m: &SchemaManager) -> Result<(), DbErr> {
        create_table(
            m,
            "run_submission_records",
            &[
                ("id", ColType::PkAuto),
                ("run_session_id", ColType::BigInteger),
                ("owner_id", ColType::String),
                ("state", ColType::String),
                ("manifest", ColType::JsonBinaryNull),
                ("validation", ColType::JsonBinaryNull),
                ("external_pass_id", ColType::BigIntegerNull),
                ("reason", ColType::StringNull),
                ("requested_at", ColType::TimestampWithTimeZoneNull),
                ("lease_until", ColType::TimestampWithTimeZoneNull),
                ("lease_owner", ColType::UuidNull),
            ],
            &[],
        )
        .await?;
        m.create_index(
            Index::create()
                .name("idx-submission-run")
                .table(Alias::new("run_submission_records"))
                .col(Alias::new("run_session_id"))
                .unique()
                .to_owned(),
        )
        .await?;
        m.create_index(
            Index::create()
                .name("idx-submission-owner")
                .table(Alias::new("run_submission_records"))
                .col(Alias::new("owner_id"))
                .col(Alias::new("id"))
                .to_owned(),
        )
        .await?;
        m.create_foreign_key(
            ForeignKey::create()
                .name("fk-submission-run")
                .from(
                    Alias::new("run_submission_records"),
                    Alias::new("run_session_id"),
                )
                .to(Alias::new("run_sessions"), Alias::new("id"))
                .on_delete(ForeignKeyAction::Cascade)
                .to_owned(),
        )
        .await
    }

    async fn down(&self, m: &SchemaManager) -> Result<(), DbErr> {
        drop_table(m, "run_submission_records").await
    }
}
