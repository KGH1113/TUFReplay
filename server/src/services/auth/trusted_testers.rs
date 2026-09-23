use super::{AccountIdentity, SubmissionDenialReason};
use loco_rs::prelude::*;
use sea_orm::{ConnectionTrait, DbBackend, Statement};
use uuid::Uuid;

/// OAuth/account eligibility and current database membership must both permit submission.
/// Do not cache membership: revocation must reach live streams and queued submissions.
pub(super) async fn apply(
    ctx: &AppContext,
    mut identity: AccountIdentity,
) -> Result<AccountIdentity> {
    if !identity.can_submit {
        return Ok(identity);
    }
    let owner = Uuid::parse_str(&identity.owner_id)
        .map_err(|_| Error::Unauthorized("invalid_owner_id".into()))?;
    let row = ctx
        .db
        .query_one_raw(Statement::from_sql_and_values(
            DbBackend::Postgres,
            "SELECT active FROM trusted_testers WHERE user_id = $1",
            [owner.into()],
        ))
        .await?;
    let active = match row {
        Some(row) => row.try_get::<bool>("", "active")?,
        None => false,
    };
    if !active {
        identity.can_submit = false;
        identity.denial_reason = Some(SubmissionDenialReason::AutoSubmissionTesterRequired);
    }
    Ok(identity)
}
