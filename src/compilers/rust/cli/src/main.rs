//! Command-line interface for the AdGuard Filter Rules Compiler.
//!
//! Provides both direct command-line arguments and an interactive menu-driven interface.

use clap::{Parser, Subcommand};
use dialoguer::{theme::ColorfulTheme, Confirm, Input, Select};
use std::path::{Path, PathBuf};
use std::process::ExitCode;

use bloqr_compiler::{
    compile_chunks_async, compile_rules, read_config, split_into_chunks, to_json, BloqrCompiler,
    ChunkingOptions, ChunkingStrategy, CompileOptions, CompilerConfig, ConfigFormat, FilterSource,
    SourceType, VersionInfo,
};

/// AdGuard Filter Rules Compiler - Rust CLI
#[derive(Parser, Debug)]
#[command(name = "bloqr-compiler")]
// This crate's own CARGO_PKG_VERSION, not bloqr_compiler::VERSION (the
// bloqr-compiler-core library crate's version) - #173 split them into
// separately-versioned crates, so `--version` should report the CLI's own
// version rather than the library's.
#[command(version = env!("CARGO_PKG_VERSION"))]
#[command(about = "Compile AdGuard filter rules using hostlist-compiler")]
#[command(
    long_about = "A high-performance Rust CLI for compiling AdGuard filter rules.\n\n\
    Supports JSON, YAML, and TOML configuration formats.\n\
    Can run in direct mode with arguments or interactive menu mode."
)]
struct Cli {
    #[command(subcommand)]
    command: Option<Commands>,

    /// Path to configuration file
    #[arg(short, long, value_name = "PATH", global = true)]
    config: Option<PathBuf>,

    /// Path to output file
    #[arg(short, long, value_name = "PATH", global = true)]
    output: Option<PathBuf>,

    /// Copy output to rules directory
    #[arg(short = 'r', long, global = true)]
    copy_to_rules: bool,

    /// Force configuration format (json, yaml, toml)
    #[arg(short, long, value_name = "FORMAT", global = true)]
    format: Option<String>,

    /// Enable debug output
    #[arg(short, long, global = true)]
    debug: bool,

    /// Run in interactive menu mode
    #[arg(short, long)]
    interactive: bool,
}

#[derive(Subcommand, Debug)]
enum Commands {
    /// Compile filter rules from configuration
    Compile {
        /// Validate configuration before compiling
        #[arg(long)]
        validate: bool,

        /// Fail compilation on validation warnings
        #[arg(long)]
        fail_on_warnings: bool,

        /// Explicitly opt out of the mandatory rules-validator syntax check on compiled
        /// output. Security-relevant: leave this off in production - compiled output is
        /// validated and compilation fails closed by default. Use only for deliberate
        /// debugging of unvalidated output.
        #[arg(long)]
        allow_unvalidated_output: bool,

        /// Compilation engine/grammar to use ("dns" or "browser"). Omit (or "auto") to
        /// use the configuration's own defaultEngine/per-source engine resolution.
        #[arg(long, value_name = "ENGINE")]
        engine: Option<String>,

        /// Output path for the browser-syntax artifact, when the configuration mixes
        /// engines. Defaults to the DNS output path with a `.browser.txt` suffix.
        #[arg(long, value_name = "PATH")]
        browser_output: Option<PathBuf>,
    },
    /// Show configuration details without compiling
    Config,
    /// Show version information for all components
    Version,
    /// Run interactive menu
    Menu,
    /// Benchmark real compilation performance, chunked vs unchunked
    ///
    /// Compiles the canned datasets under benchmarks/data/ (small/medium/large/xlarge)
    /// through the real compile_rules()/compile_chunks_async() pipeline - not a
    /// simulation - once unchunked (all sources in a single compiler invocation, the
    /// same path the `compile` subcommand uses) and once chunked (the same sources
    /// split one-per-chunk and compiled in parallel), so the two runs cover identical
    /// total workloads and the chunking strategy is the intended variable.
    ///
    /// Caveat (see #424): the unchunked and chunked paths currently shell out to two
    /// different underlying compilers (compile_rules() uses Deno + the JSR
    /// @bloqr/compiler-core package; compile_chunks_async() uses hostlist-compiler/npx
    /// directly), so part of any timing delta may reflect that difference rather than
    /// chunking overhead alone. A run needs whichever of those two tools it resolves
    /// to be installed - if only one is available, that side of the comparison fails
    /// while the other still reports real numbers.
    Benchmark {
        /// Canned dataset size(s) to benchmark: small, medium, large, xlarge, or all
        #[arg(long, value_name = "SIZE", default_value = "all")]
        size: String,

        /// Directory containing the canned benchmark data (small.txt, medium.txt, ...).
        /// Defaults to auto-discovering benchmarks/data by walking up from the current
        /// directory (like configuration file discovery).
        #[arg(long, value_name = "PATH")]
        data_dir: Option<PathBuf>,

        /// Number of (identical, duplicated) sources to use for the chunked/parallel run
        /// - also how many chunks the parallel run splits into, one source per chunk
        #[arg(long, value_name = "COUNT", default_value = "4")]
        sources: usize,

        /// Max parallel workers for the chunked run (default: CPU count, max 8)
        #[arg(long, value_name = "WORKERS")]
        max_parallel: Option<usize>,

        /// Emit machine-readable JSON instead of a human-readable table
        #[arg(long)]
        json: bool,
    },
}

