//! Core compiler functionality for AdGuard filter rules.
//!
//! This module provides the main compilation logic, wrapping the
//! adblock-compiler-core tool (published as `@bloqr/compiler-core` on
//! JSR, run via Deno) and providing statistics, hashing, and file management.

use chrono::{DateTime, Utc};
use sha2::{Digest, Sha384};
use std::fs::{self, File};
use std::io::{BufRead, BufReader, Read};
use std::path::{Path, PathBuf};
use std::process::Command;
use std::time::Instant;

use crate::config::{read_config, to_json, CompilerConfig, ConfigFormat};
use crate::error::{CompilerError, Result};
use crate::events::{
    EventDispatcher, EventTimestamp, HashComputedEventArgs, HashMismatchEventArgs,
    HashVerifiedEventArgs, ValidationEventArgs, ValidationSeverity,
};
use bloqr_validator::{ValidationConfig, Validator};

/// JSR package specifier for the compiler CLI, run via `deno run`.
const JSR_PACKAGE_SPECIFIER: &str = "jsr:@bloqr/compiler-core/cli";

/// Deno permission flags required to run the compiler CLI.
const DENO_PERMISSIONS: &[&str] = &[
    "run",
    "--allow-read",
    "--allow-write",
    "--allow-env",
    "--allow-net",
    "--allow-run",
];

/// Platform-specific information.
#[derive(Debug, Clone, Default)]
pub struct PlatformInfo {
    /// Operating system name.
    pub os_name: String,
    /// Operating system version.
    pub os_version: String,
    /// Processor architecture.
    pub architecture: String,
    /// Whether the platform is Windows.
    pub is_windows: bool,
    /// Whether the platform is Linux.
    pub is_linux: bool,
    /// Whether the platform is macOS.
    pub is_macos: bool,
}

impl PlatformInfo {
    /// Detect current platform information.
    #[must_use]
    pub fn detect() -> Self {
        Self {
            os_name: std::env::consts::OS.to_string(),
            os_version: String::new(),
            architecture: std::env::consts::ARCH.to_string(),
            is_windows: cfg!(target_os = "windows"),
            is_linux: cfg!(target_os = "linux"),
            is_macos: cfg!(target_os = "macos"),
        }
    }
}

/// Version information for all components.
#[derive(Debug, Clone, Default)]
pub struct VersionInfo {
    /// Module version.
    pub module_version: String,
    /// Rust version.
    pub rust_version: String,
    /// Node.js version (if available).
    pub node_version: Option<String>,
    /// `@bloqr/compiler-core` (JSR) version (if available).
    pub hostlist_compiler_version: Option<String>,
    /// Path to the compiler command (deno).
    pub hostlist_compiler_path: Option<String>,
    /// Platform information.
    pub platform: PlatformInfo,
}

impl VersionInfo {
    /// Collect version information for all components.
    #[must_use]
    pub fn collect() -> Self {
        let mut info = Self {
            module_version: crate::VERSION.to_string(),
            rust_version: format!("{}", rustc_version_runtime::version()),
            platform: PlatformInfo::detect(),
            ..Default::default()
        };

        // Check Deno (reported via node_version for backward compatibility
        // with existing consumers of VersionInfo)
        if let Some(deno_path) = find_command("deno") {
            let deno_str = deno_path.to_str().unwrap_or("deno");
            info.node_version = get_command_version(deno_str, &["--version"]);

            info.hostlist_compiler_path = Some(format!("{deno_str} run {JSR_PACKAGE_SPECIFIER}"));
            let mut version_args: Vec<&str> = DENO_PERMISSIONS.to_vec();
            version_args.push(JSR_PACKAGE_SPECIFIER);
            version_args.push("--version");
            info.hostlist_compiler_version = get_command_version(deno_str, &version_args);
        }

        info
    }

    /// Check if the compiler (`@bloqr/compiler-core`, via Deno) is available.
    #[must_use]
    pub fn has_compiler(&self) -> bool {
        self.hostlist_compiler_path.is_some()
    }

    /// Check if Node.js is available.
    #[must_use]
    pub fn has_node(&self) -> bool {
        self.node_version.is_some()
    }
}

/// Result of a compilation operation.
#[derive(Debug, Clone)]
pub struct CompilerResult {
    /// Whether compilation was successful.
    pub success: bool,
    /// Name from configuration.
    pub config_name: String,
    /// Version from configuration.
    pub config_version: String,
    /// Number of rules in output.
    pub rule_count: usize,
    /// Path to output file.
    pub output_path: PathBuf,
    /// SHA-384 hash of output file.
    pub output_hash: String,
    /// Whether output was copied to rules directory.
    pub copied_to_rules: bool,
    /// Destination path if copied.
    pub rules_destination: Option<PathBuf>,
    /// Elapsed time in milliseconds.
    pub elapsed_ms: u64,
    /// Start time.
    pub start_time: DateTime<Utc>,
    /// End time.
    pub end_time: DateTime<Utc>,
    /// Error message if failed.
    pub error_message: Option<String>,
    /// Standard output from compiler.
    pub stdout: String,
    /// Standard error from compiler.
    pub stderr: String,
}

impl Default for CompilerResult {
    fn default() -> Self {
        let now = Utc::now();
        Self {
            success: false,
            config_name: String::new(),
            config_version: String::new(),
            rule_count: 0,
            output_path: PathBuf::new(),
            output_hash: String::new(),
            copied_to_rules: false,
            rules_destination: None,
            elapsed_ms: 0,
            start_time: now,
            end_time: now,
            error_message: None,
            stdout: String::new(),
            stderr: String::new(),
        }
    }
}

