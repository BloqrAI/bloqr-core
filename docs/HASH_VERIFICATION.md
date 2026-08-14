# Hash Verification at Each Compilation Stage

This document explains the hash verification callback system implemented across all compilers to enforce integrity checks at each stage of the compilation pipeline.

## Overview

The hash verification system provides **cryptographic proof** that files are not tampered with at any stage of compilation. Client code can subscribe to hash events to:

- **Log all hash computations** for audit trails
- **Enforce custom verification policies** (strict vs. permissive)
- **Detect tampering** in real-time
- **Track file integrity** across the pipeline
- **Prevent MITM attacks** on downloaded sources

## Architecture

### Compilation Stages with Hash Verification

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Configuration File Loading                               │
│    ├─ Hash computed: config_file                           │
│    └─ Event fired: HashComputed                            │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. Source Files Loading (Local & Remote)                    │
│    ├─ Hash computed: input_file / downloaded_source        │
│    ├─ Event fired: HashComputed                            │
│    └─ Optional verification against expected hash          │
│         ├─ Match → Event: HashVerified                     │
│         └─ Mismatch → Event: HashMismatch (can abort)      │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. Compilation (via @bloqr/compiler-core)                │
│    └─ (No hash events - external tool)                     │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. Output File Writing                                      │
│    ├─ Hash computed: output_file                           │
│    └─ Event fired: HashComputed                            │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ 5. Rules File Copying (if requested)                        │
│    ├─ Hash computed: copied_rules_file                     │
│    ├─ Event fired: HashComputed                            │
│    └─ Can verify against output hash                       │
│         ├─ Match → Event: HashVerified                     │
│         └─ Mismatch → Event: HashMismatch                  │
└─────────────────────────────────────────────────────────────┘
```

## Event Types

### 1. Hash Computed Event

Fired whenever a hash is computed for any file.

**Data:**
- `itemIdentifier`: File path or identifier
- `itemType`: Type (e.g., "config_file", "output_file", "input_file")
- `hash`: SHA-384 hash (96 hex characters)
- `sizeBytes`: File size
- `isVerification`: Whether this is for verification purposes

### 2. Hash Verified Event

Fired when a hash successfully matches the expected value.

**Data:**
- `itemIdentifier`: File path or identifier
- `itemType`: Type of file
- `expectedHash`: Expected SHA-384 hash
- `actualHash`: Computed SHA-384 hash (should match expected)
- `sizeBytes`: File size
- `computationDurationMs`: Time taken to compute hash

### 3. Hash Mismatch Event

Fired when a hash does NOT match the expected value.

**Data:**
- `itemIdentifier`: File path or identifier
- `itemType`: Type of file
- `expectedHash`: Expected SHA-384 hash
- `actualHash`: Computed SHA-384 hash (different from expected)
- `sizeBytes`: File size
- `abort`: Whether to abort compilation (default: true)
- `abortReason`: Reason for aborting
- `allowContinuation`: Handler can set this to continue despite mismatch

**Handler Control:**
- Set `allowContinuation = true` to continue despite mismatch
- Set `abort = false` to prevent compilation failure

## Implementation by Language

### Rust

**Event Handler Trait:**
```rust
use rules_compiler::events::{
    CompilationEventHandler,
    HashComputedEventArgs,
    HashVerifiedEventArgs,
    HashMismatchEventArgs,
};

struct MyHandler;

impl CompilationEventHandler for MyHandler {
    fn on_hash_computed(&self, args: &HashComputedEventArgs) {
        println!("Hash for {}: {}", args.item_type, &args.hash[..16]);
    }

    fn on_hash_verified(&self, args: &HashVerifiedEventArgs) {
        println!("Hash verified for {}", args.item_identifier);
    }

    fn on_hash_mismatch(&self, args: &mut HashMismatchEventArgs) {
        eprintln!("Hash mismatch for {}", args.item_identifier);
        // Optionally allow continuation:
        // args.allow_continuation = true;
        // args.abort = false;
    }
}
```

**Usage:**
```rust
use rules_compiler::{compile_rules_with_events, EventDispatcher};

