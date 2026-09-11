use chrono::Utc;
use loco_rs::testing::prelude::*;
use sea_orm::{ActiveModelTrait, ActiveValue::Set, EntityTrait};
use serial_test::serial;
use tuf_replay_server::app::App;
use tuf_replay_server::models::level_revisions::ActiveModel;
use tuf_replay_server::models::level_revisions::Entity;

#[tokio::test]
#[serial]
async fn revision_identity_is_unique_and_rows_are_immutable() {
    let boot = boot_test::<App>().await.expect("test app boot");
    let db = &boot.app_context.db;
    let new_revision = || ActiveModel {
        tuf_level_id: Set(42),
        tuf_file_id: Set("file-1".to_owned()),
        canonical_payload_hash: Set(vec![1; 32]),
        payload_hash_version: Set(1),
        archive_storage_key: Set("archives/one.zip".to_owned()),
        source_updated_at: Set(None),
        fetched_at: Set(Utc::now().fixed_offset()),
        ..Default::default()
    };
    let revision = new_revision().insert(db).await.expect("insert revision");
    assert!(new_revision().insert(db).await.is_err());

    let mut active: ActiveModel = revision.into();
    active.tuf_file_id = Set("changed".to_owned());
    assert!(active.update(db).await.is_err());

    Entity::delete_many().exec(db).await.expect("cleanup");
}
