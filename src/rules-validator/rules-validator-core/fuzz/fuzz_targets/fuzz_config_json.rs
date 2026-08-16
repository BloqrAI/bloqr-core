//! Fuzzes `ValidationConfig` JSON parsing - the exact path
//! `rules_validator_new`'s `config_json` argument takes across the FFI
//! boundary from .NET/other-language callers, so it must never panic on
//! malformed input.
#![no_main]

use rules_validator::config::ValidationConfig;
use libfuzzer_sys::fuzz_target;

fuzz_target!(|data: &[u8]| {
    if let Ok(s) = std::str::from_utf8(data) {
        let _ = serde_json::from_str::<ValidationConfig>(s);
    }
});
