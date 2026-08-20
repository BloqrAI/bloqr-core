//! Chunking implementation for parallel compilation of large rule lists.
//!
//! This module provides functionality to split large filter source configurations
//! into chunks for parallel compilation, which can significantly improve
//! compilation times for large filter lists.

use std::collections::HashSet;
use std::path::PathBuf;
use std::time::Instant;

use tokio::process::Command;

use crate::config::{to_json, CompilerConfig, FilterSource};
use crate::error::{CompilerError, Result};

/// Strategy for splitting sources into chunks.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub enum ChunkingStrategy {
    /// Distribute sources evenly across chunks.
    #[default]
    Source,
    /// Balance chunks by estimated line count (not yet implemented).
    LineCount,
}

/// Configuration options for chunked parallel compilation.
#[derive(Debug, Clone)]
pub struct ChunkingOptions {
    /// Whether chunking is enabled.
    pub enabled: bool,
    /// Maximum estimated rules per chunk.
    pub chunk_size: usize,
    /// Maximum number of parallel workers.
    pub max_parallel: usize,
    /// Chunking strategy.
    pub strategy: ChunkingStrategy,
}

impl Default for ChunkingOptions {
    fn default() -> Self {
        Self {
            enabled: false,
            chunk_size: 100_000,
            max_parallel: std::thread::available_parallelism()
                .map(|p| p.get())
                .unwrap_or(4),
            strategy: ChunkingStrategy::Source,
        }
    }
}

impl ChunkingOptions {
    /// Create new chunking options with default values (disabled).
    #[must_use]
    pub fn new() -> Self {
        Self::default()
    }

    /// Create chunking options optimized for large filter lists.
    #[must_use]
    pub fn for_large_lists() -> Self {
        let parallelism = std::thread::available_parallelism()
            .map(|p| p.get())
            .unwrap_or(4);
        Self {
            enabled: true,
            chunk_size: 100_000,
            max_parallel: std::cmp::max(2, parallelism),
            strategy: ChunkingStrategy::Source,
        }
    }

    /// Enable or disable chunking.
    #[must_use]
    pub const fn with_enabled(mut self, enabled: bool) -> Self {
        self.enabled = enabled;
        self
    }

    /// Set the chunk size.
    #[must_use]
    pub const fn with_chunk_size(mut self, chunk_size: usize) -> Self {
        self.chunk_size = chunk_size;
        self
    }

    /// Set the maximum parallel workers.
    #[must_use]
    pub const fn with_max_parallel(mut self, max_parallel: usize) -> Self {
        self.max_parallel = max_parallel;
        self
    }

    /// Set the chunking strategy.
    #[must_use]
    pub const fn with_strategy(mut self, strategy: ChunkingStrategy) -> Self {
        self.strategy = strategy;
        self
    }
}

/// Metadata about a compilation chunk.
#[derive(Debug, Clone, Default)]
pub struct ChunkMetadata {
    /// Chunk index (0-based).
    pub index: usize,
    /// Total number of chunks.
    pub total: usize,
    /// Estimated rule count for this chunk.
    pub estimated_rules: usize,
    /// Actual rule count after compilation.
    pub actual_rules: Option<usize>,
    /// Sources included in this chunk.
    pub sources: Vec<FilterSource>,
    /// Compilation duration in milliseconds.
    pub elapsed_ms: Option<u64>,
    /// Whether this chunk compiled successfully.
    pub success: bool,
    /// Error message if compilation failed.
    pub error_message: Option<String>,
    /// Path to the chunk's output file.
    pub output_path: Option<PathBuf>,
}

/// Result of chunked compilation.
#[derive(Debug, Clone, Default)]
pub struct ChunkedCompilationResult {
    /// Whether all chunks compiled successfully.
    pub success: bool,
    /// Total compilation time in milliseconds.
    pub total_elapsed_ms: u64,
    /// Metadata for each chunk.
    pub chunks: Vec<ChunkMetadata>,
    /// Total rule count across all chunks.
    pub total_rules: usize,
    /// Final rule count after deduplication.
    pub final_rule_count: usize,
    /// Number of duplicate rules removed.
    pub duplicates_removed: usize,
    /// Merged output content.
    pub merged_rules: Option<Vec<String>>,
    /// Errors from failed chunks.
    pub errors: Vec<String>,
}

