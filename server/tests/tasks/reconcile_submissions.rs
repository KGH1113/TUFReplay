use chrono::{Duration, Utc};
use loco_rs::{app::Hooks, config::WorkerMode, prelude::*};
use serial_test::serial;
use tuf_replay_server::{
    app::App,
    domain::EvidenceManifest,
    models::{
        run_sessions::{Model, NewRunSession},
        run_submission_records::Entity as Records,
    },
    tasks::reconcile_submissions::ReconcileSubmissions,
};
use uuid::Uuid;

#[tokio::test]
#[serial]
async fn reconciliation_enqueues_durable_work_and_worker_processes_it() {
    let boot = boot_test::<App>().await.unwrap();
    let mut ctx = boot.app_context;
    ctx.config.workers.mode = WorkerMode::BackgroundQueue;
    let queue = loco_rs::bgworker::create_queue_provider(&ctx.config)
        .await
        .unwrap()
        .expect("Postgres queue");
    queue.setup().await.unwrap();
    ctx = ctx.into_builder().queue_provider(queue).build();
    let owner = Uuid::new_v4().to_string();
    crate::support::authenticate(&ctx, &owner);
    let run = Model::create(
        &ctx.db,
        NewRunSession {
            pid: Uuid::new_v4(),
            protocol_version: 1,
            client_game_version: "test".into(),
            client_mod_version: "test".into(),
            tuf_level_id: 42,
            level_revision_id: None,
            level_revision_chart_id: None,
            client_tuf_file_id: "test-file".into(),
            client_level_relative_path: "main.adofai".into(),
            upload_token_hash: vec![1; 32],
            lease_expires_at: (Utc::now() + Duration::seconds(45)).fixed_offset(),
            hard_expires_at: (Utc::now() + Duration::hours(1)).fixed_offset(),
        },
    )
    .await
    .unwrap();
    Records::create_authorized(&ctx.db, run.id, &owner, Some(crate::support::GRANT))
        .await
        .unwrap();
    let lease = Uuid::new_v4();
    assert!(Records::lease(&ctx.db, run.id, lease).await.unwrap());
    let manifest = EvidenceManifest {
        protocol_version: 1,
        final_sequence: 0,
        digest: "a".repeat(64),
        streams: vec![],
    };
    Records::publish(
        &ctx.db,
        run.id,
        lease,
        serde_json::to_value(manifest).unwrap(),
    )
    .await
    .unwrap();
    Records::release(&ctx.db, run.id, lease).await.unwrap();
    Records::request(&ctx.db, run.id, &owner).await.unwrap();

    let task = ReconcileSubmissions;
    // Standalone CLI tasks do not execute HTTP/worker initializers.
    let task_ctx = loco_rs::boot::create_context::<App>(&ctx.environment, ctx.config.clone())
        .await
        .unwrap();
    assert!(task_ctx
        .shared_store
        .get::<tuf_replay_server::services::ingest::RunIngestStore>()
        .is_none());
    task.run(&task_ctx, &task::Vars::default()).await.unwrap();
    task.run(&task_ctx, &task::Vars::default()).await.unwrap();
    assert_eq!(
        Records::record(&ctx.db, run.id).await.unwrap().state,
        "validation_pending",
        "the scheduler dispatches, it does not run the validator inline"
    );

    let queue = ctx.queue_provider.clone().expect("Postgres queue");
    App::connect_workers(&ctx, &queue).await.unwrap();
    let consumer = queue.clone();
    let worker = tokio::spawn(async move { consumer.run(vec![]).await });
    let result = tokio::time::timeout(std::time::Duration::from_secs(10), async {
        loop {
            if Records::record(&ctx.db, run.id).await.unwrap().state == "validator_unavailable" {
                break;
            }
            tokio::time::sleep(std::time::Duration::from_millis(25)).await;
        }
    })
    .await;
    queue.shutdown().unwrap();
    tokio::time::timeout(std::time::Duration::from_secs(5), worker)
        .await
        .unwrap()
        .unwrap()
        .unwrap();
    result.expect("queued worker completed the requested submission");
    assert_eq!(
        Records::record(&ctx.db, run.id)
            .await
            .unwrap()
            .external_pass_id,
        None
    );
}
