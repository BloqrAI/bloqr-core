//! Command-line interface for the AdGuard Filter Rules Compiler.
//!
//! Provides both direct command-line arguments and an interactive menu-driven interface.

use clap::{Parser, Subcommand};
use dialoguer::{theme::ColorfulTheme, Confirm, Input, Select};
use std::path::PathBuf;
use std::process::ExitCode;

use bloqr_compiler::{read_config, BloqrCompiler, CompileOptions, ConfigFormat, VersionInfo};

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

    /// Run synthetic benchmark to show expected chunking speedups
    #[arg(long)]
    benchmark: bool,

    /// Number of rules to simulate in benchmark (default: 200000)
    #[arg(long, value_name = "COUNT", default_value = "200000")]
    benchmark_rules: usize,

    /// Max parallel workers for benchmark (default: CPU count, max 8)
    #[arg(long, value_name = "WORKERS")]
    benchmark_parallel: Option<usize>,
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
    },
    /// Show configuration details without compiling
    Config,
    /// Show version information for all components
    Version,
    /// Run interactive menu
    Menu,
    /// Run synthetic benchmark to show expected chunking speedups
    Benchmark {
        /// Number of rules to simulate (default: 200000)
        #[arg(long, value_name = "COUNT", default_value = "200000")]
        rules: usize,

        /// Max parallel workers (default: CPU count, max 8)
        #[arg(long, value_name = "WORKERS")]
        parallel: Option<usize>,
    },
}

/// Run a synthetic benchmark to demonstrate chunking speedup.
fn run_benchmark(rule_count: usize, max_parallel: Option<usize>) -> ExitCode {
    use rand::RngExt;
    use std::time::{Duration, Instant};

    let max_parallel = max_parallel.unwrap_or_else(|| {
        std::thread::available_parallelism()
            .map(|p| std::cmp::min(p.get(), 8))
            .unwrap_or(4)
    });

    println!();
    println!("======================================================================");
    println!("CHUNKING PERFORMANCE BENCHMARK");
    println!("======================================================================");
    println!(
        "CPU cores available: {}",
        std::thread::available_parallelism()
            .map(|p| p.get())
            .unwrap_or(0)
    );
    println!("Max parallel workers: {max_parallel}");
    println!("Simulating {rule_count} rules");
    println!();

    // Simulate processing function
    fn simulate_processing(rule_count: usize) -> f64 {
        let fixed_overhead_ms = 50.0;
        let per_rule_ms = 0.01; // 10ms per 1000 rules

        let mut rng = rand::rng();
        let variation: f64 = rng.random_range(0.95..1.15);

        let processing_time = (fixed_overhead_ms + (rule_count as f64 * per_rule_ms)) * variation;

        // Sleep scaled down for demo (1% of actual time)
        std::thread::sleep(Duration::from_micros((processing_time * 10.0) as u64));

        processing_time
    }

    // Sequential benchmark
    print!("Running sequential benchmark... ");
    std::io::Write::flush(&mut std::io::stdout()).ok();
    let _start = Instant::now();
    let sequential_time = simulate_processing(rule_count);
    println!("done ({sequential_time:.0}ms simulated)");

    // Parallel benchmark
    print!("Running parallel benchmark ({max_parallel} workers)... ");
    std::io::Write::flush(&mut std::io::stdout()).ok();

    let chunk_size = rule_count.div_ceil(max_parallel);
    let _start = Instant::now();

    // Use threads to simulate parallel processing
    let handles: Vec<_> = (0..max_parallel)
        .map(|i| {
            let rules_in_chunk = if i == max_parallel - 1 {
                rule_count - (chunk_size * i)
            } else {
                chunk_size
            };
            std::thread::spawn(move || simulate_processing(rules_in_chunk))
        })
        .collect();

    let chunk_times: Vec<f64> = handles
        .into_iter()
        .map(|h| h.join().unwrap_or(0.0))
        .collect();

    let parallel_time = chunk_times
        .iter()
        .cloned()
        .max_by(|a, b| a.total_cmp(b))
        .unwrap_or(0.0);
    println!("done ({parallel_time:.0}ms simulated)");

    // Calculate results
    let speedup = if parallel_time > 0.0 {
        sequential_time / parallel_time
    } else {
        1.0
    };
    let efficiency = speedup / max_parallel as f64;
    let time_saved = sequential_time - parallel_time;

    println!();
    println!("----------------------------------------------------------------------");
    println!("RESULTS");
    println!("----------------------------------------------------------------------");
    println!("Sequential time:     {sequential_time:.0} ms");
    println!("Parallel time:       {parallel_time:.0} ms");
    println!("Speedup:             {speedup:.2}x");
    println!("Efficiency:          {:.1}%", efficiency * 100.0);
    println!("Time saved:          {time_saved:.0} ms");
    println!();

    // Show scaling table
    println!("Expected speedups at different scales:");
    println!("--------------------------------------------------");
    println!(
        "{:<15} {:<15} {:<15} Speedup",
        "Rules", "Sequential", "Parallel"
    );
    println!("--------------------------------------------------");

    for size in [10_000usize, 50_000, 200_000, 500_000] {
        let seq = 50.0 + (size as f64 * 0.01);
        let par = 50.0 + ((size as f64 / max_parallel as f64) * 0.01);
        let spd = seq / par;
        println!(
            "{:<15} {:<15} {:<15} {:.2}x",
            format!("{size}"),
            format!("{seq:.0} ms"),
            format!("{par:.0} ms"),
            spd
        );
    }

    println!("--------------------------------------------------");
    println!();
    println!("Note: Actual speedup depends on:");
    println!("  - Number of CPU cores");
    println!("  - I/O performance (especially for network sources)");
    println!("  - Rule complexity and transformations applied");
    println!();

    ExitCode::SUCCESS
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
fn run_compile(
    config_path: &PathBuf,
    output: Option<PathBuf>,
    copy_to_rules: bool,
    format: Option<ConfigFormat>,
    debug: bool,
    validate: bool,
    fail_on_warnings: bool,
    allow_unvalidated_output: bool,
) -> ExitCode {
    let options = CompileOptions::new()
        .with_copy_to_rules(copy_to_rules)
        .with_debug(debug)
        .with_validation(validate)
        .with_fail_on_warnings(fail_on_warnings)
        .with_allow_unvalidated_output(allow_unvalidated_output);

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

    // Handle benchmark flag
    if cli.benchmark {
        return run_benchmark(cli.benchmark_rules, cli.benchmark_parallel);
    }

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
            )
        }
        Some(Commands::Menu) => run_interactive_menu(cli.config),
        Some(Commands::Benchmark { rules, parallel }) => run_benchmark(rules, parallel),
    }
}
