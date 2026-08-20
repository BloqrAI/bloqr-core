//! Integration tests for configuration file discovery.
//!
//! Tests the ancestor directory traversal logic for finding configuration files.

use std::fs::{self, File};
use std::io::Write;
use std::path::PathBuf;
use tempfile::TempDir;

/// Test that we can find a config in the current directory.
#[test]
fn test_find_config_in_current_directory() {
    let temp_dir = TempDir::new().unwrap();
    let config_path = temp_dir.path().join("compiler-config.json");

    let mut file = File::create(&config_path).unwrap();
    writeln!(
        file,
        r#"{{"name": "Test", "version": "1.0.0", "sources": [{{"source": "test.txt", "type": "adblock"}}]}}"#
    )
    .unwrap();

    // Test by passing the temp directory path
    let found = find_config_starting_from(temp_dir.path());
    assert!(found.is_some());

    let found_path = found.unwrap();
    assert_eq!(found_path.file_name().unwrap(), "compiler-config.json");
}

/// Test that we can find a config in a parent directory.
#[test]
fn test_find_config_in_parent_directory() {
    let temp_dir = TempDir::new().unwrap();
    let config_path = temp_dir.path().join("compiler-config.yaml");

    let mut file = File::create(&config_path).unwrap();
    writeln!(
        file,
        "name: Parent Config\nversion: 1.0.0\nsources:\n  - source: test.txt\n    type: adblock"
    )
    .unwrap();

    // Create subdirectory
    let sub_dir = temp_dir.path().join("subdir");
    fs::create_dir(&sub_dir).unwrap();

    // Test that config file is found in parent when searching from subdir
    let found = find_config_starting_from(&sub_dir);
    assert!(found.is_some());

    let found_path = found.unwrap();
    assert_eq!(found_path.file_name().unwrap(), "compiler-config.yaml");
    assert!(found_path.starts_with(temp_dir.path()));
}

/// Test that we can find a config in a deeply nested ancestor.
#[test]
fn test_find_config_in_deeply_nested_ancestor() {
    let temp_dir = TempDir::new().unwrap();
    let config_path = temp_dir.path().join("compiler-config.toml");

    let mut file = File::create(&config_path).unwrap();
    writeln!(
        file,
        r#"name = "Deep Config"
version = "1.0.0"

[[sources]]
source = "test.txt"
type = "adblock"
"#
    )
    .unwrap();

    // Create deeply nested directory structure
    let deep_dir = temp_dir.path().join("level1").join("level2").join("level3");
    fs::create_dir_all(&deep_dir).unwrap();

    // Test that config file is found in ancestor
    let found = find_config_starting_from(&deep_dir);

    assert!(
        found.is_some(),
        "Config file should be found in ancestor directory"
    );

    let found_path = found.unwrap();
    assert_eq!(found_path.file_name().unwrap(), "compiler-config.toml");
    assert!(
        found_path.starts_with(temp_dir.path()),
        "Found config {:?} should be within temp dir {:?}",
        found_path,
        temp_dir.path()
    );
}

/// Test that we prefer the closest config file (not the furthest ancestor).
#[test]
fn test_prefer_closest_config() {
    let temp_dir = TempDir::new().unwrap();

    // Create config in root
    let root_config = temp_dir.path().join("compiler-config.json");
    let mut file = File::create(&root_config).unwrap();
    writeln!(
        file,
        r#"{{"name": "Root Config", "version": "1.0.0", "sources": [{{"source": "root.txt", "type": "adblock"}}]}}"#
    )
    .unwrap();

    // Create subdirectory with another config
    let sub_dir = temp_dir.path().join("subdir");
    fs::create_dir(&sub_dir).unwrap();
    let sub_config = sub_dir.join("compiler-config.json");
    let mut file = File::create(&sub_config).unwrap();
    writeln!(
        file,
        r#"{{"name": "Sub Config", "version": "2.0.0", "sources": [{{"source": "sub.txt", "type": "adblock"}}]}}"#
    )
    .unwrap();

    // Test that we find the closer config (in current dir)
    let found = find_config_starting_from(&sub_dir);
    assert!(found.is_some());

    let found_path = found.unwrap();
    // Should find the sub config, not the root one
    assert_eq!(found_path.file_name().unwrap(), "compiler-config.json");
    assert_eq!(found_path, sub_config);
}

/// Test format priority: finds json before yaml before toml in same directory.
#[test]
fn test_config_format_priority() {
    let temp_dir = TempDir::new().unwrap();

    // Create all three formats
    let json_config = temp_dir.path().join("compiler-config.json");
    let yaml_config = temp_dir.path().join("compiler-config.yaml");
    let toml_config = temp_dir.path().join("compiler-config.toml");

    let mut file = File::create(&json_config).unwrap();
    writeln!(file, r#"{{"name": "JSON", "version": "1.0.0", "sources": [{{"source": "test.txt", "type": "adblock"}}]}}"#).unwrap();

    let mut file = File::create(&yaml_config).unwrap();
    writeln!(
        file,
        "name: YAML\nversion: 1.0.0\nsources:\n  - source: test.txt\n    type: adblock"
    )
    .unwrap();

    let mut file = File::create(&toml_config).unwrap();
    writeln!(file, "name = \"TOML\"\nversion = \"1.0.0\"\n\n[[sources]]\nsource = \"test.txt\"\ntype = \"adblock\"").unwrap();

    // Should find JSON first (alphabetically first in our array)
    let found = find_config_starting_from(temp_dir.path());
    assert!(found.is_some());
    assert_eq!(found.unwrap().file_name().unwrap(), "compiler-config.json");
}

/// Helper function to test config discovery starting from a specific directory.
/// This mirrors the logic in main.rs but takes a starting directory parameter.
fn find_config_starting_from(start_dir: &std::path::Path) -> Option<PathBuf> {
    let config_names = [
        "compiler-config.json",
        "compiler-config.yaml",
        "compiler-config.toml",
    ];

    let mut dir = start_dir;

    // Walk up the directory tree
    loop {
        for config_name in &config_names {
            let config_path = dir.join(config_name);
            if config_path.exists() && config_path.is_file() {
                return Some(config_path);
            }
        }

        // Move to parent directory, or stop if we've reached the root
        dir = dir.parent()?;
    }
}
