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
use tuf_replay_server::models::{run_visual_selections, visual_presets};
use tuf_replay_server::services::visuals;
use uuid::Uuid;

#[tokio::test]
#[serial]
async fn submitted_replay_is_public_and_internal_evidence_is_hidden() {
    assert_public_visual_roundtrip(None).await;
}

#[tokio::test]
#[serial]
#[ignore = "requires real source fixtures; run via visual-pipeline-check"]
async fn real_importer_visuals_survive_registration_submission_and_public_api() {
    let root = Path::new(env!("CARGO_MANIFEST_DIR")).join("../build/visual-fixtures");
    for name in [
        "jipper-resourcepack-keyviewer",
        "jipper-resourcepack-overlay",
        "jipper-keyviewer",
        "dmnote",
        "impl-dmnote",
        "impl-resourcepack",
    ] {
        let bundle = serde_json::from_slice(
            &std::fs::read(root.join(format!("{name}.json")))
                .expect("generate real importer fixtures first"),
        )
        .unwrap();
        assert_public_visual_roundtrip(Some((bundle, name.to_owned()))).await;
    }
}

async fn assert_public_visual_roundtrip(fixture: Option<(serde_json::Value, String)>) {
    request::<App, _, _>(move |mut request, ctx| async move {
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
            validation_contract_version: 2,
            validation_status: tuf_replay_server::domain::ValidationStatus::SkippedTrustedTester,
            result_provenance: tuf_replay_server::domain::ResultProvenance::RecordedGameResult,
            validator_version: "test".into(),
            rules_version: "test".into(),
            evidence_digest: manifest.digest.clone(),
            official_file_id: "official-file".into(),
            chart_sha256: "f".repeat(64),
            gameplay_hash_version: 1,
            gameplay_hash: "a".repeat(64),
            speed: 1.0,
            judgments: [0, 0, 0, 0, 1, 0, 0, 0, 0],
            perfect_minus: 0,
            perfect_plus: 0,
            key_count: 1,
            is_no_hold_tap: false,
            is_adofai_v2: true,
            adofai_version: 1,
            is_x_perfect_mode: false,
        };
        let record = Submissions::record(&ctx.db, run.id).await.unwrap();
        let record_id = record.id;
        let mut active: ActiveSubmission = record.into();
        active.state = Set("submitted".into());
        active.external_pass_id = Set(Some(77));
        active.manifest = Set(Some(serde_json::to_value(&manifest).unwrap()));
        active.validation = Set(Some(serde_json::to_value(validation).unwrap()));
        active.update(&ctx.db).await.unwrap();

        let fallback = serde_json::json!({
            "schema_version": 1,
            "kind": "keyviewer",
            "source": "dmnote",
            "source_version": "2.0.2",
            "viewport": {"width": 1920, "height": 1080},
            "files": {
                "preset.json": {
                    "keys": {"4key": []},
                    "keyPositions": {"4key": []}
                }
            },
            "assets": [{
                "path": "dot.png", "media_type": "image/png",
                "data_base64": "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jS1sAAAAASUVORK5CYII="
            }]
        });
        let bundle = fixture
            .as_ref()
            .map(|(bundle, _)| bundle.clone())
            .unwrap_or(fallback);
        let kind = bundle["kind"].as_str().unwrap().to_owned();
        let validated = visuals::validate_bundle(bundle.clone()).unwrap();
        let token = crate::support::authenticate(&ctx, &owner).await;
        request.add_header(axum::http::header::AUTHORIZATION, format!("Bearer {token}"));
        let registered = request
            .post("/api/v1/visual-presets")
            .json(&serde_json::json!({"name":"Public visual", "bundle":bundle}))
            .await;
        assert_eq!(registered.status_code(), 200, "{}", registered.text());
        let preset_id = Uuid::parse_str(
            registered.json::<serde_json::Value>()["preset"]["id"]
                .as_str()
                .unwrap(),
        )
        .unwrap();
        request.clear_headers();
        run_visual_selections::insert(
            &ctx.db,
            record_id,
            &run_visual_selections::VisualSelection {
                keyviewer_id: (kind == "keyviewer").then_some(preset_id),
                overlay_id: (kind == "overlay").then_some(preset_id),
            },
        )
        .await
        .unwrap();

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
            .get(&format!("/api/v1/replays/{run_id}?format=3"))
            .await;
        assert_eq!(response.status_code(), 200, "{}", response.text());
        assert_eq!(response.headers()["cache-control"], "no-store");
        let body: serde_json::Value = response.json();
        assert_eq!(body["format_version"], 3);
        assert_eq!(body["visuals"][&kind]["preset_id"], preset_id.to_string());
        assert_eq!(
            body["visuals"][if kind == "keyviewer" {
                "overlay"
            } else {
                "keyviewer"
            }],
            serde_json::Value::Null
        );
        assert_eq!(
            body["visuals"][&kind]["url"],
            format!("/api/v1/replays/{run_id}/visuals/{kind}")
        );

        let response = request
            .get(&format!("/api/v1/replays/{run_id}/visuals/{kind}"))
            .await;
        assert_eq!(response.status_code(), 200, "{}", response.text());
        assert_eq!(response.headers()["cache-control"], "no-store");
        assert_eq!(response.headers()["content-type"], "application/json");
        assert_eq!(
            response.headers()["etag"],
            format!("\"{}\"", validated.sha256)
        );
        assert_eq!(response.as_bytes().as_ref(), validated.bytes.as_slice());
        if let Some((_, name)) = &fixture {
            let output = Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("../build/visual-fixtures")
                .join(format!("{name}.api.json"));
            std::fs::write(output, response.as_bytes()).unwrap();
        }

        ctx.shared_store.insert(
            tuf_replay_server::services::cdn::CdnSigner::new("https://cdn.example", &"s".repeat(32)).unwrap(),
        );
        let response = request
            .get(&format!("/api/v1/replays/{run_id}?format=3&asset_mode=objects&delivery=cdn"))
            .await;
        assert_eq!(response.status_code(), 200, "{}", response.text());
        let cdn: serde_json::Value = response.json();
        assert_eq!(cdn["delivery"]["origin"], "https://cdn.example");
        let grants = cdn["delivery"]["urls"].as_object().unwrap();
        assert!(!cdn["delivery"]["urls"].to_string().contains("server-timing"));
        for file in cdn["files"].as_array().unwrap() {
            let grant = grants[file["url"].as_str().unwrap()].as_str().unwrap();
            assert!(grant.starts_with(&format!("https://cdn.example/objects/evidence/{run_id}/")));
            assert!(grant.contains("&signature="));
        }
        let descriptor = &cdn["visuals"][&kind];
        let route = descriptor["url"].as_str().unwrap();
        assert!(grants[route].as_str().unwrap().starts_with("https://cdn.example/objects/visual-bundles/sha256/"));
        let key = format!("visual-bundles/sha256/{}", descriptor["sha256"].as_str().unwrap());
        let bytes: Vec<u8> = ctx.storage.download(Path::new(&key)).await.unwrap();
        let value: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(value["schema_version"], 2);
        for asset in value["assets"].as_array().unwrap() {
            let hash = asset["sha256"].as_str().unwrap();
            let route = format!("/api/v1/replays/{run_id}/visuals/{kind}/assets/{hash}");
            assert!(grants[&route].as_str().unwrap().starts_with(&format!("https://cdn.example/objects/visual-assets/sha256/{hash}?")));
        }

        if fixture.is_none() {
            use tuf_replay_server::models::pass_visuals::{self, Defaults};
            let options_route = format!("/api/v1/replays/{run_id}/visual-options");
            let response = request.get(&options_route).await;
            assert_eq!(response.status_code(), 200);
            assert_eq!(response.headers()["cache-control"], "no-store");
            assert_eq!(response.json::<serde_json::Value>()["presets"].as_array().unwrap().len(), 1);
            let internal = format!("/internal/tuf/replays/{run_id}/visuals?owner_id={owner}&pass_id=77");
            assert_eq!(request.get(&internal).await.status_code(), 401);
            request.add_header(axum::http::header::AUTHORIZATION, format!("Bearer {token}"));
            assert_eq!(request.get(&internal).await.status_code(), 401);
            request.clear_headers();
            let defaults = Defaults { keyviewer_id: Some(preset_id), overlay_id: None };
            assert!(pass_visuals::save_defaults(&ctx.db, run_id, 78, &owner, &defaults).await.is_err());
            assert!(pass_visuals::save_defaults(&ctx.db, run_id, 77, "another-owner", &defaults).await.is_err());
            assert!(pass_visuals::save_defaults(&ctx.db, run_id, 77, &owner,
                &Defaults { keyviewer_id: None, overlay_id: Some(preset_id) }).await.is_err());
            let foreign = visual_presets::create(&ctx.db, "another-owner", "Foreign", validated.kind,
                validated.source, &validated.source_version, validated.bytes.clone(), &validated.sha256).await.unwrap();
            assert!(pass_visuals::save_defaults(&ctx.db, run_id, 77, &owner,
                &Defaults { keyviewer_id: Some(foreign.id), overlay_id: None }).await.is_err());
            assert!(pass_visuals::set_hidden(&ctx.db, run_id, 77, &owner, foreign.id, true).await.is_err());
            let explicit = request.get(&format!("/api/v1/replays/{run_id}?format=3&asset_mode=objects&delivery=cdn&keyviewer_id={preset_id}&overlay_id=none")).await;
            assert_eq!(explicit.status_code(), 200, "{}", explicit.text());
            let explicit: serde_json::Value = explicit.json();
            let url = format!("/api/v1/replays/{run_id}/visuals/keyviewer?asset_mode=objects&preset_id={preset_id}");
            assert_eq!(explicit["visuals"]["keyviewer"]["url"], url);
            assert!(explicit["delivery"]["urls"][&url].is_string());
            assert_eq!(request.get(&url).await.status_code(), 200);
            let off: serde_json::Value = request.get(&format!("/api/v1/replays/{run_id}?format=3&keyviewer_id=none")).await.json();
            assert!(off["visuals"]["keyviewer"].is_null());
            let foreign_selection: serde_json::Value = request.get(&format!("/api/v1/replays/{run_id}?format=3&keyviewer_id={}", foreign.id)).await.json();
            assert!(foreign_selection["visuals"]["keyviewer"].is_null());
            assert_eq!(request.get(&format!("/api/v1/replays/{run_id}?format=3&keyviewer_id=invalid")).await.status_code(), 400);
            // Viewer selection never changes the saved pass default.
            assert_eq!(pass_visuals::public_options(&ctx.db, run_id).await.unwrap().defaults.keyviewer_id, Some(preset_id));
            let before = Submissions::record(&ctx.db, run.id).await.unwrap();
            let another_run_id = Uuid::new_v4();
            let another_run = create_run(&ctx, another_run_id).await;
            Submissions::create_authorized(&ctx.db, another_run.id, &owner, Some(crate::support::GRANT)).await.unwrap();
            let mut another: ActiveSubmission = Submissions::record(&ctx.db, another_run.id).await.unwrap().into();
            another.state = Set("submitted".into());
            another.external_pass_id = Set(Some(78));
            another.manifest = Set(before.manifest.clone());
            another.validation = Set(before.validation.clone());
            another.update(&ctx.db).await.unwrap();
            pass_visuals::save_defaults(&ctx.db, another_run_id, 78, &owner, &defaults).await.unwrap();
            pass_visuals::set_hidden(&ctx.db, run_id, 77, &owner, preset_id, true).await.unwrap();
            let public = pass_visuals::public_options(&ctx.db, run_id).await.unwrap();
            assert!(public.presets.is_empty());
            assert_eq!(public.defaults.keyviewer_id, None);
            assert_eq!(pass_visuals::public_options(&ctx.db, another_run_id).await.unwrap().defaults.keyviewer_id, None);
            let own = pass_visuals::owner_options(&ctx.db, run_id, 77, &owner).await.unwrap();
            assert!(own.presets[0].is_hidden);
            assert!(visual_presets::list(&ctx.db, &owner).await.unwrap().is_empty());
            assert!(visual_presets::active_owned(&ctx.db, &owner, preset_id).await.unwrap().is_none());
            assert!(pass_visuals::save_defaults(&ctx.db, run_id, 77, &owner, &defaults).await.is_err());
            assert_eq!(request.get(&url).await.status_code(), 404);
            pass_visuals::set_hidden(&ctx.db, run_id, 77, &owner, preset_id, false).await.unwrap();
            assert_eq!(pass_visuals::public_options(&ctx.db, run_id).await.unwrap().defaults.keyviewer_id, None);
            assert_eq!(pass_visuals::public_options(&ctx.db, another_run_id).await.unwrap().defaults.keyviewer_id, None);
            pass_visuals::save_defaults(&ctx.db, run_id, 77, &owner, &defaults).await.unwrap();
            let after = Submissions::record(&ctx.db, run.id).await.unwrap();
            assert_eq!(before.manifest, after.manifest);
            assert_eq!(before.validation, after.validation);
        }

        visual_presets::delete(&ctx.db, &owner, preset_id)
            .await
            .unwrap();
        let response = request
            .get(&format!("/api/v1/replays/{run_id}/visuals/{kind}"))
            .await;
        assert_eq!(response.status_code(), 404);
        let response = request
            .get(&format!("/api/v1/replays/{run_id}?format=3"))
            .await;
        assert_eq!(response.status_code(), 200);
        let body: serde_json::Value = response.json();
        assert_eq!(body["visuals"][&kind], serde_json::Value::Null);

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
        ctx.shared_store.insert(
            tuf_replay_server::services::cdn::CdnSigner::new(
                "https://cdn.example",
                &"s".repeat(32),
            )
            .unwrap(),
        );
        let run_id = Uuid::new_v4();
        let owner = Uuid::new_v4().to_string();
        let run = create_run(&ctx, run_id).await;
        Submissions::create_authorized(&ctx.db, run.id, &owner, Some(crate::support::GRANT))
            .await
            .unwrap();
        let response = request.get(&format!("/api/v1/replays/{run_id}")).await;
        assert_eq!(response.status_code(), 404);
        let response = request
            .get(&format!(
                "/api/v1/replays/{run_id}?format=3&asset_mode=objects&delivery=cdn"
            ))
            .await;
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
