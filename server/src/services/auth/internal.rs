use axum::http::HeaderMap;
use loco_rs::prelude::*;
use sha2::{Digest, Sha256};

/// Credentials are deliberately independent of user OAuth and upload credentials.
#[derive(Clone)]
pub struct InternalTokens {
    incoming: String,
    outgoing: String,
}

impl InternalTokens {
    pub fn from_env() -> Result<Self> {
        Self::new(
            std::env::var("TUF_TO_AUTO_SUBMISSION_TOKEN").unwrap_or_default(),
            std::env::var("AUTO_SUBMISSION_TO_TUF_TOKEN").unwrap_or_default(),
        )
    }

    fn new(incoming: String, outgoing: String) -> Result<Self> {
        let valid = |value: &str| {
            (32..=512).contains(&value.len()) && value.bytes().all(|byte| byte.is_ascii_graphic())
        };
        if !valid(&incoming) || !valid(&outgoing) || same_token(&incoming, &outgoing) {
            return Err(Error::Unauthorized("internal_auth_not_configured".into()));
        }
        Ok(Self { incoming, outgoing })
    }

    pub fn authorize(&self, headers: &HeaderMap) -> Result<()> {
        let incoming = crate::services::auth::bearer(headers)?;
        if !same_token(incoming, &self.incoming) {
            return Err(Error::Unauthorized("unauthorized".into()));
        }
        Ok(())
    }

    pub fn outgoing(&self) -> &str {
        &self.outgoing
    }
}

fn same_token(left: &str, right: &str) -> bool {
    Sha256::digest(left.as_bytes())
        .iter()
        .zip(Sha256::digest(right.as_bytes()).iter())
        .fold(0u8, |difference, (a, b)| difference | (a ^ b))
        == 0
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn only_the_incoming_direction_is_authorized() {
        let tokens = InternalTokens::new("a".repeat(48), "b".repeat(48)).unwrap();
        for (credential, accepted) in [
            (format!("Bearer {}", "a".repeat(48)), true),
            (format!("Bearer {}", "b".repeat(48)), false),
            ("Bearer eyJhbGciOiJIUzI1NiJ9.oauth.signature".into(), false),
            ("session=site-cookie".into(), false),
            ("".into(), false),
        ] {
            let mut headers = HeaderMap::new();
            headers.insert("authorization", credential.parse().unwrap());
            assert_eq!(tokens.authorize(&headers).is_ok(), accepted);
        }
        assert_eq!(tokens.outgoing(), "b".repeat(48));
    }

    #[test]
    fn missing_or_shared_credentials_fail_closed() {
        assert!(InternalTokens::new(String::new(), "b".repeat(48)).is_err());
        assert!(InternalTokens::new("a".repeat(48), "a".repeat(48)).is_err());
        assert!(InternalTokens::new("a".repeat(48), " ".repeat(48)).is_err());
    }
}
