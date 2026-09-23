use crate::models::visual_presets::{VisualKind, VisualSource};
use axum::http::StatusCode;
use base64::{engine::general_purpose, Engine as _};
use loco_rs::controller::ErrorDetail;
use loco_rs::prelude::*;
use serde_json::{Map, Value};
use sha2::{Digest, Sha256};
use std::collections::HashSet;

pub const MAX_REQUEST_BYTES: usize = 64 * 1024 * 1024;
pub const MAX_BUNDLE_BYTES: usize = 64 * 1024 * 1024;
pub const MAX_ASSET_BYTES: usize = 48 * 1024 * 1024;
pub const MAX_TOTAL_ASSET_BYTES: usize = 128 * 1024 * 1024;
pub const MAX_ASSETS: usize = 4096;
pub const MAX_JSON_NODES: usize = 32_768;
const MAX_JSON_DEPTH: usize = 128;

#[derive(Clone, Debug)]
pub struct ValidatedBundle {
    pub kind: VisualKind,
    pub source: VisualSource,
    pub source_version: String,
    pub bytes: Vec<u8>,
    pub sha256: String,
}

pub fn preset_not_found() -> Error {
    Error::CustomError(
        StatusCode::NOT_FOUND,
        ErrorDetail::with_reason("visual_preset_not_found"),
    )
}

pub fn validate_name(value: Option<&Value>) -> Result<String> {
    let name = value
        .and_then(Value::as_str)
        .map(str::trim)
        .filter(|value| !value.is_empty())
        .ok_or_else(|| Error::BadRequest("visual_name_required".into()))?;
    if name.chars().count() > 80 || name.chars().any(char::is_control) {
        return Err(Error::BadRequest("visual_name_required".into()));
    }
    Ok(name.to_owned())
}

pub fn validate_bundle(bundle: Value) -> Result<ValidatedBundle> {
    let (nodes, depth) = json_shape(&bundle, 0)?;
    if nodes > MAX_JSON_NODES || depth > MAX_JSON_DEPTH {
        return Err(Error::BadRequest("visual_payload_too_large".into()));
    }
    let object = bundle
        .as_object()
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;

    let schema_version = object
        .get("schema_version")
        .and_then(Value::as_u64)
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
    if schema_version != 1 {
        return Err(Error::BadRequest("visual_bundle_invalid".into()));
    }

    let source_name = object
        .get("source")
        .and_then(Value::as_str)
        .ok_or_else(|| Error::BadRequest("visual_source_unsupported".into()))?;
    let source = VisualSource::parse(source_name)
        .ok_or_else(|| Error::BadRequest("visual_source_unsupported".into()))?;
    let kind_name = object
        .get("kind")
        .and_then(Value::as_str)
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
    let kind = VisualKind::parse(kind_name)
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
    if !source.supports(kind) {
        return Err(Error::BadRequest("visual_source_unsupported".into()));
    }

    let source_version = object
        .get("source_version")
        .and_then(Value::as_str)
        .map(str::trim)
        .filter(|value| {
            !value.is_empty()
                && value.chars().count() <= 128
                && !value.chars().any(char::is_control)
        })
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?
        .to_owned();

    let viewport = object
        .get("viewport")
        .and_then(Value::as_object)
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
    for field in ["width", "height"] {
        let finite_positive = viewport
            .get(field)
            .and_then(Value::as_f64)
            .is_some_and(|value| value.is_finite() && value > 0.0);
        if !finite_positive {
            return Err(Error::BadRequest("visual_bundle_invalid".into()));
        }
    }

    let files = object
        .get("files")
        .and_then(Value::as_object)
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
    let assets = object
        .get("assets")
        .and_then(Value::as_array)
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
    if source.is_dmnote() {
        validate_dmnote_tabs(files)?;
    }

    let sanitized_files = sanitize_files(files, source, kind)?;
    validate_required_files(kind, source, &sanitized_files)?;
    validate_embedded_urls(
        &Value::Object(sanitized_files.clone()),
        source.is_dmnote(),
        false,
    )?;
    let (sanitized_assets, asset_paths) = sanitize_assets(assets)?;
    let mut references = Vec::new();
    collect_asset_references(
        source,
        &Value::Object(sanitized_files.clone()),
        None,
        false,
        false,
        &asset_paths,
        &mut references,
    )?;
    if source.is_dmnote() {
        collect_dmnote_source_references(&sanitized_files, &asset_paths, &mut references)?;
    }
    for reference in references {
        if !asset_paths.contains(&reference) {
            return Err(Error::BadRequest("visual_asset_missing".into()));
        }
    }

    let mut sanitized = Map::new();
    sanitized.insert("schema_version".into(), Value::from(1));
    sanitized.insert("kind".into(), Value::from(kind.as_str()));
    sanitized.insert("source".into(), Value::from(source.as_str()));
    sanitized.insert("source_version".into(), Value::from(source_version.clone()));
    sanitized.insert("viewport".into(), Value::Object(viewport.clone()));
    sanitized.insert("files".into(), Value::Object(sanitized_files));
    sanitized.insert("assets".into(), Value::Array(sanitized_assets));

    let bytes = serde_json::to_vec(&Value::Object(sanitized))?;
    if bytes.is_empty() || bytes.len() > MAX_BUNDLE_BYTES {
        return Err(Error::BadRequest("visual_payload_too_large".into()));
    }
    let sha256 = hex::encode(Sha256::digest(&bytes));
    Ok(ValidatedBundle {
        kind,
        source,
        source_version,
        bytes,
        sha256,
    })
}

fn sanitize_files(
    files: &Map<String, Value>,
    source: VisualSource,
    kind: VisualKind,
) -> Result<Map<String, Value>> {
    let allowed = allowed_file_names(source, kind);
    let mut result = Map::new();
    for (name, value) in files {
        if is_executable_name(name) {
            continue;
        }
        validate_logical_path(name)?;
        if !allowed.contains(&name.as_str()) {
            return Err(Error::BadRequest("visual_bundle_invalid".into()));
        }
        result.insert(name.clone(), sanitize_value(value.clone()));
    }
    Ok(result)
}

