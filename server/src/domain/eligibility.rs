pub fn is_eligible_difficulty(kind: &str, name: &str) -> bool {
    if kind != "PGU" || !matches!(name.as_bytes().first(), Some(b'P' | b'G')) {
        return false;
    }
    let suffix = &name[1..];
    suffix
        .parse::<u8>()
        .is_ok_and(|n| (1..=20).contains(&n) && n.to_string() == suffix)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn eligibility_is_the_official_p_and_g_ladder() {
        for band in ["P", "G"] {
            for n in 1..=20 {
                assert!(is_eligible_difficulty("PGU", &format!("{band}{n}")));
            }
        }
        for name in ["P0", "P21", "G01", "G20-U1", "U1", "Q0", "", "P∞"] {
            assert!(!is_eligible_difficulty("PGU", name));
        }
        assert!(!is_eligible_difficulty("SPECIAL", "G1"));
    }
}
