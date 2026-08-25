//! Bloqr CLI for validating adblock/hosts filter lists.

use bloqr_validator::{
    HashDatabase, ValidationConfig, ValidationEngine, Validator, VerificationMode,
};
use clap::{Parser, Subcommand};
use serde::Serialize;
use std::path::PathBuf;

#[derive(Parser)]
#[command(name = "bloqr-validate")]
#[command(about = "Bloqr CLI for validating adblock/hosts filter lists")]
#[command(version)]
struct Cli {
    #[command(subcommand)]
    command: Commands,

    /// Emit machine-readable JSON to stdout instead of human-readable text.
    ///
    /// Intended for the non-Rust compiler wrappers that shell out to this CLI - see
    /// `docs/RUNTIME_ENFORCEMENT.md`. Exit codes are unchanged (0 = valid, 1 = invalid
    /// or error) regardless of this flag; use the exit code as the pass/fail signal and
    /// the JSON body for detail.
    #[arg(long, global = true)]
    json: bool,
}

/// JSON error envelope emitted on failure when `--json` is set.
#[derive(Serialize)]
struct JsonError<'a> {
    error: &'a str,
}

#[derive(Subcommand)]
enum Commands {
    /// Validate a local filter file
    File {
        /// Path to the filter file
        path: PathBuf,

        /// Verification mode (strict, warning, disabled)
        #[arg(long, default_value = "warning")]
        mode: String,

        /// Path to the hash database sidecar (created, along with its parent
        /// directory, if it doesn't exist yet)
        #[arg(long, default_value = "data/input/.hashes.json")]
        hash_db: PathBuf,

        /// Which syntax grammar to validate against: "dns" (server-side, the default -
        /// rejects cosmetic/browser-only syntax) or "browser" (client-side - accepts
        /// cosmetic rules, extended CSS, scriptlet injection, and browser-only $
        /// modifiers). See docs/VALIDATION_ENFORCEMENT.md.
        #[arg(long, default_value = "dns")]
        engine: String,
    },

    /// Validate a remote URL
    Url {
        /// URL to validate
        url: String,

        /// Expected SHA-384 hash (optional)
        #[arg(long)]
        hash: Option<String>,
    },

    /// Show hash database information
    HashDb {
        /// Path to hash database
        #[arg(long, default_value = "data/input/.hashes.json")]
        path: PathBuf,
    },
}

fn main() -> anyhow::Result<()> {
    let cli = Cli::parse();

    match cli.command {
        Commands::File {
            path,
            mode,
            hash_db,
            engine,
        } => {
            let verification_mode = match mode.as_str() {
                "strict" => VerificationMode::Strict,
                "warning" => VerificationMode::Warning,
                "disabled" => VerificationMode::Disabled,
                _ => {
                    eprintln!("Invalid mode: {mode}. Using 'warning' instead.");
                    VerificationMode::Warning
                }
            };

            let validation_engine: ValidationEngine = engine.parse().unwrap_or_else(|e| {
                eprintln!("{e}. Using 'dns' instead.");
                ValidationEngine::Dns
            });

            let mut config = ValidationConfig::default().with_verification_mode(verification_mode);
            config.hash_verification.hash_database_path = hash_db.to_string_lossy().into_owned();

            let mut validator = Validator::new(config);

            if !cli.json {
                println!("Validating file: {}", path.display());
            }

            match validator.validate_local_file_with_engine(&path, validation_engine) {
                Ok(result) => {
                    if cli.json {
                        println!("{}", serde_json::to_string(&result)?);
                    } else {
                        println!(
                            "✓ Syntax validation: {}",
                            if result.is_valid { "PASSED" } else { "FAILED" }
                        );
                        println!("  Format: {:?}", result.format);
                        println!("  Valid rules: {}", result.valid_rules);
                        println!("  Invalid rules: {}", result.invalid_rules);

                        if !result.messages.is_empty() {
                            println!("\nMessages:");
                            for msg in &result.messages {
                                println!("  - {msg}");
                            }
                        }
                    }

                    if !result.is_valid {
                        std::process::exit(1);
                    }
                }
                Err(e) => {
                    if cli.json {
                        println!(
                            "{}",
                            serde_json::to_string(&JsonError {
                                error: &e.to_string()
                            })?
                        );
                    } else {
                        eprintln!("✗ Validation failed: {e}");
                    }
                    std::process::exit(1);
                }
            }
        }

        Commands::Url { url, hash } => {
            let config = ValidationConfig::default();
            let validator = Validator::new(config);

            if !cli.json {
                println!("Validating URL: {url}");
            }

            match validator.validate_remote_url(&url, hash.as_deref()) {
                Ok(result) => {
                    if cli.json {
                        println!("{}", serde_json::to_string(&result)?);
                    } else {
                        println!(
                            "✓ URL validation: {}",
                            if result.is_valid { "PASSED" } else { "FAILED" }
                        );

                        if let Some(size) = result.content_size {
                            println!("  Content size: {} bytes", size);
                        }

                        if let Some(hash) = &result.content_hash {
                            println!("  SHA-384: {hash}");
                        }

                        if !result.messages.is_empty() {
                            println!("\nMessages:");
                            for msg in &result.messages {
                                println!("  - {msg}");
                            }
                        }
                    }

                    if !result.is_valid {
                        std::process::exit(1);
                    }
                }
                Err(e) => {
                    if cli.json {
                        println!(
                            "{}",
                            serde_json::to_string(&JsonError {
                                error: &e.to_string()
                            })?
                        );
                    } else {
                        eprintln!("✗ Validation failed: {e}");
                    }
                    std::process::exit(1);
                }
            }
        }

        Commands::HashDb { path } => match HashDatabase::load(&path) {
            Ok(db) => {
                if cli.json {
                    println!("{}", serde_json::to_string(&db)?);
                } else {
                    println!("Hash database: {}", path.display());
                    println!("Entries: {}", db.len());

                    if !db.is_empty() {
                        println!("\nStored hashes:");
                        for (file, entry) in &db.entries {
                            println!("  {file}");
                            println!("    Hash: {}", entry.hash);
                            println!("    Size: {} bytes", entry.size);
                            println!("    Last verified: {}", entry.last_verified);
                        }
                    }
                }
            }
            Err(e) => {
                if cli.json {
                    println!(
                        "{}",
                        serde_json::to_string(&JsonError {
                            error: &e.to_string()
                        })?
                    );
                } else {
                    eprintln!("Error loading hash database: {e}");
                }
                std::process::exit(1);
            }
        },
    }

    Ok(())
}