fn allowed_file_names(source: VisualSource, kind: VisualKind) -> &'static [&'static str] {
    match (source, kind) {
        (VisualSource::JipperResourcePack, VisualKind::Keyviewer) => &["KeyViewer.json"],
        (VisualSource::JipperResourcePack, VisualKind::Overlay) => &["ResourcePack.json"],
        (VisualSource::Dmnote | VisualSource::ImplDmnote, VisualKind::Keyviewer) => {
            &["preset.json"]
        }
        (VisualSource::JipperKeyviewer, VisualKind::Keyviewer) => &["JipperKeyViewer.json"],
        (VisualSource::ImplResourcePack, VisualKind::Overlay) => &["ImplResourcePack.json"],
        _ => &[],
    }
}

fn validate_required_files(
    kind: VisualKind,
    source: VisualSource,
    files: &Map<String, Value>,
) -> Result<()> {
    let required = allowed_file_names(source, kind);
    if files
        .iter()
        .any(|(name, value)| required.contains(&name.as_str()) && !value.is_object())
    {
        return Err(Error::BadRequest("visual_bundle_invalid".into()));
    }
    if required
        .iter()
        .any(|name| !files.get(*name).is_some_and(Value::is_object))
    {
        return Err(Error::BadRequest("visual_bundle_invalid".into()));
    }
    Ok(())
}

fn validate_dmnote_tabs(files: &Map<String, Value>) -> Result<()> {
    let preset = files
        .get("preset.json")
        .and_then(Value::as_object)
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
    if let Some(placement) = preset.get("tufReplayPlacement") {
        let placement = placement
            .as_object()
            .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
        if let Some(scale) = placement.get("scale") {
            scale
                .as_f64()
                .filter(|value| value.is_finite() && (0.1..=4.0).contains(value))
                .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
        }
    }
    let keys = preset
        .get("keys")
        .and_then(Value::as_object)
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
    let mut modes = keys.iter();
    let Some((mode_id, mode_value)) = modes.next() else {
        return Err(Error::BadRequest("visual_bundle_invalid".into()));
    };
    if !mode_value.is_array() {
        return Err(Error::BadRequest("visual_bundle_invalid".into()));
    }
    if modes.next().is_some() {
        return Err(Error::BadRequest(
            "visual_multiple_tabs: export one tab".into(),
        ));
    }
    if let Some(selected) = preset
        .get("selectedKeyType")
        .or_else(|| preset.get("selected_key_type"))
        .and_then(Value::as_str)
    {
        if selected != mode_id {
            return Err(Error::BadRequest("visual_bundle_invalid".into()));
        }
    }
    for field in [
        "keyPositions",
        "statPositions",
        "graphPositions",
        "knobPositions",
        "spritePositions",
        "sprite_positions",
    ] {
        let Some(value) = preset.get(field) else {
            continue;
        };
        let modes = value
            .as_object()
            .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
        for (candidate, values) in modes {
            if candidate != mode_id {
                return Err(Error::BadRequest(
                    "visual_multiple_tabs: export one tab".into(),
                ));
            }
            if !values.is_array() {
                return Err(Error::BadRequest("visual_bundle_invalid".into()));
            }
        }
    }
    if contains_multiple_tabs(files) {
        return Err(Error::BadRequest(
            "visual_multiple_tabs: export one tab".into(),
        ));
    }
    Ok(())
}

fn sanitize_assets(assets: &[Value]) -> Result<(Vec<Value>, HashSet<String>)> {
    if assets.len() > MAX_ASSETS {
        return Err(Error::BadRequest("visual_payload_too_large".into()));
    }
    let mut total = 0usize;
    let mut result = Vec::with_capacity(assets.len());
    let mut paths = HashSet::new();
    for asset in assets {
        let object = asset
            .as_object()
            .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
        let path = object
            .get("path")
            .and_then(Value::as_str)
            .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
        let media_type = object
            .get("media_type")
            .and_then(Value::as_str)
            .map(str::trim)
            .filter(|value| !value.is_empty())
            .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
        let data = object
            .get("data_base64")
            .and_then(Value::as_str)
            .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;

        if is_executable_asset(path, media_type) {
            continue;
        }
        validate_logical_path(path)?;
        let decoded = general_purpose::STANDARD
            .decode(data)
            .or_else(|_| general_purpose::URL_SAFE_NO_PAD.decode(data))
            .map_err(|_| Error::BadRequest("visual_bundle_invalid".into()))?;
        if decoded.is_empty() || decoded.len() > MAX_ASSET_BYTES {
            return Err(Error::BadRequest("visual_payload_too_large".into()));
        }
        total = total
            .checked_add(decoded.len())
            .ok_or_else(|| Error::BadRequest("visual_payload_too_large".into()))?;
        if total > MAX_TOTAL_ASSET_BYTES {
            return Err(Error::BadRequest("visual_payload_too_large".into()));
        }
        validate_media(media_type, &decoded)?;
        if !paths.insert(path.to_owned()) {
            return Err(Error::BadRequest("visual_bundle_invalid".into()));
        }
        let mut sanitized = Map::new();
        sanitized.insert("path".into(), Value::from(path));
        sanitized.insert("media_type".into(), Value::from(media_type));
        sanitized.insert(
            "data_base64".into(),
            Value::from(general_purpose::STANDARD.encode(decoded)),
        );
        result.push(Value::Object(sanitized));
    }
    Ok((result, paths))
}

fn sanitize_value(value: Value) -> Value {
    match value {
        Value::Object(object) => Value::Object(
            object
                .into_iter()
                .filter(|(key, _)| !is_executable_name(key))
                .map(|(key, value)| (key, sanitize_value(value)))
                .collect(),
        ),
        Value::Array(values) => Value::Array(values.into_iter().map(sanitize_value).collect()),
        other => other,
    }
}

