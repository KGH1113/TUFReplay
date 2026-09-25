//! LevelData.Decode v9..v16 Pause migration, ported from the installed game.
//! Preserve the game's single-precision constants before promoting to f64.
use super::submission_gameplay_hash::SubmissionHashError;
use serde_json::{json, Value};

pub(super) fn migrate(
    actions: &mut [Value],
    angles: &[f32],
    version: i32,
) -> Result<(), SubmissionHashError> {
    if !(9..=16).contains(&version) {
        return Ok(());
    }
    let mut twirls = vec![false; angles.len()];
    let mut planets = vec![None; angles.len()];
    for action in actions.iter() {
        let Some(floor) = action["floor"]
            .as_u64()
            .and_then(|f| f.checked_sub(1))
            .map(|f| f as usize)
            .filter(|f| *f < angles.len())
        else {
            continue;
        };
        match action["eventType"].as_str() {
            Some("Twirl") => twirls[floor] = !twirls[floor],
            Some("MultiPlanet") => {
                planets[floor] = Some(
                    match super::submission_gameplay_hash::enum_text(
                        "planets",
                        action.get("planets").unwrap_or(&json!("TwoPlanets")),
                    )?
                    .as_str()
                    {
                        "TwoPlanets" => 2.0,
                        "ThreePlanets" => 3.0,
                        _ => return Err(SubmissionHashError),
                    },
                )
            }
            _ => {}
        }
    }
    #[derive(Clone)]
    struct Floor {
        entry: f64,
        exit: f64,
        ccw: bool,
        midspin: bool,
        planets: f64,
        turnaround: bool,
    }
    let mut floors = vec![Floor {
        entry: 4.71238899230957,
        exit: 0.0,
        ccw: false,
        midspin: false,
        planets: 2.0,
        turnaround: false,
    }];
    let mut ccw = false;
    let mut count = 2.0;
    let tau = f64::from(2.0 * std::f32::consts::PI);
    let pi = f64::from(std::f32::consts::PI);
    for (i, angle) in angles.iter().enumerate() {
        let exit = if *angle == 999.0 {
            f64::from(floors[i].entry as f32)
        } else {
            f64::from((-angle + 90.0) * (std::f32::consts::PI / 180.0))
        };
        floors[i].exit = exit;
        floors[i].midspin = *angle == 999.0;
        ccw ^= twirls[i];
        if let Some(n) = planets[i] {
            count = n;
        }
        floors.push(Floor {
            entry: (exit + pi) % tau,
            exit: 0.0,
            ccw,
            midspin: false,
            planets: count,
            turnaround: false,
        });
    }
    let angle_moved = |entry: f64, exit: f64, ccw: bool| {
        let x = (exit - entry) * if ccw { -1.0 } else { 1.0 };
        (x % tau + tau) % tau
    };
    for i in 0..floors.len() {
        let f = &floors[i];
        // 3.1415926 is the literal used by GetInverseAnglePerBeatMultiplanet.
        #[allow(clippy::approx_constant)] // Must match the game's literal, not mathematical PI.
        let inverse = 3.141_592_6 * (f.planets - 2.0) / f.planets;
        let sign = if f.ccw { -1.0 } else { 1.0 };
        let mut offset = if f.midspin { 0.0 } else { inverse * sign };
        if floors[i.saturating_sub(1)].midspin && f.planets > 2.0 {
            offset -= (tau + inverse) * sign;
        }
        let moved = angle_moved(
            f.entry + offset,
            f.exit + if f.midspin { offset } else { 0.0 },
            f.ccw,
        )
        .abs();
        floors[i].turnaround = (moved <= 1e-6 || moved >= 6.283184482025146) && !f.midspin;
    }
    for action in actions.iter_mut() {
        if action["eventType"] == "FreeRoam" {
            // Current LevelEvent.Decode converts duration using Convert.ToInt32
            // before the legacy migration, then clamps negative durations.
            let duration = action
                .get("duration")
                .and_then(Value::as_f64)
                .unwrap_or(16.0);
            action["duration"] = json!(duration.round_ties_even().max(0.0));
        }
        if action["eventType"] != "Pause" {
            continue;
        }
        let floor = action["floor"]
            .as_u64()
            .and_then(|i| floors.get(i as usize))
            .ok_or(SubmissionHashError)?;
        let duration = action
            .get("duration")
            .and_then(Value::as_f64)
            .unwrap_or(1.0) as f32;
        let migrated = if version == 9 {
            duration + if floor.turnaround { 1.0 } else { 0.0 }
        } else {
            let old_turnaround =
                (angle_moved(floor.entry, floor.exit, floor.ccw) - tau).abs() < 0.0001;
            if floor.turnaround ^ old_turnaround {
                (duration + if floor.turnaround { -1.0 } else { 1.0 }).max(0.0)
            } else {
                duration
            }
        };
        action["duration"] = json!(migrated);
    }
    Ok(())
}
