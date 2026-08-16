# Testing Guide

Comprehensive guide to running tests for all components in the ad-blocking repository.

## Overview

This project uses different testing frameworks for each language implementation:

| Component | Framework | Test Location | Test Command |
|-----------|-----------|---------------|--------------|
| TypeScript (Rules Compiler) | Deno Test | `src/adblock-compiler-core/tests/` | `deno task test` |
| .NET (Rules Compiler) | xUnit | `src/rules-compiler-dotnet/src/RulesCompiler.Tests/` | `dotnet test` |
| Python | pytest | `src/rules-compiler-python/tests/` | `pytest` |
| Rust (Rules Compiler) | cargo test | `src/rules-compiler-rust/src/` | `cargo test` |
| Rust (Validator) | cargo test | `src/validation/` | `cargo test` |
| PowerShell | Pester | `src/rules-compiler-powershell/` | `Invoke-Pester -Recurse` |

The AdGuard DNS API client test suites (TypeScript, .NET, Rust, PowerShell) moved with the clients to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients).

## TypeScript (Deno) Testing

### Rules Compiler Tests

```bash
cd src/adblock-compiler-core

# Run all tests
deno task test

# Run specific test file
deno test tests/compiler.test.ts

# Run with coverage
deno task test:coverage

# Run with verbose output
deno test --allow-all tests/ --trace-leaks

# Watch mode for development
deno task dev
```

### API Client Tests

The TypeScript AdGuard DNS API client test suite moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) along with the client.

### Writing Deno Tests

```typescript
import { assertEquals, assertRejects } from 'https://deno.land/std@0.203.0/assert/mod.ts';
import { describe, it, beforeEach, afterEach } from 'https://deno.land/std@0.203.0/testing/bdd.ts';

describe('MyModule', () => {
  let instance: MyModule;

  beforeEach(() => {
    instance = new MyModule();
  });

  it('should do something', () => {
    const result = instance.doSomething();
    assertEquals(result, 'expected value');
  });

  it('should handle errors', async () => {
    await assertRejects(
      async () => await instance.failingMethod(),
      Error,
      'Expected error message'
    );
  });

  afterEach(() => {
    // Cleanup
  });
});
```

### Common Deno Test Options

| Option | Description |
|--------|-------------|
| `--allow-read` | Allow file system read |
| `--allow-write` | Allow file system write |
| `--allow-net` | Allow network access |
| `--allow-env` | Allow environment access |
| `--allow-all` / `-A` | Allow all permissions |
| `--coverage` | Generate coverage |
| `--trace-leaks` | Detect resource leaks |
| `--watch` | Watch mode |

## .NET (xUnit) Testing

### Rules Compiler Tests

```bash
cd src/rules-compiler-dotnet

# Run all tests
dotnet test RulesCompiler.slnx

# Run with verbose output
dotnet test RulesCompiler.slnx --verbosity detailed

# Run specific test class
dotnet test --filter "FullyQualifiedName~RulesCompilerServiceTests"

# Run specific test method
dotnet test --filter "Name~ShouldValidateConfiguration"

# Run tests with coverage (requires coverlet)
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

Tests for the shared `Bloqr.Compiler.Core`/`Bloqr.Compiler.Abstractions` library (`ConfigurationValidator`, `ChunkingService`, `TransformationHelper`, etc.) live in a separate solution:

```bash
cd src/compiler-common-dotnet
dotnet test CompilerCommon.slnx
dotnet test --filter "FullyQualifiedName~ConfigurationValidatorTests"
```

### API Client Tests

The .NET AdGuard DNS API client test suite moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) along with the client.

### Writing xUnit Tests

```csharp
using Xunit;
using FluentAssertions;

public class MyServiceTests
{
    private readonly MyService _service;

    public MyServiceTests()
    {
        _service = new MyService();
    }

    [Fact]
    public void ShouldDoSomething()
    {
        // Arrange
        var input = "test";

        // Act
        var result = _service.DoSomething(input);

        // Assert
        result.Should().Be("expected value");
    }

    [Theory]
    [InlineData("input1", "output1")]
    [InlineData("input2", "output2")]
    public void ShouldHandleMultipleInputs(string input, string expected)
    {
        var result = _service.Process(input);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task ShouldHandleAsync()
    {
        var result = await _service.DoAsyncWork();
        result.Should().NotBeNull();
    }
}
```

### Common xUnit Filters

| Filter | Example | Description |
|--------|---------|-------------|
| `FullyQualifiedName` | `FullyQualifiedName~ConfigurationTests` | Match fully qualified name |
| `Name` | `Name~ShouldValidate` | Match test method name |
| `Category` | `Category=Integration` | Match test category/trait |
| `Priority` | `Priority=1` | Match priority trait |

## Python (pytest) Testing

### Running Tests

```bash
cd src/rules-compiler-python

# Install dev dependencies first
pip install -e ".[dev]"

# Run all tests
pytest

# Run with verbose output
pytest -v

# Run specific test file
pytest tests/test_config.py

# Run specific test function
pytest tests/test_config.py::test_read_yaml

# Run tests matching pattern
pytest -k "test_read"

# Run with coverage
pytest --cov=rules_compiler --cov-report=term-missing

# Run with coverage HTML report
pytest --cov=rules_compiler --cov-report=html

# Run in parallel (requires pytest-xdist)
pytest -n auto
```

### Writing pytest Tests

```python
import pytest
from rules_compiler import RulesCompiler

class TestRulesCompiler:
    @pytest.fixture
    def compiler(self):
        return RulesCompiler()