impl CompilerResult {
    /// Get the output path as a string.
    #[must_use]
    pub fn output_path_str(&self) -> String {
        self.output_path.display().to_string()
    }

    /// Get the rules destination path as a string.
    #[must_use]
    pub fn rules_destination_str(&self) -> Option<String> {
        self.rules_destination
            .as_ref()
            .map(|p| p.display().to_string())
    }

    /// Get elapsed time as a formatted string.
    #[must_use]
    pub fn elapsed_formatted(&self) -> String {
        if self.elapsed_ms >= 1000 {
            format!("{:.2}s", self.elapsed_ms as f64 / 1000.0)
        } else {
            format!("{}ms", self.elapsed_ms)
        }
    }

    /// Get truncated hash for display.
    #[must_use]
    pub fn hash_short(&self) -> &str {
        if self.output_hash.len() >= 32 {
            &self.output_hash[..32]
        } else {
            &self.output_hash
        }
    }
}

/// Options for running the compiler.
#[derive(Debug, Clone, Default)]
pub struct CompileOptions {
    /// Path to output file (auto-generated if None).
    pub output_path: Option<PathBuf>,
    /// Copy output to rules directory.
    pub copy_to_rules: bool,
    /// Custom rules directory.
    pub rules_directory: Option<PathBuf>,
    /// Force configuration format.
    pub format: Option<ConfigFormat>,
    /// Enable debug output.
    pub debug: bool,
    /// Validate configuration before compiling.
    pub validate: bool,
    /// Fail compilation on validation warnings (in addition to errors/critical findings,
    /// which always fail compilation unless `allow_unvalidated_output` is set).
    pub fail_on_warnings: bool,
    /// Explicit opt-out of the mandatory rules-validator syntax check on compiled output.
    /// Security-relevant: leave this `false` (the default) in production. When `false`
    /// (the default), compiled output is validated via `bloqr_validator::Validator` and
    /// compilation fails closed - both a rules-validator run failure (e.g. the file can't
    /// be read) and an invalid/error/critical finding cause `CompilerResult::success` to be
    /// `false` - there is no silent skip. Set this to `true` only for deliberate debugging
    /// of unvalidated output; doing so is logged as a warning.
    pub allow_unvalidated_output: bool,
}

impl CompileOptions {
    /// Create new compile options with default values.
    #[must_use]
    pub fn new() -> Self {
        Self::default()
    }

    /// Set the output path.
    #[must_use]
    pub fn with_output<P: Into<PathBuf>>(mut self, path: P) -> Self {
        self.output_path = Some(path.into());
        self
    }

    /// Enable copying to rules directory.
    #[must_use]
    pub const fn with_copy_to_rules(mut self, copy: bool) -> Self {
        self.copy_to_rules = copy;
        self
    }

    /// Set the rules directory.
    #[must_use]
    pub fn with_rules_directory<P: Into<PathBuf>>(mut self, path: P) -> Self {
        self.rules_directory = Some(path.into());
        self
    }

    /// Set the configuration format.
    #[must_use]
    pub const fn with_format(mut self, format: ConfigFormat) -> Self {
        self.format = Some(format);
        self
    }

    /// Enable debug output.
    #[must_use]
    pub const fn with_debug(mut self, debug: bool) -> Self {
        self.debug = debug;
        self
    }

    /// Enable validation.
    #[must_use]
    pub const fn with_validation(mut self, validate: bool) -> Self {
        self.validate = validate;
        self
    }

    /// Set fail on warnings.
    #[must_use]
    pub const fn with_fail_on_warnings(mut self, fail_on_warnings: bool) -> Self {
        self.fail_on_warnings = fail_on_warnings;
        self
    }

    /// Explicitly opt out of the mandatory rules-validator syntax check. Security-relevant -
    /// see the field doc comment. Defaults to `false` (validation enforced, fail-closed).
    #[must_use]
    pub const fn with_allow_unvalidated_output(mut self, allow_unvalidated_output: bool) -> Self {
        self.allow_unvalidated_output = allow_unvalidated_output;
        self
    }
}

/// Main compiler for AdGuard filter rules.
#[derive(Debug, Default)]
pub struct BloqrCompiler {
    options: CompileOptions,
}

impl BloqrCompiler {
    /// Create a new compiler instance with default options.
    #[must_use]
    pub fn new() -> Self {
        Self::default()
    }

    /// Create a new compiler instance with custom options.
    #[must_use]
    pub const fn with_options(options: CompileOptions) -> Self {
        Self { options }
    }

    /// Get mutable reference to options.
    pub fn options_mut(&mut self) -> &mut CompileOptions {
        &mut self.options
    }

    /// Compile filter rules from a configuration file.
    ///
    /// # Errors
    ///
    /// Returns an error if compilation fails.
    pub fn compile<P: AsRef<Path>>(&self, config_path: P) -> Result<CompilerResult> {
        compile_rules(config_path, &self.options)
    }

    /// Read configuration from a file.
    ///
    /// # Errors
    ///
    /// Returns an error if the file can't be read or parsed.
    pub fn read_config<P: AsRef<Path>>(&self, config_path: P) -> Result<CompilerConfig> {
        read_config(config_path, self.options.format)
    }

    /// Get version information.
    #[must_use]
    pub fn version_info(&self) -> VersionInfo {
        VersionInfo::collect()
    }
}

/// Find command in PATH.
fn find_command(name: &str) -> Option<PathBuf> {
    which::which(name).ok()
}

