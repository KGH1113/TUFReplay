use super::{archives::process_archive, upstream::canonical_download_url};
use super::{CatalogError, TufCatalogSettings};
use std::{fs::File, path::Path};

use std::io::Write as _;
use zip::write::SimpleFileOptions;

fn test_settings() -> TufCatalogSettings {
    TufCatalogSettings {
        tuf_api_base_url: "http://127.0.0.1:1".to_owned(),
        tuf_metadata_base_url: None,
        artifact_root: "unused".to_owned(),
        artifact_max_download_bytes: 1024 * 1024,
        artifact_max_extracted_bytes: 1024 * 1024,
        artifact_max_files: 20,
        artifact_hydration_timeout_seconds: 10,
        artifact_max_concurrent_hydrations: 1,
    }
}

#[test]
#[ignore = "requires a prepared local official ZIP and recorded runtime hashes"]
fn local_official_selection_matches_recorded_runtime_hashes() {
    use super::{
        archives::process_original_archive, types::TufMetadata,
        validation_chart::select_official_chart,
    };
    let manifest: serde_json::Value = serde_json::from_slice(
        &std::fs::read(std::env::var("LOCAL_CHART_MANIFEST").expect("manifest path")).unwrap(),
    )
    .unwrap();
    let level: i64 = std::env::var("LOCAL_CHART_LEVEL_ID")
        .unwrap()
        .parse()
        .unwrap();
    let fixture = manifest
        .as_array()
        .unwrap()
        .iter()
        .find(|v| v["levelId"].as_i64() == Some(level))
        .unwrap();
    let metadata = TufMetadata {
        file_id: fixture["fileId"].as_str().unwrap().into(),
        download_url: "unused".into(),
        confirmed_chart_path: super::upstream::confirmed_chart_path(&fixture["metadata"]).unwrap(),
    };
    assert!(
        metadata.confirmed_chart_path.is_some(),
        "fixture must have a confirmed chart"
    );
    let tmp = tempfile::tempdir().unwrap();
    let mut settings = test_settings();
    settings.artifact_max_download_bytes = 512 * 1024 * 1024;
    settings.artifact_max_extracted_bytes = 2 * 1024 * 1024 * 1024;
    settings.artifact_max_files = 20000;
    let revision = process_original_archive(
        Path::new(fixture["archive"].as_str().unwrap()),
        tmp.path(),
        &settings,
    )
    .unwrap();
    let chart = select_official_chart(revision, &metadata).unwrap();
    assert_eq!(
        chart.submission_gameplay_hash,
        std::env::var("LOCAL_ALLOWED_HASH").unwrap()
    );
    assert_ne!(
        chart.submission_gameplay_hash,
        std::env::var("LOCAL_DENIED_HASH").unwrap()
    );
}

fn write_zip(path: &Path, entries: &[(&str, &[u8])]) {
    let file = File::create(path).expect("create ZIP");
    let mut archive = zip::ZipWriter::new(file);
    for (name, contents) in entries {
        archive
            .start_file(*name, SimpleFileOptions::default())
            .expect("start ZIP entry");
        archive.write_all(contents).expect("write ZIP entry");
    }
    archive.finish().expect("finish ZIP");
}

#[test]
fn normalizes_google_drive_file_links() {
    let result =
        canonical_download_url("https://drive.google.com/file/d/abc123/view").expect("valid URL");
    assert!(result.contains("id=abc123"));
    assert!(result.starts_with("https://drive.usercontent.google.com/download"));
}

#[test]
fn extracts_multiple_charts_with_tufhelper_paths() {
    let temporary = tempfile::tempdir().expect("temporary directory");
    let archive = temporary.path().join("level.zip");
    write_zip(&archive, &[("a.adofai", b"A"), ("nested/b.adofai", b"B")]);
    let processed = process_archive(
        &archive,
        &temporary.path().join("extracted"),
        &test_settings(),
    )
    .expect("process archive");
    assert_eq!(
        processed
            .charts
            .iter()
            .map(|chart| chart.relative_path.as_str())
            .collect::<Vec<_>>(),
        ["a.adofai", "b.adofai"]
    );

    for chart in processed.charts {
        let expected = if chart.relative_path == "a.adofai" {
            b"A"
        } else {
            b"B"
        };
        assert_eq!(std::fs::read(chart.file_path).unwrap(), expected);
    }
}

#[test]
fn rejects_case_colliding_archive_paths() {
    let temporary = tempfile::tempdir().expect("temporary directory");
    let archive = temporary.path().join("level.zip");
    write_zip(&archive, &[("A.adofai", b"A"), ("a.adofai", b"B")]);
    assert!(matches!(
        process_archive(
            &archive,
            &temporary.path().join("extracted"),
            &test_settings(),
        ),
        Err(CatalogError::UnsafeArchive)
    ));
}

