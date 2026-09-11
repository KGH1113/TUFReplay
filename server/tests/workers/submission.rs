use chrono::{Duration, Utc};
use loco_rs::prelude::BackgroundWorker;
use loco_rs::testing::prelude::*;
use serial_test::serial;
use std::sync::{
    atomic::{AtomicUsize, Ordering},
    Arc,
};
use tuf_replay_server::app::App;
use tuf_replay_server::domain::*;
use tuf_replay_server::models::run_sessions::Model;
use tuf_replay_server::models::run_sessions::NewRunSession;
use tuf_replay_server::models::run_submission_records::Entity as Records;
use tuf_replay_server::services::submission::SubmissionRuntime;
use tuf_replay_server::workers::submission::{Worker, WorkerArgs};
use uuid::Uuid;

#[tokio::test]
#[serial]
async fn concurrent_submit_recovers_committed_receipt_without_registering_again() {
    let boot = boot_test::<App>().await.unwrap();
    let ctx = &boot.app_context;
    let (revision, chart) = crate::requests::run_sessions::canonical_level(&ctx.db).await;
    let id = Uuid::new_v4();
    let owner = Uuid::new_v4().to_string();
    let run = Model::create(
        &ctx.db,
        NewRunSession {
            pid: id,
            protocol_version: 1,
            client_game_version: "test".into(),
            client_mod_version: "test".into(),
            tuf_level_id: 42,
            level_revision_id: Some(revision),
            level_revision_chart_id: Some(chart),
            client_tuf_file_id: "fixture".into(),
            client_level_relative_path: "level.adofai".into(),
            upload_token_hash: vec![1; 32],
            lease_expires_at: (Utc::now() + Duration::seconds(45)).fixed_offset(),
            hard_expires_at: (Utc::now() + Duration::hours(1)).fixed_offset(),
        },
    )
    .await
    .unwrap();
    crate::support::authenticate(ctx, &owner);
    Records::create_authorized(&ctx.db, run.id, &owner, Some(crate::support::GRANT))
        .await
        .unwrap();
    let lease = Uuid::new_v4();
    assert!(Records::lease(&ctx.db, run.id, lease).await.unwrap());
    // Synthetic fixture only: production always obtains this manifest from persisted streams.
    let manifest = EvidenceManifest {
        protocol_version: 1,
        final_sequence: 2,
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
    let validator = Arc::new(TestValidator(AtomicUsize::new(0)));
    let runtime = SubmissionRuntime {
        validator: validator.clone(),
        charts: Arc::new(TestCharts),
        registrar: Arc::new(CommittedReceipt),
        evidence_slots: tokio::sync::Semaphore::new(1),
    };
    ctx.shared_store.insert(Arc::new(runtime));
    let worker = Worker::build(ctx);
    worker.perform(WorkerArgs { run_id: id }).await.unwrap();
    assert_eq!(
        validator.0.load(Ordering::SeqCst),
        0,
        "clear alone never requests validation"
    );
    Records::request(&ctx.db, run.id, &owner).await.unwrap();
    Records::request(&ctx.db, run.id, &owner).await.unwrap();
    let (first, second) = tokio::join!(
        worker.perform(WorkerArgs { run_id: id }),
        worker.perform(WorkerArgs { run_id: id })
    );
    first.unwrap();
    second.unwrap();
    assert_eq!(validator.0.load(Ordering::SeqCst), 1);
    let record = Records::record(&ctx.db, run.id).await.unwrap();
    assert_eq!(record.state, "submitted");
    assert_eq!(record.external_pass_id, Some(777));
    assert!(!Records::delete(&ctx.db, run.id, &owner).await.unwrap());
    Records::request(&ctx.db, run.id, &owner).await.unwrap();
    worker.perform(WorkerArgs { run_id: id }).await.unwrap();
    assert_eq!(validator.0.load(Ordering::SeqCst), 1);
}

struct TestValidator(AtomicUsize);
#[async_trait::async_trait]
impl GameplayValidator for TestValidator {
    async fn validate(
        &self,
        chart: &OfficialChart,
        evidence: &EvidenceManifest,
    ) -> Result<ValidationOutcome, String> {
        self.0.fetch_add(1, Ordering::SeqCst);
        Ok(ValidationOutcome::Accepted(Box::new(ValidatedResult {
            official_file_id: chart.file_id.clone(),
            chart_sha256: chart.sha256.clone(),
            gameplay_hash_version: chart.gameplay_hash_version,
            gameplay_hash: chart.gameplay_hash.clone(),
            evidence_digest: evidence.digest.clone(),
            validator_version: "test-only".into(),
            rules_version: "test-only".into(),
            speed: 1.0,
            judgments: [0, 0, 0, 0, 1, 0, 0, 0, 0],
            key_count: 1,
            is_no_hold_tap: false,
            is_adofai_v2: true,
        })))
    }
}

struct TestCharts;
#[async_trait::async_trait]
impl OfficialChartProvider for TestCharts {
    async fn acquire(&self, _: i64, _: &str) -> Result<OfficialChart, String> {
        Ok(OfficialChart {
            file_id: "test-file".into(),
            sha256: "b".repeat(64),
            gameplay_hash_version: 1,
            gameplay_hash: "c".repeat(64),
            bytes: vec![],
        })
    }
}

struct CommittedReceipt;
#[async_trait::async_trait]
impl PassRegistrar for CommittedReceipt {
    async fn lookup(&self, _: Uuid, _: &str, _: &str) -> Result<Option<i64>, String> {
        Ok(Some(777))
    }
    async fn register(
        &self,
        _: Uuid,
        _: &str,
        _: Uuid,
        _: i64,
        _: &str,
        _: &ValidatedResult,
    ) -> Result<i64, String> {
        panic!("an existing receipt must not cause another pass registration")
    }
}