/// Get version from a command.
fn get_command_version(cmd: &str, args: &[&str]) -> Option<String> {
    Command::new(cmd)
        .args(args)
        .output()
        .ok()
        .and_then(|output| {
            if output.status.success() {
                String::from_utf8(output.stdout)
                    .ok()
                    .map(|s| s.lines().next().unwrap_or("").trim().to_string())
            } else {
                None
            }
        })
}

/// Count non-empty, non-comment lines in a file.
///
/// Lines starting with `!` or `#` are considered comments.
#[must_use]
pub fn count_rules<P: AsRef<Path>>(path: P) -> usize {
    let file = match File::open(path.as_ref()) {
        Ok(f) => f,
        Err(_) => return 0,
    };

    BufReader::new(file)
        .lines()
        .map_while(std::result::Result::ok)
        .filter(|line| {
            let trimmed = line.trim();
            !trimmed.is_empty() && !trimmed.starts_with('!') && !trimmed.starts_with('#')
        })
        .count()
}

/// Asynchronously count non-empty, non-comment lines in a file.
///
/// This async version provides better performance for I/O-bound operations.
/// Lines starting with `!` or `#` are considered comments.
///
/// # Errors
///
/// Returns an error if the file can't be read.
pub async fn count_rules_async<P: AsRef<Path>>(path: P) -> Result<usize> {
    use tokio::io::{AsyncBufReadExt, BufReader};

    let file = tokio::fs::File::open(path.as_ref()).await?;
    let reader = BufReader::new(file);
    let mut lines = reader.lines();
    let mut count = 0;

    while let Some(line) = lines.next_line().await? {
        let trimmed = line.trim();
        if !trimmed.is_empty() && !trimmed.starts_with('!') && !trimmed.starts_with('#') {
            count += 1;
        }
    }

    Ok(count)
}

/// Compute SHA-384 hash of a file.
///
/// # Errors
///
/// Returns an error if the file can't be read.
pub fn compute_hash<P: AsRef<Path>>(path: P) -> Result<String> {
    let mut file = File::open(path.as_ref())?;
    let mut hasher = Sha384::new();
    let mut buffer = [0u8; 8192];

    loop {
        let bytes_read = file.read(&mut buffer)?;
        if bytes_read == 0 {
            break;
        }
        hasher.update(&buffer[..bytes_read]);
    }

    Ok(hex::encode(hasher.finalize()))
}

/// Asynchronously compute SHA-384 hash of a file.
///
/// This async version provides better performance for I/O-bound operations.
///
/// # Errors
///
/// Returns an error if the file can't be read.
pub async fn compute_hash_async<P: AsRef<Path>>(path: P) -> Result<String> {
    use tokio::io::AsyncReadExt;

    let mut file = tokio::fs::File::open(path.as_ref()).await?;
    let mut hasher = Sha384::new();
    let mut buffer = [0u8; 8192];

    loop {
        let bytes_read = file.read(&mut buffer).await?;
        if bytes_read == 0 {
            break;
        }
        hasher.update(&buffer[..bytes_read]);
    }

    Ok(hex::encode(hasher.finalize()))
}

/// Compute hash and fire events if dispatcher is provided.
///
/// # Errors
///
/// Returns an error if the file can't be read.
pub fn compute_hash_with_events<P: AsRef<Path>>(
    path: P,
    item_type: &str,
    dispatcher: Option<&EventDispatcher>,
) -> Result<String> {
    let path = path.as_ref();

    // Compute the hash
    let hash = compute_hash(path)?;
    let metadata = fs::metadata(path)?;
    let size_bytes = metadata.len();

    // Fire hash computed event
    if let Some(dispatcher) = dispatcher {
        let args = HashComputedEventArgs {
            base: EventTimestamp::default(),
            item_identifier: path.display().to_string(),
            item_type: item_type.to_string(),
            hash: hash.clone(),
            size_bytes,
            is_verification: false,
        };
        dispatcher.raise_hash_computed(&args);
    }

    Ok(hash)
}

/// Verify hash against expected value and fire events if dispatcher is provided.
///
/// # Errors
///
/// Returns an error if hashes don't match (unless allow_continuation is set by handler).
pub fn verify_hash_with_events<P: AsRef<Path>>(
    path: P,
    expected_hash: &str,
    item_type: &str,
    dispatcher: Option<&EventDispatcher>,
) -> Result<()> {
    let path = path.as_ref();
    let start = Instant::now();

    // Compute the hash
    let actual_hash = compute_hash(path)?;
    let metadata = fs::metadata(path)?;
    let size_bytes = metadata.len();
    let computation_duration_ms = start.elapsed().as_secs_f64() * 1000.0;

    if actual_hash == expected_hash {
        // Hash matches - fire verified event
        if let Some(dispatcher) = dispatcher {
            let args = HashVerifiedEventArgs {
                base: EventTimestamp::default(),
                item_identifier: path.display().to_string(),
                item_type: item_type.to_string(),
                expected_hash: expected_hash.to_string(),
                actual_hash,
                size_bytes,
                computation_duration_ms,
            };
            dispatcher.raise_hash_verified(&args);
        }
        Ok(())
    } else {
        // Hash mismatch - fire mismatch event and check if continuation is allowed
        if let Some(dispatcher) = dispatcher {
            let mut args = HashMismatchEventArgs {
                base: EventTimestamp::default(),
                item_identifier: path.display().to_string(),
                item_type: item_type.to_string(),
                expected_hash: expected_hash.to_string(),
                actual_hash: actual_hash.clone(),
                size_bytes,
                abort: true,
                abort_reason: Some(format!(
                    "Hash mismatch for {}: expected {}, got {}",
                    path.display(),
                    &expected_hash[..16.min(expected_hash.len())],
                    &actual_hash[..16]
                )),
                allow_continuation: false,
            };
            dispatcher.raise_hash_mismatch(&mut args);

            if args.allow_continuation {
                return Ok(());
            }
        }

        Err(CompilerError::HashMismatch {
            path: path.display().to_string(),
            expected: expected_hash.to_string(),
            actual: actual_hash,
        })
    }
}

