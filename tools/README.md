# Tools Directory

This directory contains utility scripts for building, testing, and maintaining the ad-blocking repository.

## Contents

### Build and Test Scripts

| Script | Purpose | Usage |
|--------|---------|-------|
| `test-build-scripts.sh` | Test suite for build scripts (Bash) | `./tools/test-build-scripts.sh` |
| `test-build-scripts.ps1` | Test suite for build scripts (PowerShell) | `pwsh tools/test-build-scripts.ps1` |
| `test-modules.ps1` | PowerShell module verification | `pwsh tools/test-modules.ps1` |

### Validation and Compliance

| Script | Purpose | Usage |
|--------|---------|-------|
| `check-validation-compliance.sh` | Verify validation library integration | `./tools/check-validation-compliance.sh` |

### Migration Scripts

| Script | Purpose | Usage |
|--------|---------|-------|
## Usage Examples

### Testing Build Scripts

The build script test suites verify functionality of `build.sh` and `build.ps1`:

```bash
# Run Bash tests (25+ unit and integration tests)
./tools/test-build-scripts.sh

# Run PowerShell tests
pwsh -File tools/test-build-scripts.ps1
```

**What's tested:**
- Help output and version display
- Argument parsing and validation
- Error handling
- Rust builds (debug and release)
- .NET builds (debug and release)
- Combined language ecosystem builds
- Profile configurations

### Testing PowerShell Modules

Verify PowerShell module imports and dependencies:

```powershell
pwsh -File tools/test-modules.ps1
```

**What's tested:**
- Legacy module imports (src/adguard-api-powershell)
- Modern module imports (src/powershell-modules)
- Function exports
- Module dependencies
- Version information

### Checking Validation Compliance

Verify that all rule compilers integrate the centralized validation library:

```bash
./tools/check-validation-compliance.sh
```

**What's checked:**
- TypeScript compiler integration
- .NET compiler integration
- Python compiler integration
- Rust compiler integration
- Integration test status

See [VALIDATION_ENFORCEMENT.md](../docs/VALIDATION_ENFORCEMENT.md) for requirements.

## Integration with CI/CD

These tools are integrated into GitHub Actions workflows:

### Build Scripts Tests Workflow
```yaml
# .github/workflows/build-scripts-tests.yml
- run: chmod +x tools/test-build-scripts.sh
- run: ./tools/test-build-scripts.sh

- run: pwsh -File tools/test-build-scripts.ps1
```

### Validation Compliance Workflow
```yaml
# .github/workflows/validation-compliance.yml
- run: chmod +x tools/check-validation-compliance.sh
- run: ./tools/check-validation-compliance.sh
```

## Adding New Tools

When adding new scripts to this directory:

1. **Use clear names** - Script name should describe its purpose
2. **Add documentation** - Include help text and examples
3. **Make executable** - `chmod +x` for shell scripts
4. **Test thoroughly** - Ensure works on all supported platforms
5. **Update this README** - Add entry to the tables above
6. **Consider CI integration** - Add to appropriate workflows

## File Organization

```
tools/
├── README.md                          # This file
├── test-build-scripts.sh              # Bash build script tests
├── test-build-scripts.ps1             # PowerShell build script tests
├── test-modules.ps1                   # PowerShell module tests
└── check-validation-compliance.sh     # Validation compliance check
```

## Related Documentation

- [Build Scripts](../README.md#build-all-projects) - Main build documentation
- [Testing Strategy](../README.md#testing) - Project-wide testing approach
- [VALIDATION_ENFORCEMENT.md](../docs/VALIDATION_ENFORCEMENT.md) - Validation requirements

## Support

For issues with these tools:
- Check the script's help text (`--help` or `-h`)
- Review related documentation
- Check CI workflow logs for usage examples
- Open an issue with the error output
