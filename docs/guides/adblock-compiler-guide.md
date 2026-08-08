# @bloqr/compiler-core Guide

A comprehensive guide to `@bloqr/compiler-core` — the in-repo, dependency-free filter compilation engine that all four compilers in this repository dogfood.

## Table of Contents

- [What is @bloqr/compiler-core?](#what-is-bloqrcompiler-core)
- [Why Use @bloqr/compiler-core?](#why-use-bloqrcompiler-core)
- [Relationship to AdGuard's hostlist-compiler and the commercial bloqr-compiler](#relationship-to-adguards-hostlist-compiler-and-the-commercial-bloqr-compiler)
- [Installation and Usage](#installation-and-usage)
- [CI/CD Integration](#cicd-integration)
- [Architecture and Design](#architecture-and-design)
- [Migrating a Compiler from hostlist-compiler](#migrating-a-compiler-from-hostlist-compiler)
- [API Reference](#api-reference)

## What is @bloqr/compiler-core?

`@bloqr/compiler-core` is this repository's own TypeScript package for compiling ad-blocking filter lists from multiple sources. Its canonical source lives at [`src/adblock-compiler-core/`](../../src/adblock-compiler-core/) in this repo, and it's published to [JSR (JavaScript Registry)](https://jsr.io/@bloqr/compiler-core) at v1.0.0. It serves as the core compilation engine for all four rules compilers in this repository (TypeScript directly, .NET/Python/Rust by shelling out to its CLI via Deno).

**Key Features:**
- ✅ **Multi-source compilation**: Combine local and remote filter lists
- ✅ **11 transformations**: Deduplicate, Validate, RemoveComments, Compress, and more
- ✅ **Multi-format support**: Adblock syntax and hosts file formats
- ✅ **Pattern matching**: Wildcards, regex, file-based inclusion/exclusion
- ✅ **TypeScript-first**: Full type safety with comprehensive interfaces
- ✅ **Dependency-free core**: no `@adguard/agtree` or other third-party AdGuard library, no Cloudflare-specific code
- ✅ **Chunked parallel compilation**: for large rule lists (10M+ entries)
- ✅ **JSR distribution**: `deno add jsr:@bloqr/compiler-core`

**Package Information:**
- **Registry**: [jsr.io/@bloqr/compiler-core](https://jsr.io/@bloqr/compiler-core)
- **Source**: [`src/adblock-compiler-core/`](../../src/adblock-compiler-core/) in this repo ([BloqrAI/bloqr-lists](https://github.com/BloqrAI/bloqr-lists))
- **Current Version**: 1.0.0
- **License**: GPL-3.0

## Why Use @bloqr/compiler-core?

### We own it

Unlike the AdGuard-maintained `@adguard/hostlist-compiler` npm package this repo used to shell out to, `@bloqr/compiler-core` is our own code, in this repo, that we can extend, fix, and version on our own schedule. See [ADR 0001](../adr/0001-canonical-rules-compilation-engine.md) for the full decision record.

### Dependency Injection

Core compilation components (`FilterCompiler`, downloader, logger) support dependency injection:
- Easy testing with mock dependencies
- Configurable logging, event hooks, and hook managers
- Swap implementations without code changes

### Enhanced Developer Experience

- **Comprehensive Type Safety**: Full TypeScript types for all APIs
- **Better Error Messages**: Descriptive errors with context and suggestions
- **JSDoc Documentation**: Every function, interface, and class documented

### Production-Ready Features

- **Chunked parallel compilation**: For large rule lists (10M+ entries), with SHA-384 hash consistency preserved across chunks
- **Memory Efficiency**: Optimized for large filter lists (100k+ rules)
- **Validation**: Zod-based schema validation prevents invalid configurations
- **1008 passing tests, clean type-check/lint/fmt**: verified on every change to `src/adblock-compiler-core/`

## Relationship to AdGuard's hostlist-compiler and the commercial bloqr-compiler

This package is **not** a drop-in npm-compatible fork of `@adguard/hostlist-compiler` with a fallback path — it fully replaced that dependency. The .NET, Python, and Rust compilers previously shelled out to `@adguard/hostlist-compiler`; they now shell out to `@bloqr/compiler-core` instead (`deno run jsr:@bloqr/compiler-core/cli`). The TypeScript compiler *is* the package — it compiles in-process and always has.

| | `@bloqr/compiler-core` (this package) | `@adguard/hostlist-compiler` (superseded) | `@bloqr/compiler` (commercial, separate) |
|---|---|---|---|
| Maintained by | This repo | AdGuard | Bloqr (`bloqr-compiler` repo) |
| Scope | Bare-minimum compilation engine + performance features | Filter compilation | Full-featured product: linting, AST tooling, diff reports, plugin system, Cloudflare Workers deployment |
| AdGuard libraries (`@adguard/agtree`, etc.) | Not used — rule classification is string/regex-based | N/A | Used, for AST-level rule parsing and validation |
| Distribution | JSR, open source | npm | Not currently published |

`@bloqr/compiler-core` is deliberately kept separate from Bloqr's commercial `@bloqr/compiler` product too — see [`src/adblock-compiler-core/README.md`](../../src/adblock-compiler-core/README.md#architecture) for that split and the backporting relationship between the two, and [`docs/backporting-policy.md`](../backporting-policy.md) for the process.

### Specific Improvements

#### 1. Better Error Handling

**Before (hostlist-compiler):**
```typescript
// Generic error
Error: Invalid configuration
```

**After (@bloqr/compiler-core):**
```typescript
// Descriptive error with context
ValidationError: Configuration validation failed
  - sources[0].source: Must be a valid URL or file path
  - transformations[2]: 'InvalidTransform' is not a valid transformation
  Valid transformations: RemoveComments, Deduplicate, Validate, ...
```

#### 2. Type Safety

**Before:**
```typescript
// No type hints
const rules = await compile(config);
```

**After:**
```typescript
// Full type inference
import type { IConfiguration } from '@bloqr/compiler-core';

const config: IConfiguration = {
  name: 'My Filter',
  sources: [/* TypeScript knows what goes here */],
  transformations: [/* Autocomplete available */]
};
```

#### 3. Dependency Injection

**Before:**
```typescript
// Hard-coded dependencies
const compiler = new FilterCompiler();
// Can't customize logging or hook into compilation events
```

**After:**
```typescript
// Inject a custom logger, or full options with event hooks
import { FilterCompiler } from '@bloqr/compiler-core';

const compiler = new FilterCompiler({
  logger: customLogger,
  events: {
    onCompilationStart: (e) => console.log('Starting', e.configName),
    onCompilationComplete: (e) => console.log('Done in', e.totalDurationMs, 'ms'),
  },
});

// A bare logger is also accepted (legacy-compatible shorthand)
const simpleCompiler = new FilterCompiler(customLogger);
```

## Installation and Usage

### Deno (Recommended)

```bash
# Add to deno.json imports
deno add jsr:@bloqr/compiler-core
```

**deno.json:**
```json
{
  "imports": {
    "@bloqr/compiler-core": "jsr:@bloqr/compiler-core@^1.0.0"
  }
}
```

### Node.js (via JSR)

```bash
# Install with npm using JSR proxy
npx jsr add @bloqr/compiler-core
```

### Basic Usage

```typescript
import { compile } from '@bloqr/compiler-core';
import type { IConfiguration } from '@bloqr/compiler-core';

// Define configuration
const config: IConfiguration = {
  name: 'My Filter List',
  description: 'Custom ad-blocking filters',
  version: '1.0.0',
  sources: [
    {
      name: 'EasyList',
      source: 'https://easylist.to/easylist/easylist.txt',
      type: 'adblock',
    },
    {
      name: 'Local Rules',
      source: './custom-rules.txt',
      type: 'adblock',
    },
  ],
  transformations: [
    'RemoveComments',
    'Deduplicate',
    'Validate',
    'InsertFinalNewLine',
  ],
};

// Compile rules
const rules: string[] = await compile(config);
console.log(`Compiled ${rules.length} rules`);
```

### Advanced Usage with Dependency Injection

```typescript
import { FilterCompiler } from '@bloqr/compiler-core';
import type { ILogger, IConfiguration } from '@bloqr/compiler-core';

// Custom logger implementation
class CustomLogger implements ILogger {
  info(message: string): void {
    console.log(`[INFO] ${message}`);
  }
  
  warn(message: string): void {
    console.warn(`[WARN] ${message}`);
  }
  
  error(message: string): void {
    console.error(`[ERROR] ${message}`);
  }
  
  debug(message: string): void {
    if (process.env.DEBUG) {
      console.debug(`[DEBUG] ${message}`);
    }
  }

  trace(message: string): void {
    if (process.env.TRACE) {
      console.debug(`[TRACE] ${message}`);
    }
  }
}

// Create compiler with custom logger
const logger = new CustomLogger();
const compiler = new FilterCompiler(logger);

// Compile with detailed logging
const result = await compiler.compile(config);
```

## CI/CD Integration

The `@bloqr/compiler-core` package is designed for seamless integration into CI/CD pipelines.

### GitHub Actions

#### Example 1: Compile and Deploy Filters

```yaml
name: Compile and Deploy Filters

on:
  push:
    branches: [main]
    paths:
      - 'data/input/**'
      - 'compiler-config.json'
  schedule:
    # Run daily to fetch updated remote lists
    - cron: '0 0 * * *'

jobs:
  compile-filters:
    runs-on: ubuntu-latest
    
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
      
      - name: Setup Deno
        uses: denoland/setup-deno@v2
        with:
          deno-version: v2.x
      
      - name: Cache Deno dependencies
        uses: actions/cache@v4
        with:
          path: ~/.cache/deno
          key: ${{ runner.os }}-deno-${{ hashFiles('**/deno.lock') }}
      
      - name: Compile filter rules
        run: |
          deno run --allow-read --allow-write --allow-env --allow-net \
            jsr:@bloqr/compiler-core/cli \
            --config compiler-config.json \
            --output data/output/filters.txt
      
      - name: Verify compilation
        run: |
          if [ ! -f data/output/filters.txt ]; then
            echo "Compilation failed - output file not created"
            exit 1
          fi
          
          RULE_COUNT=$(grep -v '^!' data/output/filters.txt | grep -v '^#' | grep -v '^$' | wc -l)
          echo "Compiled $RULE_COUNT rules"
          
          if [ $RULE_COUNT -lt 1000 ]; then
            echo "Warning: Rule count seems low ($RULE_COUNT)"
          fi
      
      - name: Commit updated filters
        run: |
          git config user.name "GitHub Actions"
          git config user.email "actions@github.com"
          git add data/output/filters.txt
          git diff --staged --quiet || git commit -m "Update compiled filters [skip ci]"
          git push
```

#### Example 2: Multi-Compiler Validation

Ensure all compilers produce identical output:

```yaml
name: Validate Compiler Equivalence

on:
  pull_request:
    paths:
      - 'src/rules-compiler-*/**'
      - 'compiler-config.json'

jobs:
  test-equivalence:
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Deno
        uses: denoland/setup-deno@v2
        with:
          deno-version: v2.x
      
      - name: Compile with TypeScript
        run: |
          cd src/adblock-compiler-core
          deno task compile
          cp ../../data/output/adguard_user_filter.txt /tmp/output-ts.txt
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Compile with .NET
        run: |
          cd src/rules-compiler-dotnet
          dotnet run --project src/RulesCompiler.Console
          cp ../../data/output/adguard_user_filter.txt /tmp/output-dotnet.txt
      
      - name: Compare outputs
        run: |
          # Files should be identical
          if ! diff /tmp/output-ts.txt /tmp/output-dotnet.txt; then
            echo "ERROR: Compiler outputs differ!"
            exit 1
          fi
          
          echo "✓ All compilers produce identical output"
```

### GitLab CI

```yaml
# .gitlab-ci.yml
stages:
  - compile
  - validate
  - deploy

compile-filters:
  stage: compile
  image: denoland/deno:latest
  
  cache:
    paths:
      - .deno_cache/
  
  before_script:
    - export DENO_DIR=.deno_cache
  
  script:
    - deno run --allow-read --allow-write --allow-env --allow-net
        jsr:@bloqr/compiler-core/cli
        --config compiler-config.json
        --output data/output/filters.txt
  
  artifacts:
    paths:
      - data/output/filters.txt
    expire_in: 30 days
  
  rules:
    - if: $CI_COMMIT_BRANCH == "main"
    - if: $CI_PIPELINE_SOURCE == "schedule"

validate-output:
  stage: validate
  image: alpine:latest
  
  dependencies:
    - compile-filters
  
  script:
    - RULE_COUNT=$(grep -c '^||' data/output/filters.txt || true)
    - echo "Compiled $RULE_COUNT adblock rules"
    - |
      if [ $RULE_COUNT -lt 100 ]; then
        echo "ERROR: Too few rules compiled"
        exit 1
      fi
```

### Jenkins Pipeline

```groovy
// Jenkinsfile
pipeline {
    agent any
    
    triggers {
        // Run daily at midnight
        cron('H 0 * * *')
    }
    
    environment {
        DENO_DIR = "${WORKSPACE}/.deno_cache"
    }
    
    stages {
        stage('Setup') {
            steps {
                sh 'curl -fsSL https://deno.land/install.sh | sh'
                sh 'export PATH="$HOME/.deno/bin:$PATH"'
            }
        }
        
        stage('Compile Filters') {
            steps {
                sh '''
                    deno run --allow-read --allow-write --allow-env --allow-net \
                        jsr:@bloqr/compiler-core/cli \
                        --config compiler-config.json \
                        --output data/output/filters.txt
                '''
            }
        }
        
        stage('Validate') {
            steps {
                script {
                    def ruleCount = sh(
                        script: "grep -v '^[!#]' data/output/filters.txt | grep -v '^\$' | wc -l",
                        returnStdout: true
                    ).trim()
                    
                    echo "Compiled ${ruleCount} rules"
                    
                    if (ruleCount.toInteger() < 1000) {
                        error("Rule count too low: ${ruleCount}")
                    }
                }
            }
        }
        
        stage('Archive') {
            steps {
                archiveArtifacts artifacts: 'data/output/filters.txt'
            }
        }
    }
    
    post {
        success {
            echo 'Filter compilation succeeded!'
        }
        failure {
            emailext(
                subject: "Filter Compilation Failed - ${env.JOB_NAME}",
                body: "Build ${env.BUILD_NUMBER} failed. Check console output.",
                to: 'team@example.com'
            )
        }
    }
}
```

### Direct CLI Usage in Scripts

#### Bash Script

```bash
#!/bin/bash
# compile-filters.sh

set -euo pipefail

CONFIG_FILE="${1:-compiler-config.json}"
OUTPUT_FILE="${2:-data/output/filters.txt}"

echo "Compiling filters using @bloqr/compiler-core..."
echo "  Config: $CONFIG_FILE"
echo "  Output: $OUTPUT_FILE"

# Compile filters
deno run --allow-read --allow-write --allow-env --allow-net \
  jsr:@bloqr/compiler-core/cli \
  --config "$CONFIG_FILE" \
  --output "$OUTPUT_FILE"

# Verify output
if [ -f "$OUTPUT_FILE" ]; then
  RULE_COUNT=$(grep -cv '^[!#]' "$OUTPUT_FILE" || true)
  echo "✓ Successfully compiled $RULE_COUNT rules"
else
  echo "✗ Compilation failed - output file not created"
  exit 1
fi

# Compute hash for verification
HASH=$(sha384sum "$OUTPUT_FILE" | awk '{print $1}')
echo "  SHA-384: $HASH"
```

#### PowerShell Script

```powershell
# Compile-Filters.ps1
[CmdletBinding()]
param(
    [string]$ConfigFile = "compiler-config.json",
    [string]$OutputFile = "data/output/filters.txt"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "Compiling filters using @bloqr/compiler-core..." -ForegroundColor Cyan
Write-Host "  Config: $ConfigFile" -ForegroundColor Gray
Write-Host "  Output: $OutputFile" -ForegroundColor Gray

# Compile filters
deno run --allow-read --allow-write --allow-env --allow-net `
  jsr:@bloqr/compiler-core/cli `
  --config $ConfigFile `
  --output $OutputFile

# Verify output
if (Test-Path $OutputFile) {
    $ruleCount = (Get-Content $OutputFile | Where-Object { 
        $_ -notmatch '^[!#]' -and $_ -ne '' 
    }).Count
    
    Write-Host "✓ Successfully compiled $ruleCount rules" -ForegroundColor Green
    
    # Compute hash
    $hash = (Get-FileHash -Path $OutputFile -Algorithm SHA384).Hash
    Write-Host "  SHA-384: $hash" -ForegroundColor Gray
} else {
    Write-Error "Compilation failed - output file not created"
    exit 1
}
```

### Docker Integration

```dockerfile
# Dockerfile for filter compilation service
FROM denoland/deno:latest

WORKDIR /app

# Copy configuration and source files
COPY compiler-config.json .
COPY data/ data/

# Install dependencies (cached layer)
RUN deno cache jsr:@bloqr/compiler-core/cli

# Compile filters on container start
CMD ["deno", "run", \
     "--allow-read", "--allow-write", "--allow-env", "--allow-net", \
     "jsr:@bloqr/compiler-core/cli", \
     "--config", "compiler-config.json", \
     "--output", "data/output/filters.txt"]
```

**docker-compose.yml:**
```yaml
version: '3.8'

services:
  filter-compiler:
    build: .
    volumes:
      - ./data/input:/app/data/input:ro
      - ./data/output:/app/data/output:rw
      - ./compiler-config.json:/app/compiler-config.json:ro
    environment:
      - DEBUG=true
```

## Architecture and Design

### Package Layout

The package is organized into a small number of top-level modules, each independently exported as a JSR subpath:

```
src/adblock-compiler-core/
├── src/index.ts            # Core compilation engine (@bloqr/compiler-core)
│                            #   FilterCompiler, SourceCompiler, compile(), transformations,
│                            #   downloader, formatters, ConfigurationValidator
├── src/orchestration/       # CLI/config/chunking wrapper layer (./orchestration)
│                            #   multi-format config reading, chunking, parallel compilation,
│                            #   hashing, structured logging, graceful shutdown
├── src/console/             # Interactive terminal UI (./console)
├── src/lib/                 # High-level builder-pattern API (./lib)
│                            #   RulesCompiler, ConfigurationBuilder
└── src/mod.ts               # Deno entry point — re-exports the core engine, runs the CLI
```

`FilterCompiler` is the orchestrator: it coordinates configuration validation (`ConfigurationValidator`), source compilation (`SourceCompiler`), and the transformation pipeline, with a logger and optional event hooks injected via its constructor. See [`src/adblock-compiler-core/README.md`](../../src/adblock-compiler-core/README.md#package-layout) for the full layout and how each subpath maps to a JSR export.

### Key Interfaces

```typescript
// Core compilation interface
export interface IConfiguration {
  name: string;
  description?: string;
  version?: string;
  homepage?: string;
  license?: string;
  sources: ISource[];
  transformations?: TransformationType[];
  inclusions?: string[];
  exclusions?: string[];
}

// Source configuration
export interface ISource {
  name?: string;
  source: string;  // URL or file path
  type?: SourceType;
  transformations?: TransformationType[];
  inclusions?: string[];
  exclusions?: string[];
}

// Logger interface for DI
export interface ILogger {
  info(message: string): void;
  warn(message: string): void;
  error(message: string): void;
  debug(message: string): void;
  trace(message: string): void;
}

// Validation result
export interface IValidationResult {
  valid: boolean;
  errors: string[];
  warnings: string[];
}
```

### Available Transformations

All 11 transformations supported by every compiler in this repository:

| Transformation | Description | Performance |
|---------------|-------------|-------------|
| `RemoveComments` | Remove comment lines (! and #) | O(n) |
| `Compress` | Convert hosts format to adblock | O(n) |
| `RemoveModifiers` | Remove unsupported modifiers | O(n) |
| `Validate` | Remove dangerous rules | O(n) |
| `ValidateAllowIp` | Validate with IP rules allowed | O(n) |
| `Deduplicate` | Remove duplicate rules | O(n log n) |
| `InvertAllow` | Convert exceptions to blocking | O(n) |
| `RemoveEmptyLines` | Remove blank lines | O(n) |
| `TrimLines` | Trim whitespace | O(n) |
| `InsertFinalNewLine` | Add final newline | O(1) |
| `ConvertToAscii` | Convert IDN to punycode | O(n) |

## Migrating a Compiler from hostlist-compiler

This is the migration this repository itself already completed for the .NET, Python, and Rust compilers (the TypeScript compiler *is* `@bloqr/compiler-core` — there's nothing to migrate there). If you maintain a similar subprocess-based compiler wrapper elsewhere, the same steps apply.

### Step 1: Update the Subprocess Target

**Before** (shelling out to AdGuard's npm package):
```bash
deno run --allow-read --allow-write --allow-env --allow-net --allow-run \
  npm:@adguard/hostlist-compiler \
  --config config.json --output output.txt
```

**After** (shelling out to this package's CLI export):
```bash
deno run --allow-read --allow-write --allow-env --allow-net --allow-run \
  jsr:@bloqr/compiler-core/cli \
  --config config.json --output output.txt
```

### Step 2: Update Library Imports (TypeScript consumers only)

**Before:**
```json
{
  "imports": {
    "@adguard/hostlist-compiler": "npm:@adguard/hostlist-compiler@^1.0.0"
  }
}
```

**After:**
```json
{
  "imports": {
    "@bloqr/compiler-core": "jsr:@bloqr/compiler-core@^1.0.0"
  }
}
```

```typescript
import { compile } from '@bloqr/compiler-core';
```

### Step 3: Verify Compilation

The two packages are **not guaranteed to produce byte-identical output** — `@bloqr/compiler-core` is an independent implementation, not a fork. Diff a real config's output against your previous compiler before switching a production pipeline over:

```bash
# Compile with the old subprocess target
deno run --allow-all old-compiler-invocation.ts > output-old.txt

# Compile with @bloqr/compiler-core
deno run --allow-read --allow-write --allow-env --allow-net --allow-run \
  jsr:@bloqr/compiler-core/cli --config config.json --output output-new.txt

diff output-old.txt output-new.txt
```

Each non-TypeScript compiler in this repo (`src/rules-compiler-dotnet/`, `src/rules-compiler-python/`, `src/rules-compiler-rust/`) keeps its existing public API and field names (e.g. `VersionInfo.HostlistCompilerVersion` in .NET) unchanged across this migration — only what populates them changed, to avoid a second breaking change for downstream consumers.

## API Reference

### Core Functions

#### `compile(config: IConfiguration): Promise<string[]>`

Compiles filter rules from the provided configuration.

**Parameters:**
- `config`: Configuration object defining sources and transformations

**Returns:**
- Promise resolving to array of compiled filter rules (strings)

**Example:**
```typescript
const rules = await compile({
  name: 'My Filter',
  sources: [{ source: 'https://example.com/rules.txt' }],
  transformations: ['Deduplicate']
});
```

### Classes

#### `FilterCompiler`

Main compiler class with dependency injection support.

**Constructor:**
```typescript
constructor(optionsOrLogger?: FilterCompilerOptions | ILogger)
```

Accepts either a bare `ILogger` (legacy shorthand) or a `FilterCompilerOptions` object with `logger`, `events` (compilation lifecycle hooks), and `hookManager` (transformation hooks).

**Methods:**

- `compile(config: IConfiguration): Promise<string[]>`
  - Compiles rules from configuration

Configuration validation is a separate concern — use `ConfigurationValidator` (below) to validate without compiling.

**Example:**
```typescript
import { FilterCompiler } from '@bloqr/compiler-core';

const compiler = new FilterCompiler(customLogger);
const rules = await compiler.compile(config);
```

#### `ConfigurationValidator`

Validates a configuration object against the package's Zod schema, independent of compilation.

```typescript
import { ConfigurationValidator } from '@bloqr/compiler-core';

const validator = new ConfigurationValidator();
const result = validator.validate(config); // IValidationResult
```

### Type Exports

All TypeScript interfaces are exported for type safety:

```typescript
import type {
  IConfiguration,
  ISource,
  ILogger,
  IValidationResult,
  TransformationType,
  SourceType
} from '@bloqr/compiler-core';
```

## Best Practices

### 1. Use Type Imports

```typescript
// Separate type imports from value imports
import { compile } from '@bloqr/compiler-core';
import type { IConfiguration } from '@bloqr/compiler-core';
```

### 2. Validate Configuration Early

```typescript
import { ConfigurationValidator, FilterCompiler } from '@bloqr/compiler-core';

const validator = new ConfigurationValidator();
const validation = validator.validate(config);

if (!validation.valid) {
  console.error('Configuration errors:', validation.errors);
  process.exit(1);
}

const compiler = new FilterCompiler();
const rules = await compiler.compile(config);
```

### 3. Use Custom Logger for Production

```typescript
import { FilterCompiler } from '@bloqr/compiler-core';
import type { ILogger } from '@bloqr/compiler-core';

class ProductionLogger implements ILogger {
  info(msg: string) { /* Send to logging service */ }
  warn(msg: string) { /* Alert monitoring */ }
  error(msg: string) { /* Create incident */ }
  debug(msg: string) { /* Disable in production */ }
  trace(msg: string) { /* Disable in production */ }
}

const compiler = new FilterCompiler(new ProductionLogger());
```

### 4. Cache Compiled Results

```typescript
// Cache compilation results to avoid redundant downloads
const cacheKey = JSON.stringify(config);
const cached = await cache.get(cacheKey);

if (cached) {
  return cached;
}

const rules = await compile(config);
await cache.set(cacheKey, rules, { ttl: 3600 });
```

## Troubleshooting

### Common Issues

#### 1. JSR Registry Unreachable

**Symptom:** `Failed to load jsr:@bloqr/compiler-core`

**Solutions:**
- Check internet connectivity to jsr.io
- Verify firewall/proxy settings allow JSR access
- For CI runners without JSR access, vendor `src/adblock-compiler-core/` directly or run from a local path import instead of `jsr:`

#### 2. Type Errors After Upgrading

**Symptom:** TypeScript errors about incompatible types after bumping the pinned version

**Solution:**
```bash
# Clear Deno cache and reload
deno cache --reload src/mod.ts
```

#### 3. deno run jsr:@bloqr/compiler-core/cli not found (.NET/Python/Rust compilers)

**Symptom:** The .NET, Python, or Rust compiler reports the compiler CLI is unavailable

**Solution:** These compilers shell out to Deno, not to a local binary. Confirm Deno is installed and on `PATH`:
```bash
deno --version
deno run --allow-read --allow-write --allow-env --allow-net --allow-run jsr:@bloqr/compiler-core/cli --version
```

## Resources

- **JSR Package**: https://jsr.io/@bloqr/compiler-core
- **Source Code**: [`src/adblock-compiler-core/`](../../src/adblock-compiler-core/) in this repo
- **Issue Tracker**: https://github.com/BloqrAI/bloqr-lists/issues
- **This Repository**: https://github.com/BloqrAI/bloqr-lists
- **Documentation Website**: https://bloqrai.github.io/bloqr-lists/
- **ADR 0001**: [Canonical Rules Compilation Engine](../adr/0001-canonical-rules-compilation-engine.md)
- **Backporting Policy**: [docs/backporting-policy.md](../backporting-policy.md)

## Version History

- **v1.0.0**: First stable release under this repo's ownership. Extracted from the commercial `bloqr-compiler`'s core engine, with all AGTree/AdGuard-library-dependent and Cloudflare-specific code excluded. Replaces the JSR namespace's previous interim use (v0.6.0–v0.96.0, which briefly pointed at commercial `bloqr-compiler` snapshots — see [ADR 0001](../adr/0001-canonical-rules-compilation-engine.md) for that history).

See [`src/adblock-compiler-core/CHANGELOG.md`](../../src/adblock-compiler-core/CHANGELOG.md) for the full changelog going forward.

## Contributing

Contributions to `@bloqr/compiler-core` are welcome! Please:

1. Work in [`src/adblock-compiler-core/`](../../src/adblock-compiler-core/) in this repository
2. Review [CONTRIBUTING.md](../../CONTRIBUTING.md)
3. Submit issues or pull requests against [BloqrAI/bloqr-lists](https://github.com/BloqrAI/bloqr-lists)

## License

GPL-3.0 - See [LICENSE](../../LICENSE) for details.
