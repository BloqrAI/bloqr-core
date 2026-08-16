//! Fuzzes `merge_chunks()`'s dedup/merge logic against arbitrary chunk
//! contents - guards against panics on pathological rule-line input
//! (empty strings, huge inputs, unusual comment-prefix boundaries).
#![no_main]

use libfuzzer_sys::fuzz_target;
use rules_compiler::merge_chunks;

fuzz_target!(|chunks: Vec<Vec<String>>| {
    let _ = merge_chunks(&chunks);
});
