pub use super::_entities::level_revisions::{ActiveModel, Column, Entity, Model};
use loco_rs::prelude::*;
pub type LevelRevisions = Entity;

#[async_trait::async_trait]
impl ActiveModelBehavior for ActiveModel {
    async fn before_save<C>(self, _db: &C, insert: bool) -> std::result::Result<Self, DbErr>
    where
        C: ConnectionTrait,
    {
        if insert {
            Ok(self)
        } else {
            Err(DbErr::Custom("level revisions are immutable".to_owned()))
        }
    }
}
