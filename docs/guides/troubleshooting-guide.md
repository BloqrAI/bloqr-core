# Troubleshooting Guide

Comprehensive guide for troubleshooting common issues across all components in the ad-blocking repository.

## Overview

This guide provides solutions for common issues you may encounter with the rules compilers, API clients, testing, deployment, and configuration.

## Quick Diagnostic Checklist

Before diving into specific issues, run through this checklist:

- [ ] Are all prerequisites installed? (Deno, .NET, Python, Rust, PowerShell)
- [ ] Are you on the correct versions? (Deno 2.0+, .NET 10+, Python 3.9+, Rust 1.70+, PowerShell 7+)
- [ ] Have you cleared caches? (Deno cache, NuGet, pip, cargo)
- [ ] Are environment variables set correctly?
- [ ] Do you have network access (for downloading filter lists)?
- [ ] Are file permissions correct?
- [ ] Have you pulled the latest changes?

## TypeScript / Deno Issues

### Deno Not Found

**Symptom:** `deno: command not found`

**Solution:**

```bash
# Check if Deno is installed
deno --version

# If not installed, install it
# macOS/Linux
curl -fsSL https://deno.land/install.sh | sh

# Windows (PowerShell)
irm https://deno.land/install.ps1 | iex

# Add to PATH
# Linux/macOS - add to ~/.bashrc or ~/.zshrc
export PATH="$HOME/.deno/bin:$PATH"

# Windows - add to PATH via System Environment Variables
# or temporarily in PowerShell:
$env:PATH += ";$HOME\.deno\bin"
```

### Module Not Found Errors

**Symptom:** `error: Module not found` or `Cannot resolve module`

**Solution:**

```bash
# Clear Deno cache
rm -rf ~/.cache/deno  # Linux/macOS
Remove-Item -Recurse -Force $env:LOCALAPPDATA\deno  # Windows

# Re-cache dependencies
deno cache src/mod.ts

# Or cache with --reload flag
deno cache --reload src/mod.ts
```

### Permission Denied Errors

**Symptom:** `PermissionDenied: Requires read access to...`

**Solution:**

```bash
# Grant specific permissions
deno run --allow-read --allow-write --allow-net --allow-run src/mod.ts

# Or grant all permissions (development only)
deno run -A src/mod.ts

# For production, use explicit permissions in deno.json
{
  "tasks": {
    "compile": "deno run --allow-read --allow-write --allow-net --allow-run src/mod.ts"
  }
}
```

### adblock-compiler Not Found

**Symptom:** `jsr:@bloqr/compiler-core not found`

**Solution:**

```bash
# Test JSR specifier
deno run --allow-all jsr:@bloqr/compiler-core --version

# If that works, the issue is with your configuration
# Check deno.json for correct JSR imports

# Ensure Deno version is 2.0+
deno upgrade

# Clear cache and retry
deno cache --reload src/mod.ts
```

### Test Failures

**Symptom:** Tests fail with resource leaks or timeouts

**Solution:**

```bash
# Run tests with trace-leaks to find resource issues
deno test --allow-all --trace-leaks tests/

# Increase timeout for slow tests
deno test --allow-all --timeout=60000 tests/

# Run tests sequentially
deno test --allow-all --parallel=1 tests/

# Check for unclosed resources (files, network connections)
# Ensure all async operations are properly awaited
```

## .NET Issues

### .NET SDK Not Found

**Symptom:** `The command 'dotnet' was not found`

**Solution:**

```bash
# Check .NET version
dotnet --version

# If not installed, download from:
# https://dotnet.microsoft.com/download/dotnet/10.0

# Verify SDK is installed (not just runtime)
dotnet --list-sdks

# Should show 10.0.x or higher
```

### NuGet Restore Failures

**Symptom:** `NU1101: Unable to find package`

**Solution:**

```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore with verbose logging
dotnet restore CompilerDotnet.slnx --verbosity detailed

# Check NuGet sources
dotnet nuget list source

# Add nuget.org if missing
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org

# Try restoring specific project
dotnet restore src/Bloqr.Compiler.Dotnet/Bloqr.Compiler.Dotnet.csproj
```

### Build Errors

**Symptom:** `CS0246: The type or namespace name could not be found`

**Solution:**

```bash
# Clean and rebuild
dotnet clean CompilerDotnet.slnx
dotnet restore CompilerDotnet.slnx
dotnet build CompilerDotnet.slnx

# Check target framework matches installed SDK
# In .csproj, should be <TargetFramework>net10.0</TargetFramework>

# Verify all project references are correct
dotnet list CompilerDotnet.slnx reference

# Build specific project
dotnet build src/Bloqr.Compiler.Dotnet/Bloqr.Compiler.Dotnet.csproj
```

### Test Discovery Issues

**Symptom:** `No test is available in...`

