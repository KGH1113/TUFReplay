use loco_rs::{task, testing::prelude::*};
use tuf_replay_server::app::App;

use loco_rs::boot::run_task;
use serial_test::serial;

#[tokio::test]
#[serial]
async fn test_can_run_cleanup_evidence() {
    let boot = boot_test::<App>().await.unwrap();

    assert!(run_task::<App>(
        &boot.app_context,
        Some(&"cleanup_evidence".to_string()),
        &task::Vars::default()
    )
    .await
    .is_ok());
}
