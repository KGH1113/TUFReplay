use loco_rs::prelude::*;
use sea_orm::{ActiveModelTrait, ActiveValue::Set, ColumnTrait, EntityTrait, QueryFilter};
use unicode_normalization::UnicodeNormalization;

pub use super::_entities::level_revision_charts::{ActiveModel, Column, Entity, Model};
pub type LevelRevisionCharts = Entity;

#[async_trait::async_trait]
impl ActiveModelBehavior for ActiveModel {
    async fn before_save<C>(self, _db: &C, insert: bool) -> std::result::Result<Self, DbErr>
    where
        C: ConnectionTrait,
    {
        if insert {
            Ok(self)
        } else {
            Err(DbErr::Custom(
                "level revision charts are immutable".to_owned(),
            ))
        }
    }
}

#[derive(Debug)]
pub struct NewLevelRevisionChart {
    pub level_revision_id: i64,
    pub relative_path: String,
    pub canonical_chart_hash: Vec<u8>,
    pub artifact_storage_key: String,
}

impl Model {
    pub async fn create(
        db: &DatabaseConnection,
        params: NewLevelRevisionChart,
    ) -> ModelResult<Self> {
        let relative_path = normalize_relative_chart_path(&params.relative_path)?;
        if params.canonical_chart_hash.len() != 32 || params.artifact_storage_key.is_empty() {
            return Err(ModelError::Message(
                "chart hash must be 32 bytes and storage key must not be empty".to_owned(),
            ));
        }
        Ok(ActiveModel {
            level_revision_id: Set(params.level_revision_id),
            relative_path: Set(relative_path),
            canonical_chart_hash: Set(params.canonical_chart_hash),
            chart_hash_version: Set(1),
            artifact_storage_key: Set(params.artifact_storage_key),
            ..Default::default()
        }
        .insert(db)
        .await?)
    }

    pub async fn find_for_revision(
        db: &DatabaseConnection,
        level_revision_id: i64,
        relative_path: &str,
    ) -> ModelResult<Self> {
        let relative_path = normalize_relative_chart_path(relative_path)?;
        Entity::find()
            .filter(Column::LevelRevisionId.eq(level_revision_id))
            .filter(Column::RelativePath.eq(relative_path))
            .one(db)
            .await?
            .ok_or(ModelError::EntityNotFound)
    }
}

pub fn normalize_relative_chart_path(path: &str) -> ModelResult<String> {
    let normalized: String = path.replace('\\', "/").nfc().collect();
    if normalized.is_empty()
        || normalized.starts_with('/')
        || normalized.contains('\0')
        || normalized
            .split('/')
            .any(|part| part.is_empty() || part == "." || part == "..")
        || !normalized.to_ascii_lowercase().ends_with(".adofai")
    {
        return Err(ModelError::Message(
            "client_level_relative_path must be a safe relative .adofai path".to_owned(),
        ));
    }
    Ok(normalized)
}
