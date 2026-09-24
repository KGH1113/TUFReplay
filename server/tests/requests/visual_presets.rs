use chrono::{Duration, Utc};
use loco_rs::{testing::prelude::*, TestServer};
use sea_orm::{ActiveModelTrait, IntoActiveModel, Set};
use serde_json::{json, Value};
use serial_test::serial;
use tuf_replay_server::{
    app::App,
    models::{
        run_sessions::{Model as Run, NewRunSession},
        run_submission_records::Entity as Submissions,
    },
};
use uuid::Uuid;

fn dmnote_bundle() -> Value {
    json!({
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
        "assets": []
    })
}

fn jipper_overlay_bundle() -> Value {
    json!({
        "schema_version": 1,
        "kind": "overlay",
        "source": "jipper-resourcepack",
        "source_version": "1.5.2.0",
        "viewport": {"width": 1920, "height": 1080},
        "files": {"ResourcePack.json": {"Feature": {}}},
        "assets": []
    })
}

fn auth(request: &mut TestServer, token: &str) {
    request.clear_headers();
    request.add_header(axum::http::header::AUTHORIZATION, format!("Bearer {token}"));
}

#[tokio::test]
#[serial]
async fn inline_asset_migration_is_resumable_and_preserves_preset_identity() {
    use base64::{engine::general_purpose::STANDARD, Engine as _};
    use loco_rs::{prelude::Task, task::Vars};
    use tuf_replay_server::{
        models::visual_presets as presets,
        services::{visual_assets, visuals},
        tasks::migrate_visual_assets::MigrateVisualAssets,
    };
    request::<App, _, _>(|_, ctx| async move {
        let mut value = dmnote_bundle();
        value["assets"] = json!([{"path":"image.png","media_type":"image/png","data_base64":STANDARD.encode(b"\x89PNG\r\n\x1a\n")}]);
        let original = visuals::validate_bundle(value).unwrap();
        let saved = presets::create(&ctx.db, "migration-owner", "Existing preset", original.kind, original.source,
            &original.source_version, original.bytes.clone(), &original.sha256).await.unwrap();
        for _ in 0..2 {
            MigrateVisualAssets.run(&ctx, &Vars::default()).await.unwrap();
            let stored = presets::active_bundle(&ctx.db, saved.id, original.kind).await.unwrap().unwrap();
            assert_eq!(stored.name, "Existing preset");
            assert_eq!(serde_json::from_slice::<Value>(&stored.bundle).unwrap()["schema_version"], 2);
            let legacy = visual_assets::inline_legacy(&ctx, &stored.bundle).await.unwrap();
            assert_eq!(serde_json::from_slice::<Value>(&legacy).unwrap(), serde_json::from_slice::<Value>(&original.bytes).unwrap());
        }
    }).await;
}

