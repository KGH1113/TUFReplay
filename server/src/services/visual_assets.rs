//! Content-addressed assets. Knowing a private digest never grants ownership.
use super::{artifacts::verify_stream, visuals};
use base64::{engine::general_purpose::STANDARD, Engine as _};
use bytes::Bytes;
use loco_rs::prelude::*;
use sea_orm::{DbBackend, FromQueryResult, Statement, Value as SqlValue};
use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use sha2::{Digest, Sha256};
use std::path::Path;

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetReference {
    pub sha256: String,
    pub bytes: i64,
    pub media_type: String,
}

impl AssetReference {
    pub fn validate(&self) -> Result<()> {
        if !valid_hash(&self.sha256)
            || self.bytes <= 0
            || self.bytes > visuals::MAX_ASSET_BYTES as i64
            || self.media_type.len() > 100
        {
            return Err(Error::BadRequest("visual_bundle_invalid".into()));
        }
        Ok(())
    }
}

#[derive(Clone, Debug, FromQueryResult)]
pub struct Asset {
    pub sha256: String,
    pub bytes: i64,
    pub media_type: String,
    pub storage_key: String,
    pub public_license: Option<String>,
}

pub fn statement(sql: &str, values: Vec<SqlValue>) -> Statement {
    Statement::from_sql_and_values(DbBackend::Postgres, sql, values)
}

pub fn valid_hash(value: &str) -> bool {
    value.len() == 64
        && value
            .bytes()
            .all(|c| c.is_ascii_digit() || (b'a'..=b'f').contains(&c))
}

pub async fn find(db: &impl ConnectionTrait, sha256: &str) -> Result<Option<Asset>> {
    if !valid_hash(sha256) {
        return Err(Error::NotFound);
    }
    Ok(Asset::find_by_statement(statement(
        "SELECT sha256,bytes,media_type,storage_key,public_license FROM visual_assets WHERE sha256=$1",
        vec![sha256.into()],
    )).one(db).await?)
}

pub async fn owned(
    db: &impl ConnectionTrait,
    owner: &str,
    reference: &AssetReference,
) -> Result<bool> {
    reference.validate()?;
    let row = db
        .query_one_raw(statement(
            "SELECT 1 FROM visual_assets a WHERE a.sha256=$1 AND a.bytes=$2 AND a.media_type=$3
         AND (a.public_license IS NOT NULL OR EXISTS
           (SELECT 1 FROM visual_asset_owners o WHERE o.sha256=a.sha256 AND o.owner_id=$4))",
            vec![
                reference.sha256.clone().into(),
                reference.bytes.into(),
                reference.media_type.clone().into(),
                owner.into(),
            ],
        ))
        .await?;
    Ok(row.is_some())
}

pub async fn store(
    ctx: &AppContext,
    owner: &str,
    reference: &AssetReference,
    data: Bytes,
) -> Result<()> {
    reference.validate()?;
    if data.len() as i64 != reference.bytes
        || hex::encode(Sha256::digest(&data)) != reference.sha256
    {
        return Err(Error::BadRequest("visual_asset_integrity_mismatch".into()));
    }
    visuals::validate_media(&reference.media_type, &data)?;
    let key = format!("visual-assets/sha256/{}", reference.sha256);
    let path = Path::new(&key);
    // Upload only verified bytes; the final immutable key is never client-writable.
    if !ctx.storage.exists(path).await? {
        ctx.storage.upload(path, &data).await?;
    }
    verify_stream(
        ctx.storage.download_stream(path).await?,
        &reference.sha256,
        reference.bytes as u64,
    )
    .await?;
    let tx = ctx.db.begin().await?;
    tx.execute_raw(statement(
        "INSERT INTO visual_assets(sha256,bytes,media_type,storage_key) VALUES($1,$2,$3,$4) ON CONFLICT DO NOTHING",
        vec![reference.sha256.clone().into(), reference.bytes.into(), reference.media_type.clone().into(), key.into()],
    )).await?;
    let asset = find(&tx, &reference.sha256).await?.ok_or(Error::NotFound)?;
    if asset.bytes != reference.bytes || asset.media_type != reference.media_type {
        return Err(Error::BadRequest("visual_asset_integrity_mismatch".into()));
    }
    tx.execute_raw(statement(
        "INSERT INTO visual_asset_owners(owner_id,sha256) VALUES($1,$2) ON CONFLICT DO NOTHING",
        vec![owner.into(), reference.sha256.clone().into()],
    ))
    .await?;
    tx.commit().await?;
    Ok(())
}

