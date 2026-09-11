use loco_rs::prelude::*;

pub struct CleanupEvidence;
#[async_trait]
impl Task for CleanupEvidence {
    fn task(&self) -> TaskInfo {
        TaskInfo {
            name: "cleanup_evidence".into(),
            detail: "Expire and delete unsubmitted evidence".into(),
        }
    }
    async fn run(&self, ctx: &AppContext, _vars: &task::Vars) -> Result<()> {
        crate::services::evidence::cleanup::cleanup(ctx).await
    }
}
