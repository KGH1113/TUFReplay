//! Private object storage. Existing local objects remain readable during migration.
use async_trait::async_trait;
use bytes::Bytes;
use futures_util::StreamExt;
use loco_rs::storage::{
    drivers::{
        opendal_adapter::OpendalAdapter, GetResponse, ListEntry, StoreDriver, UploadResponse,
    },
    stream::BytesStream,
    StorageError, StorageResult,
};
use loco_rs::{Error, Result};
use opendal::{services, ErrorKind, Operator};
use sha2::{Digest, Sha256};
use std::{collections::BTreeMap, path::Path, sync::Arc};

#[derive(Clone)]
pub struct ArtifactStores {
    pub local: Arc<dyn StoreDriver>,
    pub primary: Arc<dyn StoreDriver>,
    pub r2: Option<Operator>,
    pub local_fallback: bool,
}

impl ArtifactStores {
    pub fn from_env(root: &str, local_game: bool) -> Result<Self> {
        std::fs::create_dir_all(root)?;
        let local: Arc<dyn StoreDriver> = Arc::new(OpendalAdapter::new(
            Operator::new(services::Fs::default().root(root)).map_err(StorageError::from)?,
        ));
        let mode = std::env::var("TUF_REPLAY_STORAGE").unwrap_or_else(|_| "local".into());
        if mode == "local" {
            return Ok(Self {
                primary: local.clone(),
                local,
                r2: None,
                local_fallback: false,
            });
        }
        if mode != "r2" && !(mode == "s3-local" && local_game) {
            return Err(Error::Message(
                "TUF_REPLAY_STORAGE must be local or r2 (s3-local requires local-game)".into(),
            ));
        }
        let endpoint = required_env("R2_ENDPOINT")?;
        let url = reqwest::Url::parse(&endpoint)
            .map_err(|_| Error::Message("invalid R2_ENDPOINT".into()))?;
        let valid_host = if mode == "s3-local" {
            is_local_http_origin(&url)
        } else {
            url.scheme() == "https"
                && url
                    .host_str()
                    .is_some_and(|h| h.ends_with(".r2.cloudflarestorage.com"))
        };
        if !valid_host
            || !url.username().is_empty()
            || url.password().is_some()
            || url.path() != "/"
            || url.query().is_some()
            || url.fragment().is_some()
        {
            return Err(Error::Message(
                "R2_ENDPOINT must be the account's HTTPS S3 endpoint".into(),
            ));
        }
        let r2 = r2_operator(
            &endpoint,
            &required_env("R2_BUCKET")?,
            &required_env("R2_ACCESS_KEY_ID")?,
            &required_env("R2_SECRET_ACCESS_KEY")?,
        )?;
        let primary = Arc::new(OpendalAdapter::new(r2.clone()));
        let local_fallback = match std::env::var("R2_LOCAL_FALLBACK").as_deref() {
            Ok("true") | Err(_) => true,
            Ok("false") => false,
            _ => {
                return Err(Error::Message(
                    "R2_LOCAL_FALLBACK must be true or false".into(),
                ))
            }
        };
        Ok(Self {
            local,
            primary,
            r2: Some(r2),
            local_fallback,
        })
    }

    async fn reader(&self, path: &Path) -> StorageResult<&dyn StoreDriver> {
        if self.local_fallback {
            match self.primary.stat(path).await {
                Err(StorageError::Store(error)) if error.kind() == ErrorKind::NotFound => {
                    return Ok(&*self.local)
                }
                Err(error) => return Err(error),
                Ok(_) => {}
            }
        }
        Ok(&*self.primary)
    }
}

/// HTTP emulation is only accepted by callers explicitly running local-game.
pub(crate) fn is_local_http_origin(url: &reqwest::Url) -> bool {
    url.scheme() == "http"
        && matches!(url.host_str(), Some("127.0.0.1" | "[::1]"))
        && url.username().is_empty()
        && url.password().is_none()
        && url.path() == "/"
        && url.query().is_none()
        && url.fragment().is_none()
}

fn r2_operator(
    endpoint: &str,
    bucket: &str,
    access_key: &str,
    secret_key: &str,
) -> Result<Operator> {
    // Default features and constructor-based registration are disabled. Both API
    // and maintenance CLI processes must install the HTTP transport explicitly.
    opendal::install_default();
    Operator::new(
        services::S3::default()
            .endpoint(endpoint)
            .region("auto")
            .bucket(bucket)
            .access_key_id(access_key)
            .secret_access_key(secret_key)
            .disable_ec2_metadata(),
    )
    .map_err(StorageError::from)
    .map_err(Into::into)
}

