mod adofai_json;
pub mod eligibility;
pub mod evidence;
pub mod gameplay_hash;
pub mod registration;
pub mod submission_gameplay_hash;
mod submission_legacy_timing;
pub mod validation;

pub use eligibility::is_eligible_difficulty;
pub use evidence::{EvidenceManifest, EvidenceStream};
pub use gameplay_hash::{compute_gameplay_hash, GAMEPLAY_HASH_VERSION};
pub use registration::PassRegistrar;
pub use validation::{
    GameplayValidator, OfficialChart, OfficialChartProvider, ResultProvenance,
    UnavailableValidator, ValidatedResult, ValidationOutcome, ValidationStatus,
};
