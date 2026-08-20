//! Configuration reader with multi-format support (JSON, YAML, TOML).
//!
//! This module provides types and functions for reading, parsing, and validating
//! hostlist-compiler configuration files in multiple formats.

use serde::{Deserialize, Serialize};
use std::fmt;
use std::fs;
use std::path::{Path, PathBuf};

use crate::error::{CompilerError, Result};

/// Supported configuration file formats.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub enum ConfigFormat {
    /// JSON format (.json)
    #[default]
    Json,
    /// YAML format (.yaml, .yml)
    Yaml,
    /// TOML format (.toml)
    Toml,
}

impl ConfigFormat {
    /// Get the file extensions associated with this format.
    #[must_use]
    pub const fn extensions(&self) -> &'static [&'static str] {
        match self {
            Self::Json => &["json"],
            Self::Yaml => &["yaml", "yml"],
            Self::Toml => &["toml"],
        }
    }

    /// Detect format from file extension.
    ///
    /// # Errors
    ///
    /// Returns an error if the extension is not recognized.
    pub fn from_extension(ext: &str) -> Result<Self> {
        match ext.to_lowercase().as_str() {
            "json" => Ok(Self::Json),
            "yaml" | "yml" => Ok(Self::Yaml),
            "toml" => Ok(Self::Toml),
            _ => Err(CompilerError::unknown_extension(ext)),
        }
    }

    /// Detect format from file path.
    ///
    /// # Errors
    ///
    /// Returns an error if the extension is missing or not recognized.
    pub fn from_path<P: AsRef<Path>>(path: P) -> Result<Self> {
        let ext = path
            .as_ref()
            .extension()
            .and_then(|e| e.to_str())
            .unwrap_or("");
        Self::from_extension(ext)
    }
}

impl fmt::Display for ConfigFormat {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Json => write!(f, "JSON"),
            Self::Yaml => write!(f, "YAML"),
            Self::Toml => write!(f, "TOML"),
        }
    }
}

/// Available transformations that can be applied during compilation.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub enum Transformation {
    /// Remove comment lines from the output.
    RemoveComments,
    /// Compress the output by removing unnecessary whitespace.
    Compress,
    /// Remove rule modifiers.
    RemoveModifiers,
    /// Validate rules and remove invalid ones.
    Validate,
    /// Validate and allow IP addresses in rules.
    #[serde(rename = "ValidateAllowIp")]
    ValidateAllowIp,
    /// Remove duplicate rules.
    Deduplicate,
    /// Invert allow rules.
    InvertAllow,
    /// Remove empty lines.
    RemoveEmptyLines,
    /// Trim whitespace from line beginnings and ends.
    TrimLines,
    /// Insert a final newline at the end of output.
    InsertFinalNewLine,
    /// Convert internationalized domain names to ASCII.
    #[serde(rename = "ConvertToAscii")]
    ConvertToAscii,
}

impl fmt::Display for Transformation {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::RemoveComments => write!(f, "RemoveComments"),
            Self::Compress => write!(f, "Compress"),
            Self::RemoveModifiers => write!(f, "RemoveModifiers"),
            Self::Validate => write!(f, "Validate"),
            Self::ValidateAllowIp => write!(f, "ValidateAllowIp"),
            Self::Deduplicate => write!(f, "Deduplicate"),
            Self::InvertAllow => write!(f, "InvertAllow"),
            Self::RemoveEmptyLines => write!(f, "RemoveEmptyLines"),
            Self::TrimLines => write!(f, "TrimLines"),
            Self::InsertFinalNewLine => write!(f, "InsertFinalNewLine"),
            Self::ConvertToAscii => write!(f, "ConvertToAscii"),
        }
    }
}

/// Source type for filter lists.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum SourceType {
    /// AdBlock-style filter list.
    #[default]
    #[serde(alias = "Adblock")]
    Adblock,
    /// Hosts file format.
    #[serde(alias = "Hosts")]
    Hosts,
}

impl fmt::Display for SourceType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Adblock => write!(f, "adblock"),
            Self::Hosts => write!(f, "hosts"),
        }
    }
}

/// A source filter list to compile.
#[derive(Debug, Clone, Serialize, Deserialize, Default)]
#[serde(default)]
pub struct FilterSource {
    /// Name of the source.
    pub name: String,

    /// Source URL or file path.
    pub source: String,

    /// Type of the source.
    #[serde(rename = "type")]
    pub source_type: SourceType,

    /// Transformations to apply to this source only.
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub transformations: Vec<String>,

    /// Inclusion patterns for this source.
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub inclusions: Vec<String>,

    /// Exclusion patterns for this source.
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub exclusions: Vec<String>,
}

impl FilterSource {
    /// Create a new filter source.
    #[must_use]
    pub fn new(name: impl Into<String>, source: impl Into<String>) -> Self {
        Self {
            name: name.into(),
            source: source.into(),
            source_type: SourceType::default(),
            transformations: Vec::new(),
            inclusions: Vec::new(),
            exclusions: Vec::new(),
        }
    }

