use serde_json::{Map, Value};
use sha2::{Digest, Sha256};

pub const GAMEPLAY_HASH_VERSION: u32 = 1;
const CANONICAL_PAYLOAD_VERSION: i32 = 3;

#[derive(Debug, thiserror::Error)]
pub enum GameplayHashError {
    #[error("chart_json_invalid")]
    InvalidJson,
    #[error("chart_gameplay_data_invalid")]
    InvalidData,
}

struct CanonicalWriter(Vec<u8>);

impl CanonicalWriter {
    fn byte(&mut self, value: u8) {
        self.0.push(value);
    }

    fn int(&mut self, value: i32) {
        self.0.extend_from_slice(&value.to_be_bytes());
    }

    fn float(&mut self, value: f32) {
        self.0.extend_from_slice(&value.to_be_bytes());
    }

    fn string(&mut self, value: &str) {
        self.int(value.len() as i32);
        self.0.extend_from_slice(value.as_bytes());
    }

    fn event(&mut self, floor: i32, kind: u8) {
        self.int(floor);
        self.byte(kind);
    }

    fn finish(self) -> String {
        hex::encode(Sha256::digest(self.0))
    }
}

#[derive(Clone)]
struct GameplayEvent<'a> {
    floor: i32,
    kind: u8,
    index: usize,
    value: &'a Map<String, Value>,
}

pub fn compute_gameplay_hash(bytes: &[u8]) -> Result<String, GameplayHashError> {
    let text = std::str::from_utf8(bytes).map_err(|_| GameplayHashError::InvalidJson)?;
    let chart: Value = json5::from_str(text.trim_start_matches('\u{feff}'))
        .map_err(|_| GameplayHashError::InvalidJson)?;
    let chart = chart.as_object().ok_or(GameplayHashError::InvalidData)?;
    let settings = object(chart, "settings")?;
    let base_bpm = number(settings, "bpm", None)? as f32;
    let mut writer = CanonicalWriter(Vec::new());
    writer.int(CANONICAL_PAYLOAD_VERSION);
    writer.string(string(settings, "songFilename", ""));
    writer.float(base_bpm);
    writer.int(integer(settings, "volume", 100)?);
    writer.int(integer(settings, "offset", 0)?);
    writer.byte(boolean(settings.get("separateCountdownTime"), false) as u8);
    writer.int(integer(settings, "countdownTicks", 4)?);
    writer.float(number(settings, "speedTrialAim", Some(0.0))? as f32);
    writer.byte(boolean(settings.get("legacySpriteTiles"), false) as u8);

    if let Some(path) = chart.get("pathData").and_then(Value::as_str) {
        writer.byte(0);
        writer.string(path);
    } else {
        writer.byte(1);
        let angles = chart
            .get("angleData")
            .and_then(Value::as_array)
            .ok_or(GameplayHashError::InvalidData)?;
        writer.int(i32::try_from(angles.len()).map_err(|_| GameplayHashError::InvalidData)?);
        for angle in angles {
            let value = if angle.is_null() {
                0.0
            } else {
                angle.as_f64().ok_or(GameplayHashError::InvalidData)? as f32
            };
            writer.float(normalize_angle(value));
        }
    }

    let mut events = chart
        .get("actions")
        .and_then(Value::as_array)
        .into_iter()
        .flatten()
        .enumerate()
        .filter_map(|(index, action)| gameplay_event(action, index))
        .collect::<Result<Vec<_>, _>>()?;
    events.sort_by_key(|event| (event.floor, event.kind, event.index));
    for event in events {
        write_event(&mut writer, &event, base_bpm)?;
    }
    Ok(writer.finish())
}

fn gameplay_event(
    value: &Value,
    index: usize,
) -> Option<Result<GameplayEvent<'_>, GameplayHashError>> {
    let value = value.as_object()?;
    if !boolean(value.get("active"), true) {
        return None;
    }
    let kind = match value.get("eventType").and_then(Value::as_str)? {
        "SetSpeed" => 0,
        "Twirl" => 1,
        "Hold" => 2,
        "MultiPlanet" => 3,
        "Pause" => 4,
        "AutoPlayTiles" => 5,
        "ScaleMargin" => 6,
        "Multitap" => 7,
        "KillPlayer" => 8,
        _ => return None,
    };
    Some(integer(value, "floor", 0).map(|floor| GameplayEvent {
        floor,
        kind,
        index,
        value,
    }))
}