/// Canned benchmark dataset sizes, matching `benchmarks/data/{name}.txt` at the repo root
/// (generated by `benchmarks/generate_synthetic_data.py`).
const BENCHMARK_SIZES: [&str; 4] = ["small", "medium", "large", "xlarge"];

/// Transformations applied to every benchmark run, matching the canned
/// `benchmarks/data/config-*.json` fixtures' own transformation list.
const BENCHMARK_TRANSFORMATIONS: [&str; 3] = ["Deduplicate", "RemoveEmptyLines", "TrimLines"];

/// Result of benchmarking one canned dataset size, unchunked vs chunked.
#[derive(Debug, Clone, serde::Serialize)]
struct BenchmarkRunResult {
    size: String,
    sources: usize,
    max_parallel: usize,
    unchunked_success: bool,
    unchunked_ms: u64,
    unchunked_rule_count: usize,
    chunked_success: bool,
    chunked_ms: u64,
    chunked_rule_count: usize,
    /// `unchunked_ms / chunked_ms` - how much faster the chunked run was, `None` if either
    /// run failed or the chunked run took 0ms (nothing to divide by).
    speedup: Option<f64>,
    error: Option<String>,
}

/// Locate the repo's `benchmarks/data` directory by walking up from the current directory,
/// mirroring `find_config_in_ancestors`'s search strategy.
fn find_benchmark_data_dir() -> Option<PathBuf> {
    let current = std::env::current_dir().ok()?;
    let mut dir = current.as_path();

    loop {
        let candidate = dir.join("benchmarks").join("data");
        if candidate.is_dir() {
            return Some(candidate);
        }
        dir = dir.parent()?;
    }
}

/// Build a `CompilerConfig` with `num_sources` identical sources, all pointing at
/// `data_path` - the same "N copies of one file" shape as the (otherwise unusable, since
/// they hardcode a machine-specific absolute path - see #421) canned
/// `benchmarks/data/config-multi-Nsources.json` fixtures. Using identical sources keeps the
/// unchunked and chunked runs directly comparable: same total workload, same total rule
/// count after dedup, only the chunking strategy differs.
fn build_benchmark_config(size: &str, data_path: &Path, num_sources: usize) -> CompilerConfig {
    let mut config = CompilerConfig::new(format!("Benchmark - {size}"))
        .with_description(format!(
            "Real-pipeline benchmark of the '{size}' canned dataset"
        ))
        .with_version("1.0.0");

    for t in BENCHMARK_TRANSFORMATIONS {
        config = config.with_transformation(t);
    }

    for i in 0..num_sources.max(1) {
        let source =
            FilterSource::new(format!("source-{}", i + 1), data_path.display().to_string())
                .with_type(SourceType::Adblock);
        config = config.with_source(source);
    }

    config
}