#[tokio::test]
#[serial]
async fn binary_assets_are_verified_deduplicated_and_owner_scoped() {
    use bytes::Bytes;
    use sha2::{Digest, Sha256};
    use tuf_replay_server::services::visual_assets;
    request::<App, _, _>(|mut request, ctx| async move {
        let owner = Uuid::new_v4().to_string();
        let token = crate::support::authenticate(&ctx, &owner).await;
        auth(&mut request, &token);
        let mut png = b"\x89PNG\r\n\x1a\n".to_vec();
        png.resize(2_100_000, 0); // Exceeds Axum's default limit; the binary route has its own bound.
        let data = Bytes::from(png);
        let hash = hex::encode(Sha256::digest(&data));
        let reference = json!({"sha256": hash, "bytes": data.len(), "media_type": "image/png"});
        let checked = request.post("/api/v1/visual-assets/check").json(&json!({"assets":[reference]})).await;
        assert_eq!(checked.json::<Value>()["missing"], json!([hash]));
        let upload = request.put(&format!("/api/v1/visual-assets/{hash}"))
            .content_type("image/png").bytes(data.clone()).await;
        assert_eq!(upload.status_code(), 200, "{}", upload.text());
        let checked = request.post("/api/v1/visual-assets/check").json(&json!({"assets":[reference]})).await;
        assert_eq!(checked.json::<Value>()["missing"], json!([]));
        assert_eq!(request.get(&format!("/api/v1/visual-assets/public/{hash}")).await.status_code(), 404);
        let corrupt = request.put(&format!("/api/v1/visual-assets/{hash}"))
            .content_type("image/png").bytes(Bytes::from_static(b"corrupt!")).await;
        assert_eq!(corrupt.status_code(), 400);

        let mut bundle = dmnote_bundle();
        bundle["schema_version"] = json!(2);
        bundle["assets"] = json!([{"path":"image.png", "sha256":hash,"bytes":data.len(),"media_type":"image/png"}]);
        let saved = request.post("/api/v1/visual-presets").json(&json!({"name":"objects","bundle":bundle})).await;
        assert_eq!(saved.status_code(), 200, "{}", saved.text());
        let id = Uuid::parse_str(saved.json::<Value>()["preset"]["id"].as_str().unwrap()).unwrap();
        let stored = tuf_replay_server::models::visual_presets::active_bundle(&ctx.db, id,
            tuf_replay_server::models::visual_presets::VisualKind::Keyviewer).await.unwrap().unwrap();
        let metadata: Value = serde_json::from_slice(&stored.bundle).unwrap();
        assert_eq!(metadata["schema_version"], 2);
        assert!(metadata["assets"][0].get("data_base64").is_none());
        let legacy = visual_assets::inline_legacy(&ctx, &stored.bundle).await.unwrap();
        assert_eq!(serde_json::from_slice::<Value>(&legacy).unwrap()["schema_version"], 1);

        let other = Uuid::new_v4().to_string();
        let other_token = crate::support::authenticate(&ctx, &other).await;
        auth(&mut request, &other_token);
        let checked = request.post("/api/v1/visual-assets/check").json(&json!({"assets":[reference]})).await;
        assert_eq!(checked.json::<Value>()["missing"], json!([hash]));
        assert_eq!(request.get(&format!("/api/v1/visual-assets/{hash}")).await.status_code(), 404);
        let rejected = request.post("/api/v1/visual-presets").json(&json!({"name":"stolen","bundle":bundle})).await;
        assert_eq!(rejected.status_code(), 400);
        // Supplying the actual bytes proves possession; a guessed digest cannot claim ownership.
        assert_eq!(request.put(&format!("/api/v1/visual-assets/{hash}")).content_type("image/png").bytes(data).await.status_code(), 200);
    }).await;
}