fn write_event(
    writer: &mut CanonicalWriter,
    event: &GameplayEvent<'_>,
    base_bpm: f32,
) -> Result<(), GameplayHashError> {
    writer.event(event.floor, event.kind);
    match event.kind {
        0 => {
            let multiplier =
                event.value.get("speedType").and_then(Value::as_str) == Some("Multiplier");
            let bpm = if multiplier {
                base_bpm * number(event.value, "bpmMultiplier", Some(1.0))? as f32
            } else {
                number(event.value, "beatsPerMinute", Some(f64::from(base_bpm)))? as f32
            };
            writer.byte(0);
            writer.float(bpm);
        }
        2 => writer.int(integer(event.value, "duration", 0)?),
        3 => writer.byte(planet_count(event.value.get("planets"))?),
        4 => writer.float(number(event.value, "duration", Some(0.0))? as f32),
        5 => writer.byte(boolean(event.value.get("enabled"), true) as u8),
        6 => writer.float(number(event.value, "scale", Some(100.0))? as f32),
        7 => writer.float(number(event.value, "taps", Some(1.0))? as f32),
        _ => {}
    }
    Ok(())
}

fn object<'a>(
    value: &'a Map<String, Value>,
    key: &str,
) -> Result<&'a Map<String, Value>, GameplayHashError> {
    value
        .get(key)
        .and_then(Value::as_object)
        .ok_or(GameplayHashError::InvalidData)
}

fn string<'a>(value: &'a Map<String, Value>, key: &str, default: &'a str) -> &'a str {
    value.get(key).and_then(Value::as_str).unwrap_or(default)
}

fn number(
    value: &Map<String, Value>,
    key: &str,
    default: Option<f64>,
) -> Result<f64, GameplayHashError> {
    value
        .get(key)
        .and_then(Value::as_f64)
        .or(default)
        .filter(|value| value.is_finite())
        .ok_or(GameplayHashError::InvalidData)
}

fn integer(value: &Map<String, Value>, key: &str, default: i32) -> Result<i32, GameplayHashError> {
    let value = value
        .get(key)
        .and_then(Value::as_f64)
        .unwrap_or(f64::from(default));
    if !value.is_finite() || value < f64::from(i32::MIN) || value > f64::from(i32::MAX) {
        return Err(GameplayHashError::InvalidData);
    }
    Ok(value.trunc() as i32)
}

fn boolean(value: Option<&Value>, default: bool) -> bool {
    match value {
        Some(Value::Bool(value)) => *value,
        Some(Value::String(value)) if value == "Enabled" => true,
        Some(Value::String(value)) if value == "Disabled" => false,
        _ => default,
    }
}

fn planet_count(value: Option<&Value>) -> Result<u8, GameplayHashError> {
    let count = match value {
        Some(Value::String(value)) if value == "TwoPlanets" => 2,
        Some(Value::String(value)) if value == "ThreePlanets" => 3,
        Some(value) => value.as_u64().ok_or(GameplayHashError::InvalidData)?,
        None => 2,
    };
    Ok(count.clamp(2, 3) as u8)
}

fn normalize_angle(value: f32) -> f32 {
    if value == 999.0 {
        return value;
    }
    let normalized = value % 360.0;
    if normalized < 0.0 {
        normalized + 360.0
    } else if normalized == 0.0 {
        0.0
    } else {
        normalized
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const BASE: &str = r#"{
      angleData: [0, 90, 450, null],
      settings: { bpm: 120, songFilename: "song.ogg" },
      actions: [
        { floor: 2, eventType: "SetSpeed", speedType: "Multiplier", bpmMultiplier: 2 },
        { floor: 1, eventType: "Twirl" },
        { floor: 0, eventType: "MoveCamera", zoom: 200 }
      ]
    }"#;

    #[test]
    fn ignores_vfx_and_normalizes_semantically_equal_gameplay() {
        let changed = BASE.replace("zoom: 200", "zoom: 50, duration: 4");
        let hash = compute_gameplay_hash(BASE.as_bytes()).unwrap();
        assert_eq!(hash, compute_gameplay_hash(changed.as_bytes()).unwrap());
        assert_eq!(
            hash,
            "c210c970a9e4838ef581079c3fd0ef4fcdd9b52e975973497732f39c1c9c3284"
        );
    }

    #[test]
    fn rejects_gameplay_changes() {
        let changed = BASE.replace("bpmMultiplier: 2", "bpmMultiplier: 3");
        assert_ne!(
            compute_gameplay_hash(BASE.as_bytes()).unwrap(),
            compute_gameplay_hash(changed.as_bytes()).unwrap()
        );
    }
}
