//! Operator-only opt-in for rights-cleared common assets; never exposed as a user API.
use crate::services::{artifacts::verify_stream, visual_assets};
use loco_rs::prelude::*;
use std::path::Path;

pub struct PublishVisualAsset;

#[async_trait]
impl Task for PublishVisualAsset {
    fn task(&self) -> TaskInfo {
        TaskInfo { name: "publish_visual_asset".into(), detail: "Mark a verified common asset public after operator license review (sha256, license_url)".into() }
    }
    async fn run(&self, ctx: &AppContext, vars: &task::Vars) -> Result<()> {
        let hash = vars.cli_arg("sha256")?;
        let license = vars.cli_arg("license_url")?;
        let url = reqwest::Url::parse(license)
            .map_err(|_| Error::BadRequest("invalid license URL".into()))?;
        if url.scheme() != "https"
            || url.host_str().is_none()
            || !url.username().is_empty()
            || url.password().is_some()
            || license.len() > 2048
        {
            return Err(Error::BadRequest(
                "an HTTPS license source is required".into(),
            ));
        }
        let asset = visual_assets::find(&ctx.db, hash)
            .await?
            .ok_or(Error::NotFound)?;
        verify_stream(
            ctx.storage
                .download_stream(Path::new(&asset.storage_key))
                .await?,
            hash,
            asset.bytes as u64,
        )
        .await?;
        ctx.db
            .execute_raw(visual_assets::statement(
                "UPDATE visual_assets SET public_license=$1 WHERE sha256=$2",
                vec![license.into(), hash.into()],
            ))
            .await?;
        tracing::info!(
            sha256 = hash,
            "rights-reviewed asset enabled for public immutable delivery"
        );
        Ok(())
    }
}
