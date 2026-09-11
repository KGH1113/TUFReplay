pub use super::_entities::run_submission_records::{ActiveModel, Column, Entity, Model};
use sea_orm::entity::prelude::*;
pub type RunSubmissionRecords = Entity;

#[async_trait::async_trait]
impl ActiveModelBehavior for ActiveModel {
    async fn before_save<C>(self, _db: &C, insert: bool) -> std::result::Result<Self, DbErr>
    where
        C: ConnectionTrait,
    {
        if !insert && self.updated_at.is_unchanged() {
            let mut this = self;
            this.updated_at = sea_orm::ActiveValue::Set(chrono::Utc::now().into());
            Ok(this)
        } else {
            Ok(self)
        }
    }
}

pub mod ingest_release;
pub mod queries;
pub mod retention;
pub mod retry;
mod transitions;
pub(crate) use transitions::sql;
pub mod recovery;
pub use transitions::Transition;
