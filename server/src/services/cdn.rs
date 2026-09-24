//! Short-lived object grants. Authorization remains at the replay manifest API;
//! the edge validates each grant before consulting its shared object cache.
use super::replays::{PublishedReplay, PublishedVisuals};
use bytes::Bytes;
use hmac::{Hmac, Mac};
use loco_rs::{app::AppContext, Error, Result};
use serde::Serialize;
use sha2::Sha256;
use std::{collections::BTreeMap, path::Path};

const GRANT_SECONDS: i64 = 900;

#[derive(Clone)]
pub struct CdnSigner {
    origin: String,
    secret: Vec<u8>,
}

#[derive(Serialize)]
pub struct Delivery {
    pub origin: String,
    pub expires_at: i64,
    pub urls: BTreeMap<String, String>,
}

impl CdnSigner {
    pub fn new(origin: &str, secret: &str) -> Result<Self> {
        let url = reqwest::Url::parse(origin)
            .map_err(|_| Error::Message("invalid REPLAY_CDN_ORIGIN".into()))?;
        if url.scheme() != "https"
            || url.host_str().is_none()
            || !url.username().is_empty()
            || url.password().is_some()
            || url.path() != "/"
            || url.query().is_some()
            || url.fragment().is_some()
            || secret.len() < 32
        {
            return Err(Error::Message("invalid replay CDN configuration".into()));
        }
        Ok(Self {
            origin: url.origin().ascii_serialization(),
            secret: secret.as_bytes().to_vec(),
        })
    }

    pub fn from_env() -> Result<Option<Self>> {
        match (
            std::env::var("REPLAY_CDN_ORIGIN").ok(),
            std::env::var("REPLAY_CDN_SIGNING_SECRET").ok(),
        ) {
            (None, None) => Ok(None),
            (Some(origin), Some(secret)) => Self::new(&origin, &secret).map(Some),
            _ => Err(Error::Message(
                "replay CDN configuration is incomplete".into(),
            )),
        }
    }

    fn url(&self, key: &str, expires: i64) -> Result<String> {
        if key.len() > 512
            || ![
                "evidence/",
                "visual-assets/sha256/",
                "visual-bundles/sha256/",
            ]
            .iter()
            .any(|prefix| key.starts_with(prefix))
            || key
                .split('/')
                .any(|part| part.is_empty() || part == "." || part == "..")
            || !key
                .bytes()
                .all(|c| c.is_ascii_alphanumeric() || b"/-_".contains(&c))
        {
            return Err(Error::Message("invalid replay CDN object key".into()));
        }
        let path = format!("/objects/{key}");
        let mut mac = Hmac::<Sha256>::new_from_slice(&self.secret)
            .map_err(|_| Error::Message("invalid replay CDN signing key".into()))?;
        mac.update(format!("{expires}\n{path}").as_bytes());
        let signature = hex::encode(mac.finalize().into_bytes());
        Ok(format!(
            "{}{path}?expires={expires}&signature={signature}",
            self.origin
        ))
    }
}

pub async fn delivery(
    ctx: &AppContext,
    replay: &PublishedReplay,
    visuals: &PublishedVisuals,
) -> Result<Option<Delivery>> {
    let Some(signer) = ctx.shared_store.get::<CdnSigner>() else {
        return Ok(None);
    };
    let expires_at = chrono::Utc::now().timestamp() + GRANT_SECONDS;
    let mut urls = BTreeMap::new();
    // This allowlist excludes internal receipt evidence even when present in the
    // stored evidence manifest. Never sign arbitrary caller-supplied object keys.
    for (file, stream) in replay.files() {
        urls.insert(
            format!("/api/v1/replays/{}/files/{}", replay.run_id, file.name()),
            signer.url(&stream.storage_key, expires_at)?,
        );
    }
    for (kind, visual) in [
        ("keyviewer", &visuals.keyviewer),
        ("overlay", &visuals.overlay),
    ] {
        let Some((descriptor, bundle)) = visual else {
            continue;
        };
        let key = format!("visual-bundles/sha256/{}", descriptor.sha256);
        let path = Path::new(&key);
        // Materialize the immutable rendered JSON once; subsequent manifests only
        // check object metadata. DB metadata remains available to authorize access.
        if !ctx.storage.exists(path).await? {
            ctx.storage
                .upload(path, &Bytes::copy_from_slice(&bundle.bundle))
                .await?;
            super::artifacts::verify_stream(
                ctx.storage.download_stream(path).await?,
                &descriptor.sha256,
                descriptor.bytes,
            )
            .await?;
        }
        urls.insert(descriptor.url.clone(), signer.url(&key, expires_at)?);
        let value: serde_json::Value = serde_json::from_slice(&bundle.bundle)?;
        if value["schema_version"] != 2 {
            continue;
        }
        for asset in value["assets"].as_array().ok_or(Error::NotFound)? {
            let reference = super::visual_assets::reference_from_value(asset)?;
            let key = format!("visual-assets/sha256/{}", reference.sha256);
            let route = format!(
                "/api/v1/replays/{}/visuals/{kind}/assets/{}",
                replay.run_id, reference.sha256
            );
            urls.insert(route, signer.url(&key, expires_at)?);
        }
    }
    Ok(Some(Delivery {
        origin: signer.origin.clone(),
        expires_at,
        urls,
    }))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn grants_bind_path_expiration_and_reject_non_object_paths() {
        let signer = CdnSigner::new("https://cdn.example", &"s".repeat(32)).unwrap();
        let url = signer
            .url("visual-assets/sha256/abc", 2_000_000_000)
            .unwrap();
        assert!(url.starts_with(
            "https://cdn.example/objects/visual-assets/sha256/abc?expires=2000000000&signature="
        ));
        assert_ne!(
            url,
            signer
                .url("visual-assets/sha256/def", 2_000_000_000)
                .unwrap()
        );
        assert_ne!(
            url,
            signer
                .url("visual-assets/sha256/abc", 2_000_000_001)
                .unwrap()
        );
        for key in [
            "private/key",
            "evidence/../secret",
            "evidence/%2fsecret",
            "evidence/a?b",
            "evidence//a",
        ] {
            assert!(signer.url(key, 2_000_000_000).is_err());
        }
        assert!(CdnSigner::new("http://cdn.example", &"s".repeat(32)).is_err());
        assert!(CdnSigner::new("https://cdn.example/path", &"s".repeat(32)).is_err());
        assert!(CdnSigner::new("https://cdn.example", "short").is_err());
    }
}