/// Run the unchunked path: write `config` to a temp JSON file and compile it through the
/// real `compile_rules()` pipeline (a single hostlist-compiler invocation covering all of
/// `config`'s sources).
fn run_unchunked(config: &CompilerConfig) -> (bool, u64, usize, Option<String>) {
    let temp_config_path =
        std::env::temp_dir().join(format!("benchmark-config-{}.json", uuid::Uuid::new_v4()));
    let temp_output_path =
        std::env::temp_dir().join(format!("benchmark-output-{}.txt", uuid::Uuid::new_v4()));

    let json = match to_json(config) {
        Ok(j) => j,
        Err(e) => {
            return (
                false,
                0,
                0,
                Some(format!("failed to serialize config: {e}")),
            )
        }
    };
    if let Err(e) = std::fs::write(&temp_config_path, json) {
        return (
            false,
            0,
            0,
            Some(format!("failed to write temp config: {e}")),
        );
    }

    let options = CompileOptions::new().with_output(&temp_output_path);
    let result = compile_rules(&temp_config_path, &options);

    let _ = std::fs::remove_file(&temp_config_path);
    let _ = std::fs::remove_file(&temp_output_path);

    match result {
        Ok(r) if r.success => (true, r.elapsed_ms, r.rule_count, None),
        Ok(r) => (false, r.elapsed_ms, r.rule_count, r.error_message),
        Err(e) => (false, 0, 0, Some(e.to_string())),
    }
}

/// Run the chunked path: split `config` into one chunk per source (mirroring
/// `ChunkingStrategy::Source`, the only implemented strategy) and compile the chunks in
/// parallel through the real `compile_chunks_async()` pipeline, up to `max_parallel` at a
/// time.
fn run_chunked(config: &CompilerConfig, max_parallel: usize) -> (bool, u64, usize, Option<String>) {
    let chunking_options = ChunkingOptions::new()
        .with_enabled(true)
        .with_max_parallel(max_parallel.max(1))
        .with_strategy(ChunkingStrategy::Source);

    let chunks = split_into_chunks(config, &chunking_options);

    let runtime = match tokio::runtime::Builder::new_multi_thread()
        .enable_all()
        .build()
    {
        Ok(rt) => rt,
        Err(e) => {
            return (
                false,
                0,
                0,
                Some(format!("failed to start async runtime: {e}")),
            )
        }
    };

    match runtime.block_on(compile_chunks_async(chunks, &chunking_options, false)) {
        Ok(r) if r.success => (true, r.total_elapsed_ms, r.final_rule_count, None),
        Ok(r) => (
            false,
            r.total_elapsed_ms,
            r.final_rule_count,
            Some(r.errors.join("; ")),
        ),
        Err(e) => (false, 0, 0, Some(e.to_string())),
    }
}

