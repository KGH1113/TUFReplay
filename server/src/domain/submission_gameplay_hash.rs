//! Submission identity is separate from persisted replay/activity identities.
//! See server/contracts/submission-gameplay-v1.json and cross-language vectors.
use serde_json::Value;
use sha2::{Digest, Sha256};
use std::sync::LazyLock;

pub const SUBMISSION_GAMEPLAY_HASH_VERSION: u32 = 1;
static CONTRACT: LazyLock<Value> = LazyLock::new(|| {
    serde_json::from_str(include_str!("../../contracts/submission-gameplay-v1.json")).unwrap()
});

#[derive(Debug, thiserror::Error)]
#[error("submission_chart_unsupported")]
pub struct SubmissionHashError;
type Result<T> = std::result::Result<T, SubmissionHashError>;

#[derive(Default)]
struct Writer(Vec<u8>);
impl Writer {
    fn int(&mut self, n: i32) {
        self.0.extend(n.to_be_bytes());
    }
    fn float(&mut self, n: f32) -> Result<()> {
        if !n.is_finite() {
            return Err(SubmissionHashError);
        }
        self.0
            .extend((if n == 0.0 { 0.0 } else { n }).to_be_bytes());
        Ok(())
    }
    fn text(&mut self, s: &str) {
        self.int(s.len() as i32);
        self.0.extend(s.as_bytes());
    }
    fn fields(&mut self, source: &Value, fields: &Value, event: &str) -> Result<()> {
        for field in fields.as_array().ok_or(SubmissionHashError)? {
            let key = field[0].as_str().ok_or(SubmissionHashError)?;
            let mut v = source.get(key).unwrap_or(&field[2]);
            let speed_type =
                enum_text("speedType", source.get("speedType").unwrap_or(&Value::Null))
                    .unwrap_or_else(|_| "Bpm".into());
            if event == "SetSpeed"
                && ((key == "beatsPerMinute" && speed_type == "Multiplier")
                    || (key == "bpmMultiplier" && speed_type == "Bpm"))
            {
                v = &field[2];
            }
            match field[1].as_str() {
                Some("float") => self.float(number(v)? as f32)?,
                Some("int") => self.int(integer(v)?),
                Some("bool") => self.0.push(boolean(v)? as u8),
                Some("string") => self.text(&enum_text(key, v)?),
                Some("vector") => {
                    let a = v
                        .as_array()
                        .filter(|a| a.len() == 2)
                        .ok_or(SubmissionHashError)?;
                    self.float(number(&a[0])? as f32)?;
                    self.float(number(&a[1])? as f32)?;
                }
                _ => return Err(SubmissionHashError),
            }
        }
        Ok(())
    }
}

pub fn compute_submission_gameplay_hash(bytes: &[u8]) -> Result<String> {
    let text = std::str::from_utf8(bytes).map_err(|_| SubmissionHashError)?;
    let chart: Value =
        json5::from_str(text.trim_start_matches('\u{feff}')).map_err(|_| SubmissionHashError)?;
    let settings = chart
        .get("settings")
        .filter(|s| s.is_object())
        .ok_or(SubmissionHashError)?;
    let version = settings
        .get("version")
        .map(integer)
        .transpose()?
        .unwrap_or(0);
    if version < 5
        || settings
            .get("legacySpriteTiles")
            .map(boolean)
            .transpose()?
            .unwrap_or(false)
    {
        return Err(SubmissionHashError);
    }
    let angles = if let Some(path) = chart["pathData"].as_str() {
        migrate_path(path)?
    } else {
        chart["angleData"]
            .as_array()
            .ok_or(SubmissionHashError)?
            .iter()
            .map(|v| {
                if v.is_null() {
                    Ok(0.0)
                } else {
                    Ok(number(v)? as f32)
                }
            })
            .collect::<Result<Vec<_>>>()?
    };
    let mut writer = Writer::default();
    writer.text("tuf-submission-gameplay");
    writer.int(SUBMISSION_GAMEPLAY_HASH_VERSION as i32);
    writer.fields(settings, &CONTRACT["settings"], "")?;
    writer.int(angles.len() as i32);
    for angle in &angles {
        let value = if *angle == 999.0 {
            *angle
        } else {
            let n = *angle % 360.0;
            if n < 0.0 {
                n + 360.0
            } else {
                n
            }
        };
        writer.float(value)?;
    }
    let mut actions = chart["actions"]
        .as_array()
        .ok_or(SubmissionHashError)?
        .clone();
    // Decode migrates even inactive timing events; canonical filtering follows.
    super::submission_legacy_timing::migrate(&mut actions, &angles, version)?;
    let mut events = Vec::new();
    for action in &actions {
        // LevelEvent.Decode recognizes an actual JSON false for active.
        if action["active"] == false {
            continue;
        }
        reject_hitbox(action)?;
        let name = action["eventType"].as_str().ok_or(SubmissionHashError)?;
        if CONTRACT["events"].get(name).is_some() {
            let floor = integer(action.get("floor").ok_or(SubmissionHashError)?)?;
            if floor < 0 || floor as usize > angles.len() {
                return Err(SubmissionHashError);
            }
            events.push((floor, name, action));
        } else if !CONTRACT["visualEvents"]
            .as_array()
            .unwrap()
            .iter()
            .any(|v| v == name)
        {
            return Err(SubmissionHashError);
        }
    }
    if let Some(decorations) = chart["decorations"].as_array() {
        for decoration in decorations {
            reject_hitbox(decoration)?;
        }
    }
    // Stable sorting retains order for multiple same-kind events on one floor.
    events.sort_by_key(|(floor, name, _)| (*floor, *name));
    writer.int(events.len() as i32);
    for (floor, name, action) in events {
        writer.int(floor);
        writer.text(name);
        writer.fields(action, &CONTRACT["events"][name], name)?;
    }
    Ok(hex::encode(Sha256::digest(writer.0)))
}

