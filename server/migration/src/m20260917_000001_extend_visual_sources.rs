use sea_orm_migration::prelude::*;

#[derive(DeriveMigrationName)]
pub struct Migration;

#[async_trait::async_trait]
impl MigrationTrait for Migration {
    async fn up(&self, m: &SchemaManager) -> Result<(), DbErr> {
        m.get_connection().execute_unprepared(
            "ALTER TABLE visual_presets DROP CONSTRAINT visual_presets_source_check;
             -- Older local E2E builds persisted the UI identifier before the
             -- server standardized it as jipper-resourcepack. Normalize that
             -- snapshot data and its integrity metadata atomically. Preset IDs
             -- and frozen run selections are preserved.
             WITH normalized AS (
               SELECT id, convert_to(jsonb_set(convert_from(bundle, 'UTF8')::jsonb,
                 '{source}', '\"jipper-resourcepack\"'::jsonb)::text, 'UTF8') AS data
                 FROM visual_presets WHERE source = 'jipper'
             )
             UPDATE visual_presets AS p SET source = 'jipper-resourcepack',
               bundle = n.data, sha256 = encode(sha256(n.data), 'hex'), bytes = octet_length(n.data)
               FROM normalized n WHERE p.id = n.id;
             ALTER TABLE visual_presets ADD CONSTRAINT visual_presets_source_check
               CHECK (source IN ('jipper-resourcepack','dmnote','impl-dmnote','jipper-keyviewer','impl-resourcepack'));
             ALTER TABLE visual_presets ADD CONSTRAINT visual_presets_extended_source_kind_check
               CHECK ((source NOT IN ('impl-dmnote','jipper-keyviewer') OR kind = 'keyviewer')
                  AND (source <> 'impl-resourcepack' OR kind = 'overlay'));"
        ).await.map(|_| ())
    }

    async fn down(&self, m: &SchemaManager) -> Result<(), DbErr> {
        // Refuse rollback while new-source presets exist; never delete user snapshots.
        m.get_connection()
            .execute_unprepared(
                "ALTER TABLE visual_presets DROP CONSTRAINT visual_presets_source_check;
             ALTER TABLE visual_presets ADD CONSTRAINT visual_presets_source_check
               CHECK (source IN ('jipper-resourcepack','dmnote'));
             ALTER TABLE visual_presets DROP CONSTRAINT visual_presets_extended_source_kind_check;",
            )
            .await
            .map(|_| ())
    }
}
