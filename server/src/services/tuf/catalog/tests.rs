use super::{archives::process_archive, upstream::canonical_download_url};
use super::{CatalogError, TufCatalogSettings};
use std::{fs::File, path::Path};

use std::io::Write as _;
use zip::write::SimpleFileOptions;

fn test_settings() -> TufCatalogSettings {
    TufCatalogSettings {
        tuf_api_base_url: "http://127.0.0.1:1".to_owned(),
        artifact_root: "unused".to_owned(),
        artifact_max_download_bytes: 1024 * 1024,
        artifact_max_extracted_bytes: 1024 * 1024,
        artifact_max_files: 20,
        artifact_hydration_timeout_seconds: 10,
        artifact_max_concurrent_hydrations: 1,
    }
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