fn reject_hitbox(v: &Value) -> Result<()> {
    if v["active"] == false {
        return Ok(());
    }
    if v.get("hitbox").is_some_and(|v| !v.is_null() && v != "None")
        || v.get("failHitbox")
            .is_some_and(|v| v == true || v == "Enabled")
    {
        return Err(SubmissionHashError);
    }
    Ok(())
}
fn number(v: &Value) -> Result<f64> {
    v.as_f64()
        .filter(|v| v.is_finite())
        .ok_or(SubmissionHashError)
}
fn integer(v: &Value) -> Result<i32> {
    let n = number(v)?;
    if n < i32::MIN as f64 || n > i32::MAX as f64 {
        return Err(SubmissionHashError);
    }
    Ok(n.round_ties_even() as i32)
}
pub(super) fn enum_text(key: &str, v: &Value) -> Result<String> {
    let text = match v {
        Value::String(s) => s.clone(),
        Value::Number(n) if n.is_i64() => n.to_string(),
        _ => return Err(SubmissionHashError),
    };
    Ok(CONTRACT["enums"][key][&text]
        .as_str()
        .unwrap_or(&text)
        .to_owned())
}

fn boolean(v: &Value) -> Result<bool> {
    match v {
        Value::Bool(v) => Ok(*v),
        Value::String(v) if v == "Enabled" => Ok(true),
        Value::String(v) if v == "Disabled" => Ok(false),
        _ => Err(SubmissionHashError),
    }
}

