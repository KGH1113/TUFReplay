use std::{
    io::Write,
    net::SocketAddr,
    sync::{
        atomic::{AtomicUsize, Ordering},
        Arc,
    },
};

use axum::{routing::get, Json, Router};
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

const CHART_BYTES: &[u8] = br#"{"pathData":"R"}"#;

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
        "client_level_relative_path": "level.adofai"
    })
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
    let listener = TcpListener::bind("127.0.0.1:0").await.expect("bind mock");
    let address = listener.local_addr().expect("mock address");
    let download_url = format!("http://{address}/archive.zip");
    let archive = archive_fixture();
    let downloads = Arc::new(AtomicUsize::new(0));
    let route_downloads = Arc::clone(&downloads);
    let app = Router::new()
        .route(
            "/v2/database/levels/byId/{id}",
            get(move || {
                let download_url = download_url.clone();
                async move {
                    Json(serde_json::json!({
                        "id": 42,
                        "difficulty": {"type":"PGU","name":"G1"},
                        "fileId": "file-42",
                        "dlLink": download_url,
                        "updatedAt": "2026-09-03T00:00:00Z"
                    }))
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
async fn issuance_does_not_download_or_pin_official_chart_originals() {
    request::<App, _, _>(|mut request, ctx| async move {
        let (base_url, downloads, mock) = start_mock_catalog().await;
        let ticket = crate::support::authenticate(&ctx, &Uuid::new_v4().to_string());
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
        ingest.discard(run_id).await.expect("Redis cleanup");

        let mut outdated = valid_request();
        outdated["client_tuf_file_id"] = "file-41".into();
        let response = request.post("/api/v1/runs").json(&outdated).await;
        assert_eq!(response.status_code(), 201);

        let mut mismatch = valid_request();
        mismatch["client_installed_payload_hash_hex"] = "00".repeat(32).into();
        let response = request.post("/api/v1/runs").json(&mismatch).await;
        assert_eq!(response.status_code(), 201);

        let mut unknown_chart = valid_request();
        unknown_chart["client_level_relative_path"] = "other.adofai".into();
        let response = request.post("/api/v1/runs").json(&unknown_chart).await;
        // The claim is checked against actual official bytes only on submit.
        assert_eq!(response.status_code(), 201);
        assert_eq!(
            downloads.load(Ordering::SeqCst),
            0,
            "pre-play issuance must never download an archive"
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
    crate::support::authenticate(&ctx, &owner);
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