impl ChunkedCompilationResult {
    /// Get the estimated speedup ratio compared to sequential compilation.
    #[must_use]
    pub fn estimated_speedup(&self) -> f64 {
        if self.chunks.is_empty() || self.total_elapsed_ms == 0 {
            return 1.0;
        }
        let total_chunk_time: u64 = self.chunks.iter().filter_map(|c| c.elapsed_ms).sum();
        total_chunk_time as f64 / self.total_elapsed_ms as f64
    }
}

/// Determine if chunking should be enabled for the given configuration.
#[must_use]
pub fn should_enable_chunking(config: &CompilerConfig, options: Option<&ChunkingOptions>) -> bool {
    // If no sources, don't chunk
    if config.sources.is_empty() {
        return false;
    }

    // If explicitly disabled, don't chunk
    if let Some(opts) = options {
        if !opts.enabled {
            return false;
        }
        // If explicitly enabled, chunk
        if opts.enabled {
            return true;
        }
    }

    // For source strategy (default), chunk if we have multiple sources
    let strategy = options
        .map(|o| o.strategy)
        .unwrap_or(ChunkingStrategy::Source);
    if strategy == ChunkingStrategy::Source && config.sources.len() > 1 {
        return true;
    }

    false
}

/// Split a configuration into chunks for parallel compilation.
#[must_use]
pub fn split_into_chunks(
    config: &CompilerConfig,
    options: &ChunkingOptions,
) -> Vec<(CompilerConfig, ChunkMetadata)> {
    let sources = &config.sources;

    if sources.is_empty() {
        tracing::warn!("No sources to chunk");
        return Vec::new();
    }

    tracing::info!(
        "Splitting configuration into chunks (strategy: {:?})",
        options.strategy
    );

    match options.strategy {
        ChunkingStrategy::Source => split_by_source(config, options),
        ChunkingStrategy::LineCount => {
            tracing::warn!("LineCount strategy not yet implemented, falling back to Source");
            split_by_source(config, options)
        }
    }
}

fn split_by_source(
    config: &CompilerConfig,
    options: &ChunkingOptions,
) -> Vec<(CompilerConfig, ChunkMetadata)> {
    let sources = &config.sources;
    let mut chunks = Vec::new();

    // Calculate sources per chunk to keep chunks balanced
    let sources_per_chunk = sources.len().div_ceil(options.max_parallel);
    let sources_per_chunk = std::cmp::max(1, sources_per_chunk);
    let total_chunks = sources.len().div_ceil(sources_per_chunk);

    tracing::info!(
        "Creating {} chunks with ~{} sources each",
        total_chunks,
        sources_per_chunk
    );

    for i in 0..total_chunks {
        let start_idx = i * sources_per_chunk;
        let end_idx = std::cmp::min(start_idx + sources_per_chunk, sources.len());
        let chunk_sources: Vec<FilterSource> = sources[start_idx..end_idx].to_vec();

        let chunk_config = CompilerConfig {
            name: format!("{} (chunk {}/{})", config.name, i + 1, total_chunks),
            description: config.description.clone(),
            homepage: config.homepage.clone(),
            license: config.license.clone(),
            version: config.version.clone(),
            sources: chunk_sources.clone(),
            transformations: config.transformations.clone(),
            inclusions: config.inclusions.clone(),
            exclusions: config.exclusions.clone(),
            source_format: config.source_format,
            source_path: config.source_path.clone(),
        };

        let metadata = ChunkMetadata {
            index: i,
            total: total_chunks,
            estimated_rules: 0,
            sources: chunk_sources,
            ..Default::default()
        };

        chunks.push((chunk_config, metadata));
    }

    tracing::debug!("Created {} chunks", chunks.len());
    chunks
}