#[test]
fn authoritative_path_uses_original_zip_entries_and_never_client_basename() {
    use super::upstream::confirmed_chart_path;
    use serde_json::json;
    let mut metadata = json!({
        "pathConfirmed":true, "targetLevel":"levels/id/levelEX.adofai",
        "targetLevelRelativePath":"levelEX.adofai",
        "levelFiles":{
            "Merry Christmas/levelEX.adofai":{"path":"levels/id/levelEX.adofai"},
            "Merry Christmas/level.adofai":{"path":"levels/id/level.adofai"}
        }
    });
    assert_eq!(
        confirmed_chart_path(&metadata).unwrap().as_deref(),
        Some("Merry Christmas/levelEX.adofai")
    );
    metadata["pathConfirmed"] = json!(false);
    assert_eq!(confirmed_chart_path(&metadata).unwrap(), None);
    metadata["pathConfirmed"] = json!(true);
    metadata["levelFiles"]["other/levelEX.adofai"] = json!({"path":"levels/id/levelEX.adofai"});
    assert!(matches!(
        confirmed_chart_path(&metadata),
        Err(CatalogError::AmbiguousChart)
    ));
}

#[test]
fn unconfirmed_archives_require_one_gameplay_identity() {
    use super::{
        archives::process_original_archive, types::TufMetadata,
        validation_chart::select_official_chart,
    };
    let tmp = tempfile::tempdir().unwrap();
    let archive = tmp.path().join("level.zip");
    let original = br#"{"settings":{"version":17,"bpm":120,"trackColor":"ff0000"},"angleData":[0,180,90],"actions":[{"floor":0,"eventType":"MoveCamera"}]}"#;
    let visual = br#"{"settings":{"version":17,"bpm":120,"trackColor":"00ff00"},"angleData":[0,180,90],"actions":[]}"#;
    let excerpt = br#"{"settings":{"version":17,"bpm":120},"angleData":[0,180],"actions":[]}"#;
    let mut metadata = TufMetadata {
        file_id: "id".into(),
        download_url: "unused".into(),
        confirmed_chart_path: None,
    };
    write_zip(
        &archive,
        &[
            ("full/main.adofai", original),
            ("nodeco/main.adofai", visual),
        ],
    );
    let revision =
        process_original_archive(&archive, &tmp.path().join("same"), &test_settings()).unwrap();
    assert!(select_official_chart(revision, &metadata).is_ok());
    write_zip(
        &archive,
        &[
            ("full/main.adofai", original),
            ("excerpt/main.adofai", excerpt),
        ],
    );
    let revision =
        process_original_archive(&archive, &tmp.path().join("ambiguous"), &test_settings())
            .unwrap();
    assert!(matches!(
        select_official_chart(revision, &metadata),
        Err(CatalogError::AmbiguousChart)
    ));
    metadata.confirmed_chart_path = Some("full/main.adofai".into());
    let revision =
        process_original_archive(&archive, &tmp.path().join("confirmed"), &test_settings())
            .unwrap();
    assert_eq!(
        select_official_chart(revision, &metadata).unwrap().bytes,
        original
    );
    metadata.confirmed_chart_path = Some("main.adofai".into());
    let revision =
        process_original_archive(&archive, &tmp.path().join("basename"), &test_settings()).unwrap();
    assert!(matches!(
        select_official_chart(revision, &metadata),
        Err(CatalogError::ChartNotFound)
    ));
}

#[test]
fn native_json_compatibility_preserves_official_bytes_and_hashes() {
    use super::{
        archives::process_original_archive, types::TufMetadata,
        validation_chart::select_official_chart,
    };
    use crate::domain::{
        compute_gameplay_hash, submission_gameplay_hash::compute_submission_gameplay_hash,
    };
    let tmp = tempfile::tempdir().unwrap();
    let archive = tmp.path().join("level.zip");
    let canonical = r#"{"settings":{"version":17,"bpm":120,"useLegacyFlash":"Disabled"},"angleData":[0,180],"actions":[],"decorations":[{"eventType":"AddText","decText":"Thanks!\r\n"}]}"#;
    let native = canonical
        .replace("\"Disabled\"}", "\"Disabled\", ,}")
        .replace("Thanks!\\r", "Thanks!\r");
    write_zip(
        &archive,
        &[
            ("main.adofai", native.as_bytes()),
            ("backup.adofai", canonical.as_bytes()),
        ],
    );
    let metadata = TufMetadata {
        file_id: "id".into(),
        download_url: "unused".into(),
        confirmed_chart_path: Some("main.adofai".into()),
    };
    let revision =
        process_original_archive(&archive, &tmp.path().join("valid"), &test_settings()).unwrap();
    let chart = select_official_chart(revision, &metadata).unwrap();
    assert_eq!(chart.bytes, native.as_bytes());
    assert_eq!(
        chart.gameplay_hash,
        compute_gameplay_hash(canonical.as_bytes()).unwrap()
    );
    assert_eq!(
        chart.submission_gameplay_hash,
        compute_submission_gameplay_hash(canonical.as_bytes()).unwrap()
    );
    write_zip(
        &archive,
        &[("main.adofai", b"{\"settings\":{\"bpm\":,120}}")],
    );
    let revision =
        process_original_archive(&archive, &tmp.path().join("invalid"), &test_settings()).unwrap();
    assert!(matches!(
        select_official_chart(revision, &metadata),
        Err(CatalogError::UnsupportedChart)
    ));
}
