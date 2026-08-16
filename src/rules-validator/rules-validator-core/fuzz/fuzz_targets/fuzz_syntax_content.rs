//! Fuzzes filter-list syntax validation on arbitrary UTF-8 content - the
//! same parsing path a local file or remote-download body goes through.
#![no_main]

use rules_validator::syntax::validate_syntax_content;
use libfuzzer_sys::fuzz_target;

fuzz_target!(|content: &str| {
    let _ = validate_syntax_content(content);
});