**Solution:**

```bash
# Rebuild test project
dotnet build src/Bloqr.Compiler.Dotnet.Tests/

# Run tests with verbose output
dotnet test CompilerDotnet.slnx --verbosity detailed

# Ensure test project references xUnit
# Check Bloqr.Compiler.Dotnet.Tests.csproj for:
<PackageReference Include="xunit" Version="..." />
<PackageReference Include="xunit.runner.visualstudio" Version="..." />

# Try running specific test
dotnet test --filter "FullyQualifiedName~YourTestName"
```

### Configuration Loading Errors

**Symptom:** `Configuration file not found` or `Invalid configuration`

**Solution:**

```bash
# Check file path (use absolute paths or ensure working directory is correct)
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- -c "$(pwd)/Config/compiler-config.json"

# Verify file format
# JSON files must use .json extension
# JSONC (JSON with comments) files must use .jsonc extension

# Test configuration validity
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- -c config.json --validate

# Enable debug logging
export BLOQR_COMPILER_Logging__LogLevel__Default=Debug
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- -c config.json --verbose
```

## Python Issues

### Python Version Mismatch

**Symptom:** `Python 3.9 or higher is required`

**Solution:**

```bash
# Check Python version
python --version  # or python3 --version

# Install correct version if needed
# Use pyenv for managing Python versions
pyenv install 3.12
pyenv local 3.12

# Or use virtual environment with specific version
python3.12 -m venv venv
source venv/bin/activate  # Linux/macOS
.\venv\Scripts\Activate.ps1  # Windows PowerShell
```

### Module Import Errors

**Symptom:** `ModuleNotFoundError: No module named 'bloqr_compiler'`

**Solution:**

```bash
# Install in editable mode
pip install -e .

# Or with dev dependencies
pip install -e ".[dev]"

# Check installation
pip list | grep bloqr-compiler

# Verify PYTHONPATH includes current directory
export PYTHONPATH=$PWD:$PYTHONPATH

# Check Python is using correct virtual environment
which python  # Should point to venv/bin/python
```

### pytest Not Found

**Symptom:** `pytest: command not found`

**Solution:**

```bash
# Install pytest
pip install pytest

# Or install all dev dependencies
pip install -e ".[dev]"

# Run pytest using python module syntax
python -m pytest

# Verify pytest installation
pytest --version
```

### JSON Parsing Errors

**Symptom:** `json.decoder.JSONDecodeError` or `Invalid JSON`

**Solution:**

```bash
# Verify file format using online validators
# - JSON: https://jsonlint.com/

# Check for common JSON issues:
# - Trailing commas (not allowed in JSON)
# - Unquoted keys
# - Single quotes instead of double quotes
# - Missing colons between keys and values
# - Improper array/object syntax

# Use Python to validate locally
python3 -c "import json; json.load(open('config.json'))" && echo "Valid JSON"
```

## Rust Issues

### Cargo Build Failures

**Symptom:** `error: could not compile`

**Solution:**

```bash
# Update Rust
rustup update stable

# Clean build artifacts
cargo clean

# Build with verbose output
cargo build --verbose

# Check for specific errors:
# - Missing dependencies: cargo update
# - Compiler version: rustup show
# - Target architecture: rustc --version --verbose
```

### Dependency Resolution Errors

**Symptom:** `failed to select a version for the requirement`

**Solution:**

```bash
# Update Cargo.lock
cargo update

# Or update specific package
cargo update -p package_name

# Remove Cargo.lock and regenerate
rm Cargo.lock
cargo build

# Check for incompatible versions in Cargo.toml
# Ensure version constraints are not too restrictive
```

### linker Errors

**Symptom:** `error: linking with 'cc' failed`

**Solution:**

```bash
# Linux: Install build essentials
sudo apt-get install build-essential

# macOS: Install Xcode Command Line Tools
xcode-select --install

# Windows: Install Visual Studio Build Tools
# Download from: https://visualstudio.microsoft.com/downloads/

# Check linker
rustc --print cfg | grep target

# Try different linker
# Add to .cargo/config.toml:
[target.x86_64-unknown-linux-gnu]
linker = "clang"
```

### OpenSSL Errors

**Symptom:** `error: failed to run custom build command for 'openssl-sys'`

**Solution:**

```bash
# Linux
sudo apt-get install pkg-config libssl-dev

# macOS
brew install openssl
export OPENSSL_DIR=$(brew --prefix openssl)

# Or use vendored OpenSSL in Cargo.toml:
[dependencies]
openssl = { version = "0.10", features = ["vendored"] }
```

## PowerShell Issues

### Module Not Found

**Symptom:** `The specified module was not loaded`

**Solution:**

