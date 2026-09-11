use crate::domain::PassRegistrar;
use crate::domain::ValidatedResult;
use async_trait::async_trait;

pub struct TufRegistrar {
    pub client: reqwest::Client,
    pub base_url: String,
    pub tokens: Option<crate::services::auth::internal::InternalTokens>,
}

#[async_trait]
impl PassRegistrar for TufRegistrar {
    async fn lookup(
        &self,
        run_id: uuid::Uuid,
        owner_id: &str,
        evidence_digest: &str,
    ) -> Result<Option<i64>, String> {
        let tokens = self.tokens.as_ref().ok_or("service_not_configured")?;
        let mut url = reqwest::Url::parse(&format!(
            "{}/v2/internal/auto-submission/receipts/{run_id}",
            self.base_url
        ))
        .map_err(|_| "invalid_registration_url")?;
        url.query_pairs_mut()
            .append_pair("owner_id", owner_id)
            .append_pair("evidence_digest", evidence_digest);
        let response = self
            .client
            .get(url)
            .bearer_auth(tokens.outgoing())
            .send()
            .await
            .map_err(|_| "registration_unavailable")?;
        if response.status() == reqwest::StatusCode::NOT_FOUND {
            return Ok(None);
        }
        if !response.status().is_success() {
            return Err("registration_unavailable".into());
        }
        let value: serde_json::Value = response
            .json()
            .await
            .map_err(|_| "registration_invalid_response")?;
        value
            .get("pass_id")
            .and_then(|v| v.as_i64())
            .filter(|id| *id > 0)
            .map(Some)
            .ok_or_else(|| "registration_invalid_response".into())
    }
    async fn register(
        &self,
        run_id: uuid::Uuid,
        owner_id: &str,
        grant_id: uuid::Uuid,
        level_id: i64,
        current_file_id: &str,
        result: &ValidatedResult,
    ) -> Result<i64, String> {
        let tokens = self.tokens.as_ref().ok_or("service_not_configured")?;
        let response = self
            .client
            .post(format!(
                "{}/v2/internal/auto-submission/register",
                self.base_url
            ))
            .bearer_auth(tokens.outgoing())
            .json(
                &serde_json::json!({"run_id":run_id,"owner_id":owner_id,"grant_id":grant_id,
                "level_id":level_id,"current_file_id":current_file_id,"validation":result}),
            )
            .send()
            .await
            .map_err(|_| "registration_unavailable")?;
        if !response.status().is_success() {
            if response.status() == reqwest::StatusCode::CONFLICT {
                let body: serde_json::Value = response
                    .json()
                    .await
                    .map_err(|_| "registration_invalid_response")?;
                return Err(if body.get("error").and_then(|v| v.as_str())
                    == Some("level_revision_changed")
                {
                    "level_revision_changed"
                } else {
                    "registration_rejected"
                }
                .into());
            }
            return Err(if matches!(response.status().as_u16(), 400 | 403 | 422) {
                "registration_rejected"
            } else {
                "registration_unavailable"
            }
            .into());
        }
        let body: serde_json::Value = response
            .json()
            .await
            .map_err(|_| "registration_invalid_response")?;
        body.get("pass_id")
            .and_then(|v| v.as_i64())
            .filter(|id| *id > 0)
            .ok_or_else(|| "registration_invalid_response".into())
    }
}
