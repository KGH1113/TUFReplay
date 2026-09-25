use std::{
    io::Write,
    net::SocketAddr,
    sync::{
        atomic::{AtomicUsize, Ordering},
        Arc,
    },
};

use axum::{http::StatusCode, routing::get, Json, Router};
use base64::{engine::general_purpose::URL_SAFE_NO_PAD, Engine};
use chrono::{Duration, Utc};
use futures_util::{SinkExt, StreamExt};
use loco_rs::testing::prelude::*;
use sea_orm::{ActiveModelTrait, ActiveValue::Set, DatabaseConnection};
use serial_test::serial;
use sha2::{Digest, Sha256};
use tokio::net::TcpListener;
use tokio_tungstenite::{
    connect_async,
    tungstenite::{client::IntoClientRequest, http::HeaderValue, Message},
};
use tuf_replay_server::app::App;
use tuf_replay_server::models::level_revision_charts;
use tuf_replay_server::models::level_revision_charts::NewLevelRevisionChart;
use tuf_replay_server::models::level_revisions;
use tuf_replay_server::models::run_sessions::Model;
use tuf_replay_server::models::run_sessions::NewRunSession;
use tuf_replay_server::models::run_sessions::RunStatus;
use tuf_replay_server::services::ingest::RunIngestStore;
use tuf_replay_server::services::tuf::catalog::TufCatalogRuntime;
use tuf_replay_server::services::tuf::catalog::TufCatalogSettings;
use uuid::Uuid;
use zip::write::SimpleFileOptions;

const CHART_BYTES: &[u8] =
    br#"{"settings":{"version":17,"bpm":120},"angleData":[0,180],"actions":[]}"#;

fn canonical_payload_hash() -> String {
    let path = b"level.adofai";
    let mut hash = Sha256::new();
    hash.update(
        u32::try_from(path.len())
            .expect("path length")
            .to_le_bytes(),
    );
    hash.update(path);
    hash.update(CHART_BYTES);
    hex::encode(hash.finalize())
}

fn valid_request() -> serde_json::Value {
    serde_json::json!({
        "protocol_version": 1,
        "client_game_version": "2.8.1",
        "client_mod_version": "0.1.0",
        "tuf_level_id": 42,
        "client_tuf_file_id": "file-42",
        "client_installed_payload_hash_hex": canonical_payload_hash(),
        "client_payload_hash_version": 1,
        "client_level_relative_path": "level.adofai",
        "submission_gameplay_hash_version": 1,
        "submission_gameplay_hash_hex": submission_hash()
    })
}

fn submission_hash() -> String {
    tuf_replay_server::domain::submission_gameplay_hash::compute_submission_gameplay_hash(
        CHART_BYTES,
    )
    .unwrap()
}

fn archive_fixture() -> Vec<u8> {
    let mut output = std::io::Cursor::new(Vec::new());
    {
        let mut archive = zip::ZipWriter::new(&mut output);
        archive
            .start_file("level.adofai", SimpleFileOptions::default())
            .expect("start chart");
        archive.write_all(CHART_BYTES).expect("write chart");
        archive.finish().expect("finish ZIP");
    }
    output.into_inner()
}

async fn start_mock_catalog() -> (String, Arc<AtomicUsize>, tokio::task::JoinHandle<()>) {
    start_catalog_with(
        serde_json::json!({"id":42,"diffId":9021}),
        StatusCode::OK,
        serde_json::json!([{"id":9021,"type":"PGU","name":"G1"}]).to_string(),
    )
    .await
}

async fn start_catalog_with(
    level: serde_json::Value,
    difficulty_status: StatusCode,
    difficulty_body: String,
) -> (String, Arc<AtomicUsize>, tokio::task::JoinHandle<()>) {
    start_catalog_archive(level, difficulty_status, difficulty_body, archive_fixture()).await
}

