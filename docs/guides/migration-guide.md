# Migration Guide

Guide for migrating between different compiler implementations and SDK versions in the ad-blocking repository.

## Overview

This guide helps you migrate between:
- Different rules compiler implementations (TypeScript, .NET, Python, Rust, PowerShell, Shell)
- API SDK implementations (TypeScript, .NET, Rust)
- Versions within the same implementation
- Configuration format changes

## Rules Compiler Migration

### Migrating Between Compiler Implementations

All rules compilers share the same configuration schema, making migration straightforward.

#### From Any Compiler to Another

**Step 1: Verify Configuration Compatibility**

Your existing JSON/JSONC configuration file works across all compilers:

```json
{
  "name": "My Filter List",
  "description": "Works everywhere",
  "version": "1.0.0",
  "sources": [
    {
      "name": "EasyList",
      "source": "https://easylist.to/easylist/easylist.txt",
      "type": "adblock"
    }
  ],
  "transformations": [
    "Validate",
    "Deduplicate",
    "InsertFinalNewLine"
  ]
}
```

**Step 2: Install Target Compiler**

```bash
# TypeScript (Deno)
curl -fsSL https://deno.land/install.sh | sh
cd src/compilers/typescript
deno cache src/mod.ts

# .NET
cd src/compilers/dotnet
dotnet restore CompilerDotnet.slnx

# Python
cd src/compilers/python
pip install -e .

# Rust
cd src/compilers/rust
cargo build --release

# PowerShell
# Already installed if using PowerShell 7+
```

**Step 3: Test with Same Configuration**

```bash
# Test that output is identical
# TypeScript
deno task compile -- -c config.json -o output-ts.txt

# .NET
dotnet run --project src/Bloqr.Compiler.Dotnet.Console -- -c config.json -o output-dotnet.txt

# Python
bloqr-compiler -c config.json -o output-python.txt

# Rust
cargo run -- -c config.json -o output-rust.txt

# Compare outputs (should be identical)
diff output-ts.txt output-dotnet.txt
```

**Step 4: Update Build Scripts**

```bash
# Before (TypeScript)
deno task compile

# After (.NET)
dotnet run --project src/Bloqr.Compiler.Dotnet.Console
```

#### Migration Decision Matrix

| Current | Migrate To | Reason | Difficulty |
|---------|------------|--------|------------|
| Shell Scripts | TypeScript | Better error handling, testing | Easy |
| TypeScript | .NET | Library API, validation, DI | Easy |
| Any | Rust | Performance, single binary | Easy |
| .NET | Python | Lighter weight, scripting | Easy |
| Any | PowerShell | Windows automation | Easy |

### Specific Migration Scenarios

#### From Node.js/npm to Deno (TypeScript)

**Before (Node.js):**
```json
{
  "scripts": {
    "compile": "node compile.js"
  },
  "dependencies": {
    "@bloqr/compiler-core": "^1.0.0"
  }
}
```

**After (Deno):**
```json
{
  "tasks": {
    "compile": "deno run --allow-all src/mod.ts"
  },
  "imports": {
    "@bloqr/compiler-core": "jsr:@bloqr/compiler-core@^1.0.0"
  }
}
```

**Migration Steps:**

1. Remove `node_modules` and `package-lock.json`
2. Install Deno
3. Update import statements to use npm: specifier
4. Update scripts to use `deno run` instead of `node`
5. Add necessary permissions to deno.json

#### From Jest to Deno Test

**Before (Jest):**
```javascript
// tests/compiler.test.js
const { compile } = require('../src/compiler');

describe('Compiler', () => {
  test('should compile rules', () => {
    const result = compile('config.yaml');
    expect(result.success).toBe(true);
  });
});
```

**After (Deno Test):**
```typescript
// tests/compiler.test.ts
import { assertEquals } from 'https://deno.land/std@0.203.0/assert/mod.ts';
import { compile } from '../src/compiler.ts';

Deno.test('should compile rules', async () => {
  const result = await compile('config.yaml');
  assertEquals(result.success, true);
});
```

**Migration Steps:**

1. Rename `.test.js` to `.test.ts`
2. Replace Jest imports with Deno std/assert
3. Replace `describe`/`test` with `Deno.test`
4. Replace `expect` with `assertEquals`/`assert*`
5. Run: `deno test --allow-all tests/`

#### From .NET Framework to .NET 10

