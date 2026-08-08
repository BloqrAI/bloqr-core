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

let result = compile_rules_with_events("config.yaml", &options, &dispatcher)?;
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
  configPath: 'config.yaml',
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
```csharp
// To be implemented in compilation pipeline
```

### Python

To be implemented (similar pattern to Rust/TypeScript)

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
- Hash database persistence across compilations
- Historical hash tracking and drift detection
- Integration with external validation services
- Support for multiple hash algorithms (SHA-256, BLAKE3)
- Signature verification (GPG, minisign)
