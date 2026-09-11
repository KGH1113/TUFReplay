use crate::domain::UnavailableValidator;
use crate::services::submission::SubmissionRuntime;
use crate::services::tuf::registration::TufRegistrar;
use loco_rs::{app::Initializer, prelude::*};
use std::{sync::Arc, time::Duration};

pub struct SubmissionInitializer;

#[async_trait]
impl Initializer for SubmissionInitializer {
    fn name(&self) -> String {
        "submission".into()
    }
    async fn before_run(&self, ctx: &AppContext) -> Result<()> {
        let base = crate::settings::Settings::get(ctx)?
            .auto_submission
            .catalog
            .tuf_api_base_url;
        let tokens = ctx
            .shared_store
            .get::<crate::services::auth::internal::InternalTokens>();
        let client = reqwest::Client::builder()
            .connect_timeout(Duration::from_secs(5))
            .timeout(Duration::from_secs(20))
            .redirect(reqwest::redirect::Policy::none())
            .build()
            .map_err(|_| Error::Message("cannot create TUF client".into()))?;
        ctx.shared_store
            .insert(crate::services::auth::IdentityService(Arc::new(
                crate::services::tuf::identity::TufIdentityClient {
                    client: client.clone(),
                    tokens: tokens.clone(),
                    base_url: base.trim_end_matches('/').into(),
                },
            )));
        let runtime = Arc::new(SubmissionRuntime {
            charts: Arc::new(
                ctx.shared_store
                    .get::<crate::services::tuf::catalog::TufCatalogRuntime>()
                    .ok_or_else(|| Error::Message("catalog unavailable".into()))?,
            ),
            validator: Arc::new(UnavailableValidator),
            registrar: Arc::new(TufRegistrar {
                client,
                tokens,
                base_url: base.trim_end_matches('/').into(),
            }),
            evidence_slots: tokio::sync::Semaphore::new(2),
        });
        ctx.shared_store.insert(runtime.clone());
        Ok(())
    }
}
