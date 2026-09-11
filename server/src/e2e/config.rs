use loco_rs::prelude::*;

pub fn check_context(ctx: &AppContext) -> Result<()> {
    let settings = crate::settings::Settings::parse(&ctx.config)?;
    let base = &settings.auto_submission.catalog.tuf_api_base_url;
    if base != "http://127.0.0.1:5152"
        || ctx.config.server.binding != "127.0.0.1"
        || !local_resource(&ctx.config.database.uri, "/tuf_replay_e2e")
        || !local_resource(&settings.auto_submission.ingest.redis_url, "/14")
        || settings.auto_submission.ingest.redis_key_prefix != "e2e:"
        || !matches!(&ctx.config.queue, Some(loco_rs::config::QueueConfig::Postgres(queue)) if queue.uri == ctx.config.database.uri && !queue.dangerously_flush)
    {
        return Err(Error::Message(
            "E2E requires isolated local configuration".into(),
        ));
    }
    Ok(())
}

fn local_resource(value: &str, path: &str) -> bool {
    reqwest::Url::parse(value).is_ok_and(|url| {
        matches!(url.host_str(), Some("127.0.0.1" | "localhost" | "[::1]")) && url.path() == path
    })
}

#[cfg(test)]
mod tests {
    use super::local_resource;
    #[test]
    fn requires_local_dedicated_resources() {
        assert!(local_resource(
            "postgres://127.0.0.1/tuf_replay_e2e",
            "/tuf_replay_e2e"
        ));
        assert!(!local_resource(
            "postgres://example.com/tuf_replay_e2e",
            "/tuf_replay_e2e"
        ));
        assert!(!local_resource("redis://127.0.0.1/0", "/14"));
    }
}
