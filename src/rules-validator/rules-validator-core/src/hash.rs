//! SHA-384 hash verification for at-rest and in-flight data integrity.

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha384};
use std::collections::HashMap;
use std::fs;
use std::path::Path;

use crate::error::{Result, ValidationError};

/// Hash entry in the database.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct HashEntry {
    /// SHA-384 hash (96 hex characters).
    pub hash: String,
    /// File size in bytes.
    pub size: u64,
    /// Last modified timestamp.
    pub last_modified: DateTime<Utc>,
    /// Last verified timestamp.
    pub last_verified: DateTime<Utc>,
}

impl HashEntry {
    /// Create a new hash entry.
    pub fn new(hash: String, size: u64) -> Self {
        let now = Utc::now();
        Self {
            hash,
            size,
            last_modified: now,
            last_verified: now,
        }
    }

    /// Update last verified timestamp.
    pub fn mark_verified(&mut self) {
        self.last_verified = Utc::now();
    }
}

/// Hash database for tracking file hashes.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct HashDatabase {
    /// Map of file paths/URLs to hash entries.
    #[serde(flatten)]
    pub entries: HashMap<String, HashEntry>,
}

impl HashDatabase {
    /// Create a new empty database.
    #[must_use]
    pub fn new() -> Self {
        Self {
            entries: HashMap::new(),
        }
    }

    /// Load database from file.
    ///
    /// # Errors
    ///
    /// Returns an error if the file cannot be read or parsed.
    pub fn load<P: AsRef<Path>>(path: P) -> Result<Self> {
        let path = path.as_ref();
        if !path.exists() {
            return Ok(Self::new());
        }

        let content = fs::read_to_string(path)?;
        Ok(serde_json::from_str(&content)?)
    }

    /// Save database to file, creating its parent directory if it doesn't exist.
    ///
    /// # Errors
    ///
    /// Returns an error if the parent directory or file cannot be created/written.
    pub fn save<P: AsRef<Path>>(&self, path: P) -> Result<()> {
        let path = path.as_ref();
        if let Some(parent) = path.parent() {
            if !parent.as_os_str().is_empty() {
                fs::create_dir_all(parent)?;
            }
        }

        let content = serde_json::to_string_pretty(self)?;
        fs::write(path, content)?;
        Ok(())
    }

    /// Get hash entry for a file/URL.
    #[must_use]
    pub fn get(&self, key: &str) -> Option<&HashEntry> {
        self.entries.get(key)
    }

    /// Insert or update hash entry.
    pub fn insert(&mut self, key: String, entry: HashEntry) {
        self.entries.insert(key, entry);
    }

    /// Remove hash entry.
    pub fn remove(&mut self, key: &str) -> Option<HashEntry> {
        self.entries.remove(key)
    }

    /// Get number of entries.
    #[must_use]
    pub fn len(&self) -> usize {
        self.entries.len()
    }

    /// Check if database is empty.
    #[must_use]
    pub fn is_empty(&self) -> bool {
        self.entries.is_empty()
    }
}

impl Default for HashDatabase {
    fn default() -> Self {
        Self::new()
    }
}

/// Compute SHA-384 hash of a file.
///
/// # Errors
///
/// Returns an error if the file cannot be read.
pub fn compute_file_hash<P: AsRef<Path>>(path: P) -> Result<String> {
    let content = fs::read(path)?;
    Ok(compute_hash(&content))
}

/// Compute SHA-384 hash of bytes.
#[must_use]
pub fn compute_hash(data: &[u8]) -> String {
    let mut hasher = Sha384::new();
    hasher.update(data);
    hex::encode(hasher.finalize())
}

/// Verify file hash against expected value.
///
/// # Errors
///
/// Returns an error if hashes don't match or file cannot be read.
pub fn verify_file_hash<P: AsRef<Path>>(path: P, expected: &str) -> Result<()> {
    let path = path.as_ref();
    let actual = compute_file_hash(path)?;

    if actual != expected {
        return Err(ValidationError::hash_mismatch(
            path.display().to_string(),
            expected,
            actual,
        ));
    }

    Ok(())
}