let mut dispatcher = EventDispatcher::new();
dispatcher.add_handler(Box::new(MyHandler));

let result = compile_rules_with_events("config.json", &options, &dispatcher)?;
```

### TypeScript

**Callback Interface:**
```typescript
import type { HashVerificationCallbacks } from './types.ts';

const callbacks: HashVerificationCallbacks = {
  onHashComputed: (event) => {
    console.log(`Hash for ${event.itemType}: ${event.hash.slice(0, 16)}...`);
  },

  onHashVerified: (event) => {
    console.log(`Hash verified for ${event.itemIdentifier}`);
  },

  onHashMismatch: (event) => {
    console.error(`Hash mismatch for ${event.itemIdentifier}`);
    // Optionally allow continuation:
    // event.allowContinuation = true;
  },
};
```

**Usage:**
```typescript
import { runCompiler } from './compiler.ts';

const result = await runCompiler({
  configPath: 'config.json',
  hashCallbacks: callbacks,
});
```

### .NET

**Event Handler:**
```csharp
using Bloqr.Compiler.Abstractions;

public class MyHashHandler : CompilationEventHandlerBase
{
    public override Task OnHashComputedAsync(
        HashComputedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Hash for {args.ItemType}: {args.Hash.Substring(0, 16)}...");
        return Task.CompletedTask;
    }

    public override Task OnHashVerifiedAsync(
        HashVerifiedEventArgs args,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Hash verified for {args.ItemIdentifier}");
        return Task.CompletedTask;
    }

    public override Task OnHashMismatchAsync(
        HashMismatchEventArgs args,
        CancellationToken cancellationToken = default)
    {
        Console.Error.WriteLine($"Hash mismatch for {args.ItemIdentifier}");
        // Optionally allow continuation:
        // args.AllowContinuation = true;
        // args.Abort = false;
        return Task.CompletedTask;
    }
}
```

**Usage:**

Handlers are registered via `AddCompilationEventHandler<T>()` on the same `IServiceCollection`
that `AddRulesCompiler()` populates. `RulesCompilerService.RunAsync` raises the three hash
events at each stage from the diagram above (config file, output file, and - if `--copy` /
`CopyToRules` is used - the copied rules file), driven by the `hashVerification` block of
the compiler configuration:

```csharp
services.AddRulesCompiler();
services.AddCompilationEventHandler<MyHashHandler>();
```

```json
{
  "hashVerification": {
    "mode": "warning",
    "requireHashesForRemote": false,
    "failOnMismatch": false,
    "hashDatabasePath": "../bloqr-blocklists/input/.hashes.json"
  }
}
```

- **`mode: "disabled"`** skips the sidecar entirely - hashes are still computed and the
  `HashComputed` event still fires (for the audit trail), but nothing is checked or recorded
  in the `.hashes.json` database.
- **`mode: "warning"`** (the schema default) checks the item's hash against the database. A
  first-ever run for a given item bootstraps trust: there is nothing to compare against yet,
  so the computed hash is simply recorded. On a later run, a match raises `HashVerified`; a
  mismatch raises `HashMismatch` with `Abort = false` / `AllowContinuation = true` already set
  - compilation continues and the new hash becomes the trusted baseline - unless a registered
    handler explicitly re-tightens `Abort`/`AllowContinuation` on the event args.
- **`mode: "strict"`** (or `failOnMismatch: true` under any mode) raises `HashMismatch` with
  the constructor's default `Abort = true` / `AllowContinuation = false` left in place, so
  `RunAsync` returns a failed `CompilerResult` unless a handler explicitly opts back in.

Only the **config file**, **output file**, and **copied rules file** stages are implemented
this way today. Per-source verification of local and remote inputs (`requireHashesForRemote`)
is not yet wired up: those files are fetched by the external `@bloqr/compiler-core` Deno CLI
process that `FilterCompiler` shells out to, not read directly by .NET, so verifying them
would require either re-implementing source fetching in .NET or having that CLI report
per-source hashes back over stdout/JSON for `RulesCompilerService` to parse. Tracked as
follow-up work.

### `.hashes.json` sidecar format

The database at `hashVerification.hashDatabasePath` (resolved relative to the config file's
directory) is a flat JSON object keyed by item path:

```json
{
  "/abs/path/to/output/adguard_user_filter.txt": {
    "hash": "3f8a1c...(96 hex chars)...",
    "sizeBytes": 48213,
    "computedAt": "2026-08-10T12:34:56.789Z",
    "itemType": "output_file"
  }
}
```

This file, not a hash embedded in the output itself, is the primary trust mechanism: an
embedded hash would have to be recomputed on every manual edit to stay meaningful, while the
sidecar records what the file looked like the last time the compiler verified it.

### Output conflict strategy and archiving

`RulesCompilerService.RunAsync` also applies the config's `output` and `archiving` blocks (via
`IOutputPublisher`) immediately after a successful compile and before the output-file hash
stage, so the recorded/verified hash always describes the file at its durable, published path:

```json
{
  "output": { "path": "../bloqr-blocklists/output/adguard_user_filter.txt", "conflictStrategy": "rename" },
  "archiving": { "enabled": true, "mode": "automatic", "retentionDays": 90 }
}
```

`conflictStrategy` (matching `schemas/compiler-config.schema.json`'s enum exactly - the
`rename`/`overwrite`/`error` values below, not the `rename`/archive/replace prose that
appeared in an earlier draft of this feature's tracking issue):

- **`rename`** (default) - the existing file at `output.path` is left untouched; the new
  output is written alongside it as `name_1.ext`, `name_2.ext`, etc.
- **`overwrite`** - the existing file is replaced. If `archiving.enabled` is `true`, it is
  moved into an `archive/` subdirectory next to `output.path` first, timestamped, and entries
  in that subdirectory older than `archiving.retentionDays` are pruned.
- **`error`** - compilation fails with a clear message instead of touching the existing file.

### Python

**Event Handler:**
```python
from rules_compiler.events import (
    CompilationEventHandler,
    HashComputedEventArgs,
    HashVerifiedEventArgs,
    HashMismatchEventArgs,
)

