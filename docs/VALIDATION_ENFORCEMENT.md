# Validation Library Integration Requirements

This document defines the **mandatory** integration requirements for all rules compilers to ensure consistent security validation across the codebase.

## Enforcement Strategy

### 1. CI/CD Enforcement

All compilers **must** pass integration tests that verify they're using the validation library:

```yaml
# .github/workflows/validation-compliance.yml
name: Validation Compliance

on: [push, pull_request]

jobs:
  typescript-compliance:
    runs-on: ubuntu-latest
    steps:
      - name: Check TypeScript uses validation library
        run: |
          cd src/adblock-compiler-core
          # Verify WASM module is imported
          grep -q "bloqr_validator" package.json || exit 1
          grep -q "validate_local_file\|validate_remote_url" src/**/*.ts || exit 1
  
  dotnet-compliance:
    runs-on: ubuntu-latest
    steps:
      - name: Check .NET uses validation library
        run: |
          cd src/compilers/dotnet
          # Verify native library is referenced
          grep -q "bloqr_validator" src/**/*.csproj || exit 1
          grep -rq "ValidationLibrary\|P/Invoke" src/ || exit 1
  
  # Similar checks for Python and Rust compilers
```

### 2. Runtime Verification

Each compiler must implement a `--validate-integration` flag that verifies the validation library is properly integrated:

```bash
# All compilers must support this
npm run compile -- --validate-integration
dotnet run --project Bloqr.Compiler.Dotnet.Console -- --validate-integration
rules-compiler --validate-integration
cargo run --release -- --validate-integration
```

This flag should:
1. Attempt to load the validation library
2. Run a simple validation test
3. Exit with code 0 if successful, 1 if failed

### 3. Compilation Hook

All compilers **must** call validation library functions at these points:

#### Required Integration Points

**Before Compilation**:
```typescript
// TypeScript example (all compilers must have equivalent)
import { Validator, ValidationConfig, VerificationMode } from '@adguard/validation';

async function compile(config: CompilerConfig): Promise<CompilerResult> {
  const validator = new Validator(ValidationConfig.default());
  
  // 1. MANDATORY: Validate all local input files
  for (const localFile of getLocalInputFiles()) {
    const result = await validator.validate_local_file(localFile);
    if (!result.is_valid) {
      throw new CompilationError(`Invalid input file: ${localFile}`);
    }
  }
  
  // 2. MANDATORY: Validate all remote URLs
  for (const url of getRemoteUrls()) {
    const result = await validator.validate_remote_url(url, getExpectedHash(url));
    if (!result.is_valid) {
      throw new CompilationError(`Invalid remote URL: ${url}`);
    }
  }
  
  // 3. Proceed with compilation using @bloqr/compiler-core
  const output = await hostlistCompiler.compile(config);
  
  // 4. MANDATORY: Handle file conflicts
  const finalPath = await resolveConflict(output.path, config.conflictStrategy);
  
  // 5. MANDATORY: Create archive if enabled
  if (config.archiving.enabled) {
    await createArchive(inputDir, archiveDir, output.hash, output.ruleCount);
  }
  
  return output;
}
```

### 4. Test Requirements

Each compiler **must** have integration tests that verify:

#### Test 1: Local File Validation
```typescript
// Test that compilation fails if local file has invalid syntax
test('rejects invalid local file', async () => {
  const invalidFile = 'test-data/invalid-syntax.txt';
  await expect(compile(configWithFile(invalidFile))).rejects.toThrow();
});
```

#### Test 2: Hash Verification
```typescript
// Test that compilation detects tampered files
test('detects file tampering via hash', async () => {
  // First compile creates hash
  await compile(config);
  
  // Modify file
  modifyFile('../bloqr-blocklists/input/rules.txt');
  
  // Second compile should detect tampering
  await expect(compile(configWithStrictMode)).rejects.toThrow(/hash mismatch/i);
});
```

#### Test 3: URL Security
```typescript
// Test that HTTP URLs are rejected
test('rejects insecure HTTP URLs', async () => {
  const httpUrl = 'http://insecure.example.com/list.txt';
  await expect(compile(configWithUrl(httpUrl))).rejects.toThrow(/HTTPS/i);
});
```

#### Test 4: Archive Creation
```typescript
// Test that archives are created when enabled
test('creates archive when enabled', async () => {
  const config = { ...baseConfig, archiving: { enabled: true } };
  await compile(config);
  
  expect(fs.existsSync('../bloqr-blocklists/archive')).toBe(true);
  const archives = fs.readdirSync('../bloqr-blocklists/archive');
  expect(archives.length).toBeGreaterThan(0);
});
```

### 5. Configuration Validation

