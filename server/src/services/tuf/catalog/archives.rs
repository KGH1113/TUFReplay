use super::archive_paths::{collect_files, flatten_leaf_files, ordinal_cmp, relative_string};
use super::types::{ProcessedChart, ProcessedRevision};
use super::{CatalogError, TufCatalogSettings};
use crate::models::level_revision_charts;
use std::{
    collections::HashSet,
    fs::{self, File},
    io,
    path::Path,
};
use zip::ZipArchive;

pub(super) fn process_archive(
    archive_path: &Path,
    extract_root: &Path,
    settings: &TufCatalogSettings,
) -> Result<ProcessedRevision, CatalogError> {
    fs::create_dir_all(extract_root).map_err(|_| CatalogError::UnsafeArchive)?;
    let archive_file = File::open(archive_path).map_err(|_| CatalogError::UnsafeArchive)?;
    let mut archive = ZipArchive::new(archive_file).map_err(|_| CatalogError::UnsafeArchive)?;
    if archive.len() > settings.artifact_max_files {
        return Err(CatalogError::ArtifactTooLarge);
    }
    let mut extracted_bytes = 0_u64;
    let mut extracted_paths = HashSet::new();
    for index in 0..archive.len() {
        let mut entry = archive
            .by_index(index)
            .map_err(|_| CatalogError::UnsafeArchive)?;
        if entry
            .unix_mode()
            .is_some_and(|mode| mode & 0o170_000 == 0o120_000)
        {
            return Err(CatalogError::UnsafeArchive);
        }
        let relative = entry
            .enclosed_name()
            .ok_or(CatalogError::UnsafeArchive)?
            .to_path_buf();
        if relative.to_str().is_none() {
            return Err(CatalogError::UnsafeArchive);
        }
        let collision_key = relative.to_string_lossy().replace('\\', "/").to_lowercase();
        if !extracted_paths.insert(collision_key) {
            return Err(CatalogError::UnsafeArchive);
        }
        let destination = extract_root.join(relative);
        if entry.is_dir() {
            fs::create_dir_all(&destination).map_err(|_| CatalogError::UnsafeArchive)?;
            continue;
        }
        extracted_bytes = extracted_bytes
            .checked_add(entry.size())
            .ok_or(CatalogError::ArtifactTooLarge)?;
        if extracted_bytes > settings.artifact_max_extracted_bytes {
            return Err(CatalogError::ArtifactTooLarge);
        }
        if let Some(parent) = destination.parent() {
            fs::create_dir_all(parent).map_err(|_| CatalogError::UnsafeArchive)?;
        }
        let mut output = File::create(&destination).map_err(|_| CatalogError::UnsafeArchive)?;
        let copied = io::copy(&mut entry, &mut output).map_err(|_| CatalogError::UnsafeArchive)?;
        if copied != entry.size() {
            return Err(CatalogError::UnsafeArchive);
        }
    }

    flatten_leaf_files(extract_root, extract_root)?;
    let mut files = collect_files(extract_root)?;
    files.sort_by(|left, right| {
        ordinal_cmp(
            &relative_string(extract_root, left),
            &relative_string(extract_root, right),
        )
    });
    let mut charts = Vec::new();
    for file in files {
        if file
            .extension()
            .and_then(|extension| extension.to_str())
            .is_some_and(|extension| extension.eq_ignore_ascii_case("adofai"))
        {
            let relative_path = level_revision_charts::normalize_relative_chart_path(
                &relative_string(extract_root, &file),
            )
            .map_err(|_| CatalogError::UnsafeArchive)?;
            charts.push(ProcessedChart {
                relative_path,
                file_path: file,
            });
        }
    }
    if charts.is_empty() {
        return Err(CatalogError::NoCharts);
    }
    Ok(ProcessedRevision { charts })
}
