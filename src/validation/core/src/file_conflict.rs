//! File conflict resolution strategies.

use std::path::{Path, PathBuf};

use crate::config::ConflictStrategy;
use crate::error::{Result, ValidationError};

/// File conflict resolver.
pub struct FileConflictResolver {
    strategy: ConflictStrategy,
}

impl FileConflictResolver {
    /// Create a new resolver with the specified strategy.
    #[must_use]
    pub fn new(strategy: ConflictStrategy) -> Self {
        Self { strategy }
    }

    /// Resolve a file conflict and return the final path to use.
    ///
    /// # Errors
    ///
    /// Returns an error if strategy is Error and file exists, or if file operations fail.
    pub fn resolve<P: AsRef<Path>>(&self, path: P) -> Result<PathBuf> {
        let path = path.as_ref();

        if !path.exists() {
            return Ok(path.to_path_buf());
        }

        match self.strategy {
            ConflictStrategy::Overwrite => Ok(path.to_path_buf()),
            ConflictStrategy::Error => Err(ValidationError::file_conflict(format!(
                "File already exists: {}",
                path.display()
            ))),
            ConflictStrategy::Rename => self.find_available_name(path),
        }
    }

    /// Find an available filename by appending numbers.
    fn find_available_name<P: AsRef<Path>>(&self, path: P) -> Result<PathBuf> {
        let path = path.as_ref();
        let parent = path
            .parent()
            .ok_or_else(|| ValidationError::file_conflict("Cannot determine parent directory"))?;

        let stem = path.file_stem().and_then(|s| s.to_str()).unwrap_or("file");
        let extension = path.extension().and_then(|e| e.to_str()).unwrap_or("");

        for i in 1..=9999 {
            let new_name = if extension.is_empty() {
                format!("{stem}-{i}")
            } else {
                format!("{stem}-{i}.{extension}")
            };

            let new_path = parent.join(new_name);
            if !new_path.exists() {
                return Ok(new_path);
            }
        }

        Err(ValidationError::file_conflict(
            "Could not find available filename after 9999 attempts",
        ))
    }
}

/// Resolve file conflict using the specified strategy.
///
/// # Errors
///
/// Returns an error if resolution fails.
pub fn resolve_conflict<P: AsRef<Path>>(path: P, strategy: ConflictStrategy) -> Result<PathBuf> {
    FileConflictResolver::new(strategy).resolve(path)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;
    use tempfile::TempDir;

    #[test]
    fn test_no_conflict() {
        let dir = TempDir::new().unwrap();
        let path = dir.path().join("new-file.txt");

        let resolver = FileConflictResolver::new(ConflictStrategy::Rename);
        let result = resolver.resolve(&path).unwrap();

        assert_eq!(result, path);
    }

    #[test]
    fn test_rename_strategy() {
        let dir = TempDir::new().unwrap();
        let path = dir.path().join("file.txt");

        // Create existing file
        fs::write(&path, "existing").unwrap();

        let resolver = FileConflictResolver::new(ConflictStrategy::Rename);
        let result = resolver.resolve(&path).unwrap();

        assert_ne!(result, path);
        assert_eq!(result, dir.path().join("file-1.txt"));
    }

    #[test]
    fn test_overwrite_strategy() {
        let dir = TempDir::new().unwrap();
        let path = dir.path().join("file.txt");

        // Create existing file
        fs::write(&path, "existing").unwrap();

        let resolver = FileConflictResolver::new(ConflictStrategy::Overwrite);
        let result = resolver.resolve(&path).unwrap();

        assert_eq!(result, path);
    }

    #[test]
    fn test_error_strategy() {
        let dir = TempDir::new().unwrap();
        let path = dir.path().join("file.txt");

        // Create existing file
        fs::write(&path, "existing").unwrap();

        let resolver = FileConflictResolver::new(ConflictStrategy::Error);
        let result = resolver.resolve(&path);

        assert!(result.is_err());
    }
}
