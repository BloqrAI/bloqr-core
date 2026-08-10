# Event Pipeline and Zero-Trust Validation

This guide explains the event pipeline system available in all rules compilers for monitoring compilation progress and implementing zero-trust validation.

## Overview

The event pipeline provides hooks into every stage of the compilation process, enabling:

1. **Progress Monitoring** - Track compilation progress with detailed metrics
2. **Zero-Trust Validation** - Validate data at each stage boundary
3. **File Locking** - Lock local source files to prevent modification during compilation
4. **Error Handling** - Handle errors at specific stages
5. **Extensibility** - Add custom behavior without modifying core compilation logic

## Compilation Stages

The compilation pipeline consists of these stages:

```
┌─────────────────────────────────────────────────────────────┐
│                    COMPILATION PIPELINE                      │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─────────────────┐     ┌─────────────────┐                │
│  │ CompilationStart│────▶│ ConfigurationLoad│               │
│  │   (Cancelable)  │     │                  │               │
│  └─────────────────┘     └────────┬─────────┘               │
│                                   │                          │
│  ┌──────────────────────────────▼──────────────────────┐   │
│  │              VALIDATION CHECKPOINT                    │   │
│  │  - Configuration validation                           │   │
│  │  - Source URL/path validation                         │   │
│  │  - Zero-trust checks (abort on critical findings)     │   │
│  └──────────────────────────────┬──────────────────────┘   │
│                                   │                          │
│  ┌──────────────────────────────▼──────────────────────┐   │
│  │              SOURCE LOADING (per source)              │   │
│  │  ┌─────────────────┐     ┌─────────────────┐         │   │
│  │  │ SourceLoading   │────▶│ SourceLoaded    │         │   │
│  │  │ (Lock acquired) │     │ (Hash computed) │         │   │
│  │  └─────────────────┘     └─────────────────┘         │   │
│  └──────────────────────────────┬──────────────────────┘   │
│                                   │                          │
│  ┌──────────────────────────────▼──────────────────────┐   │
│  │              CHUNKED COMPILATION (if enabled)         │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │   │
│  │  │ ChunkStarted│  │ ChunkStarted│  │ ChunkStarted│   │   │
│  │  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘   │   │
│  │         │                │                │           │   │
│  │  ┌──────▼──────┐  ┌──────▼──────┐  ┌──────▼──────┐   │   │
│  │  │ChunkComplete│  │ChunkComplete│  │ChunkComplete│   │   │
│  │  └─────────────┘  └─────────────┘  └─────────────┘   │   │
│  │         │                │                │           │   │
│  │         └────────────────┼────────────────┘           │   │
│  │                          │                             │   │
│  │  ┌──────────────────────▼──────────────────────────┐ │   │
│  │  │ ChunksMerging ────▶ ChunksMerged                 │ │   │
│  │  │ (Deduplication, hash verification)               │ │   │
│  │  └──────────────────────────────────────────────────┘ │   │
│  └──────────────────────────────┬──────────────────────┘   │
│                                   │                          │
│  ┌──────────────────────────────▼──────────────────────┐   │
│  │         CompilationCompleted OR CompilationError      │   │
│  │         (Lock released, final validation)             │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## Event Types

### Lifecycle Events

| Event | Cancelable | Description |
|-------|------------|-------------|
| `CompilationStarted` | Yes | Compilation is about to begin |
| `ConfigurationLoaded` | No | Configuration file has been parsed |
| `CompilationCompleted` | No | Compilation finished successfully |
| `CompilationError` | No | An error occurred during compilation |

### Validation Events

| Event | Cancelable | Description |
|-------|------------|-------------|
| `Validation` | Yes (abort) | Zero-trust validation checkpoint |

### Source Events

| Event | Cancelable | Description |
|-------|------------|-------------|
| `SourceLoading` | Yes (skip) | A source is about to be loaded |
| `SourceLoaded` | No | A source has been loaded |

### File Lock Events

| Event | Cancelable | Description |
|-------|------------|-------------|
| `FileLockAcquired` | No | Read lock acquired on local file |
| `FileLockReleased` | No | Lock has been released |
| `FileLockFailed` | Yes (continue) | Lock could not be acquired |

### Chunk Events

| Event | Cancelable | Description |
|-------|------------|-------------|
| `ChunkStarted` | Yes (skip) | A chunk is about to be compiled |
| `ChunkCompleted` | No | A chunk has been compiled |
| `ChunksMerging` | No | Chunks are about to be merged |
| `ChunksMerged` | No | Chunks have been merged with deduplication |

## Zero-Trust Validation

The event pipeline implements zero-trust principles at each stage boundary:

### Validation Findings

```
┌────────────────────────────────────────────────────┐
│               VALIDATION SEVERITY LEVELS            │
├────────────────────────────────────────────────────┤
│                                                     │
│  INFO      │ Informational message, no action      │
│  WARNING   │ Potential issue, continue compilation │
│  ERROR     │ Problem found, compilation may fail   │
│  CRITICAL  │ Security issue, MUST abort            │
│                                                     │
└────────────────────────────────────────────────────┘
```

### Built-in Validation Codes

| Code | Severity | Description |
|------|----------|-------------|
| `ZT001` | Critical | Local file modified during compilation |
| `ZT002` | Critical | Source hash mismatch |
| `ZT003` | Error | Invalid source URL format |
| `ZT004` | Warning | Unencrypted HTTP source |
| `CFG001` | Error | Missing required field |
| `CFG002` | Error | Invalid transformation |
| `CFG003` | Warning | Deprecated configuration option |
| `RV001` | Warning/Error | `rules-validator` (native `rules_validator` library, #264) flagged the compiled output during syntax validation. Warning when the file is still syntactically valid but carries messages, Error when it isn't. Raised by `RulesCompilerService` via `IRulesValidatorService.ValidateLocalFileAsync`; skipped entirely when the native library isn't available (`IRulesValidatorService.IsAvailable == false`). |

## File Locking

Local source files are locked during compilation to prevent modification:

### Lock Types

- **Read Lock (Shared)** - Allows other readers, blocks writers
- **Write Lock (Exclusive)** - Blocks all other access

### Integrity Verification

1. Lock is acquired on local source file
2. SHA-256 hash is computed and stored
3. After compilation, hash is verified
4. If hash differs, `ZT001` critical finding is raised

This SHA-256 hash is a short-lived, in-memory TOCTOU check scoped to a single compilation run
- a different mechanism from, and a different algorithm than, the persisted SHA-384 hashes in
[HASH_VERIFICATION.md](HASH_VERIFICATION.md#why-file-locking-uses-sha-256-while-this-system-uses-sha-384),
which compare a file's state *across* runs via the `.hashes.json` sidecar. The split is intentional.

## Durability: Retry and Queueing (.NET)

Per the epic's "use queueing, Polly, etc for durability" ask (#274), the .NET
`CompilationEventDispatcher` and its optional `QueuedCompilationEventDispatcher` decorator add
two independent layers of resilience on top of the pipeline described above.

### Polly retry (always on)

Every individual handler invocation - across all 17 `Raise*Async` methods - runs through a
shared Polly `ResiliencePipeline`: up to 3 retries with exponential backoff and jitter, for
`IOException`, `TimeoutException`, and `HttpRequestException`. This absorbs transient faults in
I/O-bound handlers (a locked log file, a flaky download in a future source-fetching handler)
without masking genuine bugs - `OperationCanceledException` and other exception types are
deliberately excluded, so cancellation and real errors propagate immediately. This is unconditional:
it doesn't change any event's existing throw-vs-swallow behavior, only makes each handler call more
resilient to transient failures before that behavior kicks in.

### Background queueing (opt-in)

`QueuedCompilationEventDispatcher` decorates `ICompilationEventDispatcher`, splitting the 17
events into two groups:

- **Pipeline-critical events** - the ones whose `EventArgs` the pipeline inspects afterward to
  decide whether to continue (`Cancel`/`Abort`/`Skip`), or whose failure the pipeline must
  observe via a rethrown exception (`CompilationStarting`, `ConfigurationLoaded`, `Validation`,
  `SourceLoading`, `ChunkStarted`, `ChunksMerging`, `HashMismatch`) - are passed straight through
  synchronously. Queueing these would silently break the zero-trust abort semantics described
  above.
- **Fire-and-forget events** - the ones `CompilationEventDispatcher` already logs-and-continues
  on handler failure for (`SourceLoaded`, the three `FileLock*` events, `ChunkCompleted`,
  `ChunksMerged`, `CompilationCompleted`, `CompilationError`, `HashComputed`, `HashVerified`) -
  are enqueued onto an in-process `System.Threading.Channels.Channel` and processed by a single
  background consumer, so a slow handler (writing structured logs, updating a future progress UI)
  never blocks the compilation pipeline that raised the event.

This is opt-in, not the default, via `services.AddQueuedCompilationEventDispatching()` (call it
*after* `AddRulesCompiler()` - the last registration of `ICompilationEventDispatcher` wins).
Both `RulesCompiler.Console` and `Bloqr.Dashboard.Console` register it. Because the queue is
drained by a background task, callers that build their own `ServiceProvider` should dispose it
with `await using` (not a plain `using`) so `QueuedCompilationEventDispatcher.DisposeAsync()`
completes the channel and awaits any still-pending queued events before the process exits.

## Implementation Examples

### .NET

```csharp
using Bloqr.Compiler.Abstractions;

