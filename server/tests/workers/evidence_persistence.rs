use chrono::{Duration, Utc};
use loco_rs::testing::prelude::*;
use serial_test::serial;
use tuf_replay_server::app::App;
use tuf_replay_server::domain::EvidenceManifest;
use tuf_replay_server::domain::GameplayValidator;
use tuf_replay_server::domain::UnavailableValidator;
use tuf_replay_server::domain::ValidationOutcome;
use tuf_replay_server::models::run_sessions::Model;
use tuf_replay_server::models::run_sessions::NewRunSession;
use tuf_replay_server::models::run_submission_records::Entity as Records;
use tuf_replay_server::services::evidence;
use tuf_replay_server::services::ingest::RunIngestStore;
use uuid::Uuid;

#[tokio::test]
#[serial]
async fn persisted_evidence_survives_redis_removal_and_remains_unvalidated() {
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
            client_game_version: "3".into(),
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
    let store = ctx.shared_store.get::<RunIngestStore>().unwrap();
    store
        .create_session(id, "hash", (Utc::now() + Duration::hours(1)).timestamp())
        .await
        .unwrap();
    store.open(id, "hash").await.unwrap();
    let input = b"0,32,3,0,0\n";
    let hit = b"0,0,0,0,0,0,0,0,0,0,0,3,0\n";
    store.append(id, "hash", 0, 0, input).await.unwrap();
    store.append(id, "hash", 1, 1, hit).await.unwrap();
    store
        .append(id, "hash", 2, 2, b"{\"formatVersion\":3}")
        .await
        .unwrap();
    store.seal(id, "hash", 2, 1, 1).await.unwrap();
    evidence::ingest_release::release(ctx, id, run.id)
        .await
        .unwrap();
    assert_eq!(
        store.read_page(id, None).await.unwrap().len(),
        3,
        "sealed chunks must survive until the manifest is committed"
    );
    evidence::persist(ctx, id).await.unwrap();
    let first = Records::record(&ctx.db, run.id).await.unwrap();
    assert_eq!(first.state, "evidence_ready");
    assert!(first.ingest_released_at.is_some());
    assert!(store.read_page(id, None).await.unwrap().is_empty());
    let receipt = store.receipt(id, Some("hash")).await.unwrap();
    assert_eq!(receipt.status, "sealed");
    assert_eq!(receipt.acknowledged_sequence, 2);
    assert!(
        store
            .seal(id, "hash", 2, 1, 1)
            .await
            .unwrap()
            .already_sealed
    );
    let manifest: EvidenceManifest =
        serde_json::from_value(first.manifest.clone().unwrap()).unwrap();
    store.discard(id).await.unwrap();
    evidence::persist(ctx, id).await.unwrap();
    for (kind, bytes) in [(0, input.as_slice()), (1, hit.as_slice())] {
        let entry = manifest.streams.iter().find(|s| s.kind == kind).unwrap();
        let restored: Vec<u8> = ctx
            .storage
            .download(std::path::Path::new(&entry.storage_key))
            .await
            .unwrap();
        assert_eq!(restored, bytes);
    }
    assert!(Records::owned(&ctx.db, run.id, "someone-else")
        .await
        .is_err());
    Records::request(&ctx.db, run.id, &owner).await.unwrap();
    Records::request(&ctx.db, run.id, &owner).await.unwrap();
    assert_eq!(
        Records::record(&ctx.db, run.id).await.unwrap().state,
        "validation_pending"
    );
    assert!(matches!(
        UnavailableValidator
            .validate(
                &tuf_replay_server::domain::OfficialChart {
                    file_id: "unused".into(),
                    sha256: "a".repeat(64),
                    gameplay_hash_version: 1,
                    gameplay_hash: "b".repeat(64),
                    bytes: vec![],
                },
                &manifest
            )
            .await
            .unwrap(),
        ValidationOutcome::Unavailable
    ));
    let runtime = tuf_replay_server::services::submission::SubmissionRuntime {
        charts: std::sync::Arc::new(
            ctx.shared_store
                .get::<tuf_replay_server::services::tuf::catalog::TufCatalogRuntime>()
                .unwrap(),
        ),
        validator: std::sync::Arc::new(UnavailableValidator),
        registrar: std::sync::Arc::new(ExistingReceipt),
        evidence_slots: tokio::sync::Semaphore::new(1),
    };
    tuf_replay_server::services::submission::process(ctx, id, &runtime)
        .await
        .unwrap();
    assert_eq!(
        Records::record(&ctx.db, run.id).await.unwrap().state,
        "validator_unavailable"
    );
    assert!(Records::delete(&ctx.db, run.id, &owner).await.unwrap());
    tuf_replay_server::services::evidence::cleanup::cleanup(ctx)
        .await
        .unwrap();
    tuf_replay_server::services::evidence::cleanup::cleanup(ctx)
        .await
        .unwrap();
    for entry in &manifest.streams {
        assert!(!ctx
            .storage
            .exists(std::path::Path::new(&entry.storage_key))
            .await
            .unwrap());
    }
    assert!(Records::record(&ctx.db, run.id)
        .await
        .unwrap()
        .manifest
        .is_none());
}

// This receipt is only used in tests. The unavailable validator must never call it.
struct ExistingReceipt;
#[async_trait::async_trait]
impl tuf_replay_server::domain::PassRegistrar for ExistingReceipt {
    async fn lookup(&self, _: Uuid, _: &str, _: &str) -> Result<Option<i64>, String> {
        panic!("unvalidated evidence must never reach registration")
    }
    async fn register(
        &self,
        _: Uuid,
        _: &str,
        _: Uuid,
        _: i64,
        _: &str,
        _: &tuf_replay_server::domain::ValidatedResult,
    ) -> Result<i64, String> {
        panic!("unvalidated evidence must never reach registration")
    }
}
