use bytes::Bytes;
use chrono::{Duration, Utc};
use loco_rs::testing::prelude::*;
use sea_orm::{ActiveModelTrait, Set};
use serial_test::serial;
use std::path::Path;
use tuf_replay_server::app::App;
use tuf_replay_server::domain::{EvidenceManifest, EvidenceStream, ValidatedResult};
use tuf_replay_server::models::run_sessions::{Model as Run, NewRunSession};
use tuf_replay_server::models::run_submission_records::{
    ActiveModel as ActiveSubmission, Entity as Submissions,
};
use uuid::Uuid;

#[tokio::test]
#[serial]
async fn submitted_replay_is_public_and_internal_evidence_is_hidden() {
    request::<App, _, _>(|request, ctx| async move {
        let run_id = Uuid::new_v4();
        let owner = Uuid::new_v4().to_string();
        let run = create_run(&ctx, run_id).await;
        Submissions::create_authorized(&ctx.db, run.id, &owner, Some(crate::support::GRANT))
            .await
            .unwrap();

        let input = Bytes::from_static(b"0,32,3,0,0\n");
        let input_key = format!("evidence/{run_id}/{}", "a".repeat(64));
        ctx.storage
            .upload(Path::new(&input_key), &input)
            .await
            .unwrap();
        let hit = Bytes::from_static(b"0,0,0,0,0,0,0,0,0,0,0,3,0\n");
        let hit_key = format!("evidence/{run_id}/hits");
        ctx.storage.upload(Path::new(&hit_key), &hit).await.unwrap();
        let metadata = Bytes::from_static(b"{\"formatVersion\":3}");
        let metadata_key = format!("evidence/{run_id}/metadata");
        ctx.storage
            .upload(Path::new(&metadata_key), &metadata)
            .await
            .unwrap();
        let streams = vec![
            stream(0, &input_key, "a", input.len() as u64, 1),
            stream(1, &hit_key, "b", hit.len() as u64, 1),
            stream(2, &metadata_key, "c", metadata.len() as u64, 1),
            stream(6, &format!("evidence/{run_id}/server-timing"), "d", 1, 1),
        ];
        let manifest = EvidenceManifest {
            protocol_version: 1,
            final_sequence: 3,
            digest: "e".repeat(64),
            streams,
        };
        let validation = ValidatedResult {
            validator_version: "test".into(),
            rules_version: "test".into(),
            evidence_digest: manifest.digest.clone(),
            official_file_id: "official-file".into(),
            chart_sha256: "f".repeat(64),
            gameplay_hash_version: 1,
            gameplay_hash: "a".repeat(64),
            speed: 1.0,
            judgments: [0, 0, 0, 0, 1, 0, 0, 0, 0],
            key_count: 1,
            is_no_hold_tap: false,
            is_adofai_v2: true,
        };
        let record = Submissions::record(&ctx.db, run.id).await.unwrap();
        let mut active: ActiveSubmission = record.into();
        active.state = Set("submitted".into());
        active.external_pass_id = Set(Some(77));
        active.manifest = Set(Some(serde_json::to_value(&manifest).unwrap()));
        active.validation = Set(Some(serde_json::to_value(validation).unwrap()));
        active.update(&ctx.db).await.unwrap();

        let response = request.get(&format!("/api/v1/replays/{run_id}")).await;
        assert_eq!(response.status_code(), 200, "{}", response.text());
        let body: serde_json::Value = response.json();
        assert_eq!(body["format_version"], 2);
        assert_eq!(body["external_pass_id"], 77);
        assert_eq!(body["official_file_id"], "official-file");
        assert_eq!(body["gameplay_hash_version"], 1);
        assert_eq!(body["gameplay_hash"], "a".repeat(64));
        assert!(body.to_string().find("storage_key").is_none());
        assert!(body.to_string().find("server-timing").is_none());
        assert_eq!(body["files"].as_array().unwrap().len(), 3);

        let response = request
            .get(&format!("/api/v1/replays/{run_id}/files/inputs.csv"))
            .await;
        assert_eq!(response.status_code(), 200);
        assert_eq!(response.text(), std::str::from_utf8(&input).unwrap());

        let response = request
            .get(&format!("/api/v1/replays/{run_id}/files/server-timing.csv"))
            .await;
        assert_eq!(response.status_code(), 404);
    })
    .await;
}

#[tokio::test]
#[serial]
async fn unfinished_replay_is_not_public() {
    request::<App, _, _>(|request, ctx| async move {
        let run_id = Uuid::new_v4();
        let owner = Uuid::new_v4().to_string();
        let run = create_run(&ctx, run_id).await;
        Submissions::create_authorized(&ctx.db, run.id, &owner, Some(crate::support::GRANT))
            .await
            .unwrap();
        let response = request.get(&format!("/api/v1/replays/{run_id}")).await;
        assert_eq!(response.status_code(), 404);
    })
    .await;
}

async fn create_run(ctx: &loco_rs::app::AppContext, pid: Uuid) -> Run {
    Run::create(
        &ctx.db,
        NewRunSession {
            pid,
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
    .unwrap()
}

fn stream(kind: u8, storage_key: &str, digest: &str, bytes: u64, records: u64) -> EvidenceStream {
    EvidenceStream {
        kind,
        storage_key: storage_key.into(),
        sha256: digest.repeat(64),
        bytes,
        records,
    }
}
