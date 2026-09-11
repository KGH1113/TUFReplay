use serde::{Deserialize, Serialize};

pub(crate) const PROTOCOL_VERSION: i64 = 1;
pub(crate) const BINARY_HEADER_BYTES: usize = 20;
pub(crate) const BINARY_MAGIC: [u8; 4] = *b"TUFR";

#[derive(Debug, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub(crate) enum ClientControl {
    Hello {
        protocol_version: i64,
        last_acknowledged_sequence: i64,
    },
    Heartbeat,
    Complete {
        final_sequence: i64,
        input_count: u64,
        hit_context_count: u64,
    },
    Fail {
        #[serde(default)]
        reason: Option<String>,
    },
}

#[derive(Debug, Serialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub(crate) enum ServerControl {
    Ready {
        acknowledged_sequence: i64,
        heartbeat_interval_ms: u64,
        max_chunk_bytes: usize,
    },
    Ack {
        acknowledged_sequence: i64,
    },
    Nack {
        expected_sequence: i64,
    },
    Sealed {
        acknowledged_sequence: i64,
    },
    Error {
        code: &'static str,
        terminal: bool,
    },
}

#[derive(Debug)]
pub(crate) struct DataChunk<'a> {
    pub kind: u8,
    pub sequence: u64,
    pub payload: &'a [u8],
}

pub(crate) fn parse_data_chunk(
    bytes: &[u8],
    max_chunk_bytes: usize,
) -> std::result::Result<DataChunk<'_>, &'static str> {
    if bytes.len() < BINARY_HEADER_BYTES {
        return Err("malformed_chunk_header");
    }
    if bytes[0..4] != BINARY_MAGIC || bytes[4] != PROTOCOL_VERSION as u8 {
        return Err("unsupported_chunk_header");
    }
    let kind = bytes[5];
    if kind > 5 || u16::from_be_bytes([bytes[6], bytes[7]]) != 0 {
        return Err("unsupported_chunk_header");
    }
    let sequence = u64::from_be_bytes(bytes[8..16].try_into().expect("fixed header length"));
    let payload_length =
        u32::from_be_bytes(bytes[16..20].try_into().expect("fixed header length")) as usize;
    if payload_length > max_chunk_bytes {
        return Err("chunk_too_large");
    }
    if bytes.len() != BINARY_HEADER_BYTES + payload_length {
        return Err("payload_length_mismatch");
    }
    Ok(DataChunk {
        kind,
        sequence,
        payload: &bytes[BINARY_HEADER_BYTES..],
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_binary_chunk() {
        let mut bytes = Vec::from(BINARY_MAGIC);
        bytes.extend([1, 0, 0, 0]);
        bytes.extend(7_u64.to_be_bytes());
        bytes.extend(3_u32.to_be_bytes());
        bytes.extend(b"abc");
        let chunk = parse_data_chunk(&bytes, 64).expect("valid chunk");
        assert_eq!(chunk.kind, 0);
        assert_eq!(chunk.sequence, 7);
        assert_eq!(chunk.payload, b"abc");
    }

    #[test]
    fn rejects_payload_length_mismatch() {
        let mut bytes = Vec::from(BINARY_MAGIC);
        bytes.extend([1, 0, 0, 0]);
        bytes.extend(0_u64.to_be_bytes());
        bytes.extend(4_u32.to_be_bytes());
        bytes.extend(b"abc");
        assert_eq!(
            parse_data_chunk(&bytes, 64).expect_err("invalid chunk"),
            "payload_length_mismatch"
        );
    }
}
