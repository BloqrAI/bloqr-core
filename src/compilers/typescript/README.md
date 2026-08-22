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

- Deno 2.x or later (this package's own runtime)
- [Bun](https://bun.sh/) is supported as an alternative runtime target for consumers — see [Bun (Supported)](#bun-supported) below. Node.js works too, since Bun's compatibility layer is the same one Node.js implements, but Deno and Bun are the two runtimes this package is actually verified against.

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

### Benchmarking

`--benchmark` compiles the canned `benchmarks/data/{small,medium,large,xlarge}.txt` datasets
through the real `runCompiler()` pipeline - not a simulation - once unchunked and once
chunked, and reports the actual elapsed time for both. This is the reference implementation
every other language wrapper shells out to, so its numbers matter most for interpreting the
others' overhead. Part of
[epic #415](https://github.com/BloqrAI/bloqr-core/issues/415)'s per-compiler benchmark work.

```bash
# Benchmark all four canned dataset sizes, chunked vs unchunked (auto-discovers benchmarks/data)
deno task benchmark

# Just one size, with 8 duplicated sources and 8 parallel workers for the chunked run
deno task start -- --benchmark --benchmark-size large --benchmark-sources 8 --benchmark-max-parallel 8

# Machine-readable output for the root comparison script (see benchmarks/)
deno task start -- --benchmark --benchmark-json

# Point at a benchmarks/data directory explicitly (e.g. when not run from a repo checkout)
deno task start -- --benchmark --benchmark-data-dir /path/to/benchmarks/data
```

| Option | Description |
|--------|-------------|
| `--benchmark-size` | Dataset size to benchmark: `small`, `medium`, `large`, `xlarge`, or `all` (default: `all`) |
| `--benchmark-data-dir` | Directory containing the canned benchmark data (default: auto-discovered) |
| `--benchmark-sources` | Number of identical duplicated sources for the chunked run (default: 4) |
| `--benchmark-max-parallel` | Max parallel workers for the chunked run (default: CPU count, max 8) |
| `--benchmark-json` | Emit machine-readable JSON instead of a human-readable table |

Both runs cover the same total workload (`--benchmark-sources` identical copies of the dataset
file, one per chunk), so chunking strategy is the only intended variable - unlike the Rust and
.NET wrappers (see [#424](https://github.com/BloqrAI/bloqr-core/issues/424)), there's no risk of
the two runs silently using different compilers under the hood: `runCompiler()` always drives
both paths through the same `compile()` core engine.

### Bun (Supported)

[Bun](https://bun.sh/) is supported as an alternative runtime target. As a library, `import { compile } from '@bloqr/compiler-core'` works under Bun with no setup beyond installing the package. The CLI/interactive layer (`src/orchestration/`, `src/console/`) additionally depends on a few JSR and npm packages that Bun — unlike Deno — can't resolve from `deno.json`'s import map, so populate `node_modules` first, from a local clone of this package:

```bash
# JSR packages, via JSR's own Bun installer bridge (https://jsr.io/docs/with/bun)
bunx jsr add @std/yaml @std/toml @std/fmt @cliffy/table

# zod is aliased to the bare `zod` specifier (matching deno.json's import map),
# so it needs an explicit alias rather than `bunx jsr add @zod/zod`
bun add zod@npm:@jsr/zod__zod@^4.4.3

# npm-native packages with no JSR-native equivalent
bun add ora figlet @inquirer/prompts
```

Then run the CLI via the dedicated Bun entry point (mirrors `src/mod.ts`, the Deno entry point):

```bash
bun run src/mod.bun.ts -c compiler-config.json -o output.txt
bun run src/mod.bun.ts --version   # reports "Runtime: Bun x.y.z"
```

Deno remains this package's own runtime and JSR remains the source of truth for its dependencies (`deno.json`) — the `node_modules` population above is only for Bun consumers running this package's CLI/interactive layer directly, not something this repository's own tooling uses. CI (`.github/workflows/typescript.yml`, `bun-support` job) runs these exact install commands and CLI/library smoke tests against real Bun on every PR.

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
- `deno task benchmark` - Benchmark real compilation performance, chunked vs unchunked (see [Benchmarking](#benchmarking))
- `deno task test` - Run tests
- `deno task check` - Type check the code
- `deno task lint` - Lint the code
- `deno task lint:docs` - Check JSDoc coverage on exported symbols (enforces the ≥98% threshold this package's JSR score depends on — see [Development](#development))
- `deno task fmt` - Format the code
- `deno task generate:types` - Generate `.d.ts` type definition files

## Package layout

- `src/index.ts` — the core compilation engine (transformations, compiler, downloader, formatters). This is what `@bloqr/compiler-core` (package root) resolves to.
- `src/orchestration/` — the CLI/config/chunking wrapper layer built on top of the core engine (multi-format config reading, chunking, parallel compilation, hashing, structured logging, graceful shutdown). Exported as the `./orchestration` subpath.
- `src/console/` — the interactive terminal UI, exported as `./console`.
- `src/lib/` — a high-level builder-pattern API (`BloqrCompiler`, `ConfigurationBuilder`), exported as `./lib`.
- `src/mod.ts` — the Deno entry point; re-exports the core engine and runs the CLI when executed directly.
- `src/mod.bun.ts` — the Bun entry point (exported as `./bun`), for the same purpose from a local clone; see [Bun (Supported)](#bun-supported).

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

**Every exported symbol needs JSDoc.** This package's JSR score depends on
documentation coverage of its full public API — not just top-level exports,
but enum members and public interface/class properties and methods too.
`deno task lint:docs` checks exactly the set JSR itself scores against and
fails (with a list of what's missing) below 98% coverage; run it after
adding or changing any exported symbol, and expect CI to fail if you skip
it locally.