    def test_compile(self, compiler):
        result = compiler.compile("config.json")
        assert result.success
        assert result.rule_count > 0

    @pytest.mark.parametrize("config,expected", [
        ("config1.yaml", 100),
        ("config2.yaml", 200),
    ])
    def test_multiple_configs(self, compiler, config, expected):
        result = compiler.compile(config)
        assert result.rule_count == expected

    def test_error_handling(self, compiler):
        with pytest.raises(FileNotFoundError):
            compiler.compile("nonexistent.yaml")

    @pytest.mark.asyncio
    async def test_async_operation(self, compiler):
        result = await compiler.async_compile("config.json")
        assert result is not None
```

### Common pytest Options

| Option | Description |
|--------|-------------|
| `-v` | Verbose output |
| `-s` | Show print statements |
| `-x` | Stop on first failure |
| `--maxfail=N` | Stop after N failures |
| `-k EXPRESSION` | Run tests matching expression |
| `--cov=MODULE` | Code coverage for module |
| `-n auto` | Run in parallel |
| `--pdb` | Drop into debugger on failure |

## Rust (cargo test) Testing

### Rules Compiler Tests

```bash
cd src/rules-compiler-rust

# Run all tests
cargo test

# Run with output
cargo test -- --nocapture

# Run specific test
cargo test test_count_rules

# Run tests in a specific module
cargo test config::

# Run with verbose output
cargo test -- --nocapture --test-threads=1

# Run tests for specific package
cargo test --package rules-compiler

# Run doc tests
cargo test --doc
```

### API Client Tests

The Rust AdGuard DNS API client test suite moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) along with the client.

### Writing Rust Tests

```rust
#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_basic_functionality() {
        let compiler = RulesCompiler::new();
        let result = compiler.compile("config.json", None);
        assert!(result.is_ok());
    }

    #[test]
    fn test_error_handling() {
        let compiler = RulesCompiler::new();
        let result = compiler.compile("nonexistent.yaml", None);
        assert!(result.is_err());
    }

    #[test]
    #[should_panic(expected = "Invalid configuration")]
    fn test_panic() {
        panic!("Invalid configuration");
    }

    #[tokio::test]
    async fn test_async() {
        let result = async_function().await;
        assert!(result.is_ok());
    }

    #[test]
    #[ignore]  // Run with --ignored flag
    fn expensive_test() {
        // Long-running test
    }
}
```

### Common cargo test Options

| Option | Description |
|--------|-------------|
| `--` | Pass options to test binary |
| `--nocapture` | Show print statements |
| `--test-threads=N` | Number of parallel threads |
| `--ignored` | Run ignored tests |
| `--release` | Run tests in release mode |
| `--doc` | Run documentation tests |

## PowerShell (Pester) Testing

### Running Tests

```powershell
cd src/rules-compiler-powershell

# Run all tests (Common, RulesCompiler, AdGuardWebhook modules)
Invoke-Pester -Path . -Recurse

# Run with detailed output
Invoke-Pester -Path . -Recurse -Output Detailed

# Run tests for a single module
Invoke-Pester -Path ./RulesCompiler/Tests/

# Run specific test file
Invoke-Pester -Path ./RulesCompiler/Tests/CompilerResult.Tests.ps1

# Run with results export
Invoke-Pester -Path . -Recurse -Output Detailed -OutputFile ./TestResults.xml
```

### Writing Pester Tests

```powershell
Describe "RulesCompiler" {
    BeforeAll {
        Import-Module ./Invoke-RulesCompiler.psm1 -Force
    }

    Context "When compiling rules" {
        It "Should compile successfully" {
            $result = Invoke-RulesCompiler -ConfigPath "config.json"
            $result.Success | Should -Be $true
        }

        It "Should handle missing config" {
            { Invoke-RulesCompiler -ConfigPath "nonexistent.json" } | 
                Should -Throw
        }
    }

    Context "Configuration validation" {
        It "Should validate config format" {
            $config = Read-CompilerConfiguration -Path "config.json"
            $config.Name | Should -Not -BeNullOrEmpty
        }

        It "Should support parameters <ConfigPath>" -TestCases @(
            @{ ConfigPath = "config1.json" },
            @{ ConfigPath = "config2.json" }
        ) {
            param($ConfigPath)
            
            $result = Invoke-RulesCompiler -ConfigPath $ConfigPath
            $result | Should -Not -BeNull
        }
    }

    AfterAll {
        Remove-Module Invoke-RulesCompiler -Force
    }
}
```

### Common Pester Options

| Option | Description |
|--------|-------------|
| `-Path` | Test file or directory path |
| `-Output` | Output verbosity (None, Normal, Detailed, Diagnostic) |
| `-CodeCoverage` | Files to analyze for code coverage |
| `-PassThru` | Return test result object |
| `-Show` | What to show (None, All, Failed, Passed, etc.) |

## Continuous Integration

### GitHub Actions Example

```yaml
name: Run All Tests

