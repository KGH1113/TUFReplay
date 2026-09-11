use crate::models::run_sessions::Model;
use crate::services::ingest::IngestError;
use crate::services::ingest::RunIngestStore;
use crate::services::ingest::SealOutcome;
use crate::workers::evidence_persistence::Worker;
use crate::workers::evidence_persistence::WorkerArgs;
use loco_rs::prelude::*;
use uuid::Uuid;

/// Coordinates the durable run lifecycle; socket framing belongs to the controller.
pub(crate) struct RunStream<'a> {
    pub ctx: &'a AppContext,
    pub store: &'a RunIngestStore,
    pub run_id: Uuid,
    pub token_hash: &'a str,
}

#[derive(Debug)]
pub(crate) enum StreamError {
    Ingest(IngestError),
    Lifecycle,
    Authorization,
    AuthorizationUnavailable,
}

impl From<IngestError> for StreamError {
    fn from(value: IngestError) -> Self {
        Self::Ingest(value)
    }
}

impl RunStream<'_> {
    pub async fn authorize(&self) -> std::result::Result<(), StreamError> {
        self.store.authorize(self.run_id, self.token_hash).await?;
        let run = Model::find_by_pid(&self.ctx.db, self.run_id)
            .await
            .map_err(|_| StreamError::Lifecycle)?;
        let record = crate::models::run_submission_records::Entity::record(&self.ctx.db, run.id)
            .await
            .map_err(|_| StreamError::Authorization)?;
        crate::services::auth::authorize_grant(self.ctx, &record.owner_id, record.oauth_grant_id)
            .await
            .map_err(|error| match error {
                Error::Unauthorized(_) => StreamError::Authorization,
                _ => StreamError::AuthorizationUnavailable,
            })
    }

    pub async fn open(&self) -> std::result::Result<i64, StreamError> {
        self.authorize().await?;
        let sequence = self.store.open(self.run_id, self.token_hash).await?;
        let run = Model::find_by_pid(&self.ctx.db, self.run_id)
            .await
            .map_err(|_| StreamError::Lifecycle)?;
        run.mark_streaming(&self.ctx.db)
            .await
            .map_err(|_| StreamError::Lifecycle)?;
        Ok(sequence)
    }

    pub async fn seal(
        &self,
        sequence: i64,
        inputs: u64,
        hits: u64,
    ) -> std::result::Result<SealOutcome, StreamError> {
        self.authorize().await?;
        let receipt = self
            .store
            .seal(self.run_id, self.token_hash, sequence, inputs, hits)
            .await?;
        let run = Model::find_by_pid(&self.ctx.db, self.run_id)
            .await
            .map_err(|_| StreamError::Lifecycle)?;
        run.mark_sealed(&self.ctx.db)
            .await
            .map_err(|_| StreamError::Lifecycle)?;
        if let Err(error) = Worker::perform_later(
            self.ctx,
            WorkerArgs {
                run_id: self.run_id,
            },
        )
        .await
        {
            tracing::warn!(run_id=%self.run_id, %error, "persistence dispatch deferred to reconciliation");
        }
        Ok(receipt)
    }

    pub async fn fail(&self) -> std::result::Result<(), StreamError> {
        self.store.fail(self.run_id, self.token_hash).await?;
        let run = Model::find_by_pid(&self.ctx.db, self.run_id)
            .await
            .map_err(|_| StreamError::Lifecycle)?;
        run.mark_failed(&self.ctx.db)
            .await
            .map_err(|_| StreamError::Lifecycle)?;
        Ok(())
    }
}
