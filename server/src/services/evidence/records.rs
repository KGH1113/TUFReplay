use loco_rs::prelude::*;

/// Wire record boundaries are checked independently of gameplay correctness.
pub fn record_count(kind: u8, payload: &[u8]) -> Result<u64> {
    if payload.is_empty() {
        return Err(invalid());
    }
    if kind == 2 {
        return Ok(0);
    }
    let text = std::str::from_utf8(payload).map_err(|_| invalid())?;
    if !text.ends_with('\n') {
        return Err(invalid());
    }
    let mut count = 0;
    for line in text.lines() {
        if kind <= 1 {
            let cells: Vec<_> = line.split(',').collect();
            let expected = if kind == 0 { 5 } else { 13 };
            if cells.len() != expected {
                return Err(invalid());
            }
            for (i, cell) in cells.iter().enumerate() {
                if kind == 1 && i == 11 && cell.is_empty() {
                    continue;
                }
                if !cell.parse::<f64>().is_ok_and(f64::is_finite) {
                    return Err(invalid());
                }
            }
            if kind == 0
                && (cells[0].parse::<i64>().is_err()
                    || cells[1].parse::<u16>().is_err()
                    || !cells[2].parse::<u16>().is_ok_and(|n| n & !63 == 0))
            {
                return Err(invalid());
            }
            if kind == 1 && (cells[0].parse::<u32>().is_err() || cells[12].parse::<i64>().is_err())
            {
                return Err(invalid());
            }
        } else if kind <= 5 {
            let event: serde_json::Value = serde_json::from_str(line).map_err(|_| invalid())?;
            if event.get("version").and_then(|v| v.as_u64()) != Some(1) {
                return Err(invalid());
            }
        } else {
            return Err(invalid());
        }
        count += 1;
    }
    Ok(count)
}

pub fn invalid() -> Error {
    Error::BadRequest("evidence_invalid".into())
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn rejects_split_records_and_nonfinite_numbers() {
        assert!(record_count(0, b"1,32,3,0,0").is_err());
        assert!(record_count(0, b"NaN,32,3,0,0\n").is_err());
        assert_eq!(record_count(0, b"-1,32,3,0,0\n").unwrap(), 1);
        assert_eq!(record_count(1, b"0,0,0,0,0,0,0,0,0,0,0,,1\n").unwrap(), 1);
    }
}