// FloorHelper.MigratePathData from the game, including relative path letters.
fn migrate_path(path: &str) -> Result<Vec<f32>> {
    let mut previous = 0.0;
    path.chars()
        .map(|c| {
            let value = match c {
                'R' => 0.0,
                'E' => 45.0,
                'U' => 90.0,
                'Q' => 135.0,
                'L' => 180.0,
                'Z' => 225.0,
                'D' => 270.0,
                'C' => 315.0,
                'B' => 300.0,
                'T' => 60.0,
                'G' => 120.0,
                'F' => 240.0,
                'J' => 30.0,
                'H' => 150.0,
                'N' => 210.0,
                'M' => 330.0,
                'p' => 15.0,
                'o' => 75.0,
                'q' => 105.0,
                'W' => 165.0,
                'x' => 195.0,
                'V' => 255.0,
                'Y' => 285.0,
                'A' => 345.0,
                '!' => 999.0,
                '5' => previous + 72.0,
                '6' => previous - 72.0,
                '7' => previous + 52.0,
                '8' => previous - 52.0,
                '9' => previous - 30.0,
                'h' => previous + 120.0,
                'j' => previous - 120.0,
                't' => previous + 60.0,
                'y' => previous + 300.0,
                _ => return Err(SubmissionHashError),
            };
            previous = value;
            Ok(value)
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn fixtures() -> Value {
        serde_json::from_str(include_str!(
            "../../contracts/submission-gameplay-v1-vectors.json"
        ))
        .unwrap()
    }
    fn hash(v: &Value) -> String {
        compute_submission_gameplay_hash(&serde_json::to_vec(v).unwrap()).unwrap()
    }

    #[test]
    fn shared_csharp_vectors_and_visual_only_variants() {
        for fixture in fixtures().as_array().unwrap() {
            let chart = &fixture["chart"];
            assert_eq!(hash(chart), fixture["sha256"].as_str().unwrap());
            let mut visual = chart.clone();
            visual["decorations"] = json!([]);
            visual["settings"]["trackColor"] = json!("00ff00");
            visual["settings"]["backgroundColor"] = json!("ffffff");
            visual["actions"].as_array_mut().unwrap().retain(|e| {
                CONTRACT["events"]
                    .get(e["eventType"].as_str().unwrap())
                    .is_some()
            });
            assert_eq!(hash(chart), hash(&visual));
        }
    }

    #[test]
    fn excerpt_autoplay_timing_judgment_and_safety_changes_differ() {
        let chart = fixtures()[0]["chart"].clone();
        let expected = hash(&chart);
        for pointer in [
            "/settings/bpm",
            "/settings/offset",
            "/angleData/0",
            "/actions/2/angleOffset",
            "/actions/3/duration",
            "/actions/5/duration",
            "/actions/7/taps",
            "/actions/8/scale",
        ] {
            let mut changed = chart.clone();
            *changed.pointer_mut(pointer).unwrap() = json!(42);
            assert_ne!(expected, hash(&changed), "{pointer}");
        }
        for pointer in ["/actions/6/enabled", "/actions/6/safetyTiles"] {
            let mut changed = chart.clone();
            let v = changed.pointer_mut(pointer).unwrap();
            *v = json!(!v.as_bool().unwrap());
            assert_ne!(expected, hash(&changed), "{pointer}");
        }
        let mut excerpt = chart.clone();
        excerpt["angleData"].as_array_mut().unwrap().remove(0);
        excerpt["actions"] = json!([]);
        assert_ne!(expected, hash(&excerpt));
        let mut auto = chart.clone();
        auto["actions"]
            .as_array_mut()
            .unwrap()
            .push(json!({"floor":0,"eventType":"AutoPlayTiles"}));
        assert_ne!(expected, hash(&auto));
    }

    #[test]
    fn unknown_rules_hitboxes_and_unsupported_migrations_fail_closed() {
        for event in [
            "CallMethod",
            "AddComponent",
            "SetInputEvent",
            "KillPlayer",
            "FutureRule",
        ] {
            let mut v = fixtures()[0]["chart"].clone();
            v["actions"]
                .as_array_mut()
                .unwrap()
                .push(json!({"floor":0,"eventType":event}));
            assert!(compute_submission_gameplay_hash(&serde_json::to_vec(&v).unwrap()).is_err());
        }
        let mut v = fixtures()[0]["chart"].clone();
        v["decorations"][0]["hitbox"] = json!("Kill");
        assert!(compute_submission_gameplay_hash(&serde_json::to_vec(&v).unwrap()).is_err());
        v = fixtures()[0]["chart"].clone();
        v["settings"]["legacySpriteTiles"] = json!(true);
        assert!(compute_submission_gameplay_hash(&serde_json::to_vec(&v).unwrap()).is_err());
    }

    #[test]
    fn legacy_path_conversion_matches_loaded_angles() {
        let mut v = json!({"settings":{"version":17},"pathData":"RUT!5","actions":[]});
        let expected = hash(&v);
        v.as_object_mut().unwrap().remove("pathData");
        v["angleData"] = json!([0, 90, 60, 999, 1071]);
        assert_eq!(expected, hash(&v));
    }

    #[test]
    fn legacy_pause_migration_matches_game_loaded_duration() {
        let mut source = json!({"settings":{"version":9,"bpm":100},"angleData":[0,180],"actions":[{"floor":1,"eventType":"Pause","duration":1}]});
        let mut loaded = source.clone();
        loaded["settings"]["version"] = json!(17);
        loaded["actions"][0]["duration"] = json!(2);
        assert_eq!(hash(&source), hash(&loaded));
        source["settings"]["version"] = json!(16);
        loaded["actions"][0]["duration"] = json!(0);
        assert_eq!(hash(&source), hash(&loaded));
    }

    #[test]
    fn numeric_enum_encodings_match_runtime_enum_names() {
        let source = fixtures()[0]["chart"].clone();
        let mut numeric = source.clone();
        numeric["actions"][2]["speedType"] = json!(1);
        numeric["actions"][4]["planets"] = json!(3);
        numeric["actions"][5]["angleCorrectionDir"] = json!(1);
        assert_eq!(hash(&source), hash(&numeric));
    }
}