async fn start_catalog_archive(
    level: serde_json::Value,
    difficulty_status: StatusCode,
    difficulty_body: String,
    archive: Vec<u8>,
) -> (String, Arc<AtomicUsize>, tokio::task::JoinHandle<()>) {
    let listener = TcpListener::bind("127.0.0.1:0").await.expect("bind mock");
    let address = listener.local_addr().expect("mock address");
    let download_url = format!("http://{address}/archive.zip");
    let downloads = Arc::new(AtomicUsize::new(0));
    let route_downloads = Arc::clone(&downloads);
    let app = Router::new()
        .route(
            "/v2/database/levels/byId/{id}",
            get(move || {
                let download_url = download_url.clone();
                let level = level.clone();
                async move {
                    let mut metadata = serde_json::json!({
                        "fileId": "file-42",
                        "dlLink": download_url,
                        "updatedAt": "2026-09-03T00:00:00Z"
                    });
                    metadata
                        .as_object_mut()
                        .unwrap()
                        .extend(level.as_object().unwrap().clone());
                    Json(metadata)
                }
            }),
        )
        .route(
            "/v2/database/difficulties",
            get(move || {
                let body = difficulty_body.clone();
                async move {
                    (
                        difficulty_status,
                        [("content-type", "application/json")],
                        body,
                    )
                }
            }),
        )
        .route(
            "/archive.zip",
            get(move || {
                let archive = archive.clone();
                let downloads = Arc::clone(&route_downloads);
                async move {
                    downloads.fetch_add(1, Ordering::SeqCst);
                    archive
                }
            }),
        );
    let server = tokio::spawn(async move {
        axum::serve(listener, app)
            .await
            .expect("serve mock catalog");
    });
    (format!("http://{address}"), downloads, server)
}

