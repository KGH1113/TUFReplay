use loco_rs::prelude::*;

pub struct ReconcileSubmissions;
#[async_trait]
impl Task for ReconcileSubmissions {
    fn task(&self) -> TaskInfo {
        TaskInfo {
            name: "reconcile_submissions".into(),
            detail: "Enqueue durable work missed by interrupted requests".into(),
        }
    }
    async fn run(&self, ctx: &AppContext, _vars: &task::Vars) -> Result<()> {
        crate::services::submission::reconciliation::reconcile(ctx).await
    }
}
