use chrono::{Duration, Utc};
use loco_rs::testing::prelude::*;
use sea_orm::{ActiveModelTrait, ActiveValue::Set, DatabaseConnection};
use serial_test::serial;
use tuf_replay_server::app::App;
use tuf_replay_server::models::level_revision_charts;
use tuf_replay_server::models::level_revision_charts::NewLevelRevisionChart;
use tuf_replay_server::models::level_revisions;
use tuf_replay_server::models::run_sessions::Model;
use tuf_replay_server::models::run_sessions::NewRunSession;
use tuf_replay_server::models::run_sessions::RunStatus;
use uuid::Uuid;

async fn canonical_level(db: &DatabaseConnection, tuf_level_id: i64) -> (i64, i64) {
    let revision = level_revisions::ActiveModel {
        tuf_level_id: Set(tuf_level_id),
        tuf_file_id: Set(Uuid::new_v4().to_string()),
        canonical_payload_hash: Set(vec![1; 32]),
        payload_hash_version: Set(1),
        archive_storage_key: Set(format!("archives/{}.zip", Uuid::new_v4())),
        source_updated_at: Set(None),
        fetched_at: Set(Utc::now().fixed_offset()),
        ..Default::default()
    }
    .insert(db)
    .await
    .expect("insert revision");
    let chart = level_revision_charts::Model::create(
        db,
        NewLevelRevisionChart {
            level_revision_id: revision.id,
            relative_path: "level.adofai".to_owned(),
            canonical_chart_hash: vec![2; 32],
            artifact_storage_key: format!("charts/{}.adofai", Uuid::new_v4()),
        },
    )
    .await
    .expect("insert chart");
    (revision.id, chart.id)
}

fn new_run(tuf_level_id: i64, revision_id: i64, chart_id: i64) -> NewRunSession {
    NewRunSession {
        pid: Uuid::new_v4(),
        protocol_version: 1,
        client_game_version: "2.8.1".to_owned(),
        client_mod_version: "0.1.0".to_owned(),
        tuf_level_id,
        level_revision_id: Some(revision_id),
        level_revision_chart_id: Some(chart_id),
        client_tuf_file_id: "fixture".into(),
        client_level_relative_path: "level.adofai".into(),
        upload_token_hash: vec![3; 32],
        lease_expires_at: (Utc::now() + Duration::seconds(45)).fixed_offset(),
        hard_expires_at: (Utc::now() + Duration::hours(6)).fixed_offset(),
    }
}

#[tokio::test]
#[serial]
async fn enforces_revision_identity_lifecycle_and_deadlines() {
    let boot = boot_test::<App>().await.expect("test app boot");
    let db = &boot.app_context.db;
    let (revision_id, chart_id) = canonical_level(db, 42).await;

    let created = Model::create(db, new_run(42, revision_id, chart_id))
        .await
        .expect("create run");
    assert_eq!(created.status, RunStatus::Created.as_str());
    assert!(!created.lease_deadline_elapsed(Utc::now().fixed_offset()));
    assert!(created.clone().mark_sealed(db).await.is_err());

    let streaming = created.mark_streaming(db).await.expect("streaming");
    let sealed = streaming.mark_sealed(db).await.expect("sealed");
    assert_eq!(sealed.status, RunStatus::Sealed.as_str());
    assert!(sealed.sealed_at.is_some());
    assert!(sealed.clone().mark_failed(db).await.is_err());
    assert_eq!(
        sealed
            .mark_sealed(db)
            .await
            .expect("idempotent seal")
            .status,
        RunStatus::Sealed.as_str()
    );

    let mut mismatched = new_run(99, revision_id, chart_id);
    assert!(Model::create(db, mismatched).await.is_err());
    mismatched = new_run(42, revision_id, chart_id);
    mismatched.lease_expires_at = (Utc::now() - Duration::seconds(1)).fixed_offset();
    let expired = Model::create(db, mismatched)
        .await
        .expect("expired lease row");
    assert!(expired.lease_deadline_elapsed(Utc::now().fixed_offset()));
    assert!(!expired.hard_deadline_elapsed(Utc::now().fixed_offset()));
}
