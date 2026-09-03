use tuf_replay_server::models::level_revision_charts::normalize_relative_chart_path;

#[test]
fn normalizes_only_safe_relative_chart_paths() {
    assert_eq!(
        normalize_relative_chart_path("folder\\level.adofai").expect("valid path"),
        "folder/level.adofai"
    );
    for invalid in [
        "/level.adofai",
        "../level.adofai",
        "folder//level.adofai",
        "song.mp3",
    ] {
        assert!(normalize_relative_chart_path(invalid).is_err(), "{invalid}");
    }
}
