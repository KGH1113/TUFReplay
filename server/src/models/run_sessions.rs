use chrono::{DateTime, FixedOffset, Utc};
use loco_rs::prelude::*;
use sea_orm::{ActiveModelTrait, ActiveValue::Set, EntityTrait, IntoActiveModel, QueryFilter};
use uuid::Uuid;

pub use super::_entities::run_sessions::{ActiveModel, Column, Entity, Model};
use super::{level_revision_charts, level_revisions};
pub type RunSessions = Entity;

#[async_trait::async_trait]
impl ActiveModelBehavior for ActiveModel {
    async fn before_save<C>(self, _db: &C, insert: bool) -> std::result::Result<Self, DbErr>
    where
        C: ConnectionTrait,
    {
        if !insert && self.updated_at.is_unchanged() {
            let mut this = self;
            this.updated_at = Set(chrono::Utc::now().into());
            Ok(this)
        } else {
            Ok(self)
        }
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum RunStatus {
    Created,
    Streaming,
    Sealed,
    Failed,
    Expired,
}

impl RunStatus {
    #[must_use]
    pub const fn as_str(self) -> &'static str {
        match self {
            Self::Created => "created",
            Self::Streaming => "streaming",
            Self::Sealed => "sealed",
            Self::Failed => "failed",
            Self::Expired => "expired",
        }
    }
}

#[derive(Debug)]
pub struct NewRunSession {
    pub pid: Uuid,
    pub protocol_version: i64,
    pub client_game_version: String,
    pub client_mod_version: String,
    pub tuf_level_id: i64,
    pub level_revision_id: i64,
    pub level_revision_chart_id: i64,
    pub upload_token_hash: Vec<u8>,
    pub lease_expires_at: DateTime<FixedOffset>,
    pub hard_expires_at: DateTime<FixedOffset>,
}

impl Model {
    pub async fn create(db: &DatabaseConnection, params: NewRunSession) -> ModelResult<Self> {
        params.validate()?;
        let revision = level_revisions::Entity::find_by_id(params.level_revision_id)
            .one(db)
            .await?
            .ok_or(ModelError::EntityNotFound)?;
        let chart = level_revision_charts::Entity::find_by_id(params.level_revision_chart_id)
            .one(db)
            .await?
            .ok_or(ModelError::EntityNotFound)?;
        if revision.tuf_level_id != params.tuf_level_id || chart.level_revision_id != revision.id {
            return Err(ModelError::Message(
                "run level, revision, and chart do not belong together".to_owned(),
            ));
        }

        Ok(ActiveModel {
            pid: Set(params.pid),
            status: Set(RunStatus::Created.as_str().to_owned()),
            protocol_version: Set(params.protocol_version),
            client_game_version: Set(params.client_game_version),
            client_mod_version: Set(params.client_mod_version),
            tuf_level_id: Set(params.tuf_level_id),
            level_revision_id: Set(params.level_revision_id),
            level_revision_chart_id: Set(params.level_revision_chart_id),
            upload_token_hash: Set(params.upload_token_hash),
            lease_expires_at: Set(params.lease_expires_at),
            hard_expires_at: Set(params.hard_expires_at),
            sealed_at: Set(None),
            ..Default::default()
        }
        .insert(db)
        .await?)
    }

    pub async fn find_by_pid(db: &DatabaseConnection, pid: Uuid) -> ModelResult<Self> {
        Entity::find()
            .filter(Column::Pid.eq(pid))
            .one(db)
            .await?
            .ok_or(ModelError::EntityNotFound)
    }

    pub async fn mark_streaming(self, db: &DatabaseConnection) -> ModelResult<Self> {
        if self.status == RunStatus::Streaming.as_str() {
            return Ok(self);
        }
        if self.status != RunStatus::Created.as_str() {
            return Err(ModelError::Message(format!(
                "cannot transition run from {} to streaming",
                self.status
            )));
        }
        let mut active = self.into_active_model();
        active.status = Set(RunStatus::Streaming.as_str().to_owned());
        Ok(active.update(db).await?)
    }

    pub async fn mark_sealed(self, db: &DatabaseConnection) -> ModelResult<Self> {
        if self.status == RunStatus::Sealed.as_str() {
            return Ok(self);
        }
        if self.status != RunStatus::Streaming.as_str() {
            return Err(ModelError::Message(format!(
                "cannot transition run from {} to sealed",
                self.status
            )));
        }
        let mut active = self.into_active_model();
        active.status = Set(RunStatus::Sealed.as_str().to_owned());
        active.sealed_at = Set(Some(Utc::now().fixed_offset()));
        Ok(active.update(db).await?)
    }

    pub async fn mark_failed(self, db: &DatabaseConnection) -> ModelResult<Self> {
        if self.status == RunStatus::Failed.as_str() {
            return Ok(self);
        }
        if !matches!(self.status.as_str(), "created" | "streaming") {
            return Err(ModelError::Message(format!(
                "cannot transition run from {} to failed",
                self.status
            )));
        }
        let mut active = self.into_active_model();
        active.status = Set(RunStatus::Failed.as_str().to_owned());
        Ok(active.update(db).await?)
    }

    pub async fn delete_record(self, db: &DatabaseConnection) -> ModelResult<()> {
        Entity::delete_by_id(self.id).exec(db).await?;
        Ok(())
    }

    #[must_use]
    pub fn lease_deadline_elapsed(&self, now: DateTime<FixedOffset>) -> bool {
        matches!(self.status.as_str(), "created" | "streaming") && self.lease_expires_at <= now
    }

    #[must_use]
    pub fn hard_deadline_elapsed(&self, now: DateTime<FixedOffset>) -> bool {
        matches!(self.status.as_str(), "created" | "streaming") && self.hard_expires_at <= now
    }
}

impl NewRunSession {
    fn validate(&self) -> ModelResult<()> {
        if self.protocol_version != 1 {
            return Err(ModelError::Message(
                "unsupported run protocol version".to_owned(),
            ));
        }
        if self.client_game_version.is_empty()
            || self.client_game_version.len() > 64
            || self.client_mod_version.is_empty()
            || self.client_mod_version.len() > 64
        {
            return Err(ModelError::Message(
                "client game and mod versions must contain 1 to 64 bytes".to_owned(),
            ));
        }
        if self.tuf_level_id <= 0 || self.upload_token_hash.len() != 32 {
            return Err(ModelError::Message(
                "TUF level ID must be positive and upload token hash must be 32 bytes".to_owned(),
            ));
        }
        if self.lease_expires_at >= self.hard_expires_at {
            return Err(ModelError::Message(
                "run lease deadline must precede its hard deadline".to_owned(),
            ));
        }
        Ok(())
    }
}