/// Benchmark real compilation performance (chunked vs unchunked) across the requested
/// canned dataset size(s), using the same `compile_rules()`/`compile_chunks_async()`
/// pipeline the `compile` subcommand uses - not a synthetic simulation.
fn run_benchmark(
    size: &str,
    data_dir: Option<PathBuf>,
    num_sources: usize,
    max_parallel: Option<usize>,
    json_output: bool,
) -> ExitCode {
    let sizes: Vec<&str> = if size.eq_ignore_ascii_case("all") {
        BENCHMARK_SIZES.to_vec()
    } else {
        vec![size]
    };

    for s in &sizes {
        if !BENCHMARK_SIZES.contains(s) {
            eprintln!(
                "[ERROR] Unknown benchmark size '{s}'. Expected one of: {}, or 'all'.",
                BENCHMARK_SIZES.join(", ")
            );
            return ExitCode::FAILURE;
        }
    }

    let data_dir = match data_dir.or_else(find_benchmark_data_dir) {
        Some(d) => d,
        None => {
            eprintln!("[ERROR] Could not find a benchmarks/data directory.");
            eprintln!("        Pass --data-dir to point at one explicitly, or run this");
            eprintln!("        from within a clone of BloqrAI/bloqr-core.");
            return ExitCode::FAILURE;
        }
    };

    let max_parallel = max_parallel.unwrap_or_else(|| {
        std::thread::available_parallelism()
            .map(|p| std::cmp::min(p.get(), 8))
            .unwrap_or(4)
    });
    let num_sources = num_sources.max(1);

    if !json_output {
        println!();
        println!("======================================================================");
        println!("CHUNKING PERFORMANCE BENCHMARK (real compile_rules pipeline)");
        println!("======================================================================");
        println!("Data directory:       {}", data_dir.display());
        println!("Sources per dataset:  {num_sources} (identical copies, one per chunk)");
        println!("Max parallel workers: {max_parallel}");
        println!();
    }

    let mut results = Vec::with_capacity(sizes.len());

    for s in &sizes {
        let data_path = data_dir.join(format!("{s}.txt"));
        if !data_path.is_file() {
            let msg = format!("dataset file not found: {}", data_path.display());
            if !json_output {
                eprintln!("[SKIP] {s}: {msg}");
            }
            results.push(BenchmarkRunResult {
                size: (*s).to_string(),
                sources: num_sources,
                max_parallel,
                unchunked_success: false,
                unchunked_ms: 0,
                unchunked_rule_count: 0,
                chunked_success: false,
                chunked_ms: 0,
                chunked_rule_count: 0,
                speedup: None,
                error: Some(msg),
            });
            continue;
        }

        if !json_output {
            print!("Benchmarking '{s}' ({} sources)... ", num_sources);
            std::io::Write::flush(&mut std::io::stdout()).ok();
        }

        let config = build_benchmark_config(s, &data_path, num_sources);

        let (unchunked_success, unchunked_ms, unchunked_rule_count, unchunked_err) =
            run_unchunked(&config);
        let (chunked_success, chunked_ms, chunked_rule_count, chunked_err) =
            run_chunked(&config, max_parallel);

        let speedup = if unchunked_success && chunked_success && chunked_ms > 0 {
            Some(unchunked_ms as f64 / chunked_ms as f64)
        } else {
            None
        };

        if !json_output {
            match speedup {
                Some(s) => {
                    println!("done (unchunked {unchunked_ms}ms, chunked {chunked_ms}ms, {s:.2}x)")
                }
                None => println!("done (unchunked {unchunked_ms}ms, chunked {chunked_ms}ms)"),
            }
        }

        results.push(BenchmarkRunResult {
            size: (*s).to_string(),
            sources: num_sources,
            max_parallel,
            unchunked_success,
            unchunked_ms,
            unchunked_rule_count,
            chunked_success,
            chunked_ms,
            chunked_rule_count,
            speedup,
            error: unchunked_err.or(chunked_err),
        });
    }

    if json_output {
        match serde_json::to_string_pretty(&results) {
            Ok(s) => println!("{s}"),
            Err(e) => {
                eprintln!("[ERROR] Failed to serialize benchmark results: {e}");
                return ExitCode::FAILURE;
            }
        }
    } else {
        println!();
        println!("----------------------------------------------------------------------");
        println!("RESULTS");
        println!("----------------------------------------------------------------------");
        println!(
            "{:<10} {:<12} {:<12} {:<10} {:<10}",
            "Size", "Unchunked", "Chunked", "Speedup", "Rules"
        );
        println!("----------------------------------------------------------------------");
        for r in &results {
            if let Some(err) = &r.error {
                if !r.unchunked_success && !r.chunked_success {
                    println!("{:<10} FAILED: {err}", r.size);
                    continue;
                }
            }
            let speedup_str = r
                .speedup
                .map_or_else(|| "n/a".to_string(), |s| format!("{s:.2}x"));
            println!(
                "{:<10} {:<12} {:<12} {:<10} {:<10}",
                r.size,
                format!("{}ms", r.unchunked_ms),
                format!("{}ms", r.chunked_ms),
                speedup_str,
                r.chunked_rule_count,
            );
        }
        println!("----------------------------------------------------------------------");
        println!();
        println!("Note: this exercises the real compiler pipeline, so results depend on");
        println!("this machine's CPU/I-O characteristics. Unchunked needs Deno on PATH;");
        println!("chunked needs hostlist-compiler or npx on PATH - see --help and #424");
        println!("(they aren't the same underlying compiler today).");
        println!();
    }

    let any_failed = results
        .iter()
        .any(|r| !r.unchunked_success && !r.chunked_success);
    if any_failed {
        ExitCode::FAILURE
    } else {
        ExitCode::SUCCESS
    }
}

/// Parse format string to ConfigFormat.
fn parse_format(format: &str) -> Option<ConfigFormat> {
    match format.to_lowercase().as_str() {
        "json" => Some(ConfigFormat::Json),
        "yaml" | "yml" => Some(ConfigFormat::Yaml),
        "toml" => Some(ConfigFormat::Toml),
        _ => None,
    }
}