fn required_env(name: &str) -> Result<String> {
    std::env::var(name)
        .ok()
        .filter(|s| !s.trim().is_empty())
        .ok_or_else(|| Error::Message(format!("{name} is required for R2 storage")))
}

#[async_trait]
impl StoreDriver for ArtifactStores {
    async fn upload(&self, path: &Path, content: &Bytes) -> StorageResult<UploadResponse> {
        self.primary.upload(path, content).await
    }
    async fn upload_stream(
        &self,
        path: &Path,
        stream: BytesStream,
    ) -> StorageResult<UploadResponse> {
        let Some(r2) = &self.r2 else {
            return self.primary.upload_stream(path, stream).await;
        };
        let mut writer = r2
            .writer_with(&path.to_string_lossy())
            .chunk(8 * 1024 * 1024)
            .concurrent(2)
            .await?;
        let mut stream = Box::pin(stream);
        while let Some(chunk) = stream.next().await {
            let result = match chunk {
                Ok(bytes) => writer.write(bytes).await.map_err(StorageError::from),
                Err(error) => Err(StorageError::Any(Box::new(error))),
            };
            if let Err(error) = result {
                let _ = writer.abort().await;
                return Err(error);
            }
        }
        match writer.close().await {
            Ok(meta) => Ok(UploadResponse {
                e_tag: meta.etag().map(str::to_owned),
                version: meta.version().map(str::to_owned),
            }),
            Err(error) => {
                let _ = writer.abort().await;
                Err(error.into())
            }
        }
    }
    async fn get(&self, path: &Path) -> StorageResult<GetResponse> {
        self.reader(path).await?.get(path).await
    }
    async fn get_stream(&self, path: &Path) -> StorageResult<BytesStream> {
        self.reader(path).await?.get_stream(path).await
    }
    async fn delete(&self, path: &Path) -> StorageResult<()> {
        // Delete the fallback first so a primary deletion cannot resurrect it.
        if self.local_fallback {
            self.local.delete(path).await?;
        }
        self.primary.delete(path).await
    }
    async fn copy(&self, from: &Path, to: &Path) -> StorageResult<()> {
        let stream = self.get_stream(from).await?;
        self.upload_stream(to, stream).await?;
        Ok(())
    }
    async fn rename(&self, from: &Path, to: &Path) -> StorageResult<()> {
        self.copy(from, to).await?;
        self.delete(from).await
    }
    async fn exists(&self, path: &Path) -> StorageResult<bool> {
        if self.primary.exists(path).await? {
            return Ok(true);
        }
        if self.local_fallback {
            return self.local.exists(path).await;
        }
        Ok(false)
    }
    async fn stat(&self, path: &Path) -> StorageResult<ListEntry> {
        self.reader(path).await?.stat(path).await
    }
    async fn list(&self, path: &Path, recursive: bool) -> StorageResult<Vec<ListEntry>> {
        let mut entries = BTreeMap::new();
        if self.local_fallback {
            for entry in self.local.list(path, recursive).await? {
                entries.insert(entry.path.clone(), entry);
            }
        }
        for entry in self.primary.list(path, recursive).await? {
            entries.insert(entry.path.clone(), entry);
        }
        Ok(entries.into_values().collect())
    }
}

pub async fn digest_stream(stream: BytesStream) -> Result<(String, u64)> {
    let mut stream = Box::pin(stream);
    let mut digest = Sha256::new();
    let mut bytes = 0u64;
    while let Some(chunk) = stream.next().await {
        let chunk = chunk.map_err(|_| Error::Message("artifact read failed".into()))?;
        bytes = bytes
            .checked_add(chunk.len() as u64)
            .ok_or_else(|| Error::Message("artifact size overflow".into()))?;
        digest.update(&chunk);
    }
    Ok((hex::encode(digest.finalize()), bytes))
}

