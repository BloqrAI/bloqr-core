# Runtime Enforcement of Validation Library

> **Implementation status note:** this document describes the aspirational design
> (a mandatory `compile_with_validation()` wrapper with signed metadata). The .NET
> compiler's actual current integration (#264) is lighter-weight: `BloqrCompilerService`
> calls `IBloqrValidatorService.ValidateLocalFileAsync` on the compiled output and raises
> the existing `ValidationEventArgs` (code `RV001`) through `ICompilationEventDispatcher`,
> the same zero-trust event pipeline documented in `docs/event-pipeline.md` — there is no
> separate `compile_with_validation()` entry point, signature, or audit-log format. A
> registered `ICompilationEventHandler` decides whether an `RV001` finding aborts the
> compilation (`ValidationEventArgs.Abort`); nothing currently enforces that a handler is
> registered. Closing that gap, and deciding whether to build the wrapper/signature design
> below or formally supersede it, is follow-up work beyond #264's scope.

## Overview

**Runtime enforcement** ensures that validation is **always** performed, even if a developer tries to bypass it. This is achieved through:

1. **Mandatory wrapper functions** that all compilers must use
2. **Validation metadata** embedded in compilation results
3. **Verification signatures** that prove validation occurred
4. **Runtime checks** that prevent unvalidated compilation

## Architecture

```
┌─────────────────────────────────────────┐
│  Compiler Code                          │
│  (TypeScript, .NET, Python, Rust)       │
└────────────────┬────────────────────────┘
                 │
                 │ MUST USE
                 ▼
┌─────────────────────────────────────────┐
│  compile_with_validation()              │
│  (Runtime Enforcement Wrapper)          │
│                                         │
│  ✓ Validates all local files           │
│  ✓ Validates all remote URLs           │
│  ✓ Creates validation metadata         │
│  ✓ Generates verification signature    │
│  ✓ Embeds proof in result               │
└────────────────┬────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│  @bloqr/compiler-core               │
│  (JSR package)                          │
└─────────────────────────────────────────┘
```

## How It Works

### 1. Mandatory Wrapper Function

All compilers **MUST** use `compile_with_validation()` instead of calling `@bloqr/compiler-core` directly:

**❌ FORBIDDEN - Direct compilation bypass:**
```typescript
// This bypasses validation and is NOT ALLOWED
const result = await hostlistCompiler.compile(config);
```

**✅ REQUIRED - Use enforcement wrapper:**
```typescript
import { compile_with_validation, CompilationInput } from '@adguard/validation';

const input = {
  local_files: ['../bloqr-blocklists/input/rules.txt'],
  remote_urls: ['https://example.com/list.txt'],
  expected_hashes: new Map([
    ['https://example.com/list.txt', 'sha384hash...']
  ])
};

const options = {
  validation_config: ValidationConfig.default(),
  output_path: '../bloqr-blocklists/output/filter.txt',
  create_archive: true
};

// This is the ONLY allowed way to compile
const result = await compile_with_validation(input, options);
```

### 2. Validation Metadata

Every compilation result includes **proof** that validation was performed:

```typescript
interface EnforcedCompilationResult {
  success: boolean;
  rule_count: number;
  output_hash: string;
  elapsed_ms: number;
  output_path: string;
  
  // PROOF OF VALIDATION
  validation_metadata: {
    validation_timestamp: string;           // When validation occurred
    local_files_validated: number;          // Count of files validated
    remote_urls_validated: number;          // Count of URLs validated
    hash_database_entries: number;          // Hash DB size
    validation_library_version: string;     // Version used
    strict_mode: boolean;                   // Security level
    archive_created: string | null;         // Archive path if created
    signature: string;                      // SHA-384 verification signature
  }
}
```

### 3. Verification Signature

The validation metadata includes a **cryptographic signature** that proves it wasn't forged:

```typescript
function generateSignature(metadata: ValidationMetadata): string {
  const data = `${metadata.validation_timestamp}:${metadata.local_files_validated}:${metadata.remote_urls_validated}:${metadata.validation_library_version}:${metadata.strict_mode}`;
  return sha384(data); // 96 hex characters
}
```

To verify a result:

```typescript
import { verify_compilation_was_validated } from '@adguard/validation';

// This throws an error if validation is missing or invalid
verify_compilation_was_validated(result);
```

### 4. Runtime Checks

The wrapper function performs these **mandatory** checks:

```typescript
function compile_with_validation(input, options) {
  const validator = new Validator(options.validation_config);
  const metadata = createMetadata();
  
  // CHECK 1: Validate all local files (CANNOT BE SKIPPED)
  for (const file of input.local_files) {
    const syntaxResult = validator.validate_local_file(file);
    if (!syntaxResult.is_valid) {
      throw new Error(`Validation failed: ${file}`);
    }
    metadata.local_files_validated++;
  }
  
  // CHECK 2: Validate all remote URLs (CANNOT BE SKIPPED)
  for (const url of input.remote_urls) {
    const urlResult = validator.validate_remote_url(url, expectedHash);
    if (!urlResult.is_valid) {
      throw new Error(`URL validation failed: ${url}`);
    }
    metadata.remote_urls_validated++;
  }
  
  // CHECK 3: Verify at least one source was validated
  if (metadata.local_files_validated === 0 && metadata.remote_urls_validated === 0) {
    throw new Error("No sources provided for compilation");
  }
  
  // Only AFTER validation passes, call the actual compiler
  const output = await hostlistCompiler.compile(...);
  
  // Generate verification signature
  metadata.signature = generateSignature(metadata);
  
  return { ...output, validation_metadata: metadata };
}
```

## Enforcement Mechanisms

### Mechanism 1: Type System Enforcement

**TypeScript:**
```typescript
// Return type forces inclusion of validation metadata
export function compile(config: Config): Promise<EnforcedCompilationResult> {
  // Can only return EnforcedCompilationResult which requires validation_metadata
  return compile_with_validation(input, options);
}
```

**.NET:**
```csharp
// Interface enforces validation metadata
public interface ICompilationResult {
    bool Success { get; }
    int RuleCount { get; }
    ValidationMetadata ValidationMetadata { get; } // REQUIRED
}
```

### Mechanism 2: CI/CD Verification

GitHub Actions verify that results include validation:

```yaml
- name: Verify compilation includes validation
  run: |
    # Run compilation
    OUTPUT=$(npm run compile --silent)
    
    # Verify validation metadata exists
    echo "$OUTPUT" | jq '.validation_metadata' || exit 1
    
    # Verify signature is present and correct length (96 chars)
    SIG=$(echo "$OUTPUT" | jq -r '.validation_metadata.signature')
    if [ ${#SIG} -ne 96 ]; then
      echo "Invalid validation signature"
      exit 1
    fi
    
    # Verify files were actually validated
    LOCAL_COUNT=$(echo "$OUTPUT" | jq '.validation_metadata.local_files_validated')
    REMOTE_COUNT=$(echo "$OUTPUT" | jq '.validation_metadata.remote_urls_validated')
    TOTAL=$((LOCAL_COUNT + REMOTE_COUNT))
    
    if [ $TOTAL -eq 0 ]; then
      echo "No sources were validated!"
      exit 1
    fi
```

### Mechanism 3: Integration Tests

Required test that verifies enforcement:

```typescript
describe('Runtime Enforcement', () => {
  test('compilation result must include validation metadata', async () => {
    const result = await compile(config);
    
    // Metadata must exist
    expect(result.validation_metadata).toBeDefined();
    
    // Must have validated at least one source
    const total = result.validation_metadata.local_files_validated + 
                  result.validation_metadata.remote_urls_validated;
    expect(total).toBeGreaterThan(0);
    
    // Signature must be valid SHA-384 (96 hex chars)
    expect(result.validation_metadata.signature).toMatch(/^[a-f0-9]{96}$/);
    
    // Verification must pass
    expect(() => verify_compilation_was_validated(result)).not.toThrow();
  });
  
  test('cannot forge validation metadata', async () => {
    const result = await compile(config);
    
    // Try to forge metadata
    const forged = {
      ...result,
      validation_metadata: {
        ...result.validation_metadata,
        local_files_validated: 0,  // Fake it
        remote_urls_validated: 0,  // Fake it
      }
    };
    
    // Verification should fail because signature won't match
    expect(() => verify_compilation_was_validated(forged)).toThrow();
  });
});
```

### Mechanism 4: Code Review Checklist

PR template includes:

```markdown
## Runtime Enforcement Checklist

- [ ] Uses `compile_with_validation()` wrapper
- [ ] Does NOT call `hostlist-compiler` directly
- [ ] Returns `EnforcedCompilationResult` type
- [ ] Includes validation metadata in output
- [ ] Verification test passes
- [ ] CI validation check passes
```

### Mechanism 5: Static Analysis

ESLint/Clippy rules detect bypass attempts:

```javascript
// .eslintrc.js
module.exports = {
  rules: {
    'no-restricted-imports': ['error', {
      patterns: [{
        group: ['@bloqr/compiler-core'],
        message: 'Do not import hostlist-compiler directly. Use compile_with_validation() from @adguard/validation instead.'
      }]
    }]
  }
};
```

## Language-Specific Implementation

### TypeScript

```typescript
import { 
  compile_with_validation,
  CompilationInput,
  CompilationOptions,
  EnforcedCompilationResult 
} from '@adguard/validation';

export async function compileRules(
  sources: Source[]
): Promise<EnforcedCompilationResult> {
  const input: CompilationInput = {
    local_files: sources.filter(s => s.isLocal()).map(s => s.path),
    remote_urls: sources.filter(s => s.isRemote()).map(s => s.url),
    expected_hashes: new Map(
      sources
        .filter(s => s.expectedHash)
        .map(s => [s.url, s.expectedHash!])
    )
  };
  
  const options: CompilationOptions = {
    validation_config: getValidationConfig(),
    output_path: getOutputPath(),
    create_archive: shouldCreateArchive()
  };
  
  return compile_with_validation(input, options);
}
```

