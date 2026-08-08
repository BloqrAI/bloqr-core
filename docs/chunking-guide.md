# Parallel Chunking for Large Filter Lists

This guide explains the parallel chunking feature available in the rules compilers for improved performance when processing large filter lists.

## Overview

When compiling filter lists with many sources or millions of rules, chunking addresses this by:

1. **Splitting sources into chunks** - Distributes sources across multiple parallel workers
2. **Compiling chunks in parallel** - Uses multiple CPU cores simultaneously
3. **Merging results** - Combines chunk outputs with deduplication

## Performance Benefits

| Scenario | Sources | Rules | Sequential Time | Chunked Time (4 cores) | Speedup |
|----------|---------|-------|-----------------|------------------------|---------|
| Small | 10 | ~50k | 15s | 12s | 1.25x |
| Medium | 50 | ~250k | 75s | 25s | 3x |
| Large | 200 | ~1M | 300s | 85s | 3.5x |

*Times are approximate and depend on source download speed and hardware*

## Supported Compilers

| Compiler | Chunking Support | Status |
|----------|-----------------|--------|
| TypeScript | Full | Production |
| .NET | Full | Production |
| Python | Full | Production |
| Rust | Full | Production |

## Configuration

### TypeScript (Deno)

#### Configuration File

```json
{
  "name": "My Filter List",
  "sources": [...],
  "chunking": {
    "enabled": true,
    "chunkSize": 100000,
    "maxParallel": 4,
    "strategy": "source"
  }
}
```

#### CLI Flags

```bash
deno task compile -- --enable-chunking --chunk-size 100000 --max-parallel 4
```

### .NET

#### Programmatic Usage

```csharp
var options = new CompilerOptions
{
    ConfigPath = "config.yaml",
    Chunking = new ChunkingOptions
    {
        Enabled = true,
        ChunkSize = 100_000,
        MaxParallel = Environment.ProcessorCount,
        Strategy = ChunkingStrategy.Source
    }
};

var result = await compiler.CompileAsync(options);
```

#### Using Presets

```csharp
// For small lists (chunking disabled)
var options = CompilerOptions.Default;

// For large lists (chunking enabled with optimal settings)
var options = CompilerOptions.ForLargeLists;
```

#### Dependency Injection

```csharp
services.AddRulesCompiler();

// The IChunkingService is automatically registered
var chunkingService = serviceProvider.GetRequiredService<IChunkingService>();
```

### Python

#### Programmatic Usage

```python
from rules_compiler import RulesCompiler
from rules_compiler.chunking import ChunkingOptions, ChunkingStrategy

# Create chunking options
chunking_options = ChunkingOptions(
    enabled=True,
    chunk_size=100_000,
    max_parallel=os.cpu_count() or 4,
    strategy=ChunkingStrategy.SOURCE
)

# Use preset for large lists
chunking_options = ChunkingOptions.for_large_lists()

# Compile with chunking
compiler = RulesCompiler(chunking=chunking_options)
result = await compiler.compile_async("config.yaml")
```

#### CLI Usage

```bash
rules-compiler -c config.yaml --chunking --max-parallel 4
```

### Rust

#### Programmatic Usage

```rust
use rules_compiler::{
    ChunkingOptions, ChunkingStrategy, CompilerConfig,
    should_enable_chunking, split_into_chunks, compile_chunks_async, merge_chunks
};

// Create chunking options
let options = ChunkingOptions::new()
    .with_enabled(true)
    .with_chunk_size(100_000)
    .with_max_parallel(8)
    .with_strategy(ChunkingStrategy::Source);

// Use preset for large lists
let options = ChunkingOptions::for_large_lists();

// Split, compile, and merge
if should_enable_chunking(&config, Some(&options)) {
    let chunks = split_into_chunks(&config, &options);
    let result = compile_chunks_async(chunks, &options, false).await?;
    println!("Speedup: {:.2}x", result.estimated_speedup());
}
```

#### CLI Usage

