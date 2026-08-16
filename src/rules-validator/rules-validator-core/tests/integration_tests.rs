//! Integration tests for the validation library.
//!
//! These tests verify end-to-end functionality across all modules.

use rules_validator::{
    compile_with_validation, create_archive, resolve_conflict, validate_syntax,
    verify_compilation_was_validated, CompilationInput, CompilationOptions, ConflictStrategy,
    HashDatabase, ValidationConfig, Validator, VerificationMode,
};
use std::collections::HashMap;
use std::fs;
use std::io::Write;
use tempfile::{NamedTempFile, TempDir};

#[test]
fn test_end_to_end_compilation_with_local_files() {
    let temp_dir = TempDir::new().unwrap();
    let input_file = temp_dir.path().join("rules.txt");
    let output_file = temp_dir.path().join("output.txt");
    let hash_db = temp_dir.path().join(".hashes.json");

    // Create input file with valid rules
    fs::write(&input_file, "||example.com^\n@@||allowed.com^\n").unwrap();

    let mut config = ValidationConfig::default();
    config.hash_verification.hash_database_path = hash_db.display().to_string();

    let input = CompilationInput {
        local_files: vec![input_file.clone()],
        remote_urls: vec![],
        expected_hashes: HashMap::new(),
    };

    let options = CompilationOptions {
        validation_config: config,
        output_path: output_file.clone(),
        create_archive: false,
    };

    let result = compile_with_validation(input, options).unwrap();

    // Verify result
    assert!(result.success);
    assert_eq!(result.validation_metadata.local_files_validated, 1);
    assert_eq!(result.validation_metadata.remote_urls_validated, 0);
    assert_eq!(
        result.validation_metadata.validation_library_version,
        env!("CARGO_PKG_VERSION")
    );
    assert!(output_file.exists()); // Output should have been created

    // Verify can validate the result
    assert!(verify_compilation_was_validated(&result).is_ok());
}

#[test]
fn test_compilation_rejects_invalid_syntax() {
    let temp_dir = TempDir::new().unwrap();
    let input_file = temp_dir.path().join("invalid.txt");
    let output_file = temp_dir.path().join("output.txt");
    let hash_db_path = temp_dir.path().join(".hashes.json");

    // Create input file with NO valid rules (only comments)
    fs::write(&input_file, "! Comment\n# Another comment\n").unwrap();

    let input = CompilationInput {
        local_files: vec![input_file],
        remote_urls: vec![],
        expected_hashes: HashMap::new(),
    };

    let mut validation_config = ValidationConfig::default();
    validation_config.hash_verification.hash_database_path = hash_db_path.display().to_string();

    let options = CompilationOptions {
        validation_config,
        output_path: output_file,
        create_archive: false,
    };

    // Should fail due to no valid rules
    let result = compile_with_validation(input, options);
    assert!(result.is_err());
}

#[test]
fn test_strict_mode_enforcement() {
    let temp_dir = TempDir::new().unwrap();
    let input_file = temp_dir.path().join("rules.txt");
    let hash_db_path = temp_dir.path().join(".hashes.json");

    // Create and hash file
    fs::write(&input_file, "||example.com^\n").unwrap();

    let mut config = ValidationConfig::default();
    config.hash_verification.mode = VerificationMode::Strict;
    config.hash_verification.hash_database_path = hash_db_path.display().to_string();

    // First compilation - creates hash
    let mut validator = Validator::new(config.clone());
    validator.validate_local_file(&input_file).unwrap();

    // Modify file (tampering)
    fs::write(&input_file, "||modified.com^\n").unwrap();

    // Second validation in strict mode should fail
    let mut validator2 = Validator::new(config);
    let result = validator2.validate_local_file(&input_file);
    assert!(result.is_err());
}

#[test]
fn test_warning_mode_allows_hash_mismatch() {
    let temp_dir = TempDir::new().unwrap();
    let input_file = temp_dir.path().join("rules.txt");
    let hash_db_path = temp_dir.path().join(".hashes.json");

    // Create and hash file
    fs::write(&input_file, "||example.com^\n").unwrap();

    let mut config = ValidationConfig::default();
    config.hash_verification.mode = VerificationMode::Warning;
    config.hash_verification.hash_database_path = hash_db_path.display().to_string();

    // First validation - creates hash
    let mut validator = Validator::new(config.clone());
    validator.validate_local_file(&input_file).unwrap();

    // Modify file
    fs::write(&input_file, "||modified.com^\n").unwrap();

    // Second validation in warning mode should succeed
    let mut validator2 = Validator::new(config);
    let result = validator2.validate_local_file(&input_file);
    assert!(result.is_ok());
}

