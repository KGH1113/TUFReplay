//! Compatibility with GDMiniJSON: redundant or omitted object separators are
//! tolerated, and literal control characters inside strings are retained.
use serde_json::Value;
use std::fmt::Write;

pub(super) fn parse(text: &str) -> Result<Value, json5::Error> {
    let text = text.trim_start_matches('\u{feff}');
    if let Ok(value) = serde_json::from_str(text) {
        return Ok(value);
    }
    match json5::from_str(text) {
        Ok(value) => Ok(value),
        Err(original) => {
            let repaired = repair(text);
            if repaired == text {
                return Err(original);
            }
            json5::from_str(&repaired).map_err(|_| original)
        }
    }
}

fn repair(text: &str) -> String {
    let mut output = String::with_capacity(text.len());
    let mut chars = text.chars().peekable();
    let mut quote = None;
    let mut escaped = false;
    let mut previous = None;
    let mut containers = Vec::new();
    let mut completed_object_value = false;
    while let Some(c) = chars.next() {
        if let Some(delimiter) = quote {
            if escaped {
                output.push(c);
                if c == '\r' && chars.peek() == Some(&'\n') {
                    output.push(chars.next().unwrap());
                }
                escaped = false;
            } else if c == '\\' {
                output.push(c);
                escaped = true;
            } else if c == delimiter {
                output.push(c);
                quote = None;
                previous = Some(c);
            } else if c < '\u{20}' || matches!(c, '\u{2028}' | '\u{2029}') {
                write!(output, "\\u{:04x}", c as u32).unwrap();
            } else {
                output.push(c);
            }
            continue;
        }
        // JSON5 comments may contain quotes, commas and control characters.
        if c == '/' && matches!(chars.peek(), Some('/' | '*')) {
            let kind = chars.next().unwrap();
            output.push(c);
            output.push(kind);
            while let Some(comment) = chars.next() {
                output.push(comment);
                if kind == '/' && matches!(comment, '\r' | '\n' | '\u{2028}' | '\u{2029}') {
                    break;
                }
                if kind == '*' && comment == '*' && chars.peek() == Some(&'/') {
                    output.push(chars.next().unwrap());
                    break;
                }
            }
            continue;
        }
        // GDMiniJSON.ParseObject does not require a comma after a container
        // value before the next quoted property name (for example, actions
        // followed by decorations). Only repair that boundary inside objects.
        if c == '"' && completed_object_value {
            output.push(',');
            completed_object_value = false;
        }
        if c == ',' && matches!(previous, Some('{' | '[' | ',')) {
            continue;
        }
        output.push(c);
        if matches!(c, '"' | '\'') {
            quote = Some(c);
        }
        match c {
            '{' | '[' => {
                containers.push(c);
                completed_object_value = false;
            }
            '}' | ']' => {
                let opening = if c == '}' { '{' } else { '[' };
                if containers.last() == Some(&opening) {
                    containers.pop();
                }
                completed_object_value = containers.last() == Some(&'{');
            }
            ',' => completed_object_value = false,
            _ if !c.is_whitespace() && c != '"' => completed_object_value = false,
            _ => {}
        }
        if !c.is_whitespace() {
            previous = Some(c);
        }
    }
    output
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::domain::{
        compute_gameplay_hash, submission_gameplay_hash::compute_submission_gameplay_hash,
    };
    use serde_json::json;

    #[test]
    fn native_commas_and_literal_controls_preserve_values() {
        let text = "\u{feff}{,\"settings\":{\"useLegacyFlash\":\"Disabled\", ,},\"angleData\":[,0,,180,],\"text\":\"한글, ,\r\\n\t\u{0}\"}";
        assert_eq!(
            parse(text).unwrap(),
            json!({"settings":{"useLegacyFlash":"Disabled"},"angleData":[0,180],"text":"한글, ,\r\n\t\u{0}"})
        );
    }

    #[test]
    fn native_missing_object_separator_preserves_chart_hashes() {
        let canonical = r#"{"settings":{"version":17,"bpm":120},"angleData":[0,180],"actions":[],"decorations":[]}"#;
        let native = canonical.replace("],\"decorations\"", "]\r\n\"decorations\"");
        assert_eq!(parse(&native).unwrap(), parse(canonical).unwrap());
        assert_eq!(
            compute_submission_gameplay_hash(native.as_bytes()).unwrap(),
            compute_submission_gameplay_hash(canonical.as_bytes()).unwrap()
        );
        assert_eq!(
            compute_gameplay_hash(native.as_bytes()).unwrap(),
            compute_gameplay_hash(canonical.as_bytes()).unwrap()
        );
        assert_eq!(
            parse(r#"{"a":{"b":1}"c":[2]}"#).unwrap(),
            json!({"a":{"b":1},"c":[2]})
        );
    }

    #[test]
    fn comments_and_escaped_strings_do_not_change_token_state() {
        let text = r#"{,/* ' " , */ value: 'a\'b, ,', // " ,
            , other: "c\\\"d,,", list: [0, /* , ' */ ,180]}"#;
        assert_eq!(
            parse(text).unwrap(),
            json!({"value":"a'b, ,","other":"c\\\"d,,","list":[0,180]})
        );
    }

    #[test]
    fn invalid_values_are_not_repaired_into_valid_gameplay() {
        for text in [
            r#"{"bpm":,120}"#,
            r#"{"angles":[1 2]}"#,
            r#"{"x":"unterminated}"#,
            r#"{"x":1 /* unterminated}"#,
        ] {
            assert!(parse(text).is_err(), "{text}");
        }
    }

    #[test]
    fn json5_line_continuations_survive_other_repairs() {
        for newline in ["\n", "\r\n", "\r", "\u{2028}", "\u{2029}"] {
            let text = format!("{{,value: \"first\\{newline}second\"}}");
            assert_eq!(parse(&text).unwrap(), json!({"value":"firstsecond"}));
        }
    }

    #[test]
    fn compatibility_repairs_leave_both_gameplay_hashes_unchanged() {
        let canonical = r#"{"settings":{"version":17,"bpm":120,"useLegacyFlash":"Disabled"},"angleData":[0,180],"actions":[],"decorations":[{"eventType":"AddText","decText":"Thank you!\r\n"}]}"#;
        let legacy = canonical
            .replace("\"Disabled\"}", "\"Disabled\", ,}")
            .replace("Thank you!\\r", "Thank you!\r");
        assert_eq!(
            compute_gameplay_hash(canonical.as_bytes()).unwrap(),
            compute_gameplay_hash(legacy.as_bytes()).unwrap()
        );
        assert_eq!(
            compute_submission_gameplay_hash(canonical.as_bytes()).unwrap(),
            compute_submission_gameplay_hash(legacy.as_bytes()).unwrap()
        );
        let changed = legacy.replace("120", "121");
        assert_ne!(
            compute_submission_gameplay_hash(legacy.as_bytes()).unwrap(),
            compute_submission_gameplay_hash(changed.as_bytes()).unwrap()
        );
    }
}