```powershell
# Check PowerShell version
$PSVersionTable

# Should be 7.0 or higher for cross-platform features

# Import module with full path
Import-Module ./src/compilers/powershell/BloqrCompiler/BloqrCompiler.psd1 -Force

# Check module is loaded
Get-Module

# If module still not found, check file path
Test-Path ./src/compilers/powershell/BloqrCompiler/BloqrCompiler.psd1
```

### Execution Policy Errors

**Symptom:** `cannot be loaded because running scripts is disabled`

**Solution:**

```powershell
# Check current execution policy
Get-ExecutionPolicy

# Set execution policy for current user
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

# Or bypass for single command
powershell -ExecutionPolicy Bypass -File script.ps1

# For CI/CD, use:
pwsh -ExecutionPolicy Bypass -Command "Invoke-Pester"
```

### Pester Test Failures

**Symptom:** `Pester module not found` or tests not running

**Solution:**

```powershell
# Install Pester
Install-Module -Name Pester -Force -SkipPublisherCheck

# Import Pester
Import-Module Pester

# Check Pester version
Get-Module Pester -ListAvailable

# Update to latest version
Update-Module -Name Pester

# Run tests with verbose output
Invoke-Pester -Path ./Tests/ -Output Detailed
```

## Configuration Issues

### Invalid Configuration Format

**Symptom:** `Failed to parse configuration` or `Invalid JSON`

**Solution:**

1. **Validate format online:**
   - JSON: https://jsonlint.com/

2. **Common JSON issues:**
```json
❌ {
  "name": "Test",
  "sources": [
    {
      "name": "EasyList",
    }  // Trailing comma (not allowed)
  ]
}

✅ {
  "name": "Test",
  "sources": [
    {
      "name": "EasyList"
    }
  ]
}
```

3. **JSONC (JSON with Comments) is also supported:**
```jsonc
{
  // This is a comment - allowed in .jsonc files
  "name": "Test",
  "sources": [
    {
      "name": "EasyList"
    }
  ]
}
```

### Missing Required Fields

**Symptom:** `name is required` or `sources is required`

**Solution:**

```json
{
  "name": "My Filter List",
  "sources": [
    {
      "name": "Source 1",
      "source": "https://example.com/list.txt",
      "type": "adblock"
    }
  ]
}
```

Check required fields in `configuration-reference.md`.

### Invalid Transformation Names

**Symptom:** `Unknown transformation: <name>`

**Solution:**

```json
{
  "transformations": [
    "RemoveComments",
    "Compress",
    "RemoveModifiers",
    "Validate",
    "ValidateAllowIp",
    "Deduplicate",
    "InvertAllow",
    "RemoveEmptyLines",
    "TrimLines",
    "InsertFinalNewLine",
    "ConvertToAscii"
  ]
}
```

Note: Transformation names are case-sensitive. Check `configuration-reference.md` for the exact spelling.

## Network Issues

### Cannot Download Filter Lists

**Symptom:** `Failed to fetch` or `Connection timeout`

**Solution:**

```bash
# Test connectivity
curl -I https://easylist.to/easylist/easylist.txt

# Check proxy settings
echo $HTTP_PROXY
echo $HTTPS_PROXY

# Set proxy if needed
export HTTP_PROXY=http://proxy:port
export HTTPS_PROXY=http://proxy:port

# Test with specific DNS
curl --dns-servers 8.8.8.8 https://easylist.to/easylist/easylist.txt

# Increase timeout in configuration
# (depends on implementation - check docs)
```

### API Connection Failures

**Symptom:** `Failed to connect to AdGuard DNS API`

**Solution:**

```bash
# Test API connectivity
curl -H "Authorization: ApiKey your-key" https://api.adguard-dns.io/v1/limits

# Check API key format
# Should be: ADGUARD_AdGuard__ApiKey for .NET/Rust
# Or: ADGUARD_API_KEY for TypeScript

# Verify API key is valid
# Login to AdGuard DNS dashboard and regenerate if needed

# Check for rate limiting
# If getting 429 errors, implement retry delays

# Test with verbose output (example with curl)
curl -v -H "Authorization: ApiKey your-key" https://api.adguard-dns.io/v1/limits
```

## Docker Issues

### Container Fails to Start

**Symptom:** Container exits immediately or won't start

**Solution:**

```bash
# Check logs
docker logs <container_id>

# Run interactively to see errors
docker run -it ad-blocking:latest /bin/bash

# Check if entry point exists
docker inspect ad-blocking:latest | grep -A 5 Entrypoint

# Verify all files are copied
docker run -it ad-blocking:latest ls -la /app

# Check for missing dependencies
docker run -it ad-blocking:latest deno --version
docker run -it ad-blocking:latest dotnet --version
```

### Volume Permission Issues

**Symptom:** `Permission denied` when accessing mounted volumes

**Solution:**