**Before (.NET Framework 4.8):**
```xml
<TargetFramework>net48</TargetFramework>
<PackageReference Include="System.Configuration" />
```

**After (.NET 10):**
```xml
<TargetFramework>net10.0</TargetFramework>
<PackageReference Include="Microsoft.Extensions.Configuration" />
```

**Migration Steps:**

1. Update target framework in .csproj
2. Replace System.Configuration with Microsoft.Extensions.Configuration
3. Update package references to latest versions
4. Test compilation and runtime
5. Update CI/CD pipelines

#### From Python 2 to Python 3.9+

**Before (Python 2):**
```python
print "Compiling rules..."
from ConfigParser import ConfigParser
```

**After (Python 3.9+):**
```python
print("Compiling rules...")
from configparser import ConfigParser
```

**Migration Steps:**

1. Update print statements to functions
2. Fix imports (ConfigParser → configparser)
3. Update string handling (unicode, bytes)
4. Use type hints
5. Run: `python -m pytest`

## API SDK Migration

**Note:** The AdGuard DNS API clients (TypeScript, .NET, Rust, PowerShell) and their documentation have moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) (internal repository). This section remains for reference on general SDK migration patterns.

### Migrating Between SDK Implementations

#### Common API Patterns

All SDKs follow similar patterns:

**TypeScript:**
```typescript
const client = AdGuardDnsClient.withApiKey('key');
const devices = await client.devices.listDevices();
```

**.NET:**
```csharp
var config = new Configuration { ApiKey = "key" };
var api = new DevicesApi(config);
var devices = await api.ListDevicesAsync();
```

**Rust:**
```rust
let mut config = Configuration::new();
config.bearer_access_token = Some("key".to_string());
let devices = devices_api::list_devices(&config).await?;
```

#### From .NET to TypeScript SDK

**Before (.NET):**
```csharp
using AdGuard.ApiClient;
using AdGuard.ApiClient.Api;

var config = new Configuration
{
    ApiKey = new Dictionary<string, string>
    {
        { "Authorization", "ApiKey your-key" }
    }
};

var devicesApi = new DevicesApi(config);
var devices = await devicesApi.ListDevicesAsync();

foreach (var device in devices)
{
    Console.WriteLine($"{device.Name}: {device.Id}");
}
```

**After (TypeScript):**
```typescript
import { AdGuardDnsClient } from './src/index.ts';

const client = AdGuardDnsClient.withApiKey('your-key');
const devices = await client.devices.listDevices();

for (const device of devices) {
  console.log(`${device.name}: ${device.id}`);
}
```

**Key Differences:**

| Feature | .NET | TypeScript |
|---------|------|------------|
| Naming | PascalCase | camelCase |
| Async | `async/await` | `async/await` |
| Collections | `IEnumerable<T>` | `Array<T>` |
| Errors | `ApiException` | `ApiError` |
| DI | Built-in | Manual |

#### From TypeScript to Rust SDK

**Before (TypeScript):**
```typescript
const client = AdGuardDnsClient.withApiKey('key');
const limits = await client.account.getAccountLimits();
console.log(`Devices: ${limits.devices.used}/${limits.devices.limit}`);
```

**After (Rust):**
```rust
use adguard_api_lib::apis::configuration::Configuration;
use adguard_api_lib::apis::account_api;

let mut config = Configuration::new();
config.bearer_access_token = Some("key".to_string());

let limits = account_api::get_account_limits(&config).await?;
println!("Devices: {}/{}", limits.devices.used, limits.devices.limit);
```

**Key Differences:**

| Feature | TypeScript | Rust |
|---------|------------|------|
| Error Handling | try/catch | Result<T, E> |
| Async | Promise | Future |
| Null Values | `undefined` / `null` | `Option<T>` |
| Type System | Structural | Nominal |

### API Version Migration

#### From API v1.0 to v1.11

**Breaking Changes:**

1. **Authentication Header Format**
```bash
# Before (v1.0)
Authorization: Bearer your-token

# After (v1.11)
Authorization: ApiKey your-api-key
```

2. **Timestamp Format**
```typescript
// Before (v1.0) - seconds
const timestamp = Math.floor(Date.now() / 1000);

// After (v1.11) - milliseconds
const timestamp = Date.now();
```

3. **Device Types**
```typescript
// Before (v1.0)
deviceType: 'mobile'

// After (v1.11)
deviceType: 'IOS' | 'ANDROID' | 'WINDOWS' | 'MAC' | etc.
```