fn validate_embedded_urls(value: &Value, skip_dmnote_css: bool, css_context: bool) -> Result<()> {
    match value {
        Value::Object(object) => {
            for (name, value) in object {
                let child_css = css_context || is_css_container(&name.to_ascii_lowercase());
                if skip_dmnote_css && child_css {
                    continue;
                }
                validate_embedded_urls(value, skip_dmnote_css, child_css)?;
            }
        }
        Value::Array(values) => {
            for value in values {
                validate_embedded_urls(value, skip_dmnote_css, css_context)?;
            }
        }
        Value::String(value) if !css_context && value.to_ascii_lowercase().starts_with("data:") => {
            validate_data_url(value)?;
        }
        _ => {}
    }
    Ok(())
}

fn validate_data_url(value: &str) -> Result<()> {
    let (header, encoded) = value
        .split_once(',')
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
    let mut header_parts = header[5..].split(';');
    let media_type = header_parts
        .next()
        .filter(|media_type| !media_type.is_empty())
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
    if !header_parts.any(|part| part.eq_ignore_ascii_case("base64")) {
        return Err(Error::BadRequest("visual_bundle_invalid".into()));
    }
    let decoded = general_purpose::STANDARD
        .decode(encoded)
        .map_err(|_| Error::BadRequest("visual_bundle_invalid".into()))?;
    if decoded.is_empty() || decoded.len() > MAX_ASSET_BYTES {
        return Err(Error::BadRequest("visual_payload_too_large".into()));
    }
    validate_media(media_type, &decoded)
}

fn is_executable_name(name: &str) -> bool {
    let lower = name.to_ascii_lowercase();
    let compact: String = lower
        .chars()
        .filter(|character| character.is_ascii_alphanumeric())
        .collect();
    compact == "js"
        || compact == "javascript"
        || compact == "plugin"
        || compact == "plugins"
        || compact == "script"
        || compact == "scripts"
        || compact.contains("javascript")
        || compact.contains("plugin")
        || compact.contains("audio")
        || compact.contains("sound")
        || compact.ends_with("js")
        || lower.ends_with(".js")
        || lower.ends_with(".mjs")
        || lower.ends_with(".cjs")
        || lower.ends_with(".wasm")
}

fn is_executable_asset(path: &str, media_type: &str) -> bool {
    let lower_path = path.to_ascii_lowercase();
    let lower_media = media_type.to_ascii_lowercase();
    is_executable_name(path)
        || lower_media.starts_with("audio/")
        || lower_media.starts_with("video/")
        || lower_media.contains("javascript")
        || lower_media.contains("wasm")
        || [".wav", ".mp3", ".ogg", ".flac", ".aac", ".m4a"]
            .iter()
            .any(|suffix| lower_path.ends_with(suffix))
}

fn validate_logical_path(path: &str) -> Result<()> {
    if path.is_empty()
        || path.len() > 1024
        || path.contains('\0')
        || path.chars().any(char::is_control)
        || path.contains('\\')
        || path.contains(':')
        || path.starts_with('/')
        || path.contains("://")
        || path
            .split('/')
            .any(|segment| segment.is_empty() || segment == "." || segment == "..")
    {
        return Err(Error::BadRequest("visual_bundle_invalid".into()));
    }
    Ok(())
}

fn validate_media(media_type: &str, data: &[u8]) -> Result<()> {
    let media_type = media_type.to_ascii_lowercase();
    let valid = match media_type.as_str() {
        "image/png" => data.starts_with(b"\x89PNG\r\n\x1a\n"),
        "image/jpeg" => data.starts_with(&[0xff, 0xd8, 0xff]),
        "image/webp" => data.len() >= 12 && &data[0..4] == b"RIFF" && &data[8..12] == b"WEBP",
        "image/gif" => data.starts_with(b"GIF87a") || data.starts_with(b"GIF89a"),
        "image/bmp" => data.starts_with(b"BM"),
        "image/x-icon" => {
            data.len() >= 6 && data.starts_with(&[0, 0, 1, 0]) && (data[4] != 0 || data[5] != 0)
        }
        "image/avif" => {
            let size = data
                .get(..4)
                .map(|b| u32::from_be_bytes(b.try_into().unwrap()) as usize)
                .unwrap_or(0);
            size >= 16
                && size <= data.len()
                && size % 4 == 0
                && &data[4..8] == b"ftyp"
                && (8..size)
                    .step_by(4)
                    .filter(|offset| *offset != 12)
                    .any(|offset| {
                        &data[offset..offset + 4] == b"avif" || &data[offset..offset + 4] == b"avis"
                    })
        }
        "image/svg+xml" => validate_svg(data)?,
        "font/ttf" | "application/x-font-ttf" => {
            data.starts_with(&[0, 1, 0, 0]) || data.starts_with(b"true")
        }
        "font/otf" | "application/vnd.ms-opentype" => data.starts_with(b"OTTO"),
        "font/woff" => data.starts_with(b"wOFF"),
        "font/woff2" => data.starts_with(b"wOF2"),
        _ => false,
    };
    if valid {
        Ok(())
    } else {
        Err(Error::BadRequest("visual_bundle_invalid".into()))
    }
}

