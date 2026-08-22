# Benchmarks

Real-pipeline chunked-vs-unchunked compilation benchmarking, shared across all five
language wrappers. Part of [epic #415](https://github.com/BloqrAI/bloqr-core/issues/415).

## Data (single source of truth)

`data/` holds four canned, fixed-seed synthetic filter lists that every language's
`benchmark` command reads from - no language invents its own workload, so results are
directly comparable across languages and across runs on the same machine.

| Dataset | Rules | Approx. size |
|---------|------:|-------------:|
| `data/small.txt` | ~10,000 | 235 KB |
| `data/medium.txt` | ~50,000 | 1.2 MB |
| `data/large.txt` | ~200,000 | 4.7 MB |
| `data/xlarge.txt` | ~500,000 | 11.7 MB |

Regenerate them (deterministic given the same seed) with:

```bash
cd benchmarks
python3 generate_synthetic_data.py --all
```

`data/benchmark_data_summary.json` records rule/line/byte counts and a content hash for
each generated file. The data is synthetic and intentionally includes some AdGuard
syntax that `bloqr-validate` correctly rejects (mixed valid/invalid rules, matching a
real-world unsanitized list) - a benchmark run against it can fail the mandatory
rules-validator check the same way a real compile would, which is expected, not a bug in
any of the five wrappers.

**Pointing a benchmark run at a config file instead of the canned data:** every
language's `benchmark` command also accepts a data-directory override (`--benchmark-data-dir`
/ `--data-dir` / `-DataDirectory`), so you can point it at any directory containing your
own `{small,medium,large,xlarge}.txt` files shaped the same way, without touching this
directory's canonical set.

`data/config-*.json` are illustrative example configs (not read by any `benchmark`
command - each language builds its own benchmark config in memory from `--benchmark-sources`
identical copies of the dataset file) showing the config shape those copies follow.

## The benchmark JSON contract

Every language's `benchmark` command accepts the same shape of parameters and, with its
JSON-output flag set (`--json` / `--benchmark-json` / `-AsJson`), emits a JSON array with
one object per benchmarked dataset size:

```json
[
  {
    "size": "small",
    "sources": 4,
    "maxParallel": 4,
    "unchunkedSuccess": true,
    "unchunkedMs": 1482,
    "unchunkedRuleCount": 8795,
    "chunkedSuccess": true,
    "chunkedMs": 611,
    "chunkedRuleCount": 8795,
    "speedup": 2.43,
    "error": null
  }
]
```

| Field | Type | Description |
|-------|------|-------------|
| `size` | string | Dataset size benchmarked (`small`/`medium`/`large`/`xlarge`) |
| `sources` | number | Identical duplicated sources used for the chunked run (one per chunk) |
| `maxParallel` | number | Max parallel workers for the chunked run |
| `unchunkedSuccess` / `chunkedSuccess` | boolean | Whether that run completed successfully |
| `unchunkedMs` / `chunkedMs` | number | Real elapsed milliseconds for that run |
| `unchunkedRuleCount` / `chunkedRuleCount` | number | Rule count in that run's compiled output |
| `speedup` | number \| null | `unchunkedMs / chunkedMs`; `null` if either run failed or `chunkedMs` was 0 |
| `error` | string \| null | Error message if either run failed, else `null` |

Both runs within one result cover the same total workload (`sources` identical copies of
the dataset file, one per chunk) - chunking strategy is the only intended variable. See
[#424](https://github.com/BloqrAI/bloqr-core/issues/424): in Rust, .NET, and Python the
unchunked and chunked paths currently shell out to two *different* underlying compilers,
so part of any timing delta there may reflect that rather than chunking overhead alone.
TypeScript and PowerShell don't have this gap - both paths use the same underlying
compiler in both languages.

## Per-language `benchmark` commands

| Language | Command |
|----------|---------|
| Rust | `cargo run -p bloqr-compiler -- benchmark --size small --json` |
| .NET | `dotnet run --project src/compilers/dotnet/src/Bloqr.Compiler.Dotnet.Console -- --benchmark --benchmark-size small --benchmark-json` |
| TypeScript | `deno task --cwd src/compilers/typescript benchmark -- --benchmark-size small --benchmark-json` |
| Python | `bloqr-compiler --benchmark --benchmark-size small --benchmark-json` |
| PowerShell | `Invoke-BloqrCompilerBenchmark -Size small -AsJson` |

Each accepts a dataset size (`small`/`medium`/`large`/`xlarge`/`all`), a data directory
override, a source count, and a max-parallel override - see that language's own README
(`src/compilers/<language>/README.md`) for its exact flag names.

## Dashboard integration

The Dashboard's Diagnostics menu ("Run benchmark (.NET compiler)") calls the same
`Bloqr.Compiler.Dotnet` benchmark logic in-process via `IDashboardService`/`IBenchmarkService`
(#423) - not a subprocess, and not synthetic. Per the epic's scope decision, the Dashboard only
benchmarks the .NET compiler; use `benchmark-all.sh`/`.ps1` or the root Launcher's "Benchmark
Compilers" menu to compare across all five languages.

## Running all of them and comparing

`benchmark-all.sh` / `benchmark-all.ps1` at the repo root run every available language's
native `benchmark` command (skipping any whose toolchain isn't installed, the same
tool-detection convention `launcher.sh`/`launcher.ps1` use), print a comparison table, and
write a combined JSON summary. See the repo root's own docs or `--help` on either script
for usage.

## Retired scripts

`run_benchmarks.py` and `quick_benchmark.py` (both synthetic/simulated, predating the real
per-language `benchmark` commands added in epic #415) have been removed - their
orchestration purpose is now served by the root `benchmark-all.sh`/`.ps1` scripts driving
real native benchmarks instead of a Python script re-implementing timing simulation for
other languages via subprocess.