/// Display version information.
fn show_version_info() {
    let info = VersionInfo::collect();

    println!();
    println!("╔════════════════════════════════════════════════════════════╗");
    println!("║     AdGuard Filter Rules Compiler (Rust API)               ║");
    println!("╚════════════════════════════════════════════════════════════╝");
    println!();
    println!("  Version:      {}", info.module_version);
    println!("  Rust:         {}", info.rust_version);
    println!();
    println!("  Platform:");
    println!("    OS:         {}", info.platform.os_name);
    println!("    Arch:       {}", info.platform.architecture);
    println!();
    println!("  Dependencies:");
    println!(
        "    Node.js:    {}",
        info.node_version.as_deref().unwrap_or("Not found")
    );
    println!(
        "    Compiler:   {}",
        info.hostlist_compiler_version
            .as_deref()
            .unwrap_or("Not found")
    );
    if let Some(path) = &info.hostlist_compiler_path {
        println!("    Path:       {path}");
    }
    println!();
}

/// Display configuration details.
fn show_config(config_path: &PathBuf, format: Option<ConfigFormat>) -> ExitCode {
    match read_config(config_path, format) {
        Ok(config) => {
            println!();
            println!("╔════════════════════════════════════════════════════════════╗");
            println!("║                    Configuration Details                   ║");
            println!("╚════════════════════════════════════════════════════════════╝");
            println!();
            println!("  File:         {}", config_path.display());
            println!(
                "  Format:       {}",
                config.format().map(|f| f.to_string()).unwrap_or_default()
            );
            println!();
            println!("  Name:         {}", config.name);
            println!("  Version:      {}", config.version);
            println!("  License:      {}", config.license);
            if !config.description.is_empty() {
                println!("  Description:  {}", config.description);
            }
            println!();
            println!("  Sources:      {} total", config.sources.len());
            println!("    Local:      {}", config.local_sources_count());
            println!("    Remote:     {}", config.remote_sources_count());
            println!();

            if !config.transformations.is_empty() {
                println!("  Transformations:");
                for t in &config.transformations {
                    println!("    - {t}");
                }
                println!();
            }

            println!("  Source Details:");
            for (i, source) in config.sources.iter().enumerate() {
                println!("    [{i}] {}", source.name);
                println!("        Type:   {}", source.source_type);
                println!("        Source: {}", source.source);
            }
            println!();

            ExitCode::SUCCESS
        }
        Err(e) => {
            eprintln!("[ERROR] Failed to read configuration: {e}");
            ExitCode::FAILURE
        }
    }
}

/// Run compilation with the given options.
#[allow(clippy::too_many_arguments)]
fn run_compile(
    config_path: &PathBuf,
    output: Option<PathBuf>,
    copy_to_rules: bool,
    format: Option<ConfigFormat>,
    debug: bool,
    validate: bool,
    fail_on_warnings: bool,
    allow_unvalidated_output: bool,
    engine: Option<String>,
    browser_output: Option<PathBuf>,
) -> ExitCode {
    let options = CompileOptions::new()
        .with_copy_to_rules(copy_to_rules)
        .with_debug(debug)
        .with_validation(validate)
        .with_fail_on_warnings(fail_on_warnings)
        .with_allow_unvalidated_output(allow_unvalidated_output);

    let options = if let Some(engine) = engine {
        options.with_engine(engine)
    } else {
        options
    };

    let options = if let Some(path) = browser_output {
        options.with_browser_output(path)
    } else {
        options
    };

    if allow_unvalidated_output {
        eprintln!(
            "  [WARN] --allow-unvalidated-output set: compiled output will NOT be checked \
             by rules-validator. Not recommended outside deliberate debugging."
        );
    }

    let options = if let Some(path) = output {
        options.with_output(path)
    } else {
        options
    };

    let options = if let Some(fmt) = format {
        options.with_format(fmt)
    } else {
        options
    };

    let compiler = BloqrCompiler::with_options(options);

    println!();
    println!("╔════════════════════════════════════════════════════════════╗");
    println!("║                  Compiling Filter Rules                    ║");
    println!("╚════════════════════════════════════════════════════════════╝");
    println!();
    println!("  Config: {}", config_path.display());
    println!();

    match compiler.compile(config_path) {
        Ok(result) => {
            if result.success {
                println!("  ✓ Compilation successful!");
                println!();
                println!("  Results:");
                println!(
                    "    Filter:     {} v{}",
                    result.config_name, result.config_version
                );
                println!("    Rules:      {}", result.rule_count);
                println!("    Output:     {}", result.output_path_str());
                println!("    Hash:       {}...", result.hash_short());
                println!("    Elapsed:    {}", result.elapsed_formatted());

                if let Some(browser_path) = &result.browser_output_path {
                    println!();
                    println!("  Browser-syntax artifact:");
                    println!("    Output:     {}", browser_path.display());
                    if let Some(count) = result.browser_rule_count {
                        println!("    Rules:      {count}");
                    }
                    if let Some(hash) = &result.browser_output_hash {
                        let short: String = hash.chars().take(8).collect();
                        println!("    Hash:       {short}...");
                    }
                }

                if result.copied_to_rules {
                    println!();
                    println!(
                        "  ✓ Copied to:  {}",
                        result.rules_destination_str().unwrap_or_default()
                    );
                }

                println!();
                ExitCode::SUCCESS
            } else {
                eprintln!(
                    "  ✗ Compilation failed: {}",
                    result.error_message.as_deref().unwrap_or("Unknown error")
                );
                if !result.stderr.is_empty() {
                    eprintln!();
                    eprintln!("  Stderr:");
                    for line in result.stderr.lines() {
                        eprintln!("    {line}");
                    }
                }
                eprintln!();
                ExitCode::FAILURE
            }
        }
        Err(e) => {
            eprintln!("  ✗ Error: {e}");
            eprintln!();
            ExitCode::FAILURE
        }
    }
}