/// Merge compiled rules from multiple chunks.
#[must_use]
pub fn merge_chunks(chunk_results: &[Vec<String>]) -> (Vec<String>, usize) {
    tracing::info!("Merging {} chunks...", chunk_results.len());

    // Flatten all chunks
    let all_rules: Vec<&String> = chunk_results.iter().flatten().collect();
    tracing::debug!("Total rules before deduplication: {}", all_rules.len());

    // Deduplicate while preserving order
    let mut seen = HashSet::new();
    let mut deduplicated = Vec::new();

    for rule in all_rules {
        let trimmed = rule.trim();

        // Keep comments and empty lines without deduplication
        if trimmed.is_empty() || trimmed.starts_with('!') || trimmed.starts_with('#') {
            deduplicated.push(rule.clone());
            continue;
        }

        // Deduplicate actual rules
        if seen.insert(rule.clone()) {
            deduplicated.push(rule.clone());
        }
    }

    let total_before = chunk_results.iter().map(Vec::len).sum::<usize>();
    let duplicates_removed = total_before - deduplicated.len();

    tracing::info!(
        "Merged to {} rules (removed {} duplicates)",
        deduplicated.len(),
        duplicates_removed
    );

    (deduplicated, duplicates_removed)
}

/// Estimate the time savings from chunked compilation.
#[must_use]
pub fn estimate_speedup(total_rules: usize, options: &ChunkingOptions) -> f64 {
    if !options.enabled || total_rules == 0 {
        return 1.0;
    }

    // Simple linear model
    let num_chunks = total_rules.div_ceil(options.chunk_size);
    let num_chunks = num_chunks as f64;

    // Theoretical speedup = min(numChunks, maxParallel)
    f64::min(num_chunks, options.max_parallel as f64)
}

/// Compile chunks in parallel.
///
/// # Errors
///
/// Returns an error if any chunk fails to compile.
pub async fn compile_chunks_async(
    chunks: Vec<(CompilerConfig, ChunkMetadata)>,
    options: &ChunkingOptions,
    debug: bool,
) -> Result<ChunkedCompilationResult> {
    let start = Instant::now();
    let mut result = ChunkedCompilationResult::default();
    let mut chunk_results: Vec<Vec<String>> = Vec::new();

    tracing::info!(
        "Compiling {} chunks with max {} parallel workers",
        chunks.len(),
        options.max_parallel
    );

    // Process chunks in batches to limit parallelism
    for batch_start in (0..chunks.len()).step_by(options.max_parallel) {
        let batch_end = std::cmp::min(batch_start + options.max_parallel, chunks.len());
        let batch: Vec<_> = chunks[batch_start..batch_end].to_vec();

        let batch_number = batch_start / options.max_parallel + 1;
        let total_batches = chunks.len().div_ceil(options.max_parallel);

        tracing::info!(
            "Processing batch {}/{} (chunks {}-{})",
            batch_number,
            total_batches,
            batch_start + 1,
            batch_end
        );

        // Compile all chunks in this batch in parallel
        let tasks: Vec<_> = batch
            .into_iter()
            .map(|(config, metadata)| compile_single_chunk_async(config, metadata, debug))
            .collect();

        let batch_results = futures::future::join_all(tasks).await;

        for batch_result in batch_results {
            match batch_result {
                Ok((rules, metadata)) => {
                    if metadata.success {
                        chunk_results.push(rules);
                    }
                    if !metadata.success {
                        if let Some(ref error) = metadata.error_message {
                            result
                                .errors
                                .push(format!("Chunk {}: {}", metadata.index + 1, error));
                        }
                    }
                    result.chunks.push(metadata);
                }
                Err(e) => {
                    result.errors.push(e.to_string());
                }
            }
        }
    }

    // Calculate total time
    result.total_elapsed_ms = start.elapsed().as_millis() as u64;

    // Merge results
    if !chunk_results.is_empty() {
        let (merged_rules, duplicates_removed) = merge_chunks(&chunk_results);
        result.final_rule_count = merged_rules.len();
        result.duplicates_removed = duplicates_removed;
        result.merged_rules = Some(merged_rules);
    }

    result.total_rules = result.chunks.iter().filter_map(|c| c.actual_rules).sum();
    result.success = result.errors.is_empty();

    tracing::info!(
        "Chunked compilation complete: {} rules (removed {} duplicates) in {}ms",
        result.final_rule_count,
        result.duplicates_removed,
        result.total_elapsed_ms
    );

    if result.estimated_speedup() > 1.0 {
        tracing::info!("Estimated speedup: {:.2}x", result.estimated_speedup());
    }

    Ok(result)
}

