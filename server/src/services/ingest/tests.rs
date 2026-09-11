use std::time::Duration;

use redis::streams::StreamRangeReply;
use serial_test::serial;

use super::*;

#[tokio::test]
#[serial]
async fn only_one_play_streams_per_account_and_there_is_no_daily_byte_quota() {
    let store = RunIngestStore::connect(settings(10, 64, 256))
        .await
        .unwrap();
    let owner = Uuid::new_v4().to_string();
    let ids = [Uuid::new_v4(), Uuid::new_v4(), Uuid::new_v4()];
    let deadline = chrono::Utc::now().timestamp() + 60;
    for id in &ids {
        store
            .create_owned_session(*id, "hash", deadline, &owner)
            .await
            .unwrap();
    }
    store.open(ids[0], "hash").await.unwrap();
    assert!(matches!(
        store.open(ids[1], "hash").await,
        Err(IngestError::AccountLimit)
    ));
    // Reconnecting the same play keeps its slot; a completed play releases it.
    store.open(ids[0], "hash").await.unwrap();
    store.append(ids[0], "hash", 0, 0, b"a").await.unwrap();
    store.seal(ids[0], "hash", 0, 1, 0).await.unwrap();
    store.open(ids[1], "hash").await.unwrap();
    store.append(ids[1], "hash", 0, 0, b"a").await.unwrap();
    let quota = format!(
        "tufreplay:daily-bytes:{owner}:{}",
        chrono::Utc::now().timestamp() / 86400
    );
    redis::cmd("SET")
        .arg(&quota)
        .arg(1073741824u64)
        .arg("EX")
        .arg(60)
        .query_async::<()>(&mut store.connection.clone())
        .await
        .unwrap();
    assert!(matches!(
        store.append(ids[1], "hash", 0, 0, b"a").await.unwrap(),
        AppendOutcome::Duplicate { .. }
    ));
    assert!(matches!(
        store.append(ids[1], "hash", 1, 0, b"b").await.unwrap(),
        AppendOutcome::Accepted { .. }
    ));
    for id in ids {
        store.discard(id).await.unwrap();
    }
    redis::cmd("DEL")
        .arg(quota)
        .arg(format!("tufreplay:active:{owner}"))
        .query_async::<i64>(&mut store.connection.clone())
        .await
        .unwrap();
}

fn settings(
    active_ttl_seconds: u64,
    max_chunk_bytes: usize,
    max_session_bytes: u64,
) -> RunIngestSettings {
    RunIngestSettings {
        redis_key_prefix: String::new(),
        redis_url: "redis://127.0.0.1:6379/1".to_owned(),
        active_ttl_seconds,
        sealed_ttl_seconds: 60,
        hard_duration_seconds: 60,
        max_chunk_bytes,
        max_session_bytes,
        heartbeat_interval_ms: 1_000,
    }
}

async fn create_open(store: &RunIngestStore, run_id: Uuid, token: &str) {
    store
        .create_session(run_id, token, chrono::Utc::now().timestamp() + 60)
        .await
        .expect("create ingest session");
    assert_eq!(store.open(run_id, token).await.expect("open session"), -1);
}

#[tokio::test]
#[serial]
async fn append_is_ordered_idempotent_and_sealable() {
    let store = RunIngestStore::connect(settings(10, 64, 256))
        .await
        .expect("connect Redis");
    let run_id = Uuid::new_v4();
    let token = "token-hash";
    create_open(&store, run_id, token).await;

    assert_eq!(
        store
            .append(run_id, token, 0, 0, b"native-a")
            .await
            .expect("append"),
        AppendOutcome::Accepted {
            acknowledged_sequence: 0
        }
    );
    assert_eq!(
        store
            .append(run_id, token, 0, 0, b"native-a")
            .await
            .expect("duplicate"),
        AppendOutcome::Duplicate {
            acknowledged_sequence: 0
        }
    );
    assert_eq!(
        store
            .append(run_id, token, 2, 1, b"gap")
            .await
            .expect("gap"),
        AppendOutcome::Gap {
            expected_sequence: 1
        }
    );
    store
        .append(run_id, token, 1, 0, b"native-b")
        .await
        .expect("second append");

    assert!(matches!(
        store.seal(run_id, token, 0, 2, 0).await,
        Err(IngestError::FinalSequenceMismatch {
            expected_sequence: 2
        })
    ));
    let sealed = store.seal(run_id, token, 1, 2, 0).await.expect("seal");
    assert_eq!(sealed.acknowledged_sequence, 1);
    assert_eq!(sealed.total_payload_bytes, 16);
    assert!(!sealed.already_sealed);
    assert!(
        store
            .seal(run_id, token, 1, 2, 0)
            .await
            .expect("idempotent seal")
            .already_sealed
    );
    assert!(matches!(
        store.append(run_id, token, 2, 0, b"late").await,
        Err(IngestError::InvalidState)
    ));

    let mut connection = store.connection.clone();
    let stream: StreamRangeReply = redis::cmd("XRANGE")
        .arg(chunks_key(run_id))
        .arg("-")
        .arg("+")
        .query_async(&mut connection)
        .await
        .expect("read stream");
    let restored: Vec<u8> = stream
        .ids
        .iter()
        .filter(|entry| entry.get::<u8>("kind") == Some(0))
        .flat_map(|entry| entry.get::<Vec<u8>>("payload").expect("payload"))
        .collect();
    assert_eq!(restored, b"native-anative-b");

    store.discard(run_id).await.expect("cleanup");
}