```bash
# Run as current user
docker run -u $(id -u):$(id -g) ad-blocking:latest

# Fix permissions on volume
docker run --rm -v ad-blocking_rules:/data alpine chown -R 1000:1000 /data

# Or in Dockerfile
RUN useradd -m -u 1000 appuser
USER appuser
```

### Build Failures

**Symptom:** Docker build fails with various errors

**Solution:**

```bash
# Build with no cache
docker build --no-cache -t ad-blocking:latest .

# Build with verbose output
docker build --progress=plain -t ad-blocking:latest .

# Check for layer size issues
docker history ad-blocking:latest

# Use .dockerignore to exclude unnecessary files
echo "node_modules" >> .dockerignore
echo ".git" >> .dockerignore
echo "*.log" >> .dockerignore
```

## Performance Issues

### Slow Compilation

**Symptom:** Rules compilation takes very long

**Solutions:**

1. **Use local copies of frequently-used lists:**
```json
{
  "sources": [
    {
      "name": "EasyList",
      "source": "./cache/easylist.txt",
      "type": "adblock"
    }
  ]
}
```

2. **Reduce transformations:**
```json
{
  "transformations": [
    "Deduplicate",
    "InsertFinalNewLine"
  ]
}
```

3. **Enable parallel processing (where supported):**
```bash
# Rust with release mode
cargo build --release

# Python with multiple workers
pytest -n auto
```

4. **Use compiled binaries for faster execution:**
```bash
# Rust release binary is fastest
./target/release/bloqr-compiler -c config.json
```

### High Memory Usage

**Symptom:** Process consumes too much memory

**Solutions:**

1. **Limit Docker resources:**
```yaml
services:
  compiler:
    deploy:
      resources:
        limits:
          memory: 2G
```

2. **Process lists in batches:**
```json
// Instead of one huge source, split into multiple smaller ones
{
  "sources": [
    { "name": "EasyList Part 1", "source": "list1.txt" },
    { "name": "EasyList Part 2", "source": "list2.txt" }
  ]
}
```

3. **Clear caches:**
```bash
# Deno
deno cache --reload src/mod.ts

# .NET
dotnet nuget locals all --clear

# Python
pip cache purge

# Rust
cargo clean
```

## Testing Issues

### Tests Hang or Timeout

**Symptom:** Tests never complete or timeout

**Solution:**

```bash
# Deno: Set explicit timeout
deno test --allow-all --timeout=60000 tests/

# .NET: Run in parallel
dotnet test --parallel

# Python: Disable coverage during debugging
pytest --no-cov

# Rust: Run single-threaded
cargo test -- --test-threads=1

# Check for:
# - Infinite loops
# - Blocked network calls
# - Unclosed resources
# - Deadlocks
```

### Flaky Tests

**Symptom:** Tests pass sometimes, fail other times

**Solution:**

1. **Identify timing issues:**
```typescript
// Add delays for async operations
await new Promise(resolve => setTimeout(resolve, 1000));
```

2. **Fix race conditions:**
```typescript
// Ensure proper ordering
await firstOperation();
await secondOperation();  // Don't run in parallel
```

3. **Mock network calls:**
```typescript
// Don't rely on external services in tests
// Use mocks or fixtures instead
```

4. **Run tests multiple times:**
```bash
# Deno
for i in {1..10}; do deno test; done

# .NET
dotnet test --logger:"console;verbosity=detailed"

# Identify which test is flaky and fix it
```

## Getting Help

### Collecting Debug Information

When reporting issues, include:

1. **Version information:**
```bash
deno --version
dotnet --version
python --version
rustc --version
pwsh --version
git --version
```

2. **System information:**
```bash
uname -a  # Linux/macOS
systeminfo  # Windows
```

3. **Error messages:**
```bash
# Run with verbose/debug flags
deno task compile -- -d
dotnet run --verbosity detailed
pytest -vv
cargo build --verbose
```

4. **Configuration files:**
```bash
# Sanitize and include your config
cat compiler-config.json
```

5. **Steps to reproduce:**
```
1. Clean install: rm -rf node_modules
2. Install dependencies: deno cache src/mod.ts
3. Run command: deno task compile
4. Error occurs: [paste error]
```

### Where to Get Help

- **GitHub Issues**: https://github.com/BloqrAI/bloqr-core/issues
- **Documentation**: Check all guides in `docs/guides/`
- **AdGuard Forums**: https://forum.adguard.com/
- **Stack Overflow**: Tag with `adguard`, `deno`, `.net`, etc.

## Related Documentation

- [@bloqr/compiler-core Guide](./compiler-core-guide.md)
- [Testing Guide](./testing-guide.md)
- [Deployment Guide](./deployment-guide.md)
- [Configuration Reference](../configuration-reference.md)

## License

GPLv3 - See [LICENSE](../../LICENSE) for details.