#[test]
fn test_hash_database_persistence() {
    let temp_dir = TempDir::new().unwrap();
    let hash_db_path = temp_dir.path().join(".hashes.json");

    // Create database and add entries
    let mut db = HashDatabase::new();
    db.insert(
        "test.txt".to_string(),
        rules_validator::HashEntry::new("hash123".to_string(), 1024),
    );

    // Save
    db.save(&hash_db_path).unwrap();

    // Load in new instance
    let loaded_db = HashDatabase::load(&hash_db_path).unwrap();
    assert_eq!(loaded_db.len(), 1);
    assert_eq!(loaded_db.get("test.txt").unwrap().hash, "hash123");
}

#[test]
fn test_syntax_validation_adblock_format() {
    let mut file = NamedTempFile::new().unwrap();
    writeln!(file, "! Title: Test Filter").unwrap();
    writeln!(file, "||example.com^").unwrap();
    writeln!(file, "@@||allowed.com^").unwrap();
    file.flush().unwrap();

    let result = validate_syntax(file.path()).unwrap();

    assert!(result.is_valid);
    assert_eq!(result.format, rules_validator::FilterFormat::Adblock);
    assert!(result.valid_rules >= 2); // At least ||example.com^ and @@||allowed.com^
    assert_eq!(result.invalid_rules, 0);
}

#[test]
fn test_syntax_validation_rejects_cosmetic_rule() {
    // Element-hiding/cosmetic rules are meaningless at the DNS level - HostlistCompiler
    // itself rejects them (see docs/adr/0003-adguard-hostlist-compatibility.md), and this
    // validator now agrees rather than silently accepting rules the real compile step drops.
    let mut file = NamedTempFile::new().unwrap();
    writeln!(file, "||example.com^").unwrap();
    writeln!(file, "##.ad-banner").unwrap();
    file.flush().unwrap();

    let result = validate_syntax(file.path()).unwrap();

    assert!(!result.is_valid);
    assert_eq!(result.valid_rules, 1);
    assert_eq!(result.invalid_rules, 1);
}

#[test]
fn test_syntax_validation_hosts_format() {
    let mut file = NamedTempFile::new().unwrap();
    writeln!(file, "# Comment").unwrap();
    writeln!(file, "0.0.0.0 example.com").unwrap();
    writeln!(file, "0.0.0.0 ads.example.com").unwrap();
    writeln!(file, "127.0.0.1 localhost").unwrap();
    file.flush().unwrap();

    let result = validate_syntax(file.path()).unwrap();

    assert!(result.is_valid);
    assert_eq!(result.format, rules_validator::FilterFormat::Hosts);
    assert!(result.valid_rules >= 3);
}

#[test]
fn test_file_conflict_rename_strategy() {
    let temp_dir = TempDir::new().unwrap();
    let base_path = temp_dir.path().join("file.txt");

    // Create existing file
    fs::write(&base_path, "existing").unwrap();

    // Resolve conflict with rename strategy
    let resolved = resolve_conflict(&base_path, ConflictStrategy::Rename).unwrap();

    assert_ne!(resolved, base_path);
    assert_eq!(resolved, temp_dir.path().join("file-1.txt"));
    assert!(!resolved.exists());
}

#[test]
fn test_file_conflict_overwrite_strategy() {
    let temp_dir = TempDir::new().unwrap();
    let base_path = temp_dir.path().join("file.txt");

    // Create existing file
    fs::write(&base_path, "existing").unwrap();

    // Resolve conflict with overwrite strategy
    let resolved = resolve_conflict(&base_path, ConflictStrategy::Overwrite).unwrap();

    assert_eq!(resolved, base_path);
}

#[test]
fn test_file_conflict_error_strategy() {
    let temp_dir = TempDir::new().unwrap();
    let base_path = temp_dir.path().join("file.txt");

    // Create existing file
    fs::write(&base_path, "existing").unwrap();

    // Resolve conflict with error strategy should fail
    let result = resolve_conflict(&base_path, ConflictStrategy::Error);
    assert!(result.is_err());
}

#[test]
fn test_archive_creation_with_manifest() {
    let input_dir = TempDir::new().unwrap();
    let archive_dir = TempDir::new().unwrap();

    // Create input files
    fs::write(input_dir.path().join("rules.txt"), "||example.com^").unwrap();
    fs::write(input_dir.path().join("hosts.txt"), "0.0.0.0 ads.com").unwrap();

    // Create archive
    let archive_path = create_archive(
        input_dir.path(),
        archive_dir.path(),
        "output_hash_abc123",
        42,
    )
    .unwrap();

    // Verify archive exists
    assert!(archive_path.exists());
    assert!(archive_path.is_dir());

    // Verify manifest exists
    let manifest_path = archive_path.join("manifest.json");
    assert!(manifest_path.exists());

    // Verify files were copied
    assert!(archive_path.join("rules.txt").exists());
    assert!(archive_path.join("hosts.txt").exists());

    // Verify manifest content
    let manifest_content = fs::read_to_string(manifest_path).unwrap();
    assert!(manifest_content.contains("output_hash_abc123"));
    assert!(manifest_content.contains("\"rule_count\": 42"));
}