/// Verify and update hash in database.
///
/// # Errors
///
/// Returns an error if hash verification fails or file cannot be read.
pub fn verify_and_update<P: AsRef<Path>>(
    path: P,
    database: &mut HashDatabase,
    strict: bool,
) -> Result<bool> {
    let path = path.as_ref();
    let path_str = path.display().to_string();
    let actual_hash = compute_file_hash(path)?;
    let metadata = fs::metadata(path)?;
    let file_size = metadata.len();

    match database.get(&path_str) {
        Some(entry) => {
            // File exists in database - verify hash
            if entry.hash != actual_hash {
                if strict {
                    return Err(ValidationError::hash_mismatch(
                        &path_str,
                        &entry.hash,
                        actual_hash,
                    ));
                }
                // In non-strict mode, update hash and return false (changed)
                let mut new_entry = HashEntry::new(actual_hash, file_size);
                new_entry.last_modified = Utc::now();
                database.insert(path_str, new_entry);
                return Ok(false);
            }

            // Hash matches - update verified timestamp
            if let Some(entry) = database.entries.get_mut(&path_str) {
                entry.mark_verified();
            }
            Ok(true)
        }
        None => {
            // New file - add to database
            let entry = HashEntry::new(actual_hash, file_size);
            database.insert(path_str, entry);
            Ok(true)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;
    use tempfile::NamedTempFile;

    #[test]
    fn test_compute_hash() {
        let data = b"Hello, World!";
        let hash = compute_hash(data);
        assert_eq!(hash.len(), 96); // SHA-384 produces 96 hex characters
    }

    #[test]
    fn test_compute_file_hash() {
        let mut file = NamedTempFile::new().unwrap();
        file.write_all(b"Test content").unwrap();
        file.flush().unwrap();

        let hash = compute_file_hash(file.path()).unwrap();
        assert_eq!(hash.len(), 96);
    }

    #[test]
    fn test_verify_file_hash_success() {
        let mut file = NamedTempFile::new().unwrap();
        file.write_all(b"Test content").unwrap();
        file.flush().unwrap();

        let hash = compute_file_hash(file.path()).unwrap();
        assert!(verify_file_hash(file.path(), &hash).is_ok());
    }

    #[test]
    fn test_verify_file_hash_failure() {
        let mut file = NamedTempFile::new().unwrap();
        file.write_all(b"Test content").unwrap();
        file.flush().unwrap();

        let wrong_hash = "a".repeat(96);
        assert!(verify_file_hash(file.path(), &wrong_hash).is_err());
    }

    #[test]
    fn test_hash_database() {
        let mut db = HashDatabase::new();
        assert!(db.is_empty());

        let entry = HashEntry::new("abc123".to_string(), 100);
        db.insert("test.txt".to_string(), entry);

        assert_eq!(db.len(), 1);
        assert!(db.get("test.txt").is_some());
        assert_eq!(db.get("test.txt").unwrap().hash, "abc123");
    }

    #[test]
    fn test_save_creates_missing_parent_directory() {
        let dir = tempfile::TempDir::new().unwrap();
        let db_path = dir
            .path()
            .join("nested")
            .join("does-not-exist-yet")
            .join(".hashes.json");
        assert!(!db_path.parent().unwrap().exists());

        let mut db = HashDatabase::new();
        db.insert(
            "test.txt".to_string(),
            HashEntry::new("abc123".to_string(), 100),
        );
        db.save(&db_path).unwrap();

        assert!(db_path.exists());
        let reloaded = HashDatabase::load(&db_path).unwrap();
        assert_eq!(reloaded.get("test.txt").unwrap().hash, "abc123");
    }

    #[test]
    fn test_hash_entry_verified() {
        let mut entry = HashEntry::new("hash".to_string(), 100);
        let original_verified = entry.last_verified;

        std::thread::sleep(std::time::Duration::from_millis(10));
        entry.mark_verified();

        assert!(entry.last_verified > original_verified);
    }
}