public class MyEventHandler : CompilationEventHandlerBase
{
    public override async Task OnCompilationStartingAsync(
        CompilationStartedEventArgs args,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Starting compilation: {args.Options.ConfigPath}");
    }

    public override async Task OnValidationAsync(
        ValidationEventArgs args,
        CancellationToken cancellationToken)
    {
        // Add custom validation
        if (args.StageName == "configuration")
        {
            // Validate something custom
            if (!IsValid())
            {
                args.AddError("CUSTOM001", "Custom validation failed");
            }
        }
    }

    public override async Task OnSourceLoadingAsync(
        SourceLoadingEventArgs args,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Loading source {args.SourceIndex + 1}/{args.TotalSources}: {args.Source.Source}");

        // Skip certain sources
        if (args.Source.Source.Contains("blocklist"))
        {
            args.Skip = true;
            args.SkipReason = "Blocklist sources skipped";
        }
    }

    public override async Task OnFileLockAcquiredAsync(
        FileLockAcquiredEventArgs args,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Lock acquired: {args.FilePath} (Hash: {args.ContentHash?[..16]}...)");
    }
}

// Register handler
services.AddCompilationEventHandler<MyEventHandler>();
```

### Python

```python
from rules_compiler import (
    CompilationEventHandler,
    EventDispatcher,
    ValidationEventArgs,
    SourceLoadingEventArgs,
    ValidationSeverity,
)

