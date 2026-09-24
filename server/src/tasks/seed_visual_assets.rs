//! Operator-only registration of the exact assets shipped in the mod package.
use crate::services::visual_assets::{self, AssetReference, BUNDLED_DEFAULT_OWNER};
use base64::{engine::general_purpose::STANDARD, Engine as _};
use bytes::Bytes;
use loco_rs::prelude::*;
use sha2::{Digest, Sha256};
use std::path::Path;

pub struct SeedVisualAssets;

// None means provenance is recorded in the package but no public license grant
// is asserted. Those objects still require a signed published-replay grant.
const ASSETS: &[(&str, &str, Option<&str>)] = &[
    ("Fonts/MAPLESTORY_OTF_BOLD.OTF", "font/otf", None),
    ("Fonts/adofai-cjk.woff", "font/woff", None),
    (
        "Fonts/LeeSeoyun.woff.base64",
        "font/woff",
        Some("Fonts/LeeSeoyun-LICENSE.txt"),
    ),
    (
        "Fonts/DungGeunMo.woff.base64",
        "font/woff",
        Some("Fonts/DungGeunMo-LICENSE.txt"),
    ),
    (
        "Fonts/Pretendard-Regular.woff.base64",
        "font/woff",
        Some("Fonts/Pretendard-LICENSE.txt"),
    ),
    (
        "Fonts/PretendardVariable.woff2.base64",
        "font/woff2",
        Some("Fonts/Pretendard-LICENSE.txt"),
    ),
    (
        "Fonts/SUIT-Regular.woff2.base64",
        "font/woff2",
        Some("Fonts/SUIT-LICENSE.txt"),
    ),
    (
        "Jipper/GhostRain.png.base64",
        "image/png",
        Some("Jipper/LICENSE.txt"),
    ),
    (
        "Jipper/KeyBackground.png.base64",
        "image/png",
        Some("Jipper/LICENSE.txt"),
    ),
    (
        "Jipper/KeyOutline.png.base64",
        "image/png",
        Some("Jipper/LICENSE.txt"),
    ),
    ("Jipper/ProgressBackground.png.base64", "image/png", None),
];

#[async_trait]
impl Task for SeedVisualAssets {
    fn task(&self) -> TaskInfo {
        TaskInfo {
            name: "seed_visual_assets".into(),
            detail:
                "Verify and register packaged default assets (directory: path to Visual/Assets)"
                    .into(),
        }
    }

    async fn run(&self, ctx: &AppContext, vars: &task::Vars) -> Result<()> {
        let root = Path::new(vars.cli_arg("directory")?);
        for &(name, media_type, license) in ASSETS {
            let data = tokio::fs::read(root.join(name)).await?;
            let data = if name.ends_with(".base64") {
                STANDARD
                    .decode(
                        data.into_iter()
                            .filter(|b| !b.is_ascii_whitespace())
                            .collect::<Vec<_>>(),
                    )
                    .map_err(|_| Error::Message("invalid bundled base64 asset".into()))?
            } else {
                data
            };
            let reference = AssetReference {
                sha256: hex::encode(Sha256::digest(&data)),
                bytes: data.len() as i64,
                media_type: media_type.into(),
            };
            visual_assets::store(ctx, BUNDLED_DEFAULT_OWNER, &reference, Bytes::from(data)).await?;
            if let Some(license) = license {
                let source = format!("https://github.com/KGH1113/TUFReplay/blob/feat/auto-submission/TUFReplay/Visual/Assets/{license}");
                ctx.db
                    .execute_raw(visual_assets::statement(
                        "UPDATE visual_assets SET public_license=$1 WHERE sha256=$2",
                        vec![source.into(), reference.sha256.clone().into()],
                    ))
                    .await?;
            }
            tracing::info!(
                asset = name,
                sha256 = reference.sha256,
                bytes = reference.bytes,
                public = license.is_some(),
                "bundled default verified in object storage"
            );
        }
        tracing::info!(verified = ASSETS.len(), "bundled defaults registered");
        Ok(())
    }
}