/// Get compiler command and arguments.
fn get_compiler_command(config_path: &str, output_path: &str) -> Result<(String, Vec<String>)> {
    if let Some(deno_path) = find_command("deno") {
        let mut args: Vec<String> = DENO_PERMISSIONS.iter().map(|s| s.to_string()).collect();
        args.push(JSR_PACKAGE_SPECIFIER.to_string());
        args.push("--config".to_string());
        args.push(config_path.to_string());
        args.push("--output".to_string());
        args.push(output_path.to_string());

        return Ok((deno_path.display().to_string(), args));
    }

    Err(CompilerError::CompilerNotFound)
}

/// Generate default output path based on config path and timestamp.
fn generate_output_path(config_path: &Path) -> PathBuf {
    let timestamp = Utc::now().format("%Y%m%d-%H%M%S");
    let output_dir = config_path
        .parent()
        .unwrap_or(Path::new("."))
        .join("output");
    output_dir.join(format!("compiled-{timestamp}.txt"))
}

/// Determine rules directory from config path.
fn get_rules_directory(config_path: &Path, custom: Option<&Path>) -> PathBuf {
    custom.map(Path::to_path_buf).unwrap_or_else(|| {
        config_path
            .parent()
            .unwrap_or(Path::new("."))
            .parent()
            .unwrap_or(Path::new("."))
            .parent()
            .unwrap_or(Path::new("."))
            .join("rules")
    })
}

/// Run the native rules-validator syntax check against the compiled output and fire a
/// `Validation` event with its findings.
///
/// **Fail-closed by default** (`allow_unvalidated`/`fail_on_warnings` come from
/// `CompileOptions`): any Error/Critical finding aborts compilation, and so does a
/// rules-validator run failure (e.g. the file can't be read) - a validator we couldn't
/// run tells us nothing about the output's safety, so it can't be treated as "no
/// findings". `fail_on_warnings` additionally escalates Warning findings to abort. A
/// registered handler may still explicitly set `abort`/`abort_reason` on the event args
/// for custom logic, but no handler is required for the default checks to hold - this
/// closes the "no handler was registered, so nothing ever aborted" gap.
///
/// Setting `allow_unvalidated` to `true` reverts to the legacy, opt-in-only behavior
/// (silently continue on a run failure; only an explicit handler-set `abort` counts) -
/// use only for deliberate debugging of unvalidated output.
///
/// Returns `Some(reason)` if compilation should abort, `None` to continue.
fn validate_output_with_events<P: AsRef<Path>>(
    path: P,
    dispatcher: &EventDispatcher,
    config: &ValidationConfig,
    allow_unvalidated: bool,
    fail_on_warnings: bool,
) -> Option<String> {
    let path = path.as_ref();
    let mut validator = Validator::new(config.clone());

    let syntax_result = match validator.validate_local_file(path) {
        Ok(result) => result,
        Err(e) => {
            return if allow_unvalidated {
                None
            } else {
                Some(format!(
                    "rules-validator could not run against {}: {e} (pass \
                     --allow-unvalidated-output to bypass this check; not recommended)",
                    path.display()
                ))
            };
        }
    };

    let mut validation_args = ValidationEventArgs {
        stage_name: "rules-validator".to_string(),
        items_validated: syntax_result.valid_rules + syntax_result.invalid_rules,
        ..Default::default()
    };

    if syntax_result.messages.is_empty() {
        if !syntax_result.is_valid {
            validation_args.add_error(
                "RV001",
                format!(
                    "Output file failed rules-validator syntax validation ({} invalid rule(s) of {}).",
                    syntax_result.invalid_rules,
                    syntax_result.valid_rules + syntax_result.invalid_rules
                ),
            );
        }
    } else {
        for message in &syntax_result.messages {
            if syntax_result.is_valid {
                validation_args.add_warning("RV001", message.clone());
            } else {
                validation_args.add_error("RV001", message.clone());
            }
        }
    }

    dispatcher.raise_validation(&mut validation_args);

    let has_warnings = validation_args
        .findings
        .iter()
        .any(|f| matches!(f.severity, ValidationSeverity::Warning));
    let should_abort = validation_args.abort
        || (!allow_unvalidated
            && (!validation_args.passed() || (fail_on_warnings && has_warnings)));

    if should_abort {
        Some(
            validation_args.abort_reason.unwrap_or_else(|| {
                format!("rules-validator validation failed for {}", path.display())
            }),
        )
    } else {
        None
    }
}