class MyHandler(CompilationEventHandler):
    async def on_compilation_starting(self, args):
        print(f"Starting compilation: {args.config_path}")

    async def on_validation(self, args):
        if args.stage_name == "configuration":
            # Add custom validation
            if not self.is_valid():
                args.add_error("CUSTOM001", "Custom validation failed")

    async def on_source_loading(self, args):
        print(f"Loading source {args.source_index + 1}/{args.total_sources}")

        # Skip certain sources
        if "blocklist" in args.source_url:
            args.skip = True
            args.skip_reason = "Blocklist sources skipped"

    async def on_file_lock_acquired(self, args):
        print(f"Lock acquired: {args.file_path}")

# Use the handler
dispatcher = EventDispatcher()
dispatcher.add_handler(MyHandler())

# Raise events during compilation
await dispatcher.raise_compilation_starting(CompilationStartedEventArgs())
```

### Rust

```rust
use rules_compiler::{
    CompilationEventHandler, EventDispatcher,
    CompilationStartedEventArgs, ValidationEventArgs, SourceLoadingEventArgs,
};

struct MyHandler;

impl CompilationEventHandler for MyHandler {
    fn on_compilation_starting(&self, args: &mut CompilationStartedEventArgs) {
        println!("Starting compilation: {:?}", args.config_path);
    }