pub async fn verify_stream(stream: BytesStream, sha256: &str, bytes: u64) -> Result<()> {
    let actual = digest_stream(stream).await?;
    if actual.0 != sha256 || actual.1 != bytes {
        return Err(Error::Message("artifact integrity mismatch".into()));
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    #[ignore = "requires the Docker Desktop live-infra object store"]
    async fn local_s3_multipart_roundtrip() {
        let endpoint =
            std::env::var("LOCAL_OBJECT_STORE").unwrap_or_else(|_| "http://127.0.0.1:9000".into());
        assert!(is_local_http_origin(
            &reqwest::Url::parse(&endpoint).unwrap()
        ));
        let operator = r2_operator(&endpoint, "tuf-replay-local", "S3RVER", "S3RVER").unwrap();
        let stores = ArtifactStores {
            primary: Arc::new(OpendalAdapter::new(operator.clone())),
            r2: Some(operator.clone()),
            local_fallback: false,
            ..stores()
        };
        let key = format!("local-check/{}", uuid::Uuid::new_v4());
        let path = Path::new(&key);
        let bytes = Bytes::from(vec![37u8; 17 * 1024 * 1024]);
        let hash = hex::encode(Sha256::digest(&bytes));
        let chunks: Vec<_> = bytes
            .chunks(1024 * 1024)
            .map(|chunk| Ok(Bytes::copy_from_slice(chunk)))
            .collect();
        let result = async {
            stores
                .upload_stream(
                    path,
                    BytesStream::from_body_stream(futures_util::stream::iter(chunks)),
                )
                .await?;
            verify_stream(stores.get_stream(path).await?, &hash, bytes.len() as u64).await
        }
        .await;
        operator.delete(&key).await.unwrap();
        result.unwrap();
    }

    #[tokio::test]
    async fn r2_operator_installs_http_transport_for_signed_object_requests() {
        use axum::{http::HeaderMap, routing::put, Router};
        let listener = tokio::net::TcpListener::bind("127.0.0.1:0").await.unwrap();
        let endpoint = format!("http://{}", listener.local_addr().unwrap());
        let app = Router::new().route(
            "/bucket/probe",
            put(|headers: HeaderMap, body: Bytes| async move {
                assert!(headers["authorization"]
                    .to_str()
                    .unwrap()
                    .starts_with("AWS4-HMAC-SHA256 "));
                assert_eq!(body.as_ref(), b"transport probe");
            })
            .get(|| async { "transport probe" }),
        );
        let server = tokio::spawn(async move { axum::serve(listener, app).await.unwrap() });
        let operator = r2_operator(&endpoint, "bucket", "test-key", "test-secret").unwrap();
        let result = tokio::time::timeout(std::time::Duration::from_secs(5), async {
            operator.write("probe", "transport probe").await.unwrap();
            assert_eq!(
                operator.read("probe").await.unwrap().to_bytes().as_ref(),
                b"transport probe"
            );
        })
        .await;
        server.abort();
        result.unwrap();
    }

    fn stores() -> ArtifactStores {
        let memory = || -> Arc<dyn StoreDriver> {
            Arc::new(OpendalAdapter::new(
                Operator::new(services::Memory::default()).unwrap(),
            ))
        };
        ArtifactStores {
            local: memory(),
            primary: memory(),
            r2: None,
            local_fallback: true,
        }
    }

    #[tokio::test]
    async fn writes_primary_reads_legacy_and_deletes_both_copies() {
        let stores = stores();
        let path = Path::new("legacy/data");
        stores
            .local
            .upload(path, &Bytes::from_static(b"legacy"))
            .await
            .unwrap();
        assert_eq!(
            digest_stream(stores.get_stream(path).await.unwrap())
                .await
                .unwrap()
                .1,
            6
        );
        stores
            .upload(path, &Bytes::from_static(b"new"))
            .await
            .unwrap();
        assert_eq!(
            digest_stream(stores.get_stream(path).await.unwrap())
                .await
                .unwrap()
                .1,
            3
        );
        stores.delete(path).await.unwrap();
        assert!(!stores.exists(path).await.unwrap());
        assert!(!stores.local.exists(path).await.unwrap());
    }

    #[tokio::test]
    async fn copy_migrates_legacy_without_removing_original_and_rejects_corruption() {
        let stores = stores();
        let source = Path::new("old");
        let target = Path::new("new");
        stores
            .local
            .upload(source, &Bytes::from_static(b"abc"))
            .await
            .unwrap();
        stores.copy(source, target).await.unwrap();
        let hash = hex::encode(Sha256::digest(b"abc"));
        verify_stream(stores.primary.get_stream(target).await.unwrap(), &hash, 3)
            .await
            .unwrap();
        assert!(stores.local.exists(source).await.unwrap());
        assert!(
            verify_stream(stores.primary.get_stream(target).await.unwrap(), &hash, 4)
                .await
                .is_err()
        );
        assert!(verify_stream(
            stores.primary.get_stream(target).await.unwrap(),
            &"0".repeat(64),
            3
        )
        .await
        .is_err());
        let without_fallback = ArtifactStores {
            local_fallback: false,
            ..stores
        };
        assert!(!without_fallback.exists(source).await.unwrap());
    }
}
