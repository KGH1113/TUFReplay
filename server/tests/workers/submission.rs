use chrono::{Duration, Utc};
use loco_rs::prelude::BackgroundWorker;
use loco_rs::testing::prelude::*;
use serial_test::serial;
use sha2::{Digest, Sha256};
use std::path::Path;
use std::sync::{
    atomic::{AtomicUsize, Ordering},
    Arc, Mutex,
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

#[tokio::test]
#[serial]
async fn revoked_tester_can_recover_an_already_committed_receipt() {
    let boot = boot_test::<App>().await.unwrap();
    let ctx = &boot.app_context;
    let id = Uuid::new_v4();
    let owner = Uuid::new_v4().to_string();
    crate::support::authenticate(ctx, &owner);
    let run = Model::create(
        &ctx.db,
        NewRunSession {
            pid: id,
            protocol_version: 1,
            client_game_version: "3.4.0".into(),
            client_mod_version: "test".into(),
            tuf_level_id: 42,
            level_revision_id: None,
            level_revision_chart_id: None,
            client_tuf_file_id: "fixture".into(),
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

    let manifest = EvidenceManifest {
        protocol_version: 1,
        final_sequence: 0,
        digest: "a".repeat(64),
        streams: vec![],
    };
    let publish_lease = Uuid::new_v4();
    assert!(Records::lease(&ctx.db, run.id, publish_lease)
        .await
        .unwrap());
    Records::publish(
        &ctx.db,
        run.id,
        publish_lease,
        serde_json::to_value(&manifest).unwrap(),
    )
    .await
    .unwrap();
    Records::release(&ctx.db, run.id, publish_lease)
        .await
        .unwrap();
    Records::request_authorized(&ctx.db, run.id, &owner, Some(crate::support::GRANT))
        .await
        .unwrap();

    let result = ValidatedResult {
        validation_contract_version: 2,
        validation_status: ValidationStatus::SkippedTrustedTester,
        result_provenance: ResultProvenance::RecordedGameResult,
        validator_version: "trusted-tester-adapter-v1".into(),
        rules_version: "recorded-game-result-v1".into(),
        evidence_digest: manifest.digest,
        official_file_id: "current-file".into(),
        chart_sha256: "b".repeat(64),
        gameplay_hash_version: 1,
        gameplay_hash: "c".repeat(64),
        speed: 1.0,
        judgments: [0; 9],
        perfect_minus: 0,
        perfect_plus: 0,
        key_count: 4,
        is_no_hold_tap: false,
        is_adofai_v2: false,
        adofai_version: 2,
        is_x_perfect_mode: false,
    };
    let validation_lease = Uuid::new_v4();
    assert!(Records::lease(&ctx.db, run.id, validation_lease)
        .await
        .unwrap());
    assert!(Records::transition(
        &ctx.db,
        run.id,
        validation_lease,
        tuf_replay_server::models::run_submission_records::Transition {
            from: "validation_pending",
            to: "registering",
            reason: None,
            validation: Some(serde_json::to_value(result).unwrap()),
            pass: None,
        },
    )
    .await
    .unwrap());
    Records::release(&ctx.db, run.id, validation_lease)
        .await
        .unwrap();

    // The pass was committed but its response was lost. Tester eligibility is
    // revoked before the worker retries the idempotent receipt lookup.
    crate::support::authenticate_with_policy(ctx, &owner, false);
    let runtime = SubmissionRuntime {
        validator: Arc::new(tuf_replay_server::domain::UnavailableValidator),
        charts: Arc::new(TestCharts),
        registrar: Arc::new(CommittedReceipt),
        evidence_slots: tokio::sync::Semaphore::new(1),
    };
    tuf_replay_server::services::submission::process(ctx, id, &runtime)
        .await
        .unwrap();

    let record = Records::record(&ctx.db, run.id).await.unwrap();
    assert_eq!(record.state, "submitted");
    assert_eq!(record.external_pass_id, Some(777));
}

#[tokio::test]
#[serial]
async fn trusted_tester_builds_v2_result_from_bounded_persisted_game_metadata() {
    let boot = boot_test::<App>().await.unwrap();
    let ctx = &boot.app_context;
    let mut settings = tuf_replay_server::settings::Settings::get(ctx).unwrap();
    settings.auto_submission.validation_mode =
        tuf_replay_server::settings::SubmissionValidationMode::TrustedTester;
    ctx.shared_store.insert(settings);

    let id = Uuid::new_v4();
    let owner = Uuid::new_v4().to_string();
    crate::support::authenticate(ctx, &owner);
    let run = Model::create(
        &ctx.db,
        NewRunSession {
            pid: id,
            protocol_version: 1,
            client_game_version: "3.4.0".into(),
            client_mod_version: "test".into(),
            tuf_level_id: 42,
            level_revision_id: None,
            level_revision_chart_id: None,
            client_tuf_file_id: "client-file".into(),
            client_level_relative_path: "main.adofai".into(),
            upload_token_hash: vec![1; 32],
            lease_expires_at: (Utc::now() + Duration::seconds(45)).fixed_offset(),
            hard_expires_at: (Utc::now() + Duration::hours(1)).fixed_offset(),
        },
    )
    .await
    .unwrap()
    .mark_streaming(&ctx.db)
    .await
    .unwrap()
    .mark_sealed(&ctx.db)
    .await
    .unwrap();
    Records::create_authorized(&ctx.db, run.id, &owner, Some(crate::support::GRANT))
        .await
        .unwrap();

    let metadata = serde_json::json!({
        "metadataVersion": 1,
        "submissionRunId": id.to_string(),
        "wonTimeUs": 8_000_000,
        "noFailMode": false,
        "judgmentDifficulty": 2,
        "keyCount": 4,
        "effectivePitch": 1.25,
        "gameVersion": "3.4.0",
        "judgmentSystem": "ModernCompetitive",
        "holdBehavior": 0,
        "inputOverflowDropped": 0,
        "inputUnmappedEvents": 0,
        "inputReadFailures": 0,
        "inputDegradedEvents": 0,
        "submissionResult": {
            "version": 1,
            "judgments": [0, 1, 2, 3, 4, 5, 6, 7, 0],
            "perfectMinus": 3,
            "perfectPlus": 2,
            "adofaiVersion": 3,
            "isXPerfectMode": true,
            "isNoHoldTap": false
        },
        "e2e": {
            "mode": "accepted",
            "judgments": [0, 0, 0, 0, 99, 0, 0, 0, 0],
            "speed": 99.0
        }
    })
    .to_string();
    let streams = vec![
        persist_stream(ctx, id, 0, b"0,32,3,0,0\n", 1).await,
        persist_stream(ctx, id, 1, b"0,0,0,0,0,0,0,0,0,0,0,3,0\n", 1).await,
        persist_stream(ctx, id, 2, metadata.as_bytes(), 1).await,
        persist_stream(ctx, id, 6, b"0,0,1\n1,1,2\n2,2,3\n", 3).await,
    ];
    let manifest = EvidenceManifest {
        protocol_version: 1,
        final_sequence: 2,
        digest: hex::encode(Sha256::digest(serde_json::to_vec(&streams).unwrap())),
        streams,
    };
    let lease = Uuid::new_v4();
    assert!(Records::lease(&ctx.db, run.id, lease).await.unwrap());
    Records::publish(
        &ctx.db,
        run.id,
        lease,
        serde_json::to_value(&manifest).unwrap(),
    )
    .await
    .unwrap();
    Records::release(&ctx.db, run.id, lease).await.unwrap();
    Records::request_authorized(&ctx.db, run.id, &owner, Some(crate::support::GRANT))
        .await
        .unwrap();

    let charts = Arc::new(CurrentChart(AtomicUsize::new(0)));
    let registrar = Arc::new(CaptureRegistration {
        calls: AtomicUsize::new(0),
        result: Mutex::new(None),
    });
    let runtime = SubmissionRuntime {
        // Trusted mode must use the recorded snapshot adapter and never call a
        // semantic validator. The unavailable implementation is intentional.
        validator: Arc::new(tuf_replay_server::domain::UnavailableValidator),
        charts: charts.clone(),
        registrar: registrar.clone(),
        evidence_slots: tokio::sync::Semaphore::new(1),
    };
    tuf_replay_server::services::submission::process(ctx, id, &runtime)
        .await
        .unwrap();

    let record = Records::record(&ctx.db, run.id).await.unwrap();
    assert_eq!(record.state, "submitted");
    assert_eq!(record.external_pass_id, Some(991));
    assert_eq!(charts.0.load(Ordering::SeqCst), 1);
    assert_eq!(registrar.calls.load(Ordering::SeqCst), 1);
    let result = registrar.result.lock().unwrap().clone().unwrap();
    assert_eq!(result.validation_contract_version, 2);
    assert_eq!(
        result.validation_status,
        ValidationStatus::SkippedTrustedTester
    );
    assert_eq!(
        result.result_provenance,
        ResultProvenance::RecordedGameResult
    );
    assert_eq!(result.validator_version, "trusted-tester-adapter-v1");
    assert_eq!(result.speed, 1.25);
    assert_eq!(result.key_count, 4);
    assert_eq!(result.judgments, [0, 1, 2, 3, 4, 5, 6, 7, 0]);
    assert_eq!(result.perfect_minus, 3);
    assert_eq!(result.perfect_plus, 2);
    assert_eq!(result.adofai_version, 3);
    assert!(result.is_x_perfect_mode);
    assert!(!result.is_no_hold_tap);
    assert_eq!(result.official_file_id, "current-tuf-file");
    assert!(result.has_valid_v2_contract());
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
            validation_contract_version: 2,
            validation_status: ValidationStatus::Validated,
            result_provenance: ResultProvenance::GameplayValidator,
            official_file_id: chart.file_id.clone(),
            chart_sha256: chart.sha256.clone(),
            gameplay_hash_version: chart.gameplay_hash_version,
            gameplay_hash: chart.gameplay_hash.clone(),
            evidence_digest: evidence.digest.clone(),
            validator_version: "test-only".into(),
            rules_version: "test-only".into(),
            speed: 1.0,
            judgments: [0, 0, 0, 0, 1, 0, 0, 0, 0],
            perfect_minus: 0,
            perfect_plus: 0,
            key_count: 1,
            is_no_hold_tap: false,
            is_adofai_v2: true,
            adofai_version: 1,
            is_x_perfect_mode: false,
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

struct CurrentChart(AtomicUsize);

#[async_trait::async_trait]
impl OfficialChartProvider for CurrentChart {
    async fn acquire(&self, level_id: i64, relative_path: &str) -> Result<OfficialChart, String> {
        assert_eq!(level_id, 42);
        assert_eq!(relative_path, "main.adofai");
        self.0.fetch_add(1, Ordering::SeqCst);
        Ok(OfficialChart {
            file_id: "current-tuf-file".into(),
            sha256: "d".repeat(64),
            gameplay_hash_version: 1,
            gameplay_hash: "e".repeat(64),
            bytes: b"current official chart".to_vec(),
        })
    }
}

struct CaptureRegistration {
    calls: AtomicUsize,
    result: Mutex<Option<ValidatedResult>>,
}

#[async_trait::async_trait]
impl PassRegistrar for CaptureRegistration {
    async fn lookup(&self, _: Uuid, _: &str, _: &str) -> Result<Option<i64>, String> {
        Ok(None)
    }

    async fn register(
        &self,
        _: Uuid,
        _: &str,
        _: Uuid,
        _: i64,
        _: &str,
        result: &ValidatedResult,
    ) -> Result<i64, String> {
        self.calls.fetch_add(1, Ordering::SeqCst);
        *self.result.lock().unwrap() = Some(result.clone());
        Ok(991)
    }
}

async fn persist_stream(
    ctx: &loco_rs::app::AppContext,
    run_id: Uuid,
    kind: u8,
    bytes: &[u8],
    records: u64,
) -> tuf_replay_server::domain::EvidenceStream {
    let sha256 = hex::encode(Sha256::digest(bytes));
    let storage_key = format!("evidence/{run_id}/{sha256}");
    ctx.storage
        .upload(
            Path::new(&storage_key),
            &bytes::Bytes::copy_from_slice(bytes),
        )
        .await
        .unwrap();
    tuf_replay_server::domain::EvidenceStream {
        kind,
        storage_key,
        sha256,
        bytes: bytes.len() as u64,
        records,
    }
}