    /// Set the source type.
    #[must_use]
    pub fn with_type(mut self, source_type: SourceType) -> Self {
        self.source_type = source_type;
        self
    }

    /// Check if the source is a URL.
    #[must_use]
    pub fn is_url(&self) -> bool {
        self.source.starts_with("http://") || self.source.starts_with("https://")
    }

    /// Check if the source is a local file.
    #[must_use]
    pub fn is_local(&self) -> bool {
        !self.is_url()
    }
}

/// Configuration for the hostlist-compiler.
#[derive(Debug, Clone, Serialize, Deserialize, Default)]
#[serde(default)]
pub struct CompilerConfig {
    /// Name of the filter list.
    pub name: String,

    /// Description of the filter list.
    #[serde(skip_serializing_if = "String::is_empty")]
    pub description: String,

    /// Homepage URL.
    #[serde(skip_serializing_if = "String::is_empty")]
    pub homepage: String,

    /// License identifier.
    #[serde(skip_serializing_if = "String::is_empty")]
    pub license: String,

    /// Version of the filter list.
    #[serde(skip_serializing_if = "String::is_empty")]
    pub version: String,

    /// List of source filter lists to compile.
    pub sources: Vec<FilterSource>,

    /// Global transformations to apply.
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub transformations: Vec<String>,

    /// Global inclusion patterns.
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub inclusions: Vec<String>,

    /// Global exclusion patterns.
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub exclusions: Vec<String>,

    /// Source format (not serialized).
    #[serde(skip)]
    pub(crate) source_format: Option<ConfigFormat>,

    /// Source file path (not serialized).
    #[serde(skip)]
    pub(crate) source_path: Option<PathBuf>,
}

impl CompilerConfig {
    /// Create a new empty configuration.
    #[must_use]
    pub fn new(name: impl Into<String>) -> Self {
        Self {
            name: name.into(),
            ..Default::default()
        }
    }

    /// Set the description.
    #[must_use]
    pub fn with_description(mut self, description: impl Into<String>) -> Self {
        self.description = description.into();
        self
    }

    /// Set the version.
    #[must_use]
    pub fn with_version(mut self, version: impl Into<String>) -> Self {
        self.version = version.into();
        self
    }

    /// Add a source.
    #[must_use]
    pub fn with_source(mut self, source: FilterSource) -> Self {
        self.sources.push(source);
        self
    }

    /// Add a transformation.
    #[must_use]
    pub fn with_transformation(mut self, transformation: impl Into<String>) -> Self {
        self.transformations.push(transformation.into());
        self
    }

    /// Get the source format.
    #[must_use]
    pub const fn format(&self) -> Option<ConfigFormat> {
        self.source_format
    }

    /// Get the source file path.
    #[must_use]
    pub fn path(&self) -> Option<&Path> {
        self.source_path.as_deref()
    }

    /// Validate the configuration.
    ///
    /// # Errors
    ///
    /// Returns an error if validation fails.
    pub fn validate(&self) -> Result<()> {
        if self.name.is_empty() {
            return Err(CompilerError::validation_failed(
                "configuration 'name' is required",
            ));
        }

        if self.sources.is_empty() {
            return Err(CompilerError::validation_failed(
                "at least one source is required",
            ));
        }

        for (i, source) in self.sources.iter().enumerate() {
            if source.source.is_empty() {
                return Err(CompilerError::validation_failed(format!(
                    "source[{i}].source is required"
                )));
            }
        }

        Ok(())
    }

    /// Get local sources count.
    #[must_use]
    pub fn local_sources_count(&self) -> usize {
        self.sources.iter().filter(|s| s.is_local()).count()
    }

    /// Get remote sources count.
    #[must_use]
    pub fn remote_sources_count(&self) -> usize {
        self.sources.iter().filter(|s| s.is_url()).count()
    }
}

/// Read configuration from a file.
///
/// # Arguments
///
/// * `path` - Path to the configuration file.
/// * `format` - Optional format override. If `None`, format is detected from extension.
///
/// # Errors
///
/// Returns an error if the file doesn't exist, can't be read, or has invalid syntax.
pub fn read_config<P: AsRef<Path>>(
    path: P,
    format: Option<ConfigFormat>,
) -> Result<CompilerConfig> {
    let path = path.as_ref();

    if !path.exists() {
        return Err(CompilerError::config_not_found(path));
    }

    let format = format.unwrap_or_else(|| ConfigFormat::from_path(path).unwrap_or_default());
    let content = fs::read_to_string(path).map_err(|e| {
        CompilerError::file_system(format!("reading configuration from {}", path.display()), e)
    })?;

    let mut config: CompilerConfig = match format {
        ConfigFormat::Json => serde_json::from_str(&content)?,
        ConfigFormat::Yaml => serde_yaml::from_str(&content)?,
        ConfigFormat::Toml => toml::from_str(&content)?,
    };

    config.source_format = Some(format);
    config.source_path = Some(path.to_path_buf());

    Ok(config)
}

