use serde::Deserialize;
use uuid::Uuid;

pub const VERSION: i64 = 2;
pub const ENVELOPE_BYTES: usize = 20;

#[derive(Debug, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case", deny_unknown_fields)]
pub enum Control {
    SessionHello {
        protocol_version: i64,
    },
    Heartbeat,
    RunStart {
        run_id: Uuid,
        client_game_version: String,
        client_mod_version: String,
        client_tuf_file_id: String,
        client_level_relative_path: String,
        #[serde(default)]
        submission_gameplay_hash_version: i64,
        #[serde(default)]
        submission_gameplay_hash_hex: String,
        last_acknowledged_sequence: i64,
    },
    RunHeartbeat {
        run_id: Uuid,
    },
    RunFail {
        run_id: Uuid,
    },
    RunComplete {
        run_id: Uuid,
        final_sequence: i64,
        input_count: u64,
        hit_context_count: u64,
    },
}

pub fn parse_binary(bytes: &[u8]) -> Result<(Uuid, &[u8]), &'static str> {
    if bytes.len() < ENVELOPE_BYTES || &bytes[..4] != b"TUF2" {
        return Err("invalid_run_envelope");
    }
    Ok((
        Uuid::from_slice(&bytes[4..20]).map_err(|_| "invalid_run_envelope")?,
        &bytes[20..],
    ))
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn every_frame_has_an_explicit_attempt_identity() {
        let id = Uuid::parse_str("00112233-4455-6677-8899-aabbccddeeff").unwrap();
        let mut bytes = b"TUF2".to_vec();
        bytes.extend(id.as_bytes());
        bytes.extend(b"TUFR");
        assert_eq!(parse_binary(&bytes).unwrap(), (id, b"TUFR".as_slice()));
        assert!(parse_binary(b"TUFR").is_err());
    }
}