on: [push, pull_request]

jobs:
  test-typescript:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: denoland/setup-deno@v1
        with:
          deno-version: v2.x
      - name: Run TypeScript tests
        run: |
          cd src/adblock-compiler-core
          deno task test

  test-dotnet:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      - name: Run .NET tests
        run: |
          cd src/rules-compiler-dotnet
          dotnet test RulesCompiler.slnx

  test-python:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-python@v4
        with:
          python-version: '3.9'
      - name: Install and test
        run: |
          cd src/rules-compiler-python
          pip install -e ".[dev]"
          pytest --cov=rules_compiler

  test-rust:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions-rs/toolchain@v1
        with:
          toolchain: stable
      - name: Run Rust tests
        run: |
          cd src/rules-compiler-rust
          cargo test

  test-powershell:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run PowerShell tests
        shell: pwsh
        run: |
          Install-Module -Name Pester -Force -SkipPublisherCheck
          Invoke-Pester -Path ./src/rules-compiler-powershell -Recurse -Output Detailed
```

## Test Coverage

### Generating Coverage Reports

#### Deno
```bash
cd src/adblock-compiler-core
deno task test:coverage
# View coverage report at coverage/html/index.html
```

#### .NET (with coverlet)
```bash
cd src/rules-compiler-dotnet
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage.opencover.xml -targetdir:coverage
```

#### Python
```bash
cd src/rules-compiler-python
pytest --cov=rules_compiler --cov-report=html
# View coverage/index.html
```

#### Rust
```bash
cd src/rules-compiler-rust
cargo install cargo-tarpaulin
cargo tarpaulin --out Html
# View tarpaulin-report.html
```

## Best Practices

### 1. Test Organization

- Keep tests close to source code
- Use descriptive test names
- Group related tests together
- Separate unit and integration tests

### 2. Test Independence

- Each test should be independent
- Avoid shared mutable state
- Clean up after tests
- Use fixtures/setup methods

### 3. Test Coverage

- Aim for high code coverage (>80%)
- Focus on critical paths
- Test error conditions
- Include edge cases

### 4. Performance

- Keep unit tests fast (<100ms)
- Mark slow tests appropriately
- Run integration tests separately
- Use parallelization when possible

### 5. Mocking

Use mocks for external dependencies:

```typescript
// Deno
import { stub } from 'https://deno.land/std@0.203.0/testing/mock.ts';

// .NET
using Moq;
var mock = new Mock<IService>();

// Python
from unittest.mock import Mock, patch

// Rust
use mockall::predicate::*;
```

## Troubleshooting

### Deno Tests Failing

```bash
# Clear cache
deno cache --reload src/mod.ts

# Run with permissions
deno test --allow-all tests/

# Check for resource leaks
deno test --trace-leaks tests/
```

### .NET Tests Not Running

```bash
# Restore packages
dotnet restore

# Clean and rebuild
dotnet clean
dotnet build
dotnet test
```

### Python Tests Not Found

```bash
# Install in editable mode
pip install -e ".[dev]"

# Verify pytest installation
pytest --version

# Check PYTHONPATH
export PYTHONPATH=$PWD:$PYTHONPATH
```

### Rust Tests Failing

```bash
# Update dependencies
cargo update

# Clean build
cargo clean
cargo test

# Run single-threaded
cargo test -- --test-threads=1
```

## Running All Tests

To run all tests across all components:

```bash
# From repository root
./scripts/run-all-tests.sh  # Linux/macOS
.\scripts\run-all-tests.ps1  # Windows
```

Create this script to automate testing:

```bash
#!/bin/bash
# run-all-tests.sh

set -e

echo "Running TypeScript tests..."
cd src/adblock-compiler-core && deno task test && cd ../..

echo "Running .NET tests..."
cd src/rules-compiler-dotnet && dotnet test RulesCompiler.slnx && cd ../..

echo "Running Python tests..."
cd src/rules-compiler-python && pytest && cd ../..

echo "Running Rust tests..."
cd src/rules-compiler-rust && cargo test && cd ../..

echo "Running PowerShell tests..."
pwsh -Command "Invoke-Pester -Path ./src/rules-compiler-powershell -Recurse"

echo "All tests passed!"
```

## Related Documentation

- [TypeScript Rules Compiler Guide](./typescript-rules-compiler.md)
- [.NET Compiler README](../../src/rules-compiler-dotnet/README.md)
- [Python Compiler README](../../src/rules-compiler-python/README.md)
- [Rust Compiler README](../../src/rules-compiler-rust/README.md)

## License

GPLv3 - See [LICENSE](../../LICENSE) for details.