/// Convert configuration to JSON string.
///
/// # Errors
///
/// Returns an error if serialization fails.
pub fn to_json(config: &CompilerConfig) -> Result<String> {
    Ok(serde_json::to_string_pretty(config)?)
}

/// Convert configuration to YAML string.
///
/// # Errors
///
/// Returns an error if serialization fails.
pub fn to_yaml(config: &CompilerConfig) -> Result<String> {
    Ok(serde_yaml::to_string(config)?)
}

/// Convert configuration to TOML string.
///
/// # Errors
///
/// Returns an error if serialization fails.
pub fn to_toml(config: &CompilerConfig) -> std::result::Result<String, toml::ser::Error> {
    toml::to_string_pretty(config)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs::File;
    use std::io::Write;
    use tempfile::TempDir;

    #[test]
    fn test_config_format_from_extension() {
        assert_eq!(
            ConfigFormat::from_extension("json").unwrap(),
            ConfigFormat::Json
        );
        assert_eq!(
            ConfigFormat::from_extension("yaml").unwrap(),
            ConfigFormat::Yaml
        );
        assert_eq!(
            ConfigFormat::from_extension("yml").unwrap(),
            ConfigFormat::Yaml
        );
        assert_eq!(
            ConfigFormat::from_extension("toml").unwrap(),
            ConfigFormat::Toml
        );
        assert!(ConfigFormat::from_extension("txt").is_err());
    }

    #[test]
    fn test_config_format_from_path() {
        assert_eq!(
            ConfigFormat::from_path("config.json").unwrap(),
            ConfigFormat::Json
        );
        assert_eq!(
            ConfigFormat::from_path("/path/to/config.yaml").unwrap(),
            ConfigFormat::Yaml
        );
    }

    #[test]
    fn test_filter_source_is_url() {
        let url_source = FilterSource::new("test", "https://example.com/list.txt");
        assert!(url_source.is_url());
        assert!(!url_source.is_local());

        let local_source = FilterSource::new("test", "./local/list.txt");
        assert!(!local_source.is_url());
        assert!(local_source.is_local());
    }

    #[test]
    fn test_compiler_config_builder() {
        let config = CompilerConfig::new("Test Filter")
            .with_description("A test filter list")
            .with_version("1.0.0")
            .with_source(FilterSource::new("Local", "./rules.txt"))
            .with_transformation("Deduplicate");

        assert_eq!(config.name, "Test Filter");
        assert_eq!(config.description, "A test filter list");
        assert_eq!(config.version, "1.0.0");
        assert_eq!(config.sources.len(), 1);
        assert_eq!(config.transformations.len(), 1);
    }

    #[test]
    fn test_compiler_config_validate() {
        let valid = CompilerConfig::new("Test")
            .with_source(FilterSource::new("Source", "https://example.com"));
        assert!(valid.validate().is_ok());

        let no_name = CompilerConfig::default();
        assert!(no_name.validate().is_err());

        let no_sources = CompilerConfig::new("Test");
        assert!(no_sources.validate().is_err());
    }

    #[test]
    fn test_read_json_config() {
        let dir = TempDir::new().unwrap();
        let path = dir.path().join("config.json");
        let mut file = File::create(&path).unwrap();
        writeln!(
            file,
            r#"{{"name": "Test", "version": "1.0.0", "sources": [{{"source": "test.txt"}}]}}"#
        )
        .unwrap();

        let config = read_config(&path, None).unwrap();
        assert_eq!(config.name, "Test");
        assert_eq!(config.version, "1.0.0");
        assert_eq!(config.format(), Some(ConfigFormat::Json));
    }

    #[test]
    fn test_read_yaml_config() {
        let dir = TempDir::new().unwrap();
        let path = dir.path().join("config.yaml");
        let mut file = File::create(&path).unwrap();
        writeln!(
            file,
            "name: YAML Test\nversion: 2.0.0\nsources:\n  - source: test.txt"
        )
        .unwrap();

        let config = read_config(&path, None).unwrap();
        assert_eq!(config.name, "YAML Test");
        assert_eq!(config.version, "2.0.0");
        assert_eq!(config.format(), Some(ConfigFormat::Yaml));
    }

    #[test]
    fn test_to_json() {
        let config = CompilerConfig::new("Test").with_version("1.0.0");
        let json = to_json(&config).unwrap();
        assert!(json.contains("\"name\": \"Test\""));
        assert!(json.contains("\"version\": \"1.0.0\""));
    }

    #[test]
    fn test_sources_count() {
        let config = CompilerConfig::new("Test")
            .with_source(FilterSource::new("Local1", "./local.txt"))
            .with_source(FilterSource::new("Remote", "https://example.com/list.txt"))
            .with_source(FilterSource::new("Local2", "./another.txt"));

        assert_eq!(config.local_sources_count(), 2);
        assert_eq!(config.remote_sources_count(), 1);
    }
}