fn validate_svg(data: &[u8]) -> Result<bool> {
    let text =
        std::str::from_utf8(data).map_err(|_| Error::BadRequest("visual_bundle_invalid".into()))?;
    let lower = text.to_ascii_lowercase();
    let trimmed = lower.trim_start();
    if !trimmed.starts_with("<svg") && !trimmed.starts_with("<?xml") {
        return Ok(false);
    }
    if lower.contains("<!doctype")
        || lower.contains("<!entity")
        || lower.contains("<script")
        || lower.contains("<foreignobject")
        || lower.contains("<iframe")
        || lower.contains("<object")
        || lower.contains("<embed")
        || lower.contains("javascript:")
        || lower.contains("vbscript:")
    {
        return Ok(false);
    }

    // Walk tags and attributes instead of rejecting every `http://` token:
    // SVG namespace declarations conventionally use those URIs and are not
    // network resources. Resource-bearing attributes still require an
    // internal fragment or a safe inline raster data URL.
    let mut cursor = 0usize;
    let mut namespace_uris = Vec::new();
    let mut saw_svg = false;
    while let Some(relative) = lower[cursor..].find('<') {
        let start = cursor + relative;
        if lower[start..].starts_with("<!--") {
            let Some(end) = lower[start + 4..].find("-->") else {
                return Ok(false);
            };
            cursor = start + 4 + end + 3;
            continue;
        }
        if lower[start..].starts_with("<?") {
            if lower[start..].starts_with("<?xml-stylesheet") || lower[start..].starts_with("<?xsl")
            {
                return Ok(false);
            }
            let Some(end) = lower[start + 2..].find("?>") else {
                return Ok(false);
            };
            cursor = start + 2 + end + 2;
            continue;
        }
        let Some(end) = svg_tag_end(&lower, start + 1) else {
            return Ok(false);
        };
        let body = &lower[start + 1..end];
        let body = body.trim().trim_end_matches('/').trim();
        if body.is_empty() || body.starts_with('!') {
            return Ok(false);
        }
        let (tag_name, attributes) = split_svg_tag(body);
        if tag_name.is_empty()
            || tag_name == "script"
            || tag_name == "foreignobject"
            || tag_name == "iframe"
            || tag_name == "object"
            || tag_name == "embed"
        {
            return Ok(false);
        }
        if tag_name == "svg" {
            saw_svg = true;
        }
        let mut index = 0usize;
        while index < attributes.len() {
            while index < attributes.len() && attributes.as_bytes()[index].is_ascii_whitespace() {
                index += 1;
            }
            if index >= attributes.len() {
                break;
            }
            let name_start = index;
            while index < attributes.len()
                && !attributes.as_bytes()[index].is_ascii_whitespace()
                && attributes.as_bytes()[index] != b'='
            {
                index += 1;
            }
            let name = &attributes[name_start..index];
            while index < attributes.len() && attributes.as_bytes()[index].is_ascii_whitespace() {
                index += 1;
            }
            if index >= attributes.len() || attributes.as_bytes()[index] != b'=' {
                return Ok(false);
            }
            index += 1;
            while index < attributes.len() && attributes.as_bytes()[index].is_ascii_whitespace() {
                index += 1;
            }
            if index >= attributes.len() {
                return Ok(false);
            }
            let quote = attributes.as_bytes()[index];
            let value_start;
            let value_end;
            if quote == b'\'' || quote == b'"' {
                index += 1;
                value_start = index;
                let Some(relative_end) = attributes.as_bytes()[index..]
                    .iter()
                    .position(|byte| *byte == quote)
                else {
                    return Ok(false);
                };
                value_end = index + relative_end;
                index = value_end + 1;
            } else {
                value_start = index;
                while index < attributes.len()
                    && !attributes.as_bytes()[index].is_ascii_whitespace()
                {
                    index += 1;
                }
                value_end = index;
            }
            let value = &attributes[value_start..value_end];
            if name.starts_with("on") && name.len() > 2 {
                return Ok(false);
            }
            if name == "xmlns" || name.starts_with("xmlns:") {
                namespace_uris.push(value.to_owned());
                continue;
            }
            if value.contains("javascript:")
                || value.contains("vbscript:")
                || value.contains("http://")
                || value.contains("https://")
                || value.starts_with("//")
            {
                return Ok(false);
            }
            if name == "href" || name.ends_with(":href") || name == "src" {
                let inline_raster = value.starts_with("data:image/png;")
                    || value.starts_with("data:image/jpeg;")
                    || value.starts_with("data:image/gif;")
                    || value.starts_with("data:image/webp;")
                    || value.starts_with("data:image/bmp;")
                    || value.starts_with("data:image/x-icon;")
                    || value.starts_with("data:image/avif;");
                if inline_raster && validate_data_url(value).is_err() {
                    return Ok(false);
                }
                if !value.starts_with('#') && !inline_raster {
                    return Ok(false);
                }
            }
        }
        cursor = end + 1;
    }
    let mut remaining = lower;
    for namespace_uri in namespace_uris {
        remaining = remaining.replacen(&namespace_uri, "", 1);
    }
    if remaining.contains("http://")
        || remaining.contains("https://")
        || remaining.contains("url(//")
        || remaining.contains("@import")
    {
        return Ok(false);
    }
    Ok(saw_svg)
}

fn svg_tag_end(value: &str, mut index: usize) -> Option<usize> {
    let mut quote = None;
    while index < value.len() {
        let byte = value.as_bytes()[index];
        if let Some(expected) = quote {
            if byte == expected {
                quote = None;
            }
        } else if byte == b'\'' || byte == b'"' {
            quote = Some(byte);
        } else if byte == b'>' {
            return Some(index);
        }
        index += 1;
    }
    None
}

fn split_svg_tag(value: &str) -> (&str, &str) {
    let name_end = value
        .as_bytes()
        .iter()
        .position(|byte| byte.is_ascii_whitespace())
        .unwrap_or(value.len());
    let name = &value[..name_end];
    let attributes = value[name_end..].trim();
    (name, attributes)
}

fn contains_multiple_tabs(value: &Map<String, Value>) -> bool {
    value.iter().any(|(key, value)| {
        let lower = key.to_ascii_lowercase();
        let tab_array = (lower == "tabs"
            || lower == "tablist"
            || lower == "tab_definitions"
            || lower == "tabdefinitions"
            || lower == "customtabs"
            || lower == "custom_tabs"
            || lower == "viewertabs"
            || lower == "keyviewertabs")
            && value.as_array().is_some_and(|tabs| tabs.len() > 1);
        let tab_count = (lower == "tabcount" || lower == "tab_count")
            && value.as_u64().is_some_and(|count| count > 1);
        let mode_map = matches!(
            lower.as_str(),
            "keys"
                | "keypositions"
                | "statpositions"
                | "graphpositions"
                | "knobpositions"
                | "spritepositions"
                | "sprite_positions"
        ) && value.as_object().is_some_and(|modes| modes.len() > 1);
        tab_array
            || tab_count
            || mode_map
            || value.as_object().is_some_and(contains_multiple_tabs)
            || value.as_array().is_some_and(|items| {
                items
                    .iter()
                    .any(|item| item.as_object().is_some_and(contains_multiple_tabs))
            })
    })
}