/// Find default configuration file by searching current and ancestor directories.
///
/// Search strategy:
/// 1. Check current directory for compiler-config.{json,yaml,toml}
/// 2. Check src/compilers/typescript/compiler-config.json (repository-specific)
/// 3. Traverse up parent directories looking for compiler-config.{json,yaml,toml}
///
/// This mimics the behavior of tools like git, eslint, and prettier.
fn find_default_config() -> Option<PathBuf> {
    // First, try current directory with all formats
    let current_dir_paths = [
        PathBuf::from("compiler-config.json"),
        PathBuf::from("compiler-config.yaml"),
        PathBuf::from("compiler-config.toml"),
        PathBuf::from("src/compilers/typescript/compiler-config.json"),
    ];

    for path in &current_dir_paths {
        if path.exists() {
            return Some(path.clone());
        }
    }

    // Then, traverse up parent directories
    find_config_in_ancestors()
}

/// Search for configuration file in ancestor directories.
///
/// Starts from the current directory and walks up the directory tree,
/// looking for compiler-config.{json,yaml,toml} files.
fn find_config_in_ancestors() -> Option<PathBuf> {
    let current = std::env::current_dir().ok()?;
    let config_names = [
        "compiler-config.json",
        "compiler-config.yaml",
        "compiler-config.toml",
    ];

    let mut dir = current.as_path();

    // Walk up the directory tree
    loop {
        for config_name in &config_names {
            let config_path = dir.join(config_name);
            if config_path.exists() && config_path.is_file() {
                return Some(config_path);
            }
        }

        // Move to parent directory
        dir = dir.parent()?;
    }
}

/// Display helpful error message when no configuration file is found.
fn print_config_not_found_error() {
    eprintln!("[ERROR] No configuration file specified or found.");
    eprintln!();
    eprintln!("Searched for configuration files:");
    eprintln!("  - compiler-config.json");
    eprintln!("  - compiler-config.yaml");
    eprintln!("  - compiler-config.toml");
    eprintln!("  - src/compilers/typescript/compiler-config.json");
    eprintln!();

    if let Ok(current_dir) = std::env::current_dir() {
        eprintln!("Search started from: {}", current_dir.display());
        eprintln!("Also checked all parent directories up to filesystem root.");
        eprintln!();
    }

    eprintln!("Solutions:");
    eprintln!("  1. Use -c/--config to specify a configuration file");
    eprintln!("  2. Create a compiler-config.json in the current or parent directory");
    eprintln!("  3. Use -i/--interactive for menu mode");
}

