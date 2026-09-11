use loco_rs::prelude::*;

mod actions;
mod dtos;

pub fn routes() -> Routes {
    Routes::new()
        .prefix("/api/v1/replays")
        .add("/{run_id}", get(actions::manifest))
        .add("/{run_id}/files/{file_name}", get(actions::file))
}