/// Compile filter rules using adblock-compiler-core (via Deno).
///
/// # Arguments
///
/// * `config_path` - Path to the configuration file.
/// * `options` - Compilation options.
///
/// # Errors
///
/// Returns an error if compilation fails.
pub fn compile_rules<P: AsRef<Path>>(
    config_path: P,
    options: &CompileOptions,
) -> Result<CompilerResult> {
    let start = Instant::now();
    let mut result = CompilerResult {
        start_time: Utc::now(),
        ..Default::default()
    };

    let config_path = config_path.as_ref().canonicalize().map_err(|e| {
        CompilerError::file_system(
            format!("resolving config path {}", config_path.as_ref().display()),
            e,
        )
    })?;

    // Read configuration
    let config = read_config(&config_path, options.format)?;
    result.config_name = config.name.clone();
    result.config_version = config.version.clone();

    // Validate if requested
    if options.validate {
        config.validate()?;
    }

    // Determine output path
    let output_path = options
        .output_path
        .clone()
        .unwrap_or_else(|| generate_output_path(&config_path));
    result.output_path = output_path.clone();

    // Convert to JSON if needed (adblock-compiler-core only accepts JSON)
    let (compile_config_path, temp_config_path) = if config.format() != Some(ConfigFormat::Json) {
        let temp_path =
            std::env::temp_dir().join(format!("compiler-config-{}.json", uuid::Uuid::new_v4()));
        let json = to_json(&config)?;
        fs::write(&temp_path, &json).map_err(|e| {
            CompilerError::file_system(format!("writing temp config to {}", temp_path.display()), e)
        })?;

        if options.debug {
            eprintln!("[DEBUG] Created temp JSON config: {}", temp_path.display());
            eprintln!("[DEBUG] Config content:\n{json}");
        }

        (temp_path.clone(), Some(temp_path))
    } else {
        (config_path.clone(), None)
    };

    // Ensure output directory exists
    if let Some(output_dir) = output_path.parent() {
        fs::create_dir_all(output_dir).map_err(|e| {
            CompilerError::file_system(
                format!("creating output directory {}", output_dir.display()),
                e,
            )
        })?;
    }

    // Get compiler command
    let (cmd, args) = get_compiler_command(
        compile_config_path.to_str().unwrap_or(""),
        output_path.to_str().unwrap_or(""),
    )?;

    if options.debug {
        eprintln!("[DEBUG] Running: {cmd} {}", args.join(" "));
    }

    // Run compilation
    let output = Command::new(&cmd)
        .args(&args)
        .current_dir(config_path.parent().unwrap_or(Path::new(".")))
        .output()
        .map_err(|e| CompilerError::process_execution(format!("{cmd} {}", args.join(" ")), e))?;

    result.stdout = String::from_utf8_lossy(&output.stdout).to_string();
    result.stderr = String::from_utf8_lossy(&output.stderr).to_string();

    // Clean up temp file
    if let Some(temp_path) = temp_config_path {
        let _ = fs::remove_file(temp_path);
    }

    // Check for compilation failure
    if !output.status.success() {
        result.error_message = Some(format!(
            "compiler exited with code {:?}: {}",
            output.status.code(),
            result.stderr.trim()
        ));
        result.end_time = Utc::now();
        result.elapsed_ms = start.elapsed().as_millis() as u64;
        return Ok(result);
    }

    // Verify output was created
    if !output_path.exists() {
        result.error_message = Some("output file was not created".to_string());
        result.end_time = Utc::now();
        result.elapsed_ms = start.elapsed().as_millis() as u64;
        return Ok(result);
    }

    // Calculate statistics
    result.rule_count = count_rules(&output_path);
    result.output_hash = compute_hash(&output_path)?;
    result.success = true;

    // Mandatory rules-validator syntax check on the compiled output - fail-closed by
    // default (see validate_output_with_events doc comment). No EventDispatcher is
    // threaded through this plain API, so use an empty one: the check runs and enforces
    // regardless of whether any handlers are registered.
    if let Some(abort_reason) = validate_output_with_events(
        &output_path,
        &EventDispatcher::new(),
        &ValidationConfig::default(),
        options.allow_unvalidated_output,
        options.fail_on_warnings,
    ) {
        result.error_message = Some(abort_reason);
        result.success = false;
        result.end_time = Utc::now();
        result.elapsed_ms = start.elapsed().as_millis() as u64;
        return Ok(result);
    }

    // Copy to rules directory if requested
    if options.copy_to_rules {
        let rules_dir = get_rules_directory(&config_path, options.rules_directory.as_deref());
        fs::create_dir_all(&rules_dir).map_err(|e| {
            CompilerError::file_system(
                format!("creating rules directory {}", rules_dir.display()),
                e,
            )
        })?;

        let dest_path = rules_dir.join("adguard_user_filter.txt");
        fs::copy(&output_path, &dest_path).map_err(|e| {
            CompilerError::copy_failed(
                format!(
                    "copying {} to {}",
                    output_path.display(),
                    dest_path.display()
                ),
                e,
            )
        })?;

        result.copied_to_rules = true;
        result.rules_destination = Some(dest_path);
    }

    result.end_time = Utc::now();
    result.elapsed_ms = start.elapsed().as_millis() as u64;

    Ok(result)
}

