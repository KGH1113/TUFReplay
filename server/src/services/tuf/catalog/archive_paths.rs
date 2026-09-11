use super::CatalogError;
use std::{
    fs,
    path::{Path, PathBuf},
};

pub(super) fn collect_files(root: &Path) -> Result<Vec<PathBuf>, CatalogError> {
    let mut files = Vec::new();
    let mut pending = vec![root.to_path_buf()];
    while let Some(directory) = pending.pop() {
        for entry in fs::read_dir(directory).map_err(|_| CatalogError::UnsafeArchive)? {
            let entry = entry.map_err(|_| CatalogError::UnsafeArchive)?;
            let file_type = entry.file_type().map_err(|_| CatalogError::UnsafeArchive)?;
            if file_type.is_symlink() {
                return Err(CatalogError::UnsafeArchive);
            }
            if file_type.is_dir() {
                pending.push(entry.path());
            } else if file_type.is_file() {
                files.push(entry.path());
            }
        }
    }
    Ok(files)
}

pub(super) fn flatten_leaf_files(start: &Path, root: &Path) -> Result<(), CatalogError> {
    let mut directories = Vec::new();
    for entry in fs::read_dir(start).map_err(|_| CatalogError::UnsafeArchive)? {
        let entry = entry.map_err(|_| CatalogError::UnsafeArchive)?;
        if entry
            .file_type()
            .map_err(|_| CatalogError::UnsafeArchive)?
            .is_dir()
        {
            directories.push(entry.path());
        }
    }
    if !directories.is_empty() {
        for directory in directories {
            flatten_leaf_files(&directory, root)?;
        }
        return Ok(());
    }
    if start == root {
        return Ok(());
    }
    for entry in fs::read_dir(start).map_err(|_| CatalogError::UnsafeArchive)? {
        let entry = entry.map_err(|_| CatalogError::UnsafeArchive)?;
        if !entry
            .file_type()
            .map_err(|_| CatalogError::UnsafeArchive)?
            .is_file()
        {
            continue;
        }
        let destination = root.join(entry.file_name());
        if !destination.exists() {
            fs::rename(entry.path(), destination).map_err(|_| CatalogError::UnsafeArchive)?;
        }
    }
    Ok(())
}

pub(super) fn relative_string(root: &Path, path: &Path) -> String {
    path.strip_prefix(root)
        .unwrap_or(path)
        .to_string_lossy()
        .replace('\\', "/")
}

pub(super) fn ordinal_cmp(left: &str, right: &str) -> std::cmp::Ordering {
    left.encode_utf16().cmp(right.encode_utf16())
}