class MyHashHandler(CompilationEventHandler):
    async def on_hash_computed(self, args: HashComputedEventArgs) -> None:
        print(f"Hash for {args.item_type}: {args.hash[:16]}...")

    async def on_hash_verified(self, args: HashVerifiedEventArgs) -> None:
        print(f"Hash verified for {args.item_identifier}")

    async def on_hash_mismatch(self, args: HashMismatchEventArgs) -> None:
        print(f"Hash mismatch for {args.item_identifier}")
        # Optionally allow continuation:
        # args.allow_continuation = True
        # args.abort = False
```

**Usage:**

`compile_rules`/`compile_rules_async` (and `RulesCompiler.compile`/`compile_async`) accept an
optional `event_dispatcher` parameter; the three hash events are raised at each stage from the
diagram above (config file, output file, and - if `copy_to_rules` is used - the copied rules
file), driven by the `hash_verification` block of the compiler configuration. Hash
recording/verification against the `.hashes.json` sidecar happens regardless of whether a
dispatcher is supplied - the dispatcher only adds observability, matching every mode's default
behavior when no handler overrides it.

```python
from rules_compiler.compiler import compile_rules
from rules_compiler.events import EventDispatcher

dispatcher = EventDispatcher()
dispatcher.add_handler(MyHashHandler())

