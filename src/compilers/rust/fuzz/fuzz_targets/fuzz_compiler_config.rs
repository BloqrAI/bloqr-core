//! Fuzzes `CompilerConfig` deserialization across all three supported
//! formats (JSON/YAML/TOML) - the untrusted-input surface `read_config()`
//! exposes to whoever supplies a compiler config file.
#![no_main]

use libfuzzer_sys::fuzz_target;
use bloqr_compiler::CompilerConfig;

fuzz_target!(|data: &[u8]| {
    if data.is_empty() {
        return;
    }
    let Ok(s) = std::str::from_utf8(&data[1..]) else {
        return;
    };
    match data[0] % 3 {
        0 => {
            let _ = serde_json::from_str::<CompilerConfig>(s);
        }
        1 => {
            let _ = serde_yaml::from_str::<CompilerConfig>(s);
        }
        _ => {
            let _ = toml::from_str::<CompilerConfig>(s);
        }
    }
});