/// Convert validated legacy inline bundles to metadata, retaining an old-client reader.
pub async fn externalize(
    ctx: &AppContext,
    owner: &str,
    bundle: &mut visuals::ValidatedBundle,
) -> Result<()> {
    let mut value: Value = serde_json::from_slice(&bundle.bytes)?;
    let assets = value["assets"]
        .as_array_mut()
        .ok_or_else(|| Error::BadRequest("visual_bundle_invalid".into()))?;
    for asset in assets {
        if let Some(encoded) = asset["data_base64"].as_str() {
            let data = STANDARD
                .decode(encoded)
                .map_err(|_| Error::BadRequest("visual_bundle_invalid".into()))?;
            let reference = AssetReference {
                sha256: hex::encode(Sha256::digest(&data)),
                bytes: data.len() as i64,
                media_type: asset["media_type"].as_str().unwrap_or_default().into(),
            };
            store(ctx, owner, &reference, Bytes::from(data)).await?;
            *asset = json!({"path":asset["path"],"media_type":reference.media_type,"sha256":reference.sha256,"bytes":reference.bytes});
        } else {
            let reference = reference_from_value(asset)?;
            if !owned(&ctx.db, owner, &reference).await? {
                return Err(Error::BadRequest("visual_asset_missing".into()));
            }
        }
    }
    value["schema_version"] = json!(2);
    bundle.bytes = serde_json::to_vec(&value)?;
    bundle.sha256 = hex::encode(Sha256::digest(&bundle.bytes));
    Ok(())
}

pub fn reference_from_value(asset: &Value) -> Result<AssetReference> {
    let reference = AssetReference {
        sha256: asset["sha256"].as_str().unwrap_or_default().into(),
        bytes: asset["bytes"].as_i64().unwrap_or_default(),
        media_type: asset["media_type"].as_str().unwrap_or_default().into(),
    };
    reference.validate()?;
    Ok(reference)
}

pub async fn inline_legacy(ctx: &AppContext, bundle: &[u8]) -> Result<Vec<u8>> {
    let mut value: Value = serde_json::from_slice(bundle)?;
    if value["schema_version"] != 2 {
        return Ok(bundle.to_vec());
    }
    for item in value["assets"].as_array_mut().ok_or(Error::NotFound)? {
        let reference = reference_from_value(item)?;
        let asset = find(&ctx.db, &reference.sha256)
            .await?
            .ok_or(Error::NotFound)?;
        let data: Vec<u8> = ctx.storage.download(Path::new(&asset.storage_key)).await?;
        if data.len() as i64 != reference.bytes
            || hex::encode(Sha256::digest(&data)) != reference.sha256
        {
            return Err(Error::Message("visual asset integrity mismatch".into()));
        }
        *item = json!({"path":item["path"],"media_type":reference.media_type,"data_base64":STANDARD.encode(data)});
    }
    value["schema_version"] = json!(1);
    Ok(serde_json::to_vec(&value)?)
}

pub async fn public_routes(ctx: &AppContext, value: &mut Value) -> Result<()> {
    use std::collections::HashSet;
    let assets = value["assets"].as_array_mut().ok_or(Error::NotFound)?;
    if assets.is_empty() {
        return Ok(());
    }
    let hashes: Vec<SqlValue> = assets
        .iter()
        .map(|a| a["sha256"].as_str().unwrap_or_default().into())
        .collect();
    let parameters = (1..=hashes.len())
        .map(|n| format!("${n}"))
        .collect::<Vec<_>>()
        .join(",");
    let rows = ctx.db.query_all_raw(statement(&format!("SELECT sha256 FROM visual_assets WHERE public_license IS NOT NULL AND sha256 IN ({parameters})"), hashes)).await?;
    let public: HashSet<String> = rows
        .iter()
        .map(|row| row.try_get("", "sha256"))
        .collect::<std::result::Result<_, _>>()?;
    for asset in assets {
        let hash = asset["sha256"].as_str().ok_or(Error::NotFound)?;
        if public.contains(hash) {
            asset["public_url"] = json!(format!("/api/v1/visual-assets/public/{hash}"));
        }
    }
    Ok(())
}