```bash
rules-compiler -c config.yaml --chunking --max-parallel 4
```

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `enabled` | boolean | `false` | Enable parallel chunking |
| `chunkSize` | number | `100000` | Maximum estimated rules per chunk |
| `maxParallel` | number | CPU cores | Maximum parallel workers |
| `strategy` | string | `"source"` | Chunking strategy |

### Chunking Strategies

| Strategy | Description | Best For |
|----------|-------------|----------|
| `source` | Distributes sources evenly across chunks | Most use cases |
| `line-count` | Balances by estimated line count | (Planned) |

## How It Works

### Source Strategy

1. **Calculate chunks**: Sources are distributed evenly
   ```
   Total sources: 20
   Max parallel: 4
   → 4 chunks with 5 sources each
   ```

2. **Batch processing**: Chunks run in parallel batches
   ```
   Batch 1: Chunks 1-4 (parallel)
   Batch 2: Chunks 5-8 (parallel) [if needed]
   ```

3. **Merge results**: All outputs combined with deduplication
   ```
   Chunk 1: 25,000 rules
   Chunk 2: 30,000 rules
   Chunk 3: 28,000 rules
   Chunk 4: 27,000 rules
   ─────────────────────
   Total: 110,000 rules
   After dedup: 95,000 rules (removed 15,000 duplicates)
   ```

### Automatic Enablement

When `enabled` is not explicitly set:
- **Multiple sources + Source strategy** → Chunking enabled automatically
- **Single source** → Chunking disabled (no benefit)

## Merge Behavior

The merge process:
1. Flattens all chunk outputs into a single list
2. Deduplicates actual filter rules while preserving order
3. Preserves comments (`!` and `#` prefixed lines)
4. Preserves empty lines for readability
5. Reports duplicate count in logs

```
[INFO] Merging 4 chunks...
[DEBUG] Total rules before deduplication: 110000
[INFO] Merged to 95000 rules (removed 15000 duplicates)
```

## Result Metrics

The compilation result includes chunking metrics:

```csharp
var result = await compiler.CompileAsync(options);

// ChunkedCompilationResult properties:
result.TotalRules        // Sum of all chunk rules
result.FinalRuleCount    // After deduplication
result.DuplicatesRemoved // Number removed
result.TotalElapsedMs    // Wall clock time
result.EstimatedSpeedup  // Ratio of sequential/parallel time
result.Chunks            // Individual chunk metadata
```

## Best Practices

### When to Enable Chunking

| Sources | Recommendation |
|---------|---------------|
| 1-5 | Disable chunking (overhead not worth it) |
| 6-20 | Enable with default settings |
| 20+ | Enable with `maxParallel` matching CPU cores |

### Optimal Settings

```csharp
// Recommended for most large filter lists
var options = new ChunkingOptions
{
    Enabled = true,
    ChunkSize = 100_000,
    MaxParallel = Math.Max(2, Environment.ProcessorCount),
    Strategy = ChunkingStrategy.Source
};
```

### Memory Considerations