#[tokio::test]
#[serial]
async fn ambiguous_reference_and_missing_identity_never_create_submission_records() {
    use sea_orm::{EntityTrait, PaginatorTrait};
    request::<App, _, _>(|mut request, ctx| async move {
        let mut zip = zip::ZipWriter::new(std::io::Cursor::new(Vec::new()));
        zip.start_file("normal.adofai", SimpleFileOptions::default())
            .unwrap();
        zip.write_all(CHART_BYTES).unwrap();
        zip.start_file("EX.adofai", SimpleFileOptions::default())
            .unwrap();
        zip.write_all(br#"{"settings":{"version":17,"bpm":180},"angleData":[0,90],"actions":[]}"#)
            .unwrap();
        let archive = zip.finish().unwrap().into_inner();
        let (base_url, downloads, mock) = start_catalog_archive(
            serde_json::json!({"id":42,"diffId":1}),
            StatusCode::OK,
            serde_json::json!([{"id":1,"type":"PGU","name":"P1"}]).to_string(),
            archive,
        )
        .await;
        let ticket = crate::support::authenticate(&ctx, &Uuid::new_v4().to_string()).await;
        request.add_header(
            axum::http::header::AUTHORIZATION,
            format!("Bearer {ticket}"),
        );
        let root = tempfile::tempdir().unwrap();
        ctx.shared_store.insert(
            TufCatalogRuntime::new(TufCatalogSettings {
                tuf_api_base_url: base_url,
                artifact_root: root.path().to_string_lossy().into(),
                artifact_max_download_bytes: 1024 * 1024,
                artifact_max_extracted_bytes: 1024 * 1024,
                artifact_max_files: 20,
                artifact_hydration_timeout_seconds: 5,
                artifact_max_concurrent_hydrations: 2,
            })
            .unwrap(),
        );
        let before = tuf_replay_server::models::run_sessions::Entity::find()
            .count(&ctx.db)
            .await
            .unwrap();
        let records_before = tuf_replay_server::models::run_submission_records::Entity::find()
            .count(&ctx.db)
            .await
            .unwrap();
        let mut missing = valid_request();
        missing
            .as_object_mut()
            .unwrap()
            .remove("submission_gameplay_hash_hex");
        let response = request.post("/api/v1/runs").json(&missing).await;
        assert_eq!(response.status_code(), 422);
        assert_eq!(
            response.json::<serde_json::Value>()["code"],
            "submission_chart_identity_missing_or_unsupported"
        );
        assert_eq!(downloads.load(Ordering::SeqCst), 0);
        let response = request.post("/api/v1/runs").json(&valid_request()).await;
        assert_eq!(response.status_code(), 422);
        assert_eq!(
            response.json::<serde_json::Value>()["code"],
            "official_chart_ambiguous"
        );
        assert_eq!(
            tuf_replay_server::models::run_sessions::Entity::find()
                .count(&ctx.db)
                .await
                .unwrap(),
            before
        );
        assert_eq!(
            tuf_replay_server::models::run_submission_records::Entity::find()
                .count(&ctx.db)
                .await
                .unwrap(),
            records_before
        );
        assert_eq!(
            std::fs::read_dir(root.path()).unwrap().count(),
            0,
            "No replay or persistent chart artifacts are saved for denied runs"
        );
        mock.abort();
    })
    .await;
}

#[tokio::test]
#[serial]
async fn issuance_resolves_official_difficulty_ids_and_distinguishes_catalog_failures() {
    request::<App, _, _>(|mut request, ctx| async move {
        let ticket = crate::support::authenticate(&ctx, &Uuid::new_v4().to_string()).await;
        request.add_header(
            axum::http::header::AUTHORIZATION,
            format!("Bearer {ticket}").parse::<axum::http::HeaderValue>().unwrap(),
        );
        let ingest = ctx.shared_store.get::<RunIngestStore>().expect("ingest store");
        let level = serde_json::json!({"id":8068,"diffId":2,"rating":{"averageDifficultyId":2}});
        let list = serde_json::json!([{"id":2,"type":"PGU","name":"P2"}]);
        let mut cases = vec![("real #8068/P2".to_owned(), level.clone(), StatusCode::OK, list.to_string(), 201)];
        // IDs are opaque: even a large catalog ID may name an eligible P/G tier.
        for name in ["P1", "P20", "G1", "G20"] {
            cases.push((name.to_owned(), serde_json::json!({"id":8068,"diffId":9001}), StatusCode::OK,
                serde_json::json!([{"id":9001,"type":"PGU","name":name}]).to_string(), 201));
        }
        for (kind, name) in [("SPECIAL", "P2"), ("LEGACY", "G1"), ("PGU", "U1"),
            ("PGU", "P0"), ("PGU", "P21"), ("PGU", "G0"), ("PGU", "G21")] {
            cases.push((format!("{kind}/{name}"), level.clone(), StatusCode::OK,
                serde_json::json!([{"id":2,"type":kind,"name":name}]).to_string(), 422));
        }
        for malformed in [serde_json::Value::Null, serde_json::json!("2"), serde_json::json!(2.5),
            serde_json::json!(true), serde_json::json!({"id":2})] {
            cases.push((format!("invalid diffId: {malformed}"), serde_json::json!({"id":8068,"diffId":malformed}),
                StatusCode::OK, list.to_string(), 503));
        }
        cases.push(("missing diffId must not use rating or embedded difficulty".to_owned(),
            serde_json::json!({"id":8068,"rating":{"averageDifficultyId":2},"difficulty":{"type":"PGU","name":"P2"}}),
            StatusCode::OK, list.to_string(), 503));
        for malformed in [serde_json::json!({"data":list}), serde_json::Value::Null, serde_json::json!([]),
            serde_json::json!([{"id":3,"type":"PGU","name":"P2"}]),
            serde_json::json!([{"id":"2","type":"PGU","name":"P2"}]),
            serde_json::json!([{"id":2,"name":"P2"}]),
            serde_json::json!([{"id":2,"type":"PGU"}]),
            serde_json::json!([{"id":2,"type":null,"name":"P2"}]),
            serde_json::json!([{"id":2,"type":"PGU","name":2}]),
            serde_json::json!([{"id":2,"type":"","name":"P2"}]),
            serde_json::json!([{"id":2,"type":"PGU","name":""}]),
            serde_json::json!([{"id":2,"type":"PGU","name":"P2"},{"id":2,"type":"PGU","name":"U1"}])] {
            cases.push((format!("invalid catalog: {malformed}"), level.clone(), StatusCode::OK, malformed.to_string(), 503));
        }
        cases.push(("invalid catalog JSON".to_owned(), level.clone(), StatusCode::OK, "not JSON".into(), 503));
        cases.push(("catalog HTTP failure".to_owned(), level, StatusCode::SERVICE_UNAVAILABLE, list.to_string(), 503));
        for (label, level, status, body, expected) in cases {
            let (base_url, downloads, mock) = start_catalog_with(level, status, body).await;
            ctx.shared_store.insert(TufCatalogRuntime::new(TufCatalogSettings {
                tuf_api_base_url: base_url,
                artifact_root: "unused-in-memory".to_owned(),
                artifact_max_download_bytes: 1024 * 1024,
                artifact_max_extracted_bytes: 1024 * 1024,
                artifact_max_files: 20,
                artifact_hydration_timeout_seconds: 10,
                artifact_max_concurrent_hydrations: 2,
            }).expect("catalog runtime"));
            let mut input = valid_request();
            input["tuf_level_id"] = 8068.into();
            let response = request.post("/api/v1/runs").json(&input).await;
            assert_eq!(response.status_code(), expected, "{label}: {}", response.text());
            let result: serde_json::Value = response.json();
            if expected == 201 {
                let run_id = result["run_id"].as_str().unwrap().parse::<Uuid>().unwrap();
                ingest.discard(run_id).await.expect("Redis cleanup");
            } else {
                assert_eq!(result["code"], if expected == 422 { "level_not_eligible" } else { "catalog_unavailable" }, "{label}");
            }
            assert_eq!(downloads.load(Ordering::SeqCst), usize::from(expected == 201), "{label}: only eligible levels may fetch official bytes");
            mock.abort();
        }
    }).await;
}

#[tokio::test]
#[serial]
async fn issuance_checks_loaded_chart_before_persisting_a_run() {
    request::<App, _, _>(|mut request, ctx| async move {
        let (base_url, downloads, mock) = start_mock_catalog().await;
        let ticket = crate::support::authenticate(&ctx, &Uuid::new_v4().to_string()).await;
        request.add_header(
            axum::http::header::AUTHORIZATION,
            format!("Bearer {ticket}")
                .parse::<axum::http::HeaderValue>()
                .unwrap(),
        );
        let ingest = ctx
            .shared_store
            .get::<RunIngestStore>()
            .expect("ingest store");
        ctx.shared_store.insert(
            TufCatalogRuntime::new(TufCatalogSettings {
                tuf_api_base_url: base_url,
                artifact_root: "unused-in-memory".to_owned(),
                artifact_max_download_bytes: 1024 * 1024,
                artifact_max_extracted_bytes: 1024 * 1024,
                artifact_max_files: 20,
                artifact_hydration_timeout_seconds: 10,
                artifact_max_concurrent_hydrations: 2,
            })
            .expect("catalog runtime"),
        );

        let response = request.post("/api/v1/runs").json(&valid_request()).await;
        assert_eq!(response.status_code(), 201, "{}", response.text());
        let body: serde_json::Value = response.json();
        let run_id: Uuid = body["run_id"]
            .as_str()
            .expect("run id")
            .parse()
            .expect("run UUID");
        assert_eq!(
            body["websocket_url"],
            format!("/api/v1/runs/{run_id}/stream")
        );
        assert_eq!(body["lease_duration_ms"], 45_000);
        let run = Model::find_by_pid(&ctx.db, run_id)
            .await
            .expect("stored run");
        assert_eq!(run.tuf_level_id, 42);
        assert!(run.lease_expires_at < run.hard_expires_at);
        assert_eq!(run.client_game_version, "2.8.1");
        assert_eq!(run.level_revision_id, None);
        assert_eq!(run.level_revision_chart_id, None);
        assert_eq!(run.client_level_relative_path, "level.adofai");
        assert_eq!(
            run.chart_admission.as_ref().unwrap()["gameplay_hash"],
            submission_hash()
        );
        ingest.discard(run_id).await.expect("Redis cleanup");

        let mut outdated = valid_request();
        outdated["client_tuf_file_id"] = "file-41".into();
        let response = request.post("/api/v1/runs").json(&outdated).await;
        assert_eq!(response.status_code(), 422);
        assert_eq!(
            response.json::<serde_json::Value>()["code"],
            "level_revision_outdated"
        );

        let mut mismatch = valid_request();
        mismatch["submission_gameplay_hash_hex"] = "00".repeat(32).into();
        let response = request.post("/api/v1/runs").json(&mismatch).await;
        assert_eq!(response.status_code(), 422);
        assert_eq!(
            response.json::<serde_json::Value>()["code"],
            "submission_chart_gameplay_mismatch"
        );

        let mut unknown_chart = valid_request();
        unknown_chart["client_level_relative_path"] = "other.adofai".into();
        let response = request.post("/api/v1/runs").json(&unknown_chart).await;
        // Client filenames do not select the official reference. Matching gameplay is allowed.
        assert_eq!(response.status_code(), 201);
        assert_eq!(
            downloads.load(Ordering::SeqCst),
            1,
            "official bytes are cached across starts without trusting client filenames"
        );

        let mut old_contract = valid_request();
        old_contract["start_tile"] = 0.into();
        assert_eq!(
            request
                .post("/api/v1/runs")
                .json(&old_contract)
                .await
                .status_code(),
            422
        );
        mock.abort();
    })
    .await;
}

pub(crate) async fn canonical_level(db: &DatabaseConnection) -> (i64, i64) {
    let revision = level_revisions::ActiveModel {
        tuf_level_id: Set(42),
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
    .expect("revision");
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
    .expect("chart");
    (revision.id, chart.id)
}

#[tokio::test]
#[serial]
async fn websocket_reconnects_from_authoritative_ack_and_seals() {
    let boot = boot_test::<App>().await.expect("test app boot");
    let ctx = boot.app_context;
    let store = ctx
        .shared_store
        .get::<RunIngestStore>()
        .expect("ingest store");
    let (revision_id, chart_id) = canonical_level(&ctx.db).await;
    let run_id = Uuid::new_v4();
    let owner = Uuid::new_v4().to_string();
    crate::support::authenticate(&ctx, &owner).await;
    let upload_token = URL_SAFE_NO_PAD.encode([7_u8; 32]);
    let token_hash = Sha256::digest(upload_token.as_bytes());
    let token_hash_hex = hex::encode(token_hash);
    let now = Utc::now();
    let hard_expires_at = now + Duration::minutes(5);

    Model::create(
        &ctx.db,
        NewRunSession {
            pid: run_id,
            protocol_version: 1,
            client_game_version: "2.8.1".to_owned(),
            client_mod_version: "0.1.0".to_owned(),
            tuf_level_id: 42,
            level_revision_id: Some(revision_id),
            level_revision_chart_id: Some(chart_id),
            client_tuf_file_id: "fixture".into(),
            client_level_relative_path: "level.adofai".into(),
            upload_token_hash: token_hash.to_vec(),
            lease_expires_at: (now + Duration::seconds(45)).fixed_offset(),
            hard_expires_at: hard_expires_at.fixed_offset(),
        },
    )
    .await
    .expect("create PostgreSQL session");
    let run = Model::find_by_pid(&ctx.db, run_id).await.unwrap();
    use sea_orm::IntoActiveModel;
    let mut admitted = run.clone().into_active_model();
    admitted.chart_admission = Set(Some(
        serde_json::json!({"file_id":run.client_tuf_file_id,"hash_version":1,"gameplay_hash":submission_hash()}),
    ));
    admitted.update(&ctx.db).await.unwrap();
    tuf_replay_server::models::run_submission_records::Entity::create_authorized(
        &ctx.db,
        run.id,
        &owner,
        Some(crate::support::GRANT),
    )
    .await
    .unwrap();
    store
        .create_session(run_id, &token_hash_hex, hard_expires_at.timestamp())
        .await
        .expect("create Redis session");

    let listener = TcpListener::bind("127.0.0.1:0")
        .await
        .expect("bind test server");
    let address = listener.local_addr().expect("test server address");
    let router = boot.router.expect("test router");
    let server = tokio::spawn(async move {
        axum::serve(
            listener,
            router.into_make_service_with_connect_info::<SocketAddr>(),
        )
        .await
        .expect("serve test router");
    });
    let url = format!("ws://{address}/api/v1/runs/{run_id}/stream");

    let mut unauthorized = url.as_str().into_client_request().expect("request");
    unauthorized.headers_mut().insert(
        "Authorization",
        HeaderValue::from_static("Bearer wrong-token"),
    );
    let error = connect_async(unauthorized)
        .await
        .expect_err("invalid token must fail");
    match error {
        tokio_tungstenite::tungstenite::Error::Http(response) => {
            assert_eq!(response.status(), 401);
        }
        error => panic!("unexpected upgrade error: {error}"),
    }

    let mut first = connect(&url, &upload_token).await;
    send_json(
        &mut first,
        serde_json::json!({"type":"hello","protocol_version":1,"last_acknowledged_sequence":-1}),
    )
    .await;
    assert_eq!(receive_json(&mut first).await["type"], "ready");
    first
        .send(Message::Binary(binary_chunk(0, 0, b"first").into()))
        .await
        .expect("first chunk");
    assert_eq!(receive_json(&mut first).await["acknowledged_sequence"], 0);
    drop(first);

    let mut second = connect(&url, &upload_token).await;
    send_json(
        &mut second,
        serde_json::json!({"type":"hello","protocol_version":1,"last_acknowledged_sequence":-1}),
    )
    .await;
    assert_eq!(receive_json(&mut second).await["acknowledged_sequence"], 0);
    second
        .send(Message::Binary(binary_chunk(1, 1, b"second").into()))
        .await
        .expect("second chunk");
    assert_eq!(receive_json(&mut second).await["acknowledged_sequence"], 1);
    send_json(
        &mut second,
        serde_json::json!({"type":"complete","final_sequence":1,"input_count":1,"hit_context_count":1}),
    )
    .await;
    let sealed = receive_json(&mut second).await;
    assert_eq!(sealed["type"], "sealed");

    let model = Model::find_by_pid(&ctx.db, run_id)
        .await
        .expect("sealed run");
    assert_eq!(model.status, RunStatus::Sealed.as_str());
    assert!(model.sealed_at.is_some());
    assert_eq!(model.level_revision_id, Some(revision_id));
    assert_eq!(model.level_revision_chart_id, Some(chart_id));

    store.discard(run_id).await.expect("cleanup Redis");
    server.abort();
}

async fn connect(
    url: &str,
    upload_token: &str,
) -> tokio_tungstenite::WebSocketStream<tokio_tungstenite::MaybeTlsStream<tokio::net::TcpStream>> {
    let mut request = url.into_client_request().expect("WebSocket request");
    request.headers_mut().insert(
        "Authorization",
        HeaderValue::from_str(&format!("Bearer {upload_token}")).expect("authorization header"),
    );
    connect_async(request).await.expect("WebSocket upgrade").0
}

async fn send_json<S>(socket: &mut tokio_tungstenite::WebSocketStream<S>, value: serde_json::Value)
where
    S: tokio::io::AsyncRead + tokio::io::AsyncWrite + Unpin,
{
    socket
        .send(Message::Text(value.to_string().into()))
        .await
        .expect("send control message");
}

async fn receive_json<S>(socket: &mut tokio_tungstenite::WebSocketStream<S>) -> serde_json::Value
where
    S: tokio::io::AsyncRead + tokio::io::AsyncWrite + Unpin,
{
    let message = socket
        .next()
        .await
        .expect("server message")
        .expect("valid server message");
    serde_json::from_str(message.to_text().expect("text message")).expect("JSON control")
}

fn binary_chunk(kind: u8, sequence: u64, payload: &[u8]) -> Vec<u8> {
    let mut bytes = Vec::from(*b"TUFR");
    bytes.extend([1, kind, 0, 0]);
    bytes.extend(sequence.to_be_bytes());
    bytes.extend(u32::try_from(payload.len()).expect("length").to_be_bytes());
    bytes.extend(payload);
    bytes
}

fn level_start(id: Uuid) -> serde_json::Value {
    serde_json::json!({"type":"run_start", "run_id":id,
        "client_game_version":"2.8.1", "client_mod_version":"session-test",
        "client_tuf_file_id":"file-42", "client_level_relative_path":"level.adofai",
        "submission_gameplay_hash_version":1,"submission_gameplay_hash_hex":submission_hash(),
        "last_acknowledged_sequence":-1})
}

fn level_chunk(id: Uuid, sequence: u64) -> Vec<u8> {
    let mut frame = b"TUF2".to_vec();
    frame.extend(id.as_bytes());
    frame.extend(binary_chunk(0, sequence, b"1,1,1\n"));
    frame
}

async fn connect_level(
    url: &str,
    token: &str,
) -> tokio_tungstenite::WebSocketStream<tokio_tungstenite::MaybeTlsStream<tokio::net::TcpStream>> {
    let mut socket = connect(url, token).await;
    send_json(
        &mut socket,
        serde_json::json!({"type":"session_hello","protocol_version":2}),
    )
    .await;
    assert_eq!(receive_json(&mut socket).await["type"], "session_ready");
    socket
}

#[tokio::test]
#[serial]
async fn reusable_level_socket_has_no_idle_runs_and_survives_immediate_restarts() {
    use sea_orm::{EntityTrait, PaginatorTrait};
    let boot = boot_test::<App>().await.unwrap();
    let ctx = boot.app_context;
    let owner = Uuid::new_v4().to_string();
    let token = crate::support::authenticate(&ctx, &owner).await;
    let store = ctx.shared_store.get::<RunIngestStore>().unwrap();
    let (base_url, downloads, catalog) = start_mock_catalog().await;
    ctx.shared_store.insert(
        TufCatalogRuntime::new(TufCatalogSettings {
            tuf_api_base_url: base_url,
            artifact_root: "unused-in-memory".into(),
            artifact_max_download_bytes: 1024 * 1024,
            artifact_max_extracted_bytes: 1024 * 1024,
            artifact_max_files: 20,
            artifact_hydration_timeout_seconds: 10,
            artifact_max_concurrent_hydrations: 2,
        })
        .unwrap(),
    );
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let url = format!(
        "ws://{}/api/v2/levels/42/runs/stream",
        listener.local_addr().unwrap()
    );
    let router = boot.router.unwrap();
    let server = tokio::spawn(async move {
        axum::serve(listener, router).await.unwrap();
    });
    let count_before = tuf_replay_server::models::run_sessions::Entity::find()
        .count(&ctx.db)
        .await
        .unwrap();
    let mut socket = connect(&url, &token).await;
    send_json(
        &mut socket,
        serde_json::json!({"type":"session_hello","protocol_version":2}),
    )
    .await;
    assert_eq!(receive_json(&mut socket).await["type"], "session_ready");
    for _ in 0..20 {
        send_json(&mut socket, serde_json::json!({"type":"heartbeat"})).await;
        assert_eq!(receive_json(&mut socket).await["type"], "heartbeat");
    }
    assert_eq!(
        tuf_replay_server::models::run_sessions::Entity::find()
            .count(&ctx.db)
            .await
            .unwrap(),
        count_before
    );
    // An idle connection may warm the reference, but never creates a run.
    assert!(downloads.load(Ordering::SeqCst) <= 1);
    // Denied starts never allocate a run, Redis evidence stream, or append any bytes.
    for (field, value, code) in [
        (
            "submission_gameplay_hash_hex",
            serde_json::json!("0".repeat(64)),
            "submission_chart_gameplay_mismatch",
        ),
        (
            "submission_gameplay_hash_version",
            serde_json::json!(99),
            "submission_chart_identity_missing_or_unsupported",
        ),
        (
            "client_tuf_file_id",
            serde_json::json!("old-file"),
            "level_revision_outdated",
        ),
    ] {
        let rejected = Uuid::new_v4();
        let mut start = level_start(rejected);
        start[field] = value;
        send_json(&mut socket, start).await;
        assert_eq!(receive_json(&mut socket).await["code"], code);
        assert!(Model::find_by_pid(&ctx.db, rejected).await.is_err());
        assert!(store.receipt(rejected, None).await.is_err());
        socket
            .send(Message::Binary(level_chunk(rejected, 0).into()))
            .await
            .unwrap();
        assert_eq!(receive_json(&mut socket).await["code"], "run_not_active");
    }
    assert_eq!(
        tuf_replay_server::models::run_sessions::Entity::find()
            .count(&ctx.db)
            .await
            .unwrap(),
        count_before
    );
    let mut previous = None;
    for _ in 0..12 {
        let id = Uuid::new_v4();
        send_json(&mut socket, level_start(id)).await;
        assert_eq!(receive_json(&mut socket).await["type"], "ready");
        // Replayed fail from an older attempt must not detach this attempt.
        if let Some(old) = previous {
            send_json(
                &mut socket,
                serde_json::json!({"type":"run_fail", "run_id":old}),
            )
            .await;
            assert_eq!(receive_json(&mut socket).await["type"], "failed");
        }
        socket
            .send(Message::Binary(level_chunk(id, 0).into()))
            .await
            .unwrap();
        assert_eq!(receive_json(&mut socket).await["acknowledged_sequence"], 0);
        send_json(
            &mut socket,
            serde_json::json!({"type":"run_fail", "run_id":id}),
        )
        .await;
        assert_eq!(receive_json(&mut socket).await["type"], "failed");
        assert_eq!(
            Model::find_by_pid(&ctx.db, id).await.unwrap().status,
            "failed"
        );
        assert!(
            store.receipt(id, None).await.is_err(),
            "failed evidence must be released before the next start"
        );
        previous = Some(id);
    }
    let id = Uuid::new_v4();
    send_json(&mut socket, level_start(id)).await;
    assert_eq!(receive_json(&mut socket).await["type"], "ready");
    socket
        .send(Message::Binary(level_chunk(id, 0).into()))
        .await
        .unwrap();
    // Deliberately leave the ACK unread and reconnect, keeping the stale socket open.
    let mut resumed = connect(&url, &token).await;
    send_json(
        &mut resumed,
        serde_json::json!({"type":"session_hello","protocol_version":2}),
    )
    .await;
    assert_eq!(receive_json(&mut resumed).await["type"], "session_ready");
    send_json(&mut resumed, level_start(id)).await;
    assert_eq!(receive_json(&mut resumed).await["acknowledged_sequence"], 0);
    let mut changed_hash = level_start(id);
    changed_hash["submission_gameplay_hash_hex"] = serde_json::json!("0".repeat(64));
    send_json(&mut resumed, changed_hash).await;
    assert_eq!(
        receive_json(&mut resumed).await["code"],
        "run_start_conflict"
    );
    assert_eq!(receive_json(&mut socket).await["acknowledged_sequence"], 0);
    socket
        .send(Message::Binary(level_chunk(id, 1).into()))
        .await
        .unwrap();
    assert_eq!(receive_json(&mut socket).await["code"], "stream_conflict");
    drop(socket);
    send_json(&mut resumed, level_start(id)).await;
    assert_eq!(receive_json(&mut resumed).await["acknowledged_sequence"], 0);
    let mut conflict = level_start(id);
    conflict["client_tuf_file_id"] = "changed-file".into();
    send_json(&mut resumed, conflict).await;
    assert_eq!(
        receive_json(&mut resumed).await["code"],
        "run_start_conflict"
    );
    resumed
        .send(Message::Binary(level_chunk(Uuid::new_v4(), 1).into()))
        .await
        .unwrap();
    assert_eq!(receive_json(&mut resumed).await["code"], "run_not_active");
    let complete = serde_json::json!({"type":"run_complete","run_id":id,"final_sequence":0,"input_count":1,"hit_context_count":0});
    send_json(&mut resumed, complete.clone()).await;
    assert_eq!(receive_json(&mut resumed).await["type"], "sealed");
    send_json(
        &mut resumed,
        serde_json::json!({"type":"run_fail","run_id":id}),
    )
    .await;
    assert_eq!(
        receive_json(&mut resumed).await["type"],
        "sealed",
        "late fail must not undo a complete"
    );
    send_json(&mut resumed, complete).await;
    assert_eq!(receive_json(&mut resumed).await["type"], "sealed");
    let mut wrong_level = connect_level(&url.replace("/42/", "/43/"), &token).await;
    send_json(
        &mut wrong_level,
        serde_json::json!({"type":"run_fail","run_id":id}),
    )
    .await;
    assert_eq!(
        receive_json(&mut wrong_level).await["code"],
        "run_not_found"
    );
    drop(wrong_level);
    assert_eq!(
        tuf_replay_server::models::run_sessions::Entity::find()
            .count(&ctx.db)
            .await
            .unwrap(),
        count_before + 13
    );
    let lost = Uuid::new_v4();
    send_json(&mut resumed, level_start(lost)).await;
    assert_eq!(receive_json(&mut resumed).await["type"], "ready");
    resumed
        .send(Message::Binary(level_chunk(lost, 0).into()))
        .await
        .unwrap();
    assert_eq!(receive_json(&mut resumed).await["acknowledged_sequence"], 0);
    drop(resumed);
    let mut resumed = connect_level(&url, &token).await;
    send_json(&mut resumed, level_start(lost)).await;
    assert_eq!(receive_json(&mut resumed).await["acknowledged_sequence"], 0);
    drop(resumed);
    // Redis loss/TTL expiry must not silently recreate an issued run without its evidence.
    store.discard(lost).await.unwrap();
    let mut resumed = connect_level(&url, &token).await;
    send_json(&mut resumed, level_start(lost)).await;
    assert_eq!(
        receive_json(&mut resumed).await["code"],
        "run_evidence_expired"
    );
    let active = Uuid::new_v4();
    send_json(&mut resumed, level_start(active)).await;
    assert_eq!(receive_json(&mut resumed).await["type"], "ready");
    // Revoke only local DB membership; the OAuth provider still permits this account.
    crate::support::set_tester(&ctx, &owner, false).await;
    let revoked = tokio::time::timeout(
        std::time::Duration::from_secs(7),
        receive_json(&mut resumed),
    )
    .await
    .unwrap();
    assert_eq!(revoked["code"], "submission_authorization_unavailable");
    assert_eq!(
        Model::find_by_pid(&ctx.db, active).await.unwrap().status,
        "failed"
    );
    drop(resumed);
    let other_token = crate::support::authenticate(&ctx, &Uuid::new_v4().to_string()).await;
    let mut other = connect_level(&url, &other_token).await;
    send_json(&mut other, level_start(id)).await;
    assert_eq!(receive_json(&mut other).await["code"], "run_owner_mismatch");
    send_json(
        &mut other,
        serde_json::json!({"type":"run_fail","run_id":id}),
    )
    .await;
    assert_eq!(receive_json(&mut other).await["code"], "run_not_found");
    drop(other);
    let mut unsupported = connect(&url, &other_token).await;
    send_json(
        &mut unsupported,
        serde_json::json!({"type":"session_hello","protocol_version":1}),
    )
    .await;
    assert_eq!(
        receive_json(&mut unsupported).await["code"],
        "unsupported_protocol"
    );
    drop(unsupported);
    store.discard(id).await.unwrap();
    server.abort();
    catalog.abort();
}