#[tokio::test]
#[serial]
async fn reconnect_fences_old_connection_and_conflicting_duplicates() {
    let base = RunIngestStore::connect(settings(10, 64, 256))
        .await
        .unwrap();
    let run_id = Uuid::new_v4();
    let token = "fenced-token";
    let old = base.clone().for_connection(Uuid::new_v4());
    create_open(&old, run_id, token).await;
    old.append(run_id, token, 0, 0, b"original").await.unwrap();
    assert!(matches!(
        old.append(run_id, token, 0, 0, b"changed").await,
        Err(IngestError::Conflict)
    ));
    let current = base.clone().for_connection(Uuid::new_v4());
    assert_eq!(current.open(run_id, token).await.unwrap(), 0);
    assert!(old.heartbeat(run_id, token).await.is_err());
    assert!(old.append(run_id, token, 1, 0, b"late").await.is_err());
    current.seal(run_id, token, 0, 1, 0).await.unwrap();
    assert!(base.authorize(run_id, token).await.is_ok());
    assert_eq!(
        base.receipt(run_id, Some(token)).await.unwrap().status,
        "sealed"
    );
    base.discard(run_id).await.unwrap();
}

#[tokio::test]
#[serial]
async fn concurrent_duplicate_is_appended_once() {
    let store = RunIngestStore::connect(settings(10, 64, 256))
        .await
        .expect("connect Redis");
    let run_id = Uuid::new_v4();
    let token = "concurrent-token";
    create_open(&store, run_id, token).await;

    let left = store.clone();
    let right = store.clone();
    let (left, right) = tokio::join!(
        left.append(run_id, token, 0, 0, b"same"),
        right.append(run_id, token, 0, 0, b"same")
    );
    assert!(left.is_ok() && right.is_ok());

    let mut connection = store.connection.clone();
    let length: i64 = redis::cmd("XLEN")
        .arg(chunks_key(run_id))
        .query_async(&mut connection)
        .await
        .expect("stream length");
    assert_eq!(length, 1);
    store.discard(run_id).await.expect("cleanup");
}

#[tokio::test]
#[serial]
async fn enforces_limits_refreshes_ttl_and_expires() {
    let store = RunIngestStore::connect(settings(2, 4, 6))
        .await
        .expect("connect Redis");
    let run_id = Uuid::new_v4();
    let token = "limits-token";
    create_open(&store, run_id, token).await;
    assert!(matches!(
        store.append(run_id, token, 0, 0, b"12345").await,
        Err(IngestError::SessionTooLarge)
    ));
    store
        .append(run_id, token, 0, 0, b"1234")
        .await
        .expect("within chunk limit");
    assert!(matches!(
        store.append(run_id, token, 1, 0, b"123").await,
        Err(IngestError::SessionTooLarge)
    ));

    tokio::time::sleep(Duration::from_millis(1_100)).await;
    store.heartbeat(run_id, token).await.expect("heartbeat");
    let mut connection = store.connection.clone();
    let ttl: i64 = redis::cmd("TTL")
        .arg(meta_key(run_id))
        .query_async(&mut connection)
        .await
        .expect("TTL");
    assert!(ttl >= 1);
    store.discard(run_id).await.expect("cleanup");

    let expiring = RunIngestStore::connect(settings(1, 4, 6))
        .await
        .expect("connect Redis");
    let expired_run_id = Uuid::new_v4();
    expiring
        .create_session(expired_run_id, token, chrono::Utc::now().timestamp() + 60)
        .await
        .expect("create expiring session");
    tokio::time::sleep(Duration::from_millis(1_200)).await;
    assert!(matches!(
        expiring.authorize(expired_run_id, token).await,
        Err(IngestError::NotFound)
    ));
}