#[test]
fn test_validation_metadata_signature_uniqueness() {
    let meta1 = rules_validator::ValidationMetadata {
        validation_timestamp: "2024-12-27T10:00:00Z".to_string(),
        local_files_validated: 5,
        remote_urls_validated: 3,
        hash_database_entries: 8,
        validation_library_version: "1.0.0".to_string(),
        strict_mode: true,
        archive_created: None,
    };

    let meta2 = rules_validator::ValidationMetadata {
        validation_timestamp: "2024-12-27T11:00:00Z".to_string(), // Different timestamp
        local_files_validated: 5,
        remote_urls_validated: 3,
        hash_database_entries: 8,
        validation_library_version: "1.0.0".to_string(),
        strict_mode: true,
        archive_created: None,
    };

    // Different metadata should produce different signatures
    assert_ne!(meta1.signature(), meta2.signature());
}

#[test]
fn test_compilation_with_archiving() {
    // Test archive creation separately since compile_internal is a placeholder
    let input_dir = TempDir::new().unwrap();
    let archive_dir = TempDir::new().unwrap();

    // Create input file
    let input_file = input_dir.path().join("rules.txt");
    fs::write(&input_file, "||example.com^\n").unwrap();

    // Create archive directly (bypassing compile_with_validation for this test)
    let archive_path =
        create_archive(input_dir.path(), archive_dir.path(), "test_hash_abc123", 10).unwrap();

    // Verify archive was created
    assert!(archive_path.exists());
    assert!(archive_path.join("manifest.json").exists());
    assert!(archive_path.join("rules.txt").exists());
}

#[test]
fn test_url_validation_rejects_http() {
    use rules_validator::validate_url;

    let result = validate_url("http://insecure.example.com/list.txt", None).unwrap();

    assert!(!result.is_valid);
    assert!(result.messages.iter().any(|m| m.contains("HTTPS")));
}

#[test]
fn test_verification_rejects_forged_metadata() {
    let temp_dir = TempDir::new().unwrap();

    // Create a result with fake metadata (0 validations)
    let fake_result = rules_validator::EnforcedCompilationResult {
        success: true,
        rule_count: 100,
        output_hash: "abc123".to_string(),
        elapsed_ms: 1000,
        output_path: temp_dir.path().join("output.txt"),
        validation_metadata: rules_validator::ValidationMetadata {
            validation_timestamp: chrono::Utc::now().to_rfc3339(),
            local_files_validated: 0, // Fake - no validation
            remote_urls_validated: 0, // Fake - no validation
            hash_database_entries: 0,
            validation_library_version: "1.0.0".to_string(),
            strict_mode: false,
            archive_created: None,
        },
    };

    // Verification should reject this
    let verification = verify_compilation_was_validated(&fake_result);
    assert!(verification.is_err());
}

#[test]
fn test_multiple_local_files_validation() {
    let temp_dir = TempDir::new().unwrap();
    let file1 = temp_dir.path().join("rules1.txt");
    let file2 = temp_dir.path().join("rules2.txt");
    let file3 = temp_dir.path().join("rules3.txt");
    let output_file = temp_dir.path().join("output.txt");
    let hash_db = temp_dir.path().join(".hashes.json");

    // Create multiple input files
    fs::write(&file1, "||example1.com^\n").unwrap();
    fs::write(&file2, "||example2.com^\n").unwrap();
    fs::write(&file3, "||example3.com^\n").unwrap();

    let mut config = ValidationConfig::default();
    config.hash_verification.hash_database_path = hash_db.display().to_string();

    let input = CompilationInput {
        local_files: vec![file1, file2, file3],
        remote_urls: vec![],
        expected_hashes: HashMap::new(),
    };

    let options = CompilationOptions {
        validation_config: config,
        output_path: output_file.clone(),
        create_archive: false,
    };

    let result = compile_with_validation(input, options).unwrap();

    // Verify all 3 files were validated
    assert_eq!(result.validation_metadata.local_files_validated, 3);
    assert!(output_file.exists()); // Output should have been created
}

#[test]
fn test_config_serialization_roundtrip() {
    use rules_validator::ValidationConfig;

    let original = ValidationConfig::default()
        .with_verification_mode(VerificationMode::Strict)
        .with_archiving(true)
        .with_output_path("custom/path.txt");

    // Serialize to JSON
    let json = serde_json::to_string(&original).unwrap();

    // Deserialize back
    let deserialized: ValidationConfig = serde_json::from_str(&json).unwrap();

    // Verify fields match
    assert_eq!(
        original.hash_verification.mode,
        deserialized.hash_verification.mode
    );
    assert_eq!(original.archiving.enabled, deserialized.archiving.enabled);
    assert_eq!(original.output.path, deserialized.output.path);
}