    fn on_validation(&self, args: &mut ValidationEventArgs) {
        if args.stage_name == "configuration" {
            // Add custom validation
            if !self.is_valid() {
                args.add_error("CUSTOM001", "Custom validation failed");
            }
        }
    }

    fn on_source_loading(&self, args: &mut SourceLoadingEventArgs) {
        println!("Loading source {}/{}", args.source_index + 1, args.total_sources);

        // Skip certain sources
        if args.source_url.contains("blocklist") {
            args.skip = true;
            args.skip_reason = Some("Blocklist sources skipped".to_string());
        }
    }

    fn on_file_lock_acquired(&self, args: &FileLockAcquiredEventArgs) {
        println!("Lock acquired: {:?}", args.file_path);
    }
}

// Use the handler
let mut dispatcher = EventDispatcher::new();
dispatcher.add_handler(Box::new(MyHandler));

// Raise events during compilation
let mut args = CompilationStartedEventArgs::default();
dispatcher.raise_compilation_starting(&mut args);
```

## File Lock Service

### .NET

```csharp
// Inject via DI
var lockService = serviceProvider.GetRequiredService<IFileLockService>();

// Acquire lock with hash
await using var handle = await lockService.AcquireReadLockAsync(
    "local-rules.txt",
    computeHash: true);

Console.WriteLine($"Lock ID: {handle.LockId}");
Console.WriteLine($"Hash: {handle.ContentHash}");

// Do work with locked file...

// Verify integrity before release
var stillValid = await lockService.VerifyIntegrityAsync(
    "local-rules.txt",
    handle.ContentHash!);
```

### Python

```python
from rules_compiler import FileLockService

lock_service = FileLockService()

# Acquire lock with hash
async with await lock_service.acquire_read_lock("local-rules.txt") as handle:
    print(f"Lock ID: {handle.lock_id}")
    print(f"Hash: {handle.content_hash}")

    # Do work with locked file...

# Verify integrity
is_valid = await lock_service.verify_integrity(
    "local-rules.txt",
    expected_hash
)
```

### Rust

```rust
use rules_compiler::FileLockService;

let service = FileLockService::new();

// Acquire lock with hash
let handle = service.acquire_read_lock("local-rules.txt", true)?;
println!("Lock ID: {}", handle.lock_id);
println!("Hash: {:?}", handle.content_hash);

// Do work with locked file...

// Verify integrity
let is_valid = service.verify_integrity("local-rules.txt", &expected_hash)?;

// Lock is released when handle is dropped
```

## Best Practices

### 1. Always Validate at Boundaries

Raise validation events at each stage boundary to catch issues early:

```
Configuration Loaded → Validate configuration
Source Loading → Validate source URL/path
Chunk Completed → Validate chunk output
Compilation Completed → Validate final output
```

### 2. Use Appropriate Severity Levels

- **INFO**: Progress information
- **WARNING**: Non-blocking issues (e.g., HTTP sources)
- **ERROR**: Problems that may cause failures
- **CRITICAL**: Security issues that MUST abort

### 3. Lock Local Files

Always acquire read locks on local source files to prevent TOCTOU attacks:

```
1. Acquire lock
2. Compute hash
3. Read content
4. Verify hash unchanged
5. Release lock
```

### 4. Handle Lock Failures Gracefully

In `OnFileLockFailed`, decide whether to:
- Abort compilation (strict mode)
- Continue without lock (permissive mode)
- Retry with backoff

### 5. Log Event Activity

Enable debug logging to trace event flow:

```bash
# .NET
RULESCOMPILER_Logging__LogLevel__Default=Debug

# Python
LOG_LEVEL=DEBUG

# Rust
RUST_LOG=debug
```

## API Reference

See the language-specific API documentation:

- [.NET API Reference](../src/rules-compiler-dotnet/README.md)
- [Python API Reference](../src/rules-compiler-python/README.md)
- [Rust API Reference](../src/rules-compiler-rust/README.md)