fn collect_asset_references(
    source: VisualSource,
    value: &Value,
    key: Option<&str>,
    css_context: bool,
    embedded_context: bool,
    asset_paths: &HashSet<String>,
    references: &mut Vec<String>,
) -> Result<()> {
    match value {
        Value::Object(object) => {
            for (name, value) in object {
                let lower = name.to_ascii_lowercase();
                // Embedded entries describe the identity and payload of an
                // asset. Their imageId/fontId fields are identifiers, not
                // paths that must be present verbatim in `assets`.
                if is_embedded_asset_collection(&lower) {
                    continue;
                }
                let child_css = css_context || (!source.is_dmnote() && is_css_container(&lower));
                let child_embedded = embedded_context || is_embedded_asset_collection(&lower);
                collect_asset_references(
                    source,
                    value,
                    Some(name),
                    child_css,
                    child_embedded,
                    asset_paths,
                    references,
                )?;
            }
        }
        Value::Array(values) => {
            for value in values {
                collect_asset_references(
                    source,
                    value,
                    key,
                    css_context,
                    embedded_context,
                    asset_paths,
                    references,
                )?;
            }
        }
        Value::String(value) => {
            let value = value.trim();
            if value.is_empty() || embedded_context {
                return Ok(());
            }

            if css_context {
                collect_css_references(value, asset_paths, references)?;
                return Ok(());
            }

            if is_dmnote_local_image(value) {
                let id = dmnote_local_image_id(value).unwrap_or_default();
                if !dmnote_asset_exists(asset_paths, "image", id) {
                    return Err(Error::BadRequest("visual_asset_missing".into()));
                }
                return Ok(());
            }
            if value
                .to_ascii_lowercase()
                .starts_with("dmnote-local-sound://")
                || value.to_ascii_lowercase().starts_with("data:")
            {
                return Ok(());
            }

            let key_hint = key.is_some_and(is_asset_key);
            // Font family/style names and empty image slots are ordinary
            // values. A font/image field becomes a reference only when it
            // has a path-like value (or a known media extension).
            if (key_hint && looks_like_asset_path(value)) || is_image_or_font_path(value) {
                references.push(value.to_owned());
            }
        }
        _ => {}
    }
    Ok(())
}

fn is_asset_key(name: &str) -> bool {
    let lower = name.to_ascii_lowercase();
    lower.contains("image")
        || lower == "font"
        || lower == "texture"
        || lower == "sprite"
        || lower.ends_with("fontname")
        || lower == "font_name"
}

fn is_embedded_asset_collection(name: &str) -> bool {
    matches!(
        name,
        "embeddedlocalimages"
            | "embedded_local_images"
            | "embeddedlocalfonts"
            | "embedded_local_fonts"
            | "embeddedlocalsounds"
            | "embedded_local_sounds"
    )
}

fn is_css_container(name: &str) -> bool {
    name == "csscontent"
        || name == "css_content"
        || name == "css"
        || name == "customcss"
        || name == "custom_css"
        || name == "tabcssoverrides"
        || name == "tab_css_overrides"
        || name == "style"
        || name == "styles"
        || name == "stylesheet"
        || name == "stylesheets"
}

fn collect_dmnote_source_references(
    files: &Map<String, Value>,
    asset_paths: &HashSet<String>,
    references: &mut Vec<String>,
) -> Result<()> {
    let Some(preset) = files.get("preset.json") else {
        return Ok(());
    };
    let Some(preset) = preset.as_object() else {
        return Ok(());
    };

    // DMNote applies no custom CSS while the global switch is off. With the
    // switch on, a selected tab override wins when it is enabled and has
    // content. The importer may strip its machine-local path while retaining
    // the CSS content; otherwise the global CSS content is used.
    let global_enabled = preset
        .get("useCustomCSS")
        .or_else(|| preset.get("use_custom_css"))
        .and_then(Value::as_bool)
        .or_else(|| {
            preset
                .get("customCSS")
                .or_else(|| preset.get("custom_css"))
                .and_then(Value::as_object)
                .and_then(|css| css.get("enabled"))
                .and_then(Value::as_bool)
        })
        .unwrap_or(false);
    if global_enabled {
        let selected = preset
            .get("selectedKeyType")
            .or_else(|| preset.get("selected_key_type"))
            .and_then(Value::as_str);
        let tab_override = preset
            .get("tabCssOverrides")
            .or_else(|| preset.get("tab_css_overrides"))
            .and_then(Value::as_object)
            .and_then(|overrides| selected.and_then(|selected| overrides.get(selected)));

        let mut used_tab_override = false;
        if let Some(tab) = tab_override.and_then(Value::as_object) {
            let enabled = tab.get("enabled").and_then(Value::as_bool).unwrap_or(true);
            if !enabled {
                // The active tab explicitly disables both tab and global CSS.
                used_tab_override = true;
            } else if tab
                .get("content")
                .and_then(Value::as_str)
                .is_some_and(|content| !content.is_empty())
            {
                collect_css_references(
                    tab.get("content")
                        .and_then(Value::as_str)
                        .unwrap_or_default(),
                    asset_paths,
                    references,
                )?;
                used_tab_override = true;
            }
        }

        if !used_tab_override {
            if let Some(content) = preset
                .get("customCSS")
                .or_else(|| preset.get("custom_css"))
                .and_then(Value::as_object)
                .and_then(|css| css.get("content"))
                .and_then(Value::as_str)
            {
                collect_css_references(content, asset_paths, references)?;
            }
        }
    }

    // A local custom font is another source-specific reference. Family names,
    // styles and display labels are deliberately ignored; only enabled local
    // font identities resolve to canonical DMNote asset IDs.
    collect_dmnote_custom_font_references(&Value::Object(preset.clone()), asset_paths, references)?;
    Ok(())
}