#[tokio::test]
#[serial]
async fn legacy_source_migration_preserves_references_and_bundle_integrity() {
    use migration::{MigratorTrait, SchemaManager};
    use sea_orm::{ConnectionTrait, TransactionTrait};
    request::<App, _, _>(|_, ctx| async move {
        let tx = ctx.db.begin().await.unwrap();
        tx.execute_unprepared("CREATE TEMP TABLE visual_presets (
            id integer PRIMARY KEY, source text, kind text, bundle bytea, sha256 text, bytes integer,
            CONSTRAINT visual_presets_source_check CHECK(source IN ('jipper','dmnote')));
            CREATE TEMP TABLE visual_fixture_refs (preset_id integer REFERENCES visual_presets(id));
            INSERT INTO visual_presets VALUES (1,'jipper','keyviewer',convert_to('{\"source\":\"jipper\",\"assets\":[\"preserved\"]}','UTF8'),'old',1);
            INSERT INTO visual_fixture_refs VALUES(1),(1);").await.unwrap();
        let migration = migration::Migrator::migrations().into_iter()
            .find(|m| m.name() == "m20260917_000001_extend_visual_sources").unwrap();
        migration.up(&SchemaManager::new(&tx)).await.unwrap();
        let row = tx.query_one_raw(sea_orm::Statement::from_string(sea_orm::DbBackend::Postgres,
            "SELECT source='jipper-resourcepack' AND convert_from(bundle,'UTF8')::jsonb->>'source'='jipper-resourcepack'
              AND convert_from(bundle,'UTF8')::jsonb->'assets'='[\"preserved\"]'::jsonb
              AND bytes=octet_length(bundle) AND sha256=encode(sha256(bundle),'hex')
              AND (SELECT count(*) FROM visual_fixture_refs WHERE preset_id=1)=2 AS valid FROM visual_presets WHERE id=1".to_owned())).await.unwrap().unwrap();
        assert!(row.try_get::<bool>("", "valid").unwrap());
        tx.rollback().await.unwrap();
    }).await;
}

#[tokio::test]
#[serial]
async fn visual_presets_are_owner_scoped_and_tombstoned() {
    request::<App, _, _>(|mut request, ctx| async move {
        let owner = Uuid::new_v4().to_string();
        let owner_token = crate::support::authenticate(&ctx, &owner).await;
        auth(&mut request, &owner_token);

        let created = request
            .post("/api/v1/visual-presets")
            .json(&json!({"name": "Shared Name", "bundle": dmnote_bundle()}))
            .await;
        assert_eq!(created.status_code(), 200, "{}", created.text());
        let created: Value = created.json();
        let preset_id = created["preset"]["id"]
            .as_str()
            .expect("preset id")
            .to_owned();

        let duplicate_kind = request
            .post("/api/v1/visual-presets")
            .json(&json!({"name": "shared name", "bundle": jipper_overlay_bundle()}))
            .await;
        assert_eq!(duplicate_kind.status_code(), 400);
        assert!(duplicate_kind.text().contains("visual_name_taken"));

        let listed: Value = request.get("/api/v1/visual-presets").await.json();
        assert_eq!(listed["presets"].as_array().unwrap().len(), 1);
        assert_eq!(listed["presets"][0]["id"], preset_id);
        assert!(listed["presets"][0].get("bundle").is_none());

        let other_owner = Uuid::new_v4().to_string();
        let other_token = crate::support::authenticate(&ctx, &other_owner).await;
        auth(&mut request, &other_token);
        let other_list: Value = request.get("/api/v1/visual-presets").await.json();
        assert!(other_list["presets"].as_array().unwrap().is_empty());
        let hidden_delete = request
            .delete(&format!("/api/v1/visual-presets/{preset_id}"))
            .await;
        assert_eq!(hidden_delete.status_code(), 404);

        let owner_token = crate::support::authenticate(&ctx, &owner).await;
        auth(&mut request, &owner_token);
        let deleted = request
            .delete(&format!("/api/v1/visual-presets/{preset_id}"))
            .await;
        assert_eq!(deleted.status_code(), 200, "{}", deleted.text());
        assert_eq!(deleted.json::<Value>()["deleted"], true);

        let repeated = request
            .delete(&format!("/api/v1/visual-presets/{preset_id}"))
            .await;
        assert_eq!(repeated.status_code(), 404);
        let listed: Value = request.get("/api/v1/visual-presets").await.json();
        assert!(listed["presets"].as_array().unwrap().is_empty());
    })
    .await;
}

#[tokio::test]
#[serial]
async fn visual_import_rejects_missing_assets_and_multiple_dmnote_tabs() {
    request::<App, _, _>(|mut request, ctx| async move {
        let owner = Uuid::new_v4().to_string();
        let token = crate::support::authenticate(&ctx, &owner).await;
        auth(&mut request, &token);

        let mut missing_asset = dmnote_bundle();
        missing_asset["files"]["preset.json"]["image"] = json!("missing.png");
        let missing = request
            .post("/api/v1/visual-presets")
            .json(&json!({"name": "Missing", "bundle": missing_asset}))
            .await;
        assert_eq!(missing.status_code(), 400);
        assert!(missing.text().contains("visual_asset_missing"));

        let mut multiple_tabs = dmnote_bundle();
        multiple_tabs["files"]["preset.json"]["keys"] = json!({
            "4key": [],
            "5key": []
        });
        let multiple = request
            .post("/api/v1/visual-presets")
            .json(&json!({"name": "Multiple", "bundle": multiple_tabs}))
            .await;
        assert_eq!(multiple.status_code(), 400);
        assert!(multiple.text().contains("visual_multiple_tabs"));
    })
    .await;
}

#[tokio::test]
#[serial]
async fn visual_import_accepts_valid_snapshots_above_default_body_limit() {
    request::<App, _, _>(|mut request, ctx| async move {
        let owner = Uuid::new_v4().to_string();
        let token = crate::support::authenticate(&ctx, &owner).await;
        auth(&mut request, &token);

        let mut bundle = dmnote_bundle();
        bundle["files"]["preset.json"]["padding"] = Value::String("x".repeat(2_100_000));
        let response = request
            .post("/api/v1/visual-presets")
            .json(&json!({"name": "Large snapshot", "bundle": bundle}))
            .await;
        assert_eq!(response.status_code(), 200, "{}", response.text());
    })
    .await;
}

#[tokio::test]
#[serial]
async fn submission_selection_is_fixed_across_bodyless_retries_and_deletion() {
    request::<App, _, _>(|mut request, ctx| async move {
        let owner = Uuid::new_v4().to_string();
        let token = crate::support::authenticate(&ctx, &owner).await;
        auth(&mut request, &token);
        let run_id = Uuid::new_v4();
        let run = Run::create(
            &ctx.db,
            NewRunSession {
                pid: run_id,
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
        .expect("create run")
        .mark_streaming(&ctx.db)
        .await
        .expect("stream run")
        .mark_sealed(&ctx.db)
        .await
        .expect("seal run");
        Submissions::create_authorized(&ctx.db, run.id, &owner, Some(crate::support::GRANT))
            .await
            .expect("create submission");
        let record = Submissions::record(&ctx.db, run.id)
            .await
            .expect("submission");
        let mut active = record.into_active_model();
        active.state = Set("evidence_ready".into());
        active.manifest = Set(Some(json!({"formatVersion": 3})));
        active.update(&ctx.db).await.expect("prepare submission");

        let first = request
            .post("/api/v1/visual-presets")
            .json(&json!({"name": "Frozen", "bundle": dmnote_bundle()}))
            .await;
        assert_eq!(first.status_code(), 200, "{}", first.text());
        let first_id = first.json::<Value>()["preset"]["id"]
            .as_str()
            .expect("first preset id")
            .to_owned();
        let second = request
            .post("/api/v1/visual-presets")
            .json(&json!({"name": "Replacement", "bundle": dmnote_bundle()}))
            .await;
        assert_eq!(second.status_code(), 200, "{}", second.text());
        let second_id = second.json::<Value>()["preset"]["id"]
            .as_str()
            .expect("second preset id")
            .to_owned();

        let submitted = request
            .post(&format!("/api/v1/runs/{run_id}/submit"))
            .json(&json!({
                "presentation": {"keyviewer_id": first_id, "overlay_id": null}
            }))
            .await;
        assert_eq!(submitted.status_code(), 200, "{}", submitted.text());
        let submitted: Value = submitted.json();
        assert_eq!(submitted["presentation"]["keyviewer_id"], first_id);
        assert_eq!(submitted["presentation"]["overlay_id"], Value::Null);

        let deleted = request
            .delete(&format!("/api/v1/visual-presets/{first_id}"))
            .await;
        assert_eq!(deleted.status_code(), 200, "{}", deleted.text());

        let retry = request.post(&format!("/api/v1/runs/{run_id}/submit")).await;
        assert_eq!(retry.status_code(), 200, "{}", retry.text());
        let retry: Value = retry.json();
        assert_eq!(retry["presentation"]["keyviewer_id"], first_id);

        let changed = request
            .post(&format!("/api/v1/runs/{run_id}/submit"))
            .json(&json!({
                "presentation": {"keyviewer_id": second_id, "overlay_id": null}
            }))
            .await;
        assert_eq!(changed.status_code(), 400);
        assert!(changed.text().contains("visual_selection_conflict"));
    })
    .await;
}

#[tokio::test]
#[serial]
async fn additional_visual_sources_roundtrip_through_database() {
    request::<App, _, _>(|mut request, ctx| async move {
        let owner = Uuid::new_v4().to_string();
        let token = crate::support::authenticate(&ctx, &owner).await;
        auth(&mut request, &token);
        for (source, kind, files) in [
            (
                "impl-dmnote",
                "keyviewer",
                json!({"preset.json": {"keys": {"one": []}}}),
            ),
            (
                "jipper-keyviewer",
                "keyviewer",
                json!({"JipperKeyViewer.json": {"Version": 6, "Data": {}}}),
            ),
            (
                "impl-resourcepack",
                "overlay",
                json!({"ImplResourcePack.json": {"layoutVersion": 1}}),
            ),
        ] {
            let mut bundle = dmnote_bundle();
            bundle["source"] = json!(source);
            bundle["kind"] = json!(kind);
            bundle["files"] = files;
            let created = request
                .post("/api/v1/visual-presets")
                .json(&json!({"name": source, "bundle": bundle}))
                .await;
            assert_eq!(created.status_code(), 200, "{}", created.text());
            assert_eq!(created.json::<Value>()["preset"]["source"], source);
            bundle["kind"] = json!(if kind == "overlay" {
                "keyviewer"
            } else {
                "overlay"
            });
            let rejected = request
                .post("/api/v1/visual-presets")
                .json(&json!({"name": format!("wrong-{source}"), "bundle": bundle}))
                .await;
            assert_eq!(rejected.status_code(), 400);
            assert!(rejected.text().contains("visual_source_unsupported"));
        }
        let listed: Value = request.get("/api/v1/visual-presets").await.json();
        assert_eq!(listed["presets"].as_array().unwrap().len(), 3);
    })
    .await;
}