/// Interactive menu loop.
fn run_interactive_menu(initial_config: Option<PathBuf>) -> ExitCode {
    let theme = ColorfulTheme::default();
    let mut config_path = initial_config.or_else(find_default_config);

    println!();
    println!("╔════════════════════════════════════════════════════════════╗");
    println!("║     AdGuard Filter Rules Compiler - Interactive Mode       ║");
    println!("╚════════════════════════════════════════════════════════════╝");
    println!();

    loop {
        let menu_items = vec![
            "Compile Rules",
            "View Configuration",
            "Change Configuration File",
            "Version Information",
            "Exit",
        ];

        let current_config = config_path
            .as_ref()
            .map(|p| p.display().to_string())
            .unwrap_or_else(|| "Not set".to_string());

        println!("  Current config: {current_config}");
        println!();

        let selection = Select::with_theme(&theme)
            .with_prompt("Select an action")
            .items(&menu_items)
            .default(0)
            .interact();

        let selection = match selection {
            Ok(s) => s,
            Err(_) => {
                println!();
                println!("  Exiting...");
                return ExitCode::SUCCESS;
            }
        };

        println!();

        match selection {
            0 => {
                // Compile Rules
                if let Some(ref path) = config_path {
                    let copy_to_rules = Confirm::with_theme(&theme)
                        .with_prompt("Copy output to rules directory?")
                        .default(false)
                        .interact()
                        .unwrap_or(false);

                    let validate = Confirm::with_theme(&theme)
                        .with_prompt("Validate configuration before compiling?")
                        .default(true)
                        .interact()
                        .unwrap_or(true);

                    let fail_on_warnings = if validate {
                        Confirm::with_theme(&theme)
                            .with_prompt("Fail compilation on validation warnings?")
                            .default(false)
                            .interact()
                            .unwrap_or(false)
                    } else {
                        false
                    };

                    run_compile(
                        path,
                        None,
                        copy_to_rules,
                        None,
                        false,
                        validate,
                        fail_on_warnings,
                        false,
                        None,
                        None,
                    );
                } else {
                    eprintln!("  No configuration file selected.");
                    eprintln!("  Use 'Change Configuration File' to select one.");
                    eprintln!();
                }
            }
            1 => {
                // View Configuration
                if let Some(ref path) = config_path {
                    show_config(path, None);
                } else {
                    eprintln!("  No configuration file selected.");
                    eprintln!();
                }
            }
            2 => {
                // Change Configuration File
                let input: Result<String, _> = Input::with_theme(&theme)
                    .with_prompt("Enter configuration file path")
                    .with_initial_text(
                        config_path
                            .as_ref()
                            .map(|p| p.display().to_string())
                            .unwrap_or_default(),
                    )
                    .interact_text();

                if let Ok(path_str) = input {
                    let path = PathBuf::from(path_str.trim());
                    if path.exists() {
                        config_path = Some(path);
                        println!("  ✓ Configuration file updated.");
                    } else {
                        eprintln!("  ✗ File not found: {}", path.display());
                    }
                }
                println!();
            }
            3 => {
                // Version Information
                show_version_info();
            }
            4 => {
                // Exit
                println!("  Goodbye!");
                println!();
                return ExitCode::SUCCESS;
            }
            _ => {}
        }
    }
}

fn main() -> ExitCode {
    let cli = Cli::parse();

    // Parse format if provided
    let format = cli.format.as_deref().and_then(parse_format);

    // Handle interactive mode
    if cli.interactive || matches!(cli.command, Some(Commands::Menu)) {
        return run_interactive_menu(cli.config);
    }

    // Handle subcommands
    match cli.command {
        Some(Commands::Version) => {
            show_version_info();
            ExitCode::SUCCESS
        }
        Some(Commands::Config) => {
            let config_path = match cli.config.or_else(find_default_config) {
                Some(path) => path,
                None => {
                    print_config_not_found_error();
                    return ExitCode::FAILURE;
                }
            };
            show_config(&config_path, format)
        }
        Some(Commands::Compile {
            validate,
            fail_on_warnings,
            allow_unvalidated_output,
            engine,
            browser_output,
        }) => {
            let config_path = match cli.config.or_else(find_default_config) {
                Some(path) => path,
                None => {
                    print_config_not_found_error();
                    return ExitCode::FAILURE;
                }
            };

            run_compile(
                &config_path,
                cli.output,
                cli.copy_to_rules,
                format,
                cli.debug,
                validate,
                fail_on_warnings,
                allow_unvalidated_output,
                engine,
                browser_output,
            )
        }
        None => {
            let config_path = match cli.config.or_else(find_default_config) {
                Some(path) => path,
                None => {
                    print_config_not_found_error();
                    return ExitCode::FAILURE;
                }
            };

            run_compile(
                &config_path,
                cli.output,
                cli.copy_to_rules,
                format,
                cli.debug,
                false,
                false,
                false,
                None,
                None,
            )
        }
        Some(Commands::Menu) => run_interactive_menu(cli.config),
        Some(Commands::Benchmark {
            size,
            data_dir,
            sources,
            max_parallel,
            json,
        }) => run_benchmark(&size, data_dir, sources, max_parallel, json),
    }
}