fn collect_dmnote_custom_font_references(
    value: &Value,
    asset_paths: &HashSet<String>,
    references: &mut Vec<String>,
) -> Result<()> {
    match value {
        Value::Object(object) => {
            for (name, value) in object {
                if name.eq_ignore_ascii_case("customFonts")
                    || name.eq_ignore_ascii_case("custom_fonts")
                {
                    if let Some(fonts) = value.as_array() {
                        for font in fonts {
                            let Some(font) = font.as_object() else {
                                continue;
                            };
                            let enabled = font
                                .get("enabled")
                                .and_then(Value::as_bool)
                                .unwrap_or(false);
                            let kind = font
                                .get("type")
                                .or_else(|| font.get("fontType"))
                                .or_else(|| font.get("font_type"))
                                .and_then(Value::as_str);
                            if !enabled
                                || !kind.is_some_and(|kind| kind.eq_ignore_ascii_case("local"))
                            {
                                if enabled
                                    && kind.is_some_and(|kind| kind.eq_ignore_ascii_case("web"))
                                {
                                    if let Some(css) = font
                                        .get("cssContent")
                                        .or_else(|| font.get("css_content"))
                                        .and_then(Value::as_str)
                                    {
                                        collect_css_references(css, asset_paths, references)?;
                                    }
                                }
                                continue;
                            }
                            let id = font.get("id").and_then(Value::as_str).unwrap_or_default();
                            if !dmnote_asset_exists(asset_paths, "font", id) {
                                return Err(Error::BadRequest("visual_asset_missing".into()));
                            }
                        }
                    }
                }
                collect_dmnote_custom_font_references(value, asset_paths, references)?;
            }
        }
        Value::Array(values) => {
            for value in values {
                collect_dmnote_custom_font_references(value, asset_paths, references)?;
            }
        }
        _ => {}
    }
    Ok(())
}

fn is_dmnote_local_image(value: &str) -> bool {
    dmnote_local_image_id(value).is_some()
}

fn looks_like_asset_path(value: &str) -> bool {
    value.contains('/') || is_image_or_font_path(value)
}

fn dmnote_local_image_id(value: &str) -> Option<&str> {
    let prefix = "dmnote-local-image://";
    value
        .get(..prefix.len())
        .filter(|candidate| candidate.eq_ignore_ascii_case(prefix))
        .map(|_| &value[prefix.len()..])
}

fn dmnote_asset_exists(asset_paths: &HashSet<String>, category: &str, id: &str) -> bool {
    let id = canonical_dmnote_id(id);
    let prefix = format!("assets/dmnote-{category}/{id}.");
    asset_paths.iter().any(|path| path.starts_with(&prefix))
}

fn canonical_dmnote_id(id: &str) -> String {
    let mut result = String::new();
    for value in id.trim().chars() {
        if result.chars().count() >= 96 {
            break;
        }
        if value.is_ascii_alphanumeric() || matches!(value, '-' | '_' | '.') {
            result.push(value);
        } else {
            result.push('_');
        }
    }
    if result.is_empty() {
        "embedded".into()
    } else {
        result
    }
}

fn collect_css_references(
    value: &str,
    asset_paths: &HashSet<String>,
    references: &mut Vec<String>,
) -> Result<()> {
    let lower = value.to_ascii_lowercase();
    let bytes = lower.as_bytes();
    let mut cursor = 0usize;
    while cursor < bytes.len() {
        let Some(relative) = lower[cursor..].find("url") else {
            break;
        };
        let start = cursor + relative;
        let mut open = start + 3;
        while open < bytes.len() && bytes[open].is_ascii_whitespace() {
            open += 1;
        }
        if open >= bytes.len() || bytes[open] != b'(' {
            cursor = start + 3;
            continue;
        }
        let Some(close_relative) = lower[open + 1..].find(')') else {
            return Err(Error::BadRequest("visual_bundle_invalid".into()));
        };
        let close = open + 1 + close_relative;
        let reference = value[open + 1..close]
            .trim()
            .trim_matches(['\'', '"'])
            .trim();
        if !reference.is_empty() && !reference.starts_with('#') {
            if reference.to_ascii_lowercase().starts_with("data:") {
                validate_data_url(reference)?;
            } else if is_dmnote_local_image(reference) {
                let id = dmnote_local_image_id(reference).unwrap_or_default();
                if !dmnote_asset_exists(asset_paths, "image", id) {
                    return Err(Error::BadRequest("visual_asset_missing".into()));
                }
            } else if reference
                .to_ascii_lowercase()
                .starts_with("dmnote-local-sound://")
            {
                // Sound references are stripped by the source adapters.
            } else {
                if validate_logical_path(reference).is_err() {
                    return Err(Error::BadRequest("visual_bundle_invalid".into()));
                }
                references.push(reference.to_owned());
            }
        }
        cursor = close + 1;
    }
    Ok(())
}

fn is_image_or_font_path(value: &str) -> bool {
    let value = value
        .split('?')
        .next()
        .unwrap_or(value)
        .to_ascii_lowercase();
    [
        ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".svg", ".avif", ".ico", ".ttf", ".otf",
        ".woff", ".woff2",
    ]
    .iter()
    .any(|suffix| value.ends_with(suffix))
}

fn json_shape(value: &Value, depth: usize) -> Result<(usize, usize)> {
    if depth > MAX_JSON_DEPTH {
        return Err(Error::BadRequest("visual_payload_too_large".into()));
    }
    let child = match value {
        Value::Object(object) => object
            .values()
            .map(|value| json_shape(value, depth + 1))
            .collect::<Result<Vec<_>>>()?,
        Value::Array(values) => values
            .iter()
            .map(|value| json_shape(value, depth + 1))
            .collect::<Result<Vec<_>>>()?,
        _ => Vec::new(),
    };
    let nodes = 1usize
        .checked_add(child.iter().map(|(nodes, _)| *nodes).sum::<usize>())
        .ok_or_else(|| Error::BadRequest("visual_payload_too_large".into()))?;
    let max_depth = child.iter().map(|(_, depth)| *depth).max().unwrap_or(depth);
    Ok((nodes, max_depth))
}