- Each chunk runs a separate `@bloqr/compiler-core` process (via `deno run jsr:@bloqr/compiler-core/cli` for .NET/Python/Rust, or the TypeScript compiler's own chunking layer)
- Memory usage scales with `maxParallel`
- For memory-constrained systems, reduce `maxParallel` to 2-4

## Limitations

1. **Network-bound sources**: Chunking helps less when sources are slow to download
2. **Single large source**: Cannot parallelize a single source file
3. **Transformation order**: Global transformations run after merge, not per-chunk

## Troubleshooting

### Chunking not enabled

Check that:
- `enabled: true` in configuration
- Multiple sources exist (for automatic enablement)
- `ChunkingService` is registered (DI scenarios)

### Poor speedup

Possible causes:
- Sources are network-bound (download time dominates)
- Too few sources to benefit from parallelism
- `maxParallel` set too low

### High memory usage

Solutions:
- Reduce `maxParallel` to 2-4
- Ensure sufficient RAM (2GB+ recommended for large lists)

## API Reference

### .NET Types

```csharp
// Options
public class ChunkingOptions
{
    public bool Enabled { get; set; }
    public int ChunkSize { get; set; }
    public int MaxParallel { get; set; }
    public ChunkingStrategy Strategy { get; set; }
}

// Result
public class ChunkedCompilationResult
{
    public bool Success { get; set; }
    public long TotalElapsedMs { get; set; }
    public List<ChunkMetadata> Chunks { get; set; }
    public int TotalRules { get; set; }
    public int FinalRuleCount { get; set; }
    public int DuplicatesRemoved { get; set; }
    public double EstimatedSpeedup { get; }
}

// Service interface
public interface IChunkingService
{
    bool ShouldEnableChunking(CompilerConfiguration config, ChunkingOptions? options);
    List<(CompilerConfiguration Config, ChunkMetadata Metadata)> SplitIntoChunks(...);
    Task<ChunkedCompilationResult> CompileChunksAsync(...);
    (string[] Rules, int DuplicatesRemoved) MergeChunks(List<string[]> chunkResults);
    double EstimateSpeedup(int totalRules, ChunkingOptions options);
}
```

### Python Types

```python
from dataclasses import dataclass
from enum import Enum

class ChunkingStrategy(Enum):
    SOURCE = "source"
    LINE_COUNT = "line_count"

@dataclass
class ChunkingOptions:
    enabled: bool = False
    chunk_size: int = 100_000
    max_parallel: int = os.cpu_count() or 4
    strategy: ChunkingStrategy = ChunkingStrategy.SOURCE

    @classmethod
    def default(cls) -> "ChunkingOptions": ...

    @classmethod
    def for_large_lists(cls) -> "ChunkingOptions": ...

@dataclass
class ChunkMetadata:
    index: int
    total: int
    estimated_rules: int = 0
    actual_rules: int | None = None
    sources: list[FilterSource] = field(default_factory=list)
    elapsed_ms: int | None = None
    success: bool = False

@dataclass
class ChunkedCompilationResult:
    success: bool = False
    total_elapsed_ms: int = 0
    chunks: list[ChunkMetadata] = field(default_factory=list)
    total_rules: int = 0
    final_rule_count: int = 0
    duplicates_removed: int = 0

    @property
    def estimated_speedup(self) -> float: ...

# Functions
def should_enable_chunking(config: CompilerConfiguration, options: ChunkingOptions | None) -> bool: ...
def split_into_chunks(config: CompilerConfiguration, options: ChunkingOptions) -> list[tuple[CompilerConfiguration, ChunkMetadata]]: ...
async def compile_chunks_async(chunks: list, options: ChunkingOptions, debug: bool = False) -> ChunkedCompilationResult: ...
def merge_chunks(chunk_results: list[list[str]]) -> tuple[list[str], int]: ...
def estimate_speedup(total_rules: int, options: ChunkingOptions) -> float: ...
```

### Rust Types

```rust
// Strategy enum
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub enum ChunkingStrategy {
    #[default]
    Source,
    LineCount,
}

// Options
pub struct ChunkingOptions {
    pub enabled: bool,
    pub chunk_size: usize,
    pub max_parallel: usize,
    pub strategy: ChunkingStrategy,
}

impl ChunkingOptions {
    pub fn new() -> Self;
    pub fn for_large_lists() -> Self;
    pub fn with_enabled(self, enabled: bool) -> Self;
    pub fn with_chunk_size(self, chunk_size: usize) -> Self;
    pub fn with_max_parallel(self, max_parallel: usize) -> Self;
    pub fn with_strategy(self, strategy: ChunkingStrategy) -> Self;
}

// Metadata
pub struct ChunkMetadata {
    pub index: usize,
    pub total: usize,
    pub estimated_rules: usize,
    pub actual_rules: Option<usize>,
    pub sources: Vec<FilterSource>,
    pub elapsed_ms: Option<u64>,
    pub success: bool,
    pub error_message: Option<String>,
    pub output_path: Option<PathBuf>,
}

// Result
pub struct ChunkedCompilationResult {
    pub success: bool,
    pub total_elapsed_ms: u64,
    pub chunks: Vec<ChunkMetadata>,
    pub total_rules: usize,
    pub final_rule_count: usize,
    pub duplicates_removed: usize,
    pub merged_rules: Option<Vec<String>>,
    pub errors: Vec<String>,
}

impl ChunkedCompilationResult {
    pub fn estimated_speedup(&self) -> f64;
}

// Functions
pub fn should_enable_chunking(config: &CompilerConfig, options: Option<&ChunkingOptions>) -> bool;
pub fn split_into_chunks(config: &CompilerConfig, options: &ChunkingOptions) -> Vec<(CompilerConfig, ChunkMetadata)>;
pub async fn compile_chunks_async(chunks: Vec<(CompilerConfig, ChunkMetadata)>, options: &ChunkingOptions, debug: bool) -> Result<ChunkedCompilationResult>;
pub fn merge_chunks(chunk_results: &[Vec<String>]) -> (Vec<String>, usize);
pub fn estimate_speedup(total_rules: usize, options: &ChunkingOptions) -> f64;
```

## Benchmarking

The repository includes a comprehensive benchmark suite to measure chunking performance.

### Quick Synthetic Benchmark

Run a quick simulation to see expected speedups on your system:

```bash
cd benchmarks

# Run comparison suite (recommended)
python quick_benchmark.py --suite

# Run parallel scaling test
python quick_benchmark.py --scaling

# Custom benchmark
python quick_benchmark.py --rules 500000 --parallel 8

# Interactive mode
python quick_benchmark.py --interactive
```

Example output:
```
======================================================================
CHUNKING PERFORMANCE COMPARISON SUITE
======================================================================
CPU cores available: 8
Max parallel workers: 8

Size            Sequential      Parallel        Speedup      Efficiency
----------------------------------------------------------------------
10K rules       150 ms          70 ms           2.14x        27%
50K rules       570 ms          130 ms          4.38x        55%
200K rules      2,350 ms        350 ms          6.71x        84%
500K rules      5,400 ms        800 ms          6.75x        84%
----------------------------------------------------------------------

Average speedup: 5.00x
Maximum speedup: 6.75x
```

### Full Benchmark with Real Compilation

Generate synthetic test data and run actual compilation benchmarks:

```bash
cd benchmarks

# Generate test data (small, medium, large, xlarge filter lists)
python generate_synthetic_data.py --all

# Run benchmarks across all compilers
python run_benchmarks.py

# Run specific compiler only
python run_benchmarks.py --compiler python --iterations 5

# Run specific size only
python run_benchmarks.py --size large
```

### Expected Performance

Based on synthetic benchmarks:

| Rule Count | Sequential | 4 Workers | 8 Workers | Speedup (8w) |
|------------|-----------|-----------|-----------|--------------|
| 10,000 | ~150ms | ~60ms | ~40ms | 3.75x |
| 50,000 | ~600ms | ~200ms | ~120ms | 5.0x |
| 200,000 | ~2.5s | ~800ms | ~400ms | 6.25x |
| 500,000 | ~6s | ~1.8s | ~900ms | 6.67x |

*Actual times vary by hardware, I/O speed, and network latency for remote sources*

### Parallel Scaling

Speedup scales with CPU cores but with diminishing returns:

| Workers | Theoretical Max | Typical Efficiency |
|---------|-----------------|-------------------|
| 2 | 2.0x | 90-100% |
| 4 | 4.0x | 85-95% |
| 8 | 8.0x | 75-90% |
| 16 | 16.0x | 60-80% |

Efficiency decreases due to:
- Process startup overhead
- Merge/deduplication time
- Memory bandwidth limits
- I/O contention

## Future Enhancements

- **Line-count strategy**: Balance chunks by estimated rule count
- **Streaming merge**: Reduce memory usage for very large outputs
- **Source caching**: Cache downloaded sources across chunks
- **Progress callbacks**: Real-time progress reporting