async fn compile_single_chunk_async(
    config: CompilerConfig,
    mut metadata: ChunkMetadata,
    debug: bool,
) -> Result<(Vec<String>, ChunkMetadata)> {
    let start = Instant::now();

    tracing::debug!(
        "Starting chunk {}/{}: {}",
        metadata.index + 1,
        metadata.total,
        config.name
    );

    // Create temporary config and output files
    let temp_config_path =
        std::env::temp_dir().join(format!("chunk-config-{}.json", uuid::Uuid::new_v4()));
    let temp_output_path =
        std::env::temp_dir().join(format!("chunk-output-{}.txt", uuid::Uuid::new_v4()));

    // Ensure cleanup on all paths
    struct TempFileCleanup {
        config_path: PathBuf,
        output_path: PathBuf,
    }

    impl Drop for TempFileCleanup {
        fn drop(&mut self) {
            let _ = std::fs::remove_file(&self.config_path);
            let _ = std::fs::remove_file(&self.output_path);
        }
    }

    let _cleanup = TempFileCleanup {
        config_path: temp_config_path.clone(),
        output_path: temp_output_path.clone(),
    };

    // Write config to temp file
    let json = to_json(&config)?;
    tokio::fs::write(&temp_config_path, &json)
        .await
        .map_err(|e| {
            CompilerError::file_system(
                format!("writing chunk config to {}", temp_config_path.display()),
                e,
            )
        })?;

    // Get compiler command
    let (cmd, args) = get_compiler_command(
        temp_config_path.to_str().unwrap_or(""),
        temp_output_path.to_str().unwrap_or(""),
    )?;

    if debug {
        eprintln!("[DEBUG] Running: {} {}", cmd, args.join(" "));
    }

    // Execute compiler asynchronously
    let output =
        Command::new(&cmd).args(&args).output().await.map_err(|e| {
            CompilerError::process_execution(format!("{} {}", cmd, args.join(" ")), e)
        })?;

    if !output.status.success() {
        let stderr = String::from_utf8_lossy(&output.stderr);
        metadata.success = false;
        metadata.elapsed_ms = Some(start.elapsed().as_millis() as u64);
        metadata.error_message = Some(format!(
            "Compilation failed with exit code {:?}: {}",
            output.status.code(),
            stderr.trim()
        ));

        tracing::error!(
            "Chunk {}/{} failed: {}",
            metadata.index + 1,
            metadata.total,
            metadata.error_message.as_ref().unwrap_or(&String::new())
        );

        return Ok((Vec::new(), metadata));
    }

    // Read output
    let rules = if tokio::fs::try_exists(&temp_output_path)
        .await
        .unwrap_or(false)
    {
        let content = tokio::fs::read_to_string(&temp_output_path)
            .await
            .map_err(|e| {
                CompilerError::file_system(
                    format!("reading chunk output from {}", temp_output_path.display()),
                    e,
                )
            })?;
        content.lines().map(String::from).collect()
    } else {
        Vec::new()
    };

    // Update metadata
    metadata.success = true;
    metadata.elapsed_ms = Some(start.elapsed().as_millis() as u64);
    metadata.actual_rules = Some(rules.len());
    metadata.output_path = Some(temp_output_path.clone());

    tracing::info!(
        "Chunk {}/{} complete: {} rules in {}ms",
        metadata.index + 1,
        metadata.total,
        rules.len(),
        metadata.elapsed_ms.unwrap_or(0)
    );

    Ok((rules, metadata))
}