/// Compile filter rules with event dispatcher for hash verification callbacks.
///
/// This extended version fires hash verification events at each compilation stage:
/// - Configuration file loading (computes hash)
/// - Output file writing (computes and optionally verifies hash)
/// - Rules file copying (computes hash)
///
/// # Arguments
///
/// * `config_path` - Path to the configuration file.
/// * `options` - Compilation options.
/// * `dispatcher` - Event dispatcher for firing hash verification events.
///
/// # Errors
///
/// Returns an error if compilation fails.
pub fn compile_rules_with_events<P: AsRef<Path>>(
    config_path: P,
    options: &CompileOptions,
    dispatcher: &EventDispatcher,
) -> Result<CompilerResult> {
    let start = Instant::now();
    let mut result = CompilerResult {
        start_time: Utc::now(),
        ..Default::default()
    };

    let config_path = config_path.as_ref().canonicalize().map_err(|e| {
        CompilerError::file_system(
            format!("resolving config path {}", config_path.as_ref().display()),
            e,
        )
    })?;

    // Compute hash of input config file (at-rest verification)
    let _config_hash = compute_hash_with_events(&config_path, "config_file", Some(dispatcher))?;

    // Read configuration
    let config = read_config(&config_path, options.format)?;
    result.config_name = config.name.clone();
    result.config_version = config.version.clone();

    // Validate if requested
    if options.validate {
        config.validate()?;
    }

    // Determine output path
    let output_path = options
        .output_path
        .clone()
        .unwrap_or_else(|| generate_output_path(&config_path));
    result.output_path = output_path.clone();

    // Convert to JSON if needed (adblock-compiler-core only accepts JSON)
    let (compile_config_path, temp_config_path) = if config.format() != Some(ConfigFormat::Json) {
        let temp_path =
            std::env::temp_dir().join(format!("compiler-config-{}.json", uuid::Uuid::new_v4()));
        let json = to_json(&config)?;
        fs::write(&temp_path, &json).map_err(|e| {
            CompilerError::file_system(format!("writing temp config to {}", temp_path.display()), e)
        })?;

        if options.debug {
            eprintln!("[DEBUG] Created temp JSON config: {}", temp_path.display());
            eprintln!("[DEBUG] Config content:\n{json}");
        }

        (temp_path.clone(), Some(temp_path))
    } else {
        (config_path.clone(), None)
    };

    // Ensure output directory exists
    if let Some(output_dir) = output_path.parent() {
        fs::create_dir_all(output_dir).map_err(|e| {
            CompilerError::file_system(
                format!("creating output directory {}", output_dir.display()),
                e,
            )
        })?;
    }

    // Get compiler command
    let (cmd, args) = get_compiler_command(
        compile_config_path.to_str().unwrap_or(""),
        output_path.to_str().unwrap_or(""),
    )?;

    if options.debug {
        eprintln!("[DEBUG] Running: {cmd} {}", args.join(" "));
    }

    // Run compilation
    let output = Command::new(&cmd)
        .args(&args)
        .current_dir(config_path.parent().unwrap_or(Path::new(".")))
        .output()
        .map_err(|e| CompilerError::process_execution(format!("{cmd} {}", args.join(" ")), e))?;

    result.stdout = String::from_utf8_lossy(&output.stdout).to_string();
    result.stderr = String::from_utf8_lossy(&output.stderr).to_string();

    // Clean up temp file
    if let Some(temp_path) = temp_config_path {
        let _ = fs::remove_file(temp_path);
    }

    // Check for compilation failure
    if !output.status.success() {
        result.error_message = Some(format!(
            "compiler exited with code {:?}: {}",
            output.status.code(),
            result.stderr.trim()
        ));
        result.end_time = Utc::now();
        result.elapsed_ms = start.elapsed().as_millis() as u64;
        return Ok(result);
    }

    // Verify output was created
    if !output_path.exists() {
        result.error_message = Some("output file was not created".to_string());
        result.end_time = Utc::now();
        result.elapsed_ms = start.elapsed().as_millis() as u64;
        return Ok(result);
    }

    // Calculate statistics and compute output hash with events
    result.rule_count = count_rules(&output_path);
    result.output_hash = compute_hash_with_events(&output_path, "output_file", Some(dispatcher))?;
    result.success = true;

    // Mandatory rules-validator syntax check - fail-closed by default (see
    // validate_output_with_events doc comment); handlers may still customize via the
    // dispatched event, but nothing has to be registered for the default checks to hold.
    if let Some(abort_reason) = validate_output_with_events(
        &output_path,
        dispatcher,
        &ValidationConfig::default(),
        options.allow_unvalidated_output,
        options.fail_on_warnings,
    ) {
        result.error_message = Some(abort_reason);
        result.success = false;
        result.end_time = Utc::now();
        result.elapsed_ms = start.elapsed().as_millis() as u64;
        return Ok(result);
    }

    // Copy to rules directory if requested
    if options.copy_to_rules {
        let rules_dir = get_rules_directory(&config_path, options.rules_directory.as_deref());
        fs::create_dir_all(&rules_dir).map_err(|e| {
            CompilerError::file_system(
                format!("creating rules directory {}", rules_dir.display()),
                e,
            )
        })?;

        let dest_path = rules_dir.join("adguard_user_filter.txt");
        fs::copy(&output_path, &dest_path).map_err(|e| {
            CompilerError::copy_failed(
                format!(
                    "copying {} to {}",
                    output_path.display(),
                    dest_path.display()
                ),
                e,
            )
        })?;

        // Compute hash of copied file to verify integrity
        let _dest_hash =
            compute_hash_with_events(&dest_path, "copied_rules_file", Some(dispatcher))?;

        result.copied_to_rules = true;
        result.rules_destination = Some(dest_path);
    }

    result.end_time = Utc::now();
    result.elapsed_ms = start.elapsed().as_millis() as u64;

    Ok(result)
}

