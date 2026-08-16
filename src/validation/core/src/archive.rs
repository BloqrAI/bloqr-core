//! Archive creation and management with manifest tracking.

use chrono::Utc;
use serde::{Deserialize, Serialize};
use std::fs;
use std::path::{Path, PathBuf};

use crate::error::Result;
use crate::hash::compute_file_hash;

/// Archive manifest containing metadata about archived files.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ArchiveManifest {
    /// Timestamp when archive was created.
    pub created_at: String,
    /// Compilation result hash.
    pub output_hash: String,
    /// Number of rules compiled.
    pub rule_count: usize,
    /// Files included in archive.
    pub files: Vec<ArchivedFile>,
}

/// Information about an archived file.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ArchivedFile {
    /// Original file path.
    pub path: String,
    /// SHA-384 hash.
    pub hash: String,
    /// File size in bytes.
    pub size: u64,
}

/// Create an archive of input files.
///
/// # Errors
///
/// Returns an error if archive cannot be created or files cannot be copied.
pub fn create_archive<P: AsRef<Path>>(
    input_dir: P,
    archive_root: P,
    output_hash: &str,
    rule_count: usize,
) -> Result<PathBuf> {
    let archive_root = archive_root.as_ref();

    // Create timestamped archive directory
    let timestamp = Utc::now().format("%Y-%m-%d_%H-%M-%S").to_string();
    let archive_dir = archive_root.join(&timestamp);
    fs::create_dir_all(&archive_dir)?;

    let mut files = Vec::new();

    // Copy all files from input directory
    for entry in walkdir::WalkDir::new(input_dir.as_ref())
        .follow_links(false)
        .into_iter()
        .filter_map(|e| e.ok())
    {
        if entry.file_type().is_file() {
            let path = entry.path();
            let relative_path = path
                .strip_prefix(input_dir.as_ref())
                .unwrap_or(path)
                .display()
                .to_string();

            let hash = compute_file_hash(path)?;
            let size = fs::metadata(path)?.len();

            // Copy file to archive
            let dest = archive_dir.join(&relative_path);
            if let Some(parent) = dest.parent() {
                fs::create_dir_all(parent)?;
            }
            fs::copy(path, dest)?;

            files.push(ArchivedFile {
                path: relative_path,
                hash,
                size,
            });
        }
    }

    // Create manifest
    let manifest = ArchiveManifest {
        created_at: timestamp.clone(),
        output_hash: output_hash.to_string(),
        rule_count,
        files,
    };

    // Save manifest
    let manifest_path = archive_dir.join("manifest.json");
    let manifest_json = serde_json::to_string_pretty(&manifest)?;
    fs::write(manifest_path, manifest_json)?;

    Ok(archive_dir)
}

/// Clean up old archives based on retention policy.
///
/// # Errors
///
/// Returns an error if archives cannot be cleaned up.
pub fn cleanup_old_archives<P: AsRef<Path>>(archive_root: P, retention_days: u32) -> Result<usize> {
    let archive_root = archive_root.as_ref();
    if !archive_root.exists() {
        return Ok(0);
    }

    let cutoff = Utc::now() - chrono::Duration::days(i64::from(retention_days));
    let mut removed_count = 0;

    for entry in fs::read_dir(archive_root)? {
        let entry = entry?;
        if !entry.file_type()?.is_dir() {
            continue;
        }

        let metadata = entry.metadata()?;
        if let Ok(modified) = metadata.modified() {
            let modified_time: chrono::DateTime<Utc> = modified.into();
            if modified_time < cutoff {
                fs::remove_dir_all(entry.path())?;
                removed_count += 1;
            }
        }
    }

    Ok(removed_count)
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;

    #[test]
    fn test_archive_manifest_serialization() {
        let manifest = ArchiveManifest {
            created_at: "2024-12-27".to_string(),
            output_hash: "abc123".to_string(),
            rule_count: 100,
            files: vec![ArchivedFile {
                path: "test.txt".to_string(),
                hash: "hash123".to_string(),
                size: 1024,
            }],
        };

        let json = serde_json::to_string(&manifest).unwrap();
        let deserialized: ArchiveManifest = serde_json::from_str(&json).unwrap();

        assert_eq!(deserialized.rule_count, 100);
        assert_eq!(deserialized.files.len(), 1);
    }

    #[test]
    fn test_create_archive() {
        let input_dir = TempDir::new().unwrap();
        let archive_root = TempDir::new().unwrap();

        // Create test file
        let test_file = input_dir.path().join("test.txt");
        fs::write(&test_file, "test content").unwrap();

        let archive_dir =
            create_archive(input_dir.path(), archive_root.path(), "output_hash", 42).unwrap();

        assert!(archive_dir.exists());
        assert!(archive_dir.join("manifest.json").exists());
        assert!(archive_dir.join("test.txt").exists());
    }
}