**Migration Steps:**

1. Update authentication method
2. Convert timestamps to milliseconds
3. Update device type values
4. Test all API endpoints
5. Update error handling for new status codes

## Configuration Format Migration

**Note:** As of issue #259, only **JSON** and **JSONC** (JSON with Comments) are the documented, supported configuration formats. Earlier YAML and TOML support was for backward compatibility only; all new configurations should use JSON or JSONC.

### From Legacy YAML/TOML to JSON

If you have an existing YAML or TOML configuration, convert it to JSON for full schema validation and IDE autocomplete support:

**From YAML:**
```bash
# Option 1: Use yq (if available)
yq eval -o=json config.yaml > config.json

# Option 2: Manual conversion - rewrite in JSON format following the schema at:
# https://github.com/BloqrAI/bloqr-core/blob/main/schemas/compiler-config.schema.json
```

**From TOML:**
```bash
# Option 1: Use a TOML-to-JSON converter online or via CLI
python -c 'import toml, json; print(json.dumps(toml.load(open("config.toml"))))' > config.json

# Option 2: Manual conversion - rewrite in JSON format
```

### JSON vs JSONC

Both JSON and JSONC are supported. JSONC (JSON with Comments) is useful for documenting configurations inline:

**JSON (strict):**
```json
{
  "name": "My Filter",
  "sources": [
    {
      "name": "EasyList",
      "source": "https://easylist.to/easylist/easylist.txt",
      "type": "adblock"
    }
  ],
  "transformations": ["Deduplicate", "Validate"]
}
```

**JSONC (with comments):**
```jsonc
{
  "name": "My Filter",
  // Main filter sources
  "sources": [
    {
      "name": "EasyList",
      "source": "https://easylist.to/easylist/easylist.txt",
      "type": "adblock"  // Adblock format list
    }
  ],
  // Cleanup and deduplication
  "transformations": ["Deduplicate", "Validate"]
}
```

**Compiler Support:**
- **.NET** and **Dashboard** can read both `.json` and `.jsonc` files
- **TypeScript, Python, and Rust** compilers parse with strict JSON (no comments) — use `.json` extension with those, or convert via .NET/Dashboard first if you need comments

## Environment Variable Migration

### From Legacy to Current Format

#### API Keys

**Before:**
```bash
export ADGUARD_API_TOKEN="your-token"
export ADGUARD_API_URL="https://api.adguard-dns.io"
```

**After:**
```bash
export ADGUARD_AdGuard__ApiKey="your-api-key"
export ADGUARD_AdGuard__BaseUrl="https://api.adguard-dns.io"
```

#### Compiler Configuration

**Before:**
```bash
export CONFIG_FILE="config.json"
export OUTPUT_FILE="output.txt"
```

**After:**
```bash
export CONFIG_PATH="config.yaml"
export RULES_DIR="./rules"
```

### Cross-Platform Environment Variables

**Windows (CMD):**
```cmd
set ADGUARD_AdGuard__ApiKey=your-key
```

**Windows (PowerShell):**
```powershell
$env:ADGUARD_AdGuard__ApiKey="your-key"
```

**Linux/macOS:**
```bash
export ADGUARD_AdGuard__ApiKey="your-key"
```

## Docker Migration

### From Single Stage to Multi-Stage Build

**Before:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app
COPY . .
RUN dotnet publish -c Release
ENTRYPOINT ["dotnet", "bin/Release/net10.0/App.dll"]
```

**After:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "App.dll"]
```

**Benefits:**
- Smaller final image size
- Better layer caching
- Separation of build and runtime dependencies

### From Docker Compose v2 to v3

**Before (v2):**
```yaml
version: '2'
services:
  compiler:
    build: .
    volumes:
      - ./rules:/app/rules
    mem_limit: 2g
```

**After (v3):**
```yaml
version: '3.8'
services:
  compiler:
    build: .
    volumes:
      - ./rules:/app/rules
    deploy:
      resources:
        limits:
          memory: 2G
```

## CI/CD Migration

### From Travis CI to GitHub Actions

**Before (Travis CI - .travis.yml):**
```yaml
language: node_js
node_js:
  - 18
script:
  - npm test
  - npm run build
```

**After (GitHub Actions - .github/workflows/test.yml):**
```yaml
name: Test
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: denoland/setup-deno@v1
      - run: deno task test
      - run: deno task build
```

### From Jenkins to GitLab CI

