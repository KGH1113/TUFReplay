use chrono::{Duration, Utc};
use loco_rs::testing::prelude::*;
use sea_orm::{ConnectionTrait, DbBackend, Statement};
use serial_test::serial;
use tuf_replay_server::app::App;
use tuf_replay_server::models::run_sessions::Model;
use tuf_replay_server::models::run_sessions::NewRunSession;
use tuf_replay_server::models::run_submission_records::retention as evidence_cleanup;
use tuf_replay_server::models::run_submission_records::retry as submission_retry;
use tuf_replay_server::models::run_submission_records::Entity as Records;
use uuid::Uuid;

#[tokio::test]
#[serial]
async fn expiry_blocks_new_submissions_and_retries_stop_without_losing_registration_identity() {
    let boot = boot_test::<App>().await.unwrap();
    let db = &boot.app_context.db;
    let owner = Uuid::new_v4().to_string();
    let run = Model::create(
        db,
        NewRunSession {
            pid: Uuid::new_v4(),
            protocol_version: 1,
            client_game_version: "test".into(),
            client_mod_version: "test".into(),
            tuf_level_id: 42,
            level_revision_id: None,
            level_revision_chart_id: None,
            client_tuf_file_id: "fixture".into(),
            client_level_relative_path: "level.adofai".into(),
            upload_token_hash: vec![1; 32],
            lease_expires_at: (Utc::now() + Duration::seconds(45)).fixed_offset(),
            hard_expires_at: (Utc::now() + Duration::hours(1)).fixed_offset(),
        },
    )
    .await
    .unwrap();
    Records::create(db, run.id, &owner).await.unwrap();
    let lease = Uuid::new_v4();
    assert!(Records::lease(db, run.id, lease).await.unwrap());
    Records::publish(db, run.id, lease, serde_json::json!({"fixture":true}))
        .await
        .unwrap();
    Records::release(db, run.id, lease).await.unwrap();
    assert!(Records::record(db, run.id)
        .await
        .unwrap()
        .evidence_expires_at
        .is_none());
    db.execute_raw(Statement::from_sql_and_values(DbBackend::Postgres,
        "UPDATE run_submission_records SET created_at=NOW()-INTERVAL '365 days',updated_at=NOW()-INTERVAL '365 days' WHERE run_session_id=$1",
        [run.id.into()])).await.unwrap();
    assert!(!evidence_cleanup::pending(db)
        .await
        .unwrap()
        .iter()
        .any(|r| r.run_id == run.pid));
    // A year-old saved clear remains available for its first submission.
    Records::request(db, run.id, &owner).await.unwrap();
    for attempt in 0..4 {
        let lease = Uuid::new_v4();
        assert!(Records::lease(db, run.id, lease).await.unwrap());
        submission_retry::defer(db, run.id, lease).await.unwrap();
        Records::release(db, run.id, lease).await.unwrap();
        assert!(!Records::lease(db, run.id, Uuid::new_v4()).await.unwrap());
        let record = Records::record(db, run.id).await.unwrap();
        assert_eq!(
            record.state,
            if attempt < 3 {
                "validation_pending"
            } else {
                "validation_error"
            }
        );
        db.execute_raw(Statement::from_sql_and_values(DbBackend::Postgres,
            "UPDATE run_submission_records SET next_attempt_at=NOW()-INTERVAL '1 second' WHERE run_session_id=$1",
            [run.id.into()])).await.unwrap();
    }
    Records::request(db, run.id, &owner).await.unwrap();
    assert_eq!(
        Records::record(db, run.id).await.unwrap().state,
        "validation_pending"
    );
    db.execute_raw(Statement::from_sql_and_values(DbBackend::Postgres,
        "UPDATE run_submission_records SET state='evidence_ready',evidence_expires_at=NOW()-INTERVAL '1 second' WHERE run_session_id=$1",
        [run.id.into()])).await.unwrap();
    assert!(Records::request(db, run.id, &owner).await.is_err());
    assert_eq!(
        Records::record(db, run.id).await.unwrap().state,
        "evidence_ready"
    );
    assert!(evidence_cleanup::pending(db)
        .await
        .unwrap()
        .iter()
        .any(|r| r.run_id == run.pid));
    assert_eq!(Records::record(db, run.id).await.unwrap().state, "expired");
    // A lost registration response is ambiguous: expiry cannot destroy its evidence.
    db.execute_raw(Statement::from_sql_and_values(
        DbBackend::Postgres,
        "UPDATE run_submission_records SET state='registration_error' WHERE run_session_id=$1",
        [run.id.into()],
    ))
    .await
    .unwrap();
    assert!(!evidence_cleanup::pending(db)
        .await
        .unwrap()
        .iter()
        .any(|r| r.run_id == run.pid));
    Records::request(db, run.id, &owner).await.unwrap();
    assert_eq!(
        Records::record(db, run.id).await.unwrap().state,
        "registering"
    );
}
