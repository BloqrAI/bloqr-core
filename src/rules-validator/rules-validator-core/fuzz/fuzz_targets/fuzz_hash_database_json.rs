//! Fuzzes `HashDatabase` JSON parsing (`.hashes.json` sidecar files).
#![no_main]

use rules_validator::hash::HashDatabase;
use libfuzzer_sys::fuzz_target;

fuzz_target!(|data: &[u8]| {
    if let Ok(s) = std::str::from_utf8(data) {
        let _ = serde_json::from_str::<HashDatabase>(s);
    }
});
