use chrono::{Duration, Utc};
use loco_rs::testing::prelude::*;
use sea_orm::{ActiveModelTrait, ActiveValue::Set, IntoActiveModel};
use serial_test::serial;
use tuf_replay_server::{
    app::App,
    models::{
        run_sessions::{Model, NewRunSession},
        run_submission_records::Entity as Records,
    },
};
use uuid::Uuid;

#[tokio::test]
#[serial]
async fn denied_tester_can_view_and_delete_own_runs_but_cannot_issue_or_submit() {
    request::<App, _, _>(|mut request, ctx| async move {
        let owner = Uuid::new_v4().to_string();
        let token = crate::support::authenticate_with_policy(&ctx, &owner, false).await;
        request.add_header(
            axum::http::header::AUTHORIZATION,
            format!("Bearer {token}")
                .parse::<axum::http::HeaderValue>()
                .unwrap(),
        );

        let account = request.get("/api/v1/account").await;
        assert_eq!(account.status_code(), 200, "{}", account.text());
        let account: serde_json::Value = account.json();
        assert_eq!(account["owner_id"], owner);
        assert_eq!(account["grant_id"], crate::support::GRANT.to_string());
        assert_eq!(account["client_id"], "fixture-client");
        assert_eq!(account["username"], "fixture-user");
        assert_eq!(account["nickname"], "Fixture User");
        assert_eq!(account["can_submit"], false);
        assert_eq!(account["denial_reason"], "auto_submission_tester_required");

        let run_id = Uuid::new_v4();
        let run = Model::create(
            &ctx.db,
            NewRunSession {
                pid: run_id,
                protocol_version: 1,
                client_game_version: "3.4.0".into(),
                client_mod_version: "test".into(),
                tuf_level_id: 42,
                level_revision_id: None,
                level_revision_chart_id: None,
                client_tuf_file_id: "fixture-file".into(),
                client_level_relative_path: "main.adofai".into(),
                upload_token_hash: vec![1; 32],
                lease_expires_at: (Utc::now() + Duration::seconds(45)).fixed_offset(),
                hard_expires_at: (Utc::now() + Duration::hours(1)).fixed_offset(),
            },
        )
        .await
        .unwrap()
        .mark_streaming(&ctx.db)
        .await
        .unwrap()
        .mark_sealed(&ctx.db)
        .await
        .unwrap();
        Records::create_authorized(&ctx.db, run.id, &owner, Some(crate::support::GRANT))
            .await
            .unwrap();
        let record = Records::record(&ctx.db, run.id).await.unwrap();
        let mut active = record.into_active_model();
        active.state = Set("evidence_ready".into());
        active.update(&ctx.db).await.unwrap();

        let list = request.get("/api/v1/runs").await;
        assert_eq!(list.status_code(), 200, "{}", list.text());
        let list: serde_json::Value = list.json();
        assert_eq!(list["runs"][0]["run_id"], run_id.to_string());

        let get = request.get(&format!("/api/v1/runs/{run_id}")).await;
        assert_eq!(get.status_code(), 200, "{}", get.text());
        assert_eq!(
            get.json::<serde_json::Value>()["run_id"],
            run_id.to_string()
        );

        let delete = request.delete(&format!("/api/v1/runs/{run_id}")).await;
        assert_eq!(delete.status_code(), 200, "{}", delete.text());
        assert_eq!(delete.json::<serde_json::Value>()["deleted"], true);

        let issue = request
            .post("/api/v1/runs")
            .json(&serde_json::json!({
                "protocol_version": 1,
                "client_game_version": "3.4.0",
                "client_mod_version": "test",
                "tuf_level_id": 42,
                "client_tuf_file_id": "fixture-file",
                "client_installed_payload_hash_hex": "a".repeat(64),
                "client_payload_hash_version": 1,
                "client_level_relative_path": "main.adofai"
            }))
            .await;
        assert_eq!(issue.status_code(), 401, "{}", issue.text());

        let submit = request.post(&format!("/api/v1/runs/{run_id}/submit")).await;
        assert_eq!(submit.status_code(), 401, "{}", submit.text());
    })
    .await;
}

#[tokio::test]
#[serial]
async fn local_membership_is_required_and_revocation_is_not_cached() {
    request::<App, _, _>(|mut request, ctx| async move {
        use sea_orm::{ConnectionTrait, DbBackend, Statement};
        use tuf_replay_server::services::auth::authorize_grant;
        let owner = Uuid::new_v4().to_string();
        let token = crate::support::authenticate(&ctx, &owner).await;
        request.add_header(
            axum::http::header::AUTHORIZATION,
            format!("Bearer {token}")
                .parse::<axum::http::HeaderValue>()
                .unwrap(),
        );
        assert!(authorize_grant(&ctx, &owner, Some(crate::support::GRANT))
            .await
            .is_ok());
        crate::support::set_tester(&ctx, &owner, false).await;
        assert!(authorize_grant(&ctx, &owner, Some(crate::support::GRANT))
            .await
            .is_err());
        let denied: serde_json::Value = request.get("/api/v1/account").await.json();
        assert_eq!(denied["can_submit"], false);
        assert_eq!(denied["denial_reason"], "auto_submission_tester_required");
        crate::support::set_tester(&ctx, &owner, true).await;
        assert!(authorize_grant(&ctx, &owner, Some(crate::support::GRANT))
            .await
            .is_ok());
        ctx.db
            .execute_raw(Statement::from_sql_and_values(
                DbBackend::Postgres,
                "DELETE FROM trusted_testers WHERE user_id=$1",
                [Uuid::parse_str(&owner).unwrap().into()],
            ))
            .await
            .unwrap();
        assert!(authorize_grant(&ctx, &owner, Some(crate::support::GRANT))
            .await
            .is_err());
        // A local grant cannot override an OAuth/account denial.
        crate::support::authenticate_with_policy(&ctx, &owner, false).await;
        crate::support::set_tester(&ctx, &owner, true).await;
        assert!(authorize_grant(&ctx, &owner, Some(crate::support::GRANT))
            .await
            .is_err());
    })
    .await;
}