### .NET

```csharp
using AdGuard.Validation;

public class BloqrCompiler {
    public async Task<EnforcedCompilationResult> CompileAsync(
        IEnumerable<Source> sources) {
        
        var input = new CompilationInput {
            LocalFiles = sources.Where(s => s.IsLocal).Select(s => s.Path).ToList(),
            RemoteUrls = sources.Where(s => !s.IsLocal).Select(s => s.Url).ToList(),
            ExpectedHashes = sources
                .Where(s => s.ExpectedHash != null)
                .ToDictionary(s => s.Url, s => s.ExpectedHash)
        };
        
        var options = new CompilationOptions {
            ValidationConfig = GetValidationConfig(),
            OutputPath = GetOutputPath(),
            CreateArchive = ShouldCreateArchive()
        };
        
        return await ValidationLibrary.CompileWithValidation(input, options);
    }
}
```

### Python

```python
from bloqr_validator import (
    compile_with_validation,
    CompilationInput,
    CompilationOptions,
    verify_compilation_was_validated
)

def compile_rules(sources: list[Source]) -> EnforcedCompilationResult:
    input_data = CompilationInput(
        local_files=[s.path for s in sources if s.is_local],
        remote_urls=[s.url for s in sources if not s.is_local],
        expected_hashes={
            s.url: s.expected_hash 
            for s in sources 
            if s.expected_hash
        }
    )
    
    options = CompilationOptions(
        validation_config=get_validation_config(),
        output_path=get_output_path(),
        create_archive=should_create_archive()
    )
    
    result = compile_with_validation(input_data, options)
    
    # Verify before returning
    verify_compilation_was_validated(result)
    
    return result
```

### Rust

```rust
use bloqr_validator::{
    compile_with_validation,
    CompilationInput,
    CompilationOptions,
    EnforcedCompilationResult,
};

pub fn compile_rules(sources: &[Source]) -> Result<EnforcedCompilationResult> {
    let input = CompilationInput {
        local_files: sources
            .iter()
            .filter(|s| s.is_local())
            .map(|s| s.path.clone())
            .collect(),
        remote_urls: sources
            .iter()
            .filter(|s| !s.is_local())
            .map(|s| s.url.clone())
            .collect(),
        expected_hashes: sources
            .iter()
            .filter_map(|s| s.expected_hash.as_ref().map(|h| (s.url.clone(), h.clone())))
            .collect(),
    };
    
    let options = CompilationOptions {
        validation_config: get_validation_config(),
        output_path: get_output_path(),
        create_archive: should_create_archive(),
    };
    
    compile_with_validation(input, options)
}
```

## Audit Trail

Every compilation creates an audit log:

```json
{
  "timestamp": "2024-12-27T10:30:00Z",
  "compiler": "typescript",
  "compiler_version": "1.0.0",
  "validation_library_version": "1.0.0",
  "sources_validated": {
    "local": 5,
    "remote": 3
  },
  "validation_mode": "strict",
  "output_hash": "abc123...",
  "validation_signature": "def456...",
  "archive_created": "../bloqr-blocklists/archive/2024-12-27_10-30-00"
}
```

## Bypassing Prevention

### What Happens if Someone Tries to Bypass?

**Scenario 1: Direct compilation**
```typescript
// Attempt to bypass
const result = await hostlistCompiler.compile(config);
```

**Prevention:**
- ESLint error: "Do not import hostlist-compiler directly"
- TypeScript error: Return type mismatch (no validation_metadata)
- CI fails: "Validation metadata missing from result"
- Code review: Automatic rejection

**Scenario 2: Fake metadata**
```typescript
const result = {
  ...compilerOutput,
  validation_metadata: { /* fake data */ }
};
```

**Prevention:**
- Signature verification fails
- `verify_compilation_was_validated()` throws error
- CI verification step fails
- Audit log shows mismatch

**Scenario 3: Skip validation in wrapper**
```typescript
function compile_with_validation() {
  // Skip actual validation
  return { validation_metadata: makeFakeMetadata() };
}
```

**Prevention:**
- Integration tests fail (no actual files validated)
- Signature won't match expected pattern
- Code review catches missing validation calls
- Function is in external library (can't be modified)

## Summary

Runtime enforcement ensures validation **cannot** be bypassed through:

1. **Mandatory wrapper** - Only entry point for compilation
2. **Cryptographic signatures** - Proof of validation that can't be forged
3. **Type system** - Compiler enforces validation metadata presence
4. **CI/CD checks** - Automated verification of every compilation
5. **Integration tests** - Tests verify validation occurred
6. **Code review** - Human verification of compliance
7. **Static analysis** - Lint rules prevent direct compiler access
8. **Audit logs** - Permanent record of all compilations

**Result**: It's easier to do validation correctly than to bypass it.