#[cfg(test)]
mod tests {
    #[test]
    fn ico_avif_and_fallback_fonts_are_validated() {
        assert!(super::validate_media("image/x-icon", &[0, 0, 1, 0, 1, 0]).is_ok());
        assert!(super::validate_media("image/avif", b"\0\0\0\x10ftypavif\0\0\0\0").is_ok());
        assert!(super::validate_media("image/avif", b"\0\0\0\x10ftypheic\0\0\0\0").is_err());
        assert!(super::is_asset_key("FallbackFontName"));
        assert!(super::is_asset_key("defaultFontName"));
    }
    use super::*;
    use serde_json::json;

    fn bundle(files: Value, assets: Value) -> Value {
        json!({
            "schema_version": 1,
            "kind": "keyviewer",
            "source": "dmnote",
            "source_version": "1.0",
            "viewport": {"width": 1920, "height": 1080},
            "files": files,
            "assets": assets,
            "ignored": {"javascript": "alert(1)"}
        })
    }

    #[test]
    fn new_sources_preserve_identity_and_reject_wrong_kinds() {
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
            let mut input = bundle(files, json!([]));
            input["source"] = json!(source);
            input["kind"] = json!(kind);
            let validated = validate_bundle(input.clone()).unwrap();
            assert_eq!(validated.source.as_str(), source);
            input["kind"] = json!(if kind == "keyviewer" {
                "overlay"
            } else {
                "keyviewer"
            });
            assert!(
                matches!(validate_bundle(input), Err(Error::BadRequest(code)) if code == "visual_source_unsupported")
            );
        }
    }

    #[test]
    fn impl_dmnote_keeps_css_validation_and_single_tab_limits() {
        let mut input = bundle(
            json!({"preset.json": {
                "keys": {"one": []}, "useCustomCSS": true,
                "customCSS": {"content": ".key {background: url(missing.png)}"}
            }}),
            json!([]),
        );
        input["source"] = json!("impl-dmnote");
        assert!(
            matches!(validate_bundle(input.clone()), Err(Error::BadRequest(code)) if code == "visual_asset_missing")
        );
        input["files"]["preset.json"]["useCustomCSS"] = json!(false);
        assert!(validate_bundle(input.clone()).is_ok());
        input["files"]["preset.json"]["keys"]["two"] = json!([]);
        assert!(
            matches!(validate_bundle(input), Err(Error::BadRequest(code)) if code.starts_with("visual_multiple_tabs"))
        );
    }

    #[test]
    fn preserves_placement_scale_in_stored_bundle_and_supports_legacy_presets() {
        for scale in [None, Some(0.1), Some(0.5), Some(1.75), Some(4.0)] {
            let mut placement = json!({"x": 60, "y": 420});
            if let Some(scale) = scale {
                placement["scale"] = json!(scale);
            }
            let validated = validate_bundle(bundle(
                json!({"preset.json": {"keys": {"one": ["A"]}, "tufReplayPlacement": placement}}),
                json!([]),
            ))
            .expect("valid scale");
            let stored: Value = serde_json::from_slice(&validated.bytes).unwrap();
            assert_eq!(
                stored["files"]["preset.json"]["tufReplayPlacement"],
                placement
            );
        }
    }

    #[test]
    fn rejects_invalid_placement_scale() {
        for scale in [
            json!(0),
            json!(-1),
            json!(0.09),
            json!(4.01),
            json!("1.5"),
            json!(null),
            json!(true),
        ] {
            assert!(validate_bundle(bundle(
                json!({"preset.json": {"keys": {"one": []}, "tufReplayPlacement": {"x": 0, "y": 0, "scale": scale}}}),
                json!([]),
            )).is_err());
        }
    }

    #[test]
    fn strips_javascript_and_audio_without_storing_them() {
        let result = validate_bundle(bundle(
            json!({
                "preset.json": {"label": "ok", "keys": {"4key": []}, "customJS": "alert(1)", "sound": "x.wav"},
                "custom.js": "alert(2)"
            }),
            json!([
                {"path":"x.wav", "media_type":"audio/wav", "data_base64":"aA=="}
            ]),
        ))
        .expect("valid sanitized bundle");
        let stored: Value = serde_json::from_slice(&result.bytes).unwrap();
        assert_eq!(stored["files"]["preset.json"]["label"], "ok");
        assert!(stored["files"]["preset.json"].get("customJS").is_none());
        assert!(stored["files"].get("custom.js").is_none());
        assert_eq!(stored["assets"].as_array().unwrap().len(), 0);
    }

    #[test]
    fn rejects_missing_image_and_multiple_dmnote_tabs() {
        let missing = validate_bundle(bundle(
            json!({"preset.json": {"keys": {"4key": []}, "image": "missing.png"}}),
            json!([]),
        ));
        assert!(matches!(missing, Err(Error::BadRequest(code)) if code == "visual_asset_missing"));

        let multiple = validate_bundle(bundle(
            json!({"preset.json": {"keys": {"4key": [], "5key": []}}}),
            json!([]),
        ));
        assert!(
            matches!(multiple, Err(Error::BadRequest(code)) if code.starts_with("visual_multiple_tabs"))
        );

        let multiple_positions = validate_bundle(bundle(
            json!({
                "preset.json": {
                    "keys": {"4key": []},
                    "keyPositions": {"4key": [], "5key": []}
                }
            }),
            json!([]),
        ));
        assert!(matches!(
            multiple_positions,
            Err(Error::BadRequest(code)) if code.starts_with("visual_multiple_tabs")
        ));
    }

    #[test]
    fn validates_media_signature_and_source_kind() {
        let bad_media = validate_bundle(bundle(
            json!({"preset.json": {"keys": {"4key": []}}}),
            json!([{"path":"font.ttf", "media_type":"font/ttf", "data_base64":"aA=="}]),
        ));
        assert!(
            matches!(bad_media, Err(Error::BadRequest(code)) if code == "visual_bundle_invalid")
        );

        let mut overlay = bundle(json!({"preset.json": {"keys": {"4key": []}}}), json!([]));
        overlay["kind"] = json!("overlay");
        assert!(
            matches!(validate_bundle(overlay), Err(Error::BadRequest(code)) if code == "visual_source_unsupported")
        );
    }

    #[test]
    fn requires_source_specific_logical_files() {
        let mut missing = bundle(json!({}), json!([]));
        assert!(matches!(
            validate_bundle(missing.clone()),
            Err(Error::BadRequest(code)) if code == "visual_bundle_invalid"
        ));

        missing["files"] = json!({"ResourcePack.json": {}});
        missing["kind"] = json!("overlay");
        missing["source"] = json!("jipper-resourcepack");
        assert!(validate_bundle(missing).is_ok());
    }

    #[test]
    fn rejects_executable_embedded_data_urls() {
        let mut bundle = bundle(json!({"preset.json": {"keys": {"4key": []}}}), json!([]));
        bundle["files"]["preset.json"]["image"] = json!("data:text/javascript;base64,YWxlcnQoMSk=");
        assert!(matches!(
            validate_bundle(bundle),
            Err(Error::BadRequest(code)) if code == "visual_bundle_invalid"
        ));
    }

    #[test]
    fn ignores_empty_labels_and_embedded_asset_identifiers() {
        let result = validate_bundle(bundle(
            json!({
                "preset.json": {
                    "keys": {"4key": []},
                    "keyPositions": {"4key": [{
                        "activeImage": "",
                        "inactiveImage": "",
                        "fontFamily": "system-ui, sans-serif",
                        "fontStyle": "normal",
                        "fontName": ""
                    }]},
                    "embeddedLocalImages": [{
                        "imageId": "image-1",
                        "extension": "png",
                        "dataBase64": "iVBORw0KGgo="
                    }]
                }
            }),
            json!([]),
        ));
        assert!(result.is_ok(), "empty labels must not become asset paths");
    }

    #[test]
    fn requires_canonical_dmnote_local_image_assets() {
        let mut missing = bundle(
            json!({
                "preset.json": {
                    "keys": {"4key": []},
                    "keyPositions": {"4key": [{
                        "activeImage": "dmnote-local-image://image-1"
                    }]}
                }
            }),
            json!([]),
        );
        assert!(matches!(
            validate_bundle(missing.clone()),
            Err(Error::BadRequest(code)) if code == "visual_asset_missing"
        ));

        missing["assets"] = json!([{
            "path": "assets/dmnote-image/image-1.png",
            "media_type": "image/png",
            "data_base64": "iVBORw0KGgo="
        }]);
        assert!(validate_bundle(missing).is_ok());
    }

    #[test]
    fn requires_enabled_local_font_ids_but_ignores_family_labels() {
        let mut bundle = bundle(
            json!({
                "preset.json": {
                    "keys": {"4key": []},
                    "keyPositions": {"4key": [{
                        "fontFamily": "MAPLESTORY_OTF_BOLD",
                        "fontStyle": "normal",
                        "fontName": "MAPLESTORY_OTF_BOLD"
                    }]},
                    "fontSettings": {"customFonts": [{
                        "id": "font-1",
                        "type": "local",
                        "name": "MAPLESTORY_OTF_BOLD",
                        "enabled": true
                    }]}
                }
            }),
            json!([]),
        );
        assert!(matches!(
            validate_bundle(bundle.clone()),
            Err(Error::BadRequest(code)) if code == "visual_asset_missing"
        ));

        bundle["assets"] = json!([{
            "path": "assets/dmnote-font/font-1.otf",
            "media_type": "font/otf",
            "data_base64": "T1RUTw=="
        }]);
        assert!(validate_bundle(bundle).is_ok());
    }

    #[test]
    fn only_active_dmnote_css_requires_assets() {
        let mut inactive = bundle(
            json!({
                "preset.json": {
                    "keys": {"4key": []},
                    "useCustomCSS": false,
                    "customCSS": {"content": ".key { background: url(missing.png); }"}
                }
            }),
            json!([]),
        );
        assert!(validate_bundle(inactive.clone()).is_ok());

        inactive["files"]["preset.json"]["useCustomCSS"] = json!(true);
        assert!(matches!(
            validate_bundle(inactive),
            Err(Error::BadRequest(code)) if code == "visual_asset_missing"
        ));
    }

    #[test]
    fn selected_dmnote_tab_css_uses_content_without_machine_path() {
        let bundle = bundle(
            json!({
                "preset.json": {
                    "keys": {"4key": []},
                    "selectedKeyType": "4key",
                    "useCustomCSS": true,
                    "customCSS": {"content": ""},
                    "tabCssOverrides": {
                        "4key": {
                            "enabled": true,
                            "content": ".key { background: url(missing.png); }"
                        }
                    }
                }
            }),
            json!([]),
        );
        assert!(matches!(
            validate_bundle(bundle),
            Err(Error::BadRequest(code)) if code == "visual_asset_missing"
        ));
    }

    #[test]
    fn accepts_svg_namespace_but_rejects_external_resources() {
        let valid =
            br#"<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1 1"><path d="M0 0"/></svg>"#;
        assert!(validate_media("image/svg+xml", valid).is_ok());

        let external = br#"<svg xmlns="http://www.w3.org/2000/svg"><image href="https://example.test/a.png"/></svg>"#;
        assert!(matches!(
            validate_media("image/svg+xml", external),
            Err(Error::BadRequest(code)) if code == "visual_bundle_invalid"
        ));
    }
}