All compilers must validate that configuration includes validation settings:

```typescript
function validateConfig(config: CompilerConfig): void {
  // MANDATORY: hashVerification section must exist
  if (!config.hashVerification) {
    throw new Error('Missing hashVerification configuration');
  }
  
  // MANDATORY: Must specify verification mode
  if (!['strict', 'warning', 'disabled'].includes(config.hashVerification.mode)) {
    throw new Error('Invalid hashVerification.mode');
  }
  
  // Additional validation...
}
```

### 6. Dependency Declaration

Each compiler's dependency file **must** declare the validation library:

**TypeScript (package.json)**:
```json
{
  "dependencies": {
    "@adguard/validation": "file:../validation/pkg"
  }
}
```

**\.NET (csproj)**:
```xml
<ItemGroup>
  <NativeLibraryReference Include="bloqr_validator" />
</ItemGroup>
```

**Python (requirements.txt)**:
```
bloqr-validator>=1.0.0
```

**Rust (Cargo.toml)**:
```toml
[dependencies]
bloqr_validator = { path = "../validation/core", package = "bloqr-validator-core" }
```

### 7. Pre-commit Hooks

A pre-commit hook verifies validation library integration:

```bash
#!/bin/bash
# .git/hooks/pre-commit

echo "Checking validation library integration..."

# Check TypeScript
if ! grep -q "bloqr_validator" src/adblock-compiler-core/package.json; then
  echo "ERROR: TypeScript compiler missing validation library dependency"
  exit 1
fi

# Check .NET
if ! grep -q "bloqr_validator" src/compilers/dotnet/src/**/*.csproj; then
  echo "ERROR: .NET compiler missing validation library reference"
  exit 1
fi

# Similar checks for Python and Rust
echo "✓ All compilers properly integrated with validation library"
```

### 8. Documentation Requirements

Each compiler's README must include:

1. **Integration Status** section showing validation library usage
2. **Security Features** section listing which validations are performed
3. **Configuration** section documenting validation settings
4. Code examples showing validation in action

### 9. Pull Request Template

PR template includes validation library checklist:

```markdown
## Validation Library Integration

- [ ] Uses validation library for file validation
- [ ] Uses validation library for URL security
- [ ] Implements hash verification
- [ ] Creates archives when enabled
- [ ] Handles file conflicts
- [ ] Integration tests pass
- [ ] Documentation updated
```

### 10. Breaking Changes Policy

**Any PR that bypasses the validation library is automatically rejected.**

Exceptions require:
1. Written justification
2. Security team approval
3. Alternative security measures documented
4. Temporary exemption with deadline

## Verification Commands

Maintainers can verify compliance with:

```bash
# Run compliance check across all compilers
./tools/check-validation-compliance.sh

# Expected output:
# ✓ TypeScript: Validation library integrated
# ✓ .NET: Validation library integrated
# ✓ Python: Validation library integrated
# ✓ Rust: Validation library integrated
# ✓ All integration tests passing
```

## Penalties for Non-Compliance

1. **CI fails** - PR cannot be merged
2. **Code review rejection** - Automatic "Request Changes"
3. **Security alert** - Flagged in security dashboard
4. **Rollback** - Non-compliant code reverted if merged

## Migration Timeline

- **Phase 1**: Validation library created, documented.
- **Phase 2**: Integrate into .NET compiler. **Done** (#264) — `Bloqr.Compiler.Core.Services.BloqrValidatorService`
  P/Invokes `bloqr_validator_{new,validate_local_file,validate_remote_url,free,free_string}`
  (see `src/validation/core/README.md`'s ".NET / C#" section for the P/Invoke reference this
  implementation follows). `BloqrCompilerService` runs syntax validation on the compiled output
  and raises `ValidationEventArgs` (code `RV001`) through the existing zero-trust event pipeline
  (see `docs/event-pipeline.md`); handlers may set `Abort = true` to fail the compilation. The
  Dashboard's Diagnostics menu reports native-library availability and offers a standalone
  "Validate a filter file" action. The native library is not yet packaged/deployed alongside the
  .NET output automatically — that's #276 — so `IBloqrValidatorService.IsAvailable` degrades to
  `false` gracefully wherever the library isn't found rather than failing compilation.
- **Phase 3**: Integrate into TypeScript, Python, Rust compilers.
- **Phase 4**: Enable CI enforcement (warnings only).
- **Phase 5**: Enable CI enforcement (blocking).
- **Phase 6**: Remove legacy validation code.

## Support

Questions about integration? See:
- `src/validation/README.md` - Full integration guide
- `docs/validation-integration-guide.md` - Step-by-step tutorial
- GitHub Discussions - Ask the community