result = compile_rules("config.json", event_dispatcher=dispatcher)
```

```json
{
  "hashVerification": {
    "mode": "warning",
    "requireHashesForRemote": false,
    "failOnMismatch": false,
    "hashDatabasePath": "../bloqr-blocklists/input/.hashes.json"
  }
}
```

Mode semantics, output conflict strategy, and archiving behave identically to the .NET
implementation described above - see `rules_compiler.hash_database` and
`rules_compiler.output_publisher` for the sidecar and publishing implementations. As with
.NET, only the config file, output file, and copied rules file stages are implemented today;
per-source verification of local and remote inputs is tracked as follow-up work.

## Use Cases

### 1. Audit Trail Logging

```rust
impl CompilationEventHandler for AuditLogger {
    fn on_hash_computed(&self, args: &HashComputedEventArgs) {
        self.log(format!(
            "AUDIT: {} hash={} size={} timestamp={}",
            args.item_type,
            args.hash,
            args.size_bytes,
            chrono::Utc::now()
        ));
    }
}
```

### 2. Strict Zero-Trust Verification

```rust
impl CompilationEventHandler for StrictVerifier {
    fn on_hash_mismatch(&self, args: &mut HashMismatchEventArgs) {
        // Never allow continuation on mismatch
        args.abort = true;
        args.allow_continuation = false;
        self.alert_security_team(args);
    }
}
```

### 3. Permissive Development Mode

```rust
impl CompilationEventHandler for DevModeHandler {
    fn on_hash_mismatch(&self, args: &mut HashMismatchEventArgs) {
        // Log but don't fail in development
        eprintln!("WARN: Hash mismatch but allowing continuation in dev mode");
        args.allow_continuation = true;
        args.abort = false;
    }
}
```

### 4. Database Tracking

```rust
impl CompilationEventHandler for DatabaseTracker {
    fn on_hash_computed(&self, args: &HashComputedEventArgs) {
        self.db.insert_hash_record(
            args.item_identifier.clone(),
            args.hash.clone(),
            chrono::Utc::now(),
        );
    }
}
```

## Security Considerations

1. **SHA-384 Algorithm**: All hashes use SHA-384 (96 hex characters) for cryptographic strength
2. **At-Rest Verification**: Local files are hashed to detect tampering
3. **In-Flight Verification**: Downloaded sources can be verified against expected hashes
4. **MITM Prevention**: Hash verification on downloads prevents man-in-the-middle attacks
5. **Immutable Audit Trail**: Hash events create an immutable log of all file states

### Why file locking uses SHA-256 while this system uses SHA-384

This is intentional, not accidental drift: the two hashes protect against different threats.
`IFileLockService` (see [event-pipeline.md](event-pipeline.md#file-locking)) uses SHA-256
purely to detect whether a locked local source file changed *during a single compilation run*
(a TOCTOU check) - the hash never leaves process memory, isn't persisted, and is compared
moments later, so SHA-256's collision resistance is more than sufficient and its lower
computational cost matters more since it runs on every locked file every run. The
hash-verification system in this document persists hashes to disk in the `.hashes.json`
sidecar and compares them *across* runs, potentially long after the fact, as an audit trail
and tamper-evidence record - a higher-stakes, longer-lived use case that warrants SHA-384's
larger margin. `.NET`'s `OutputWriter.ComputeHashAsync` (the SHA-384 implementation feeding
this document's events) and `FileLockService`'s internal SHA-256 computation are deliberately
separate code paths for this reason; neither should be changed to match the other.

## Testing

Example tests are included in `examples/hash_audit_handler.rs` demonstrating:
- Strict verification (fails on mismatch)
- Permissive verification (logs but continues)
- Custom policy implementation

## Example Handler

See `examples/hash_audit_handler.rs` for a complete implementation of:
- Logging all hash events
- Customizable strictness (strict vs. permissive)
- Comprehensive test coverage

## Future Enhancements

Potential additions:
- ~~Hash database persistence across compilations~~ - implemented for .NET via the
  `.hashes.json` sidecar and `IHashDatabaseService`, and for Python via
  `rules_compiler.hash_database`.
- Per-source (local and remote input) hash verification, requiring either re-implementing
  source fetching in .NET/Python or having `@bloqr/compiler-core` report per-source hashes back
- Historical hash tracking and drift detection
- Integration with external validation services
- Support for multiple hash algorithms (SHA-256, BLAKE3)
- Signature verification (GPG, minisign)