/// Asynchronously compile filter rules using adblock-compiler-core (via Deno).
///
/// This async version provides better performance for I/O-bound operations
/// and allows compilation to be integrated into async applications.
///
/// # Arguments
///
/// * `config_path` - Path to the configuration file.
/// * `options` - Compilation options.
///
/// # Errors
///
/// Returns an error if compilation fails.
pub async fn compile_rules_async<P: AsRef<Path>>(
    config_path: P,
    options: &CompileOptions,
) -> Result<CompilerResult> {
    let start = Instant::now();
    let mut result = CompilerResult {
        start_time: Utc::now(),
        ..Default::default()
    };

    let config_path = tokio::fs::canonicalize(config_path.as_ref())
        .await
        .map_err(|e| {
            CompilerError::file_system(
                format!("resolving config path {}", config_path.as_ref().display()),
                e,
            )
        })?;

    // Read configuration
    let config = read_config(&config_path, options.format)?;
    result.config_name = config.name.clone();
    result.config_version = config.version.clone();

    // Validate if requested
    if options.validate {
        config.validate()?;
    }

    // Determine output path
    let output_path = options
        .output_path
        .clone()
        .unwrap_or_else(|| generate_output_path(&config_path));
    result.output_path = output_path.clone();

    // Convert to JSON if needed (adblock-compiler-core only accepts JSON)
    let (compile_config_path, temp_config_path) = if config.format() != Some(ConfigFormat::Json) {
        let temp_path =
            std::env::temp_dir().join(format!("compiler-config-{}.json", uuid::Uuid::new_v4()));
        let json = to_json(&config)?;
        tokio::fs::write(&temp_path, &json).await.map_err(|e| {
            CompilerError::file_system(format!("writing temp config to {}", temp_path.display()), e)
        })?;

        if options.debug {
            eprintln!("[DEBUG] Created temp JSON config: {}", temp_path.display());
            eprintln!("[DEBUG] Config content:\n{json}");
        }

        (temp_path.clone(), Some(temp_path))
    } else {
        (config_path.clone(), None)
    };

    // Ensure output directory exists
    if let Some(output_dir) = output_path.parent() {
        tokio::fs::create_dir_all(output_dir).await.map_err(|e| {
            CompilerError::file_system(
                format!("creating output directory {}", output_dir.display()),
                e,
            )
        })?;
    }

    // Get compiler command
    let (cmd, args) = get_compiler_command(
        compile_config_path.to_str().unwrap_or(""),
        output_path.to_str().unwrap_or(""),
    )?;

    if options.debug {
        eprintln!("[DEBUG] Running: {cmd} {}", args.join(" "));
    }

    // Run compilation asynchronously
    let output = tokio::process::Command::new(&cmd)
        .args(&args)
        .current_dir(config_path.parent().unwrap_or(Path::new(".")))
        .output()
        .await
        .map_err(|e| CompilerError::process_execution(format!("{cmd} {}", args.join(" ")), e))?;

    result.stdout = String::from_utf8_lossy(&output.stdout).to_string();
    result.stderr = String::from_utf8_lossy(&output.stderr).to_string();

    // Clean up temp file
    if let Some(temp_path) = temp_config_path {
        let _ = tokio::fs::remove_file(temp_path).await;
    }

    // Check for compilation failure
    if !output.status.success() {
        result.error_message = Some(format!(
            "compiler exited with code {:?}: {}",
            output.status.code(),
            result.stderr.trim()
        ));
        result.end_time = Utc::now();
        result.elapsed_ms = start.elapsed().as_millis() as u64;
        return Ok(result);
    }

    // Verify output was created
    if !tokio::fs::try_exists(&output_path).await.unwrap_or(false) {
        result.error_message = Some("output file was not created".to_string());
        result.end_time = Utc::now();
        result.elapsed_ms = start.elapsed().as_millis() as u64;
        return Ok(result);
    }

    // Calculate statistics asynchronously
    result.rule_count = count_rules_async(&output_path).await?;
    result.output_hash = compute_hash_async(&output_path).await?;
    result.success = true;

    // Copy to rules directory if requested
    if options.copy_to_rules {
        let rules_dir = get_rules_directory(&config_path, options.rules_directory.as_deref());
        tokio::fs::create_dir_all(&rules_dir).await.map_err(|e| {
            CompilerError::file_system(
                format!("creating rules directory {}", rules_dir.display()),
                e,
            )
        })?;

        let dest_path = rules_dir.join("adguard_user_filter.txt");
        tokio::fs::copy(&output_path, &dest_path)
            .await
            .map_err(|e| {
                CompilerError::copy_failed(
                    format!(
                        "copying {} to {}",
                        output_path.display(),
                        dest_path.display()
                    ),
                    e,
                )
            })?;

        result.copied_to_rules = true;
        result.rules_destination = Some(dest_path);
    }

    result.end_time = Utc::now();
    result.elapsed_ms = start.elapsed().as_millis() as u64;

    Ok(result)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;
    use tempfile::TempDir;

    #[test]
    fn test_platform_info_detect() {
        let info = PlatformInfo::detect();
        assert!(!info.os_name.is_empty());
        assert!(!info.architecture.is_empty());
    }

    #[test]
    fn test_version_info_collect() {
        let info = VersionInfo::collect();
        assert!(!info.module_version.is_empty());
        assert!(!info.rust_version.is_empty());
    }

    #[test]
    fn test_count_rules() {
        let dir = TempDir::new().unwrap();
        let path = dir.path().join("rules.txt");
        let mut file = File::create(&path).unwrap();
        writeln!(file, "! Comment").unwrap();
        writeln!(file, "# Another comment").unwrap();
        writeln!(file, "||example.com^").unwrap();
        writeln!(file, "||test.org^").unwrap();
        writeln!(file).unwrap();
        writeln!(file, "@@||allowed.com^").unwrap();

        assert_eq!(count_rules(&path), 3);
    }

    #[test]
    fn test_count_rules_empty_file() {
        let dir = TempDir::new().unwrap();
        let path = dir.path().join("empty.txt");
        File::create(&path).unwrap();

        assert_eq!(count_rules(&path), 0);
    }

    #[test]
    fn test_count_rules_nonexistent() {
        assert_eq!(count_rules("/nonexistent/path.txt"), 0);
    }

    #[test]
    fn test_compute_hash() {
        let dir = TempDir::new().unwrap();
        let path = dir.path().join("test.txt");
        let mut file = File::create(&path).unwrap();
        writeln!(file, "Test content").unwrap();

        let hash = compute_hash(&path).unwrap();
        assert_eq!(hash.len(), 96); // SHA-384 = 96 hex chars
    }

    #[test]
    fn test_compile_options_builder() {
        let options = CompileOptions::new()
            .with_output("/output/path.txt")
            .with_copy_to_rules(true)
            .with_debug(true)
            .with_validation(true)
            .with_allow_unvalidated_output(true);

        assert_eq!(options.output_path, Some(PathBuf::from("/output/path.txt")));
        assert!(options.copy_to_rules);
        assert!(options.debug);
        assert!(options.validate);
        assert!(options.allow_unvalidated_output);
    }

    #[test]
    fn test_compile_options_default_is_fail_closed() {
        // Security-relevant default: unvalidated output must never be silently allowed.
        assert!(!CompileOptions::default().allow_unvalidated_output);
    }

    #[test]
    fn test_compiler_result_helpers() {
        let mut result = CompilerResult {
            output_path: PathBuf::from("/path/to/output.txt"),
            output_hash: "a".repeat(96),
            elapsed_ms: 1500,
            ..Default::default()
        };

        assert_eq!(result.output_path_str(), "/path/to/output.txt");
        assert_eq!(result.hash_short().len(), 32);
        assert_eq!(result.elapsed_formatted(), "1.50s");

        result.elapsed_ms = 500;
        assert_eq!(result.elapsed_formatted(), "500ms");
    }

    #[test]
    fn test_generate_output_path() {
        let config_path = PathBuf::from("/project/config/compiler.json");
        let output_path = generate_output_path(&config_path);
        assert!(output_path.to_str().unwrap().contains("compiled-"));
        assert!(output_path.to_str().unwrap().ends_with(".txt"));
    }

    /// Test-only [`crate::events::CompilationEventHandler`] that unconditionally aborts
    /// on the `Validation` event, mirroring how a real handler would opt in to failing
    /// compilation over rules-validator findings.
    struct AbortingHandler;

    impl crate::events::CompilationEventHandler for AbortingHandler {
        fn on_validation(&self, args: &mut ValidationEventArgs) {
            args.abort = true;
            args.abort_reason = Some("aborted by test handler".to_string());
        }
    }

    fn scoped_validation_config(dir: &TempDir) -> ValidationConfig {
        let mut config = ValidationConfig::default();
        config.hash_verification.hash_database_path = dir
            .path()
            .join(".hashes.json")
            .to_string_lossy()
            .into_owned();
        config
    }

    #[test]
    fn test_validate_output_with_events_no_handler_no_findings_does_not_abort() {
        let dir = TempDir::new().unwrap();
        let path = dir.path().join("output.txt");
        fs::write(&path, "||example.com^\n").unwrap();

        let dispatcher = EventDispatcher::new();
        let config = scoped_validation_config(&dir);

        // No handlers registered, and the content is valid - nothing to abort over.
        assert_eq!(
            validate_output_with_events(&path, &dispatcher, &config, false, false),
            None
        );
    }

    #[test]
    fn test_validate_output_with_events_invalid_content_aborts_with_no_handler() {
        let dir = TempDir::new().unwrap();
        let path = dir.path().join("output.txt");
        // Public-suffix-only rule: rejected by default since it would block an entire
        // public suffix - see docs/adr/0003-adguard-hostlist-compatibility.md.
        fs::write(&path, "||co.uk^\n").unwrap();

        let dispatcher = EventDispatcher::new();
        let config = scoped_validation_config(&dir);

        // Fail-closed by default: an Error/Critical finding aborts even with zero
        // handlers registered - this is the gap the fail-closed rewrite closes.
        assert!(validate_output_with_events(&path, &dispatcher, &config, false, false).is_some());
    }

    #[test]
    fn test_validate_output_with_events_allow_unvalidated_skips_default_abort() {
        let dir = TempDir::new().unwrap();
        let path = dir.path().join("output.txt");
        fs::write(&path, "||co.uk^\n").unwrap();

        let dispatcher = EventDispatcher::new();
        let config = scoped_validation_config(&dir);

        // Explicit opt-out reverts to legacy behavior: only a handler-set abort counts.
        assert_eq!(
            validate_output_with_events(&path, &dispatcher, &config, true, false),
            None
        );
    }

    #[test]
    fn test_validate_output_with_events_handler_can_abort() {
        let dir = TempDir::new().unwrap();
        let path = dir.path().join("output.txt");
        fs::write(&path, "||example.com^\n").unwrap();

        let mut dispatcher = EventDispatcher::new();
        dispatcher.add_handler(Box::new(AbortingHandler));
        let config = scoped_validation_config(&dir);

        assert_eq!(
            validate_output_with_events(&path, &dispatcher, &config, false, false),
            Some("aborted by test handler".to_string())
        );
    }

    #[test]
    fn test_validate_output_with_events_unreadable_file_aborts_by_default() {
        let dispatcher = EventDispatcher::new();
        let config = ValidationConfig::default();

        // A validator that couldn't run tells us nothing about the output's safety, so
        // fail-closed treats that the same as a failed check, not a silent skip.
        assert!(validate_output_with_events(
            "/nonexistent/output.txt",
            &dispatcher,
            &config,
            false,
            false
        )
        .is_some());
    }

    #[test]
    fn test_validate_output_with_events_unreadable_file_allow_unvalidated_returns_none() {
        let dispatcher = EventDispatcher::new();
        let config = ValidationConfig::default();

        assert_eq!(
            validate_output_with_events(
                "/nonexistent/output.txt",
                &dispatcher,
                &config,
                true,
                false
            ),
            None
        );
    }
}