**Before (Jenkinsfile):**
```groovy
pipeline {
    agent any
    stages {
        stage('Test') {
            steps {
                sh 'dotnet test'
            }
        }
    }
}
```

**After (GitLab CI - .gitlab-ci.yml):**
```yaml
test:
  image: mcr.microsoft.com/dotnet/sdk:10.0
  script:
    - dotnet test
```

## Testing Framework Migration

### From mocha to Deno test

**Before (mocha):**
```javascript
const assert = require('assert');
const { myFunction } = require('../src/index');

describe('myFunction', () => {
  it('should return true', () => {
    assert.strictEqual(myFunction(), true);
  });
});
```

**After (Deno test):**
```typescript
import { assertEquals } from 'https://deno.land/std@0.203.0/assert/mod.ts';
import { myFunction } from '../src/index.ts';

Deno.test('myFunction should return true', () => {
  assertEquals(myFunction(), true);
});
```

### From MSTest to xUnit

**Before (MSTest):**
```csharp
[TestClass]
public class MyTests
{
    [TestMethod]
    public void TestMethod()
    {
        Assert.IsTrue(true);
    }
}
```

**After (xUnit):**
```csharp
public class MyTests
{
    [Fact]
    public void TestMethod()
    {
        Assert.True(true);
    }
}
```

## Rollback Strategies

### Safe Migration Process

1. **Create Backup:**
```bash
# Backup configuration
cp config.yaml config.yaml.backup

# Backup rules
cp -r data/ data.backup/

# Backup database/state if applicable
tar czf backup-$(date +%Y%m%d).tar.gz config/ data/
```

2. **Test in Parallel:**
```bash
# Run both old and new implementations
old-compiler -c config.yaml -o output-old.txt
new-compiler -c config.yaml -o output-new.txt

# Compare outputs
diff output-old.txt output-new.txt
```

3. **Gradual Rollout:**
```yaml
# Use feature flags or environment variables
if [ "$USE_NEW_COMPILER" = "true" ]; then
  new-compiler -c config.yaml
else
  old-compiler -c config.yaml
fi
```

4. **Rollback Plan:**
```bash
# Quick rollback script
#!/bin/bash
echo "Rolling back to previous version..."
git checkout HEAD~1
docker-compose down
docker-compose up -d
echo "Rollback complete"
```

## Migration Checklist

### Pre-Migration

- [ ] Backup all configuration files
- [ ] Backup existing ../bloqr-blocklists/output
- [ ] Document current setup
- [ ] Test current implementation
- [ ] Review new implementation docs

### During Migration

- [ ] Install new dependencies
- [ ] Convert configuration if needed
- [ ] Update environment variables
- [ ] Test new implementation
- [ ] Compare outputs
- [ ] Update scripts/automation
- [ ] Update CI/CD pipelines

### Post-Migration

- [ ] Verify all features work
- [ ] Monitor for issues
- [ ] Update documentation
- [ ] Train team if needed
- [ ] Remove old dependencies
- [ ] Update README/docs

## Troubleshooting Migration Issues

### Configuration Not Working

```bash
# Validate configuration with new compiler
new-compiler -c config.yaml --validate

# Check for format-specific issues
yamllint config.yaml  # For YAML
jsonlint config.json  # For JSON
```

### Output Differences

```bash
# Check transformation order
# Transformations are applied in fixed order regardless of config

# Check for version differences in the underlying @bloqr/compiler-core engine
deno run jsr:@bloqr/compiler-core/cli --version

# Enable debug output
new-compiler -c config.yaml -d
```

### Performance Regression

```bash
# Profile old vs new
time old-compiler -c config.yaml
time new-compiler -c config.yaml

# Check resource usage
/usr/bin/time -v new-compiler -c config.yaml

# Enable performance monitoring
new-compiler -c config.yaml --verbose
```

## Getting Help

If you encounter issues during migration:

1. Check the [Troubleshooting Guide](./troubleshooting-guide.md)
2. Review component-specific documentation
3. Open an issue on GitHub with:
   - Source and target implementations
   - Configuration files (sanitized)
   - Error messages
   - Steps to reproduce

## Related Documentation

- [@bloqr/compiler-core Guide](./compiler-core-guide.md)
- [Deployment Guide](./deployment-guide.md)
- [Configuration Reference](../configuration-reference.md)
- [Compiler Comparison](../compiler-comparison.md)

## License

GPLv3 - See [LICENSE](../../LICENSE) for details.