fn get_compiler_command(config_path: &str, output_path: &str) -> Result<(String, Vec<String>)> {
    if let Ok(compiler_path) = which::which("hostlist-compiler") {
        return Ok((
            compiler_path.display().to_string(),
            vec![
                "--config".to_string(),
                config_path.to_string(),
                "--output".to_string(),
                output_path.to_string(),
            ],
        ));
    }

    if let Ok(npx_path) = which::which("npx") {
        return Ok((
            npx_path.display().to_string(),
            vec![
                "@adguard/hostlist-compiler".to_string(),
                "--config".to_string(),
                config_path.to_string(),
                "--output".to_string(),
                output_path.to_string(),
            ],
        ));
    }

    Err(CompilerError::CompilerNotFound)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_chunking_options_default() {
        let options = ChunkingOptions::default();
        assert!(!options.enabled);
        assert_eq!(options.chunk_size, 100_000);
        assert!(options.max_parallel >= 1);
        assert_eq!(options.strategy, ChunkingStrategy::Source);
    }

    #[test]
    fn test_chunking_options_for_large_lists() {
        let options = ChunkingOptions::for_large_lists();
        assert!(options.enabled);
        assert!(options.max_parallel >= 2);
    }

    #[test]
    fn test_should_enable_chunking_no_sources() {
        let config = CompilerConfig::new("Test");
        let options = ChunkingOptions::new().with_enabled(true);
        assert!(!should_enable_chunking(&config, Some(&options)));
    }

    #[test]
    fn test_should_enable_chunking_explicitly_disabled() {
        let config = CompilerConfig::new("Test")
            .with_source(FilterSource::new("Source", "http://example.com/list.txt"));
        let options = ChunkingOptions::new().with_enabled(false);
        assert!(!should_enable_chunking(&config, Some(&options)));
    }

    #[test]
    fn test_should_enable_chunking_explicitly_enabled() {
        let config = CompilerConfig::new("Test")
            .with_source(FilterSource::new("Source", "http://example.com/list.txt"));
        let options = ChunkingOptions::new().with_enabled(true);
        assert!(should_enable_chunking(&config, Some(&options)));
    }

    #[test]
    fn test_should_enable_chunking_multiple_sources() {
        let config = CompilerConfig::new("Test")
            .with_source(FilterSource::new("Source1", "http://example.com/list1.txt"))
            .with_source(FilterSource::new("Source2", "http://example.com/list2.txt"));
        assert!(should_enable_chunking(&config, None));
    }

    #[test]
    fn test_split_into_chunks_empty() {
        let config = CompilerConfig::new("Test");
        let options = ChunkingOptions::new().with_max_parallel(4);
        let chunks = split_into_chunks(&config, &options);
        assert!(chunks.is_empty());
    }

    #[test]
    fn test_split_into_chunks_four_sources_two_parallel() {
        let config = CompilerConfig::new("Test")
            .with_source(FilterSource::new("S1", "http://example.com/1.txt"))
            .with_source(FilterSource::new("S2", "http://example.com/2.txt"))
            .with_source(FilterSource::new("S3", "http://example.com/3.txt"))
            .with_source(FilterSource::new("S4", "http://example.com/4.txt"));
        let options = ChunkingOptions::new()
            .with_max_parallel(2)
            .with_strategy(ChunkingStrategy::Source);

        let chunks = split_into_chunks(&config, &options);

        assert_eq!(chunks.len(), 2);
        assert_eq!(chunks[0].0.sources.len(), 2);
        assert_eq!(chunks[1].0.sources.len(), 2);
    }

    #[test]
    fn test_split_into_chunks_preserves_properties() {
        let config = CompilerConfig::new("Test Filter")
            .with_description("Test description")
            .with_version("1.0.0")
            .with_source(FilterSource::new("Source", "http://example.com/list.txt"))
            .with_transformation("Deduplicate");
        let options = ChunkingOptions::new().with_max_parallel(4);

        let chunks = split_into_chunks(&config, &options);

        let chunk_config = &chunks[0].0;
        assert!(chunk_config.name.contains("Test Filter"));
        assert_eq!(chunk_config.description, config.description);
        assert_eq!(chunk_config.version, config.version);
        assert_eq!(chunk_config.transformations, config.transformations);
    }

    #[test]
    fn test_merge_chunks_removes_duplicates() {
        let chunk_results = vec![
            vec!["||example.com^".to_string(), "||test.com^".to_string()],
            vec!["||example.com^".to_string(), "||other.com^".to_string()],
        ];

        let (rules, duplicates_removed) = merge_chunks(&chunk_results);

        assert_eq!(rules.len(), 3);
        assert_eq!(duplicates_removed, 1);
    }

    #[test]
    fn test_merge_chunks_preserves_comments() {
        let chunk_results = vec![
            vec!["! Comment 1".to_string(), "||example.com^".to_string()],
            vec!["! Comment 1".to_string(), "||other.com^".to_string()],
        ];

        let (rules, duplicates_removed) = merge_chunks(&chunk_results);

        assert_eq!(rules.len(), 4); // Both comments preserved
        assert_eq!(duplicates_removed, 0);
    }

    #[test]
    fn test_merge_chunks_preserves_empty_lines() {
        let chunk_results = vec![
            vec![
                "||example.com^".to_string(),
                String::new(),
                "||test.com^".to_string(),
            ],
            vec!["||other.com^".to_string(), String::new(), String::new()],
        ];

        let (rules, duplicates_removed) = merge_chunks(&chunk_results);

        assert_eq!(rules.len(), 6);
        assert_eq!(duplicates_removed, 0);
    }

    #[test]
    fn test_estimate_speedup_disabled() {
        let options = ChunkingOptions::new().with_enabled(false);
        assert_eq!(estimate_speedup(100_000, &options), 1.0);
    }

    #[test]
    fn test_estimate_speedup_zero_rules() {
        let options = ChunkingOptions::new().with_enabled(true);
        assert_eq!(estimate_speedup(0, &options), 1.0);
    }

    #[test]
    fn test_estimate_speedup_many_rules() {
        let options = ChunkingOptions::new()
            .with_enabled(true)
            .with_chunk_size(100_000)
            .with_max_parallel(8);

        // 800,000 rules = 8 chunks = ~8x speedup
        let speedup = estimate_speedup(800_000, &options);
        assert_eq!(speedup, 8.0);
    }

    #[test]
    fn test_estimate_speedup_limited_by_max_parallel() {
        let options = ChunkingOptions::new()
            .with_enabled(true)
            .with_chunk_size(100_000)
            .with_max_parallel(4);

        // 1,000,000 rules = 10 chunks, but limited to 4 parallel
        let speedup = estimate_speedup(1_000_000, &options);
        assert_eq!(speedup, 4.0);
    }

    #[test]
    fn test_chunked_compilation_result_estimated_speedup() {
        let result = ChunkedCompilationResult {
            total_elapsed_ms: 1000,
            chunks: vec![
                ChunkMetadata {
                    elapsed_ms: Some(800),
                    ..Default::default()
                },
                ChunkMetadata {
                    elapsed_ms: Some(900),
                    ..Default::default()
                },
                ChunkMetadata {
                    elapsed_ms: Some(850),
                    ..Default::default()
                },
                ChunkMetadata {
                    elapsed_ms: Some(750),
                    ..Default::default()
                },
            ],
            ..Default::default()
        };

        // Sum: 3300ms, Parallel: 1000ms, Speedup: 3.3x
        let speedup = result.estimated_speedup();
        assert!((speedup - 3.3).abs() < 0.1);
    }

    #[test]
    fn test_chunked_compilation_result_no_chunks() {
        let result = ChunkedCompilationResult {
            total_elapsed_ms: 1000,
            chunks: Vec::new(),
            ..Default::default()
        };

        assert_eq!(result.estimated_speedup(), 1.0);
    }
}
