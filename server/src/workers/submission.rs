use crate::services::submission::{process, SubmissionRuntime};
use loco_rs::prelude::*;
use serde::{Deserialize, Serialize};

pub struct Worker {
    pub ctx: AppContext,
}

#[derive(Deserialize, Debug, Serialize)]
pub struct WorkerArgs {
    pub run_id: uuid::Uuid,
}

#[async_trait]
impl BackgroundWorker<WorkerArgs> for Worker {
    fn build(ctx: &AppContext) -> Self {
        Self { ctx: ctx.clone() }
    }
    fn class_name() -> String {
        "Submission".into()
    }
    async fn perform(&self, args: WorkerArgs) -> Result<()> {
        let runtime = self
            .ctx
            .shared_store
            .get::<std::sync::Arc<SubmissionRuntime>>()
            .ok_or_else(|| Error::Message("submission runtime unavailable".into()))?;
        process(&self.ctx, args.run_id, &runtime).await
    }
}
