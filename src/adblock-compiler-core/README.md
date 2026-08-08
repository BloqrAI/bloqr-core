# @bloqr/compiler-core

TypeScript/Deno implementation of an AdGuard-hostlist-compiler-compatible filter rules compiler, with chunked parallel compilation, hash verification, and a full interactive console.

This is the canonical source for the `@bloqr/compiler-core` JSR package. It's a minimal, dependency-free compilation engine — no `@adguard/agtree` or other third-party AdGuard library, no Cloudflare-specific code, no commercial features. See [Architecture](#architecture) below for how this relates to Bloqr's commercial compiler.

## Features

- Compiles AdGuard-syntax filter rules from multiple sources (URL or local file)
- Supports JSON, YAML, and TOML configuration formats
- SHA-384 hashing for output verification
- Chunked parallel compilation for large rule lists (10M+ entries)
- Interactive and CLI modes
- Comprehensive error handling and structured logging
- Zero third-party AdGuard library dependency (see [Architecture](#architecture))

## Requirements

- Deno 2.x or later

## Installation

```bash
deno add jsr:@bloqr/compiler-core
```

## Usage

### As a library

```typescript
import { compile } from '@bloqr/compiler-core';

const rules = await compile({
  name: 'My Filter List',
  sources: [{ source: 'https://example.com/list.txt', type: 'adblock' }],
  transformations: ['RemoveComments', 'Deduplicate'],
});
console.log(`Compiled ${rules.length} rules`);
```

### Interactive Mode

```bash
deno task interactive
```

### Compile from Config

```bash
deno task compile
```

### Command-Line Options

```bash
# Show help
deno task start -- --help

# Compile with specific config
deno task start -- -c config.yaml

# Compile with validation disabled
deno task start -- -c config.yaml --no-validate-config

# Fail on validation warnings
deno task start -- -c config.yaml --fail-on-warnings

# Validate configuration only
deno task start -- --validate -c config.yaml

# Show version information
deno task start -- --version
```

| Option | Description |
|--------|-------------|
| `-c, --config PATH` | Path to configuration file |
| `-o, --output PATH` | Path to output file |
| `-r, --copy-to-rules` | Copy output to rules directory |
| `--rules-dir PATH` | Custom rules directory path |
| `-f, --format FORMAT` | Force configuration format (json, yaml, toml) |
| `-v, --version` | Show version information |
| `-h, --help` | Show help message |
| `-d, --debug` | Enable debug output |
| `--show-config` | Show parsed configuration (don't compile) |
| `--validate-config` | Enable configuration validation before compilation (default: true) |
| `--no-validate-config` | Disable configuration validation before compilation |
| `--fail-on-warnings` | Fail compilation if configuration has validation warnings |
| `-i, --interactive` | Run in interactive menu mode |
| `--compile` | Run in CLI mode (compile and exit) |
| `--validate` | Validate configuration only |
| `--enable-chunking` | Enable chunked parallel compilation for large rule lists |
| `--chunk-size N` | Number of sources per chunk (when using source-based chunking) |
| `--max-parallel N` | Maximum number of chunks to compile in parallel (default: CPU count) |

### Chunked Parallel Compilation

For large rule lists (e.g., 10+ million entries), chunking splits compilation into parallel chunks for improved performance:

**Command-line usage:**
```bash
# Enable chunking with default settings (CPU count parallel workers)
deno task start -- --enable-chunking

# Custom parallel workers and chunk settings
deno task start -- --enable-chunking --max-parallel 8

# Fine-tune chunk size (sources per chunk)
deno task start -- --enable-chunking --chunk-size 50000 --max-parallel 4
```

**Configuration file:**
```json
{
  "name": "My Filter List",
  "chunking": {
    "enabled": true,
    "strategy": "source",
    "maxParallel": 4
  },
  "sources": [
    { "source": "https://example.com/list1.txt" },
    { "source": "https://example.com/list2.txt" }
  ]
}
```

**How it works:**
1. Splits sources into N chunks (based on `maxParallel`)
2. Compiles each chunk in parallel using Promise.all batching
3. Merges and deduplicates results
4. Preserves SHA-384 hash consistency

**When to use:**
- Large number of sources (10+)
- Very large individual sources (1M+ rules)
- Multi-core systems with available CPU resources

### Generate Type Definitions

To generate TypeScript declaration files (`.d.ts`):

```bash
deno task generate:types
```

This creates `.d.ts` files in the `dist/` directory that re-export types from the source files, for consumers of this library on non-Deno toolchains. These files are generated, not hand-edited, and excluded from version control.

## Available Tasks

- `deno task start` - Run the compiler
- `deno task interactive` - Run in interactive mode
- `deno task compile` - Compile from default config
- `deno task test` - Run tests
- `deno task check` - Type check the code
- `deno task lint` - Lint the code
- `deno task fmt` - Format the code
- `deno task generate:types` - Generate `.d.ts` type definition files

## Package layout

- `src/index.ts` — the core compilation engine (transformations, compiler, downloader, formatters). This is what `@bloqr/compiler-core` (package root) resolves to.
- `src/orchestration/` — the CLI/config/chunking wrapper layer built on top of the core engine (multi-format config reading, chunking, parallel compilation, hashing, structured logging, graceful shutdown). Exported as the `./orchestration` subpath.
- `src/console/` — the interactive terminal UI, exported as `./console`.
- `src/lib/` — a high-level builder-pattern API (`RulesCompiler`, `ConfigurationBuilder`), exported as `./lib`.
- `src/mod.ts` — the Deno entry point; re-exports the core engine and runs the CLI when executed directly.

## Architecture

This package is one of two Bloqr filter-list compilers, and the two are deliberately kept separate:

| | `@bloqr/compiler-core` (this package) | Bloqr's commercial compiler ([`bloqr-compiler`](https://github.com/BloqrAI/bloqr-compiler) repo, private) |
|---|---|---|
| Scope | Bare-minimum compilation engine + performance features | Full-featured product: linting, AST tooling, diff reports, plugin system, Cloudflare Workers deployment, observability |
| AdGuard libraries (`@adguard/agtree`, etc.) | Not used — rule classification is string/regex-based | Used, for AST-level rule parsing and validation |
| Distribution | JSR, open source (`@bloqr/compiler-core`) | Not published to JSR — consumes `@bloqr/compiler-core` as a regular dependency instead of vendoring its own copy |

**Why this moved off the `@jk-com` scope**: the package was originally published as `@jk-com/adblock-compiler`, a personal-project JSR scope. All Bloqr JSR packages — this one and future ones like `@bloqr/diagnostics` — now live under the `@bloqr` scope. `@jk-com/adblock-compiler`'s early versions (0.6.0 through 0.96.0) were actually snapshots of the commercial `bloqr-compiler` product, before that product grew Cloudflare-specific and AdGuard-library-dependent features that don't belong in an open-source, dependency-free package; this repository's compiler — previously a thin orchestration wrapper *around* whatever the old JSR package resolved to — was promoted to be the actual engine, starting at v1.0.0, now published as `@bloqr/compiler-core`.

**Backporting**: performance improvements and core-engine bug fixes discovered in the commercial compiler may be backported into this package when they don't require an AdGuard library or commercial-only infrastructure. See `docs/backporting-policy.md` (repo root) for the criteria and process.

**AGTree decoupling**: `bloqr-compiler` is moving to depend on this package directly (via JSR) rather than vendoring its own core copy, eliminating manual backporting — tracked as [bloqr-compiler#2200](https://github.com/BloqrAI/bloqr-compiler/issues/2200).

## Development

1. Make changes to the TypeScript source files in `src/`
2. Run tests: `deno task test`
3. Type check: `deno task check`
4. Generate type definitions: `deno task generate:types`
