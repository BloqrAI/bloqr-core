# Docker Guide

This guide covers the development container for Bloqr Core. The repository does
not provide a production service image; deployable services belong in their
own repositories and environments.

## Docker Development Environment

The repository includes a pre-configured Linux development environment for
TypeScript/Deno, .NET, Python, Rust, and PowerShell. Swift is macOS-native and
is built and tested on the macOS CI runner.

### Included Components

| Component | Version | Purpose |
|-----------|---------|---------|
| .NET SDK | 10.0 | .NET compiler, common library, and Dashboard |
| Deno | 2.6.3 | TypeScript compiler and `@bloqr/compiler-core` |
| Python | 3.12 | Python compiler |
| Rust | 1.86 | Rust compiler and validator |
| PowerShell | 7.x | PowerShell scripts and modules |
| Git | Latest | Version control |
| Ubuntu | 24.04 (Noble) | Base OS |

### Pre-installed Tools

- **Deno packages**: `@bloqr/compiler-core` (cached from JSR)
- **Python packages**: `pytest`, `pytest-cov`, `mypy`, `ruff`, `pyyaml`, `tomlkit`
- **Rust components**: `clippy`, `rustfmt`
- **PowerShell modules**: `Pester`, `PSScriptAnalyzer`

## Building the Docker Image

### Standard Build

```bash
docker build -f Dockerfile.warp -t ad-blocking-dev .
```

The image versions are defined in `Dockerfile.warp` and aligned with
`rust-toolchain.toml`. Override them only when intentionally testing another
toolchain version.

```bash
docker build -f Dockerfile.warp \
  --build-arg DENO_VERSION=2.6.3 \
  --build-arg RUST_VERSION=1.86 \
  -t ad-blocking-dev:custom .
```

### Multi-platform Build

```bash
docker buildx build -f Dockerfile.warp \
  --platform linux/amd64,linux/arm64 \
  -t ad-blocking-dev:multiarch .
```

## Running the Container

### Interactive Mode

```bash
docker run -it -v $(pwd):/workspace ad-blocking-dev
```

### With Environment Variables

```bash
docker run -it \
  -v $(pwd):/workspace \
  -e "DEBUG=1" \
  ad-blocking-dev
```

Do not pass credentials through the Compose configuration. The compilers in
this repository do not require AdGuard API credentials.

### Detached Mode

```bash
docker run -d \
  -v $(pwd):/workspace \
  --name ad-blocking-container \
  ad-blocking-dev \
  sleep infinity
```

Then execute commands:

```bash
docker exec ad-blocking-container deno task compile
```

## Docker Compose

The repository includes a `docker-compose.yml` for multi-service orchestration.

### Available Services

| Service | Description | Profile |
|---------|-------------|---------|
| `dev` | Main development environment | default |
| `typescript-compiler` | TypeScript rules compiler | compile |
| `dotnet-compiler` | .NET compiler | compile |
| `python-compiler` | Python rules compiler | compile |
| `rust-compiler` | Rust rules compiler | compile |
| `powershell-compiler` | PowerShell compiler toolkit | compile |
| `test` | Run Linux-supported repository test suites | test |

### Basic Usage

```bash
# Start main development environment
docker compose up -d dev

# Enter the container
docker compose exec dev bash

# Stop all services
docker compose down
```

### Running Specific Services

```bash
# Run TypeScript compiler
docker compose --profile compile run --rm typescript-compiler

# Run a compiler once (choose one service)
docker compose --profile compile run --rm dotnet-compiler
docker compose --profile compile run --rm python-compiler
docker compose --profile compile run --rm rust-compiler
docker compose --profile compile run --rm powershell-compiler

# Run all tests
docker compose --profile test run --rm test
```

> The AdGuard DNS API client console UI moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) and no longer has a service in this repo's `docker-compose.yml`.

### Environment Variables

The optional `DEBUG` variable can be set in a root `.env` file:

```bash
# .env
DEBUG=1
```

## Development Workflow

### Initial Setup (Inside Container)

```bash
# TypeScript compiler (Deno caches dependencies automatically)
cd /workspace/src/compilers/typescript
deno cache src/mod.ts

# Python compiler
cd /workspace/src/compilers/python
python3 -m pip install --break-system-packages -e ".[dev]"

# Rust compiler (core lib + CLI crate)
cargo build -p bloqr-compiler-core -p bloqr-compiler
```

### Compiling Filter Rules

```bash
# TypeScript (Deno)
cd /workspace/src/compilers/typescript
deno task compile

# .NET
cd /workspace/src/compilers/dotnet
dotnet run --project src/Bloqr.Compiler.Dotnet.Console

# Python
cd /workspace/src/compilers/python
bloqr-compiler

# Rust
cd /workspace/src/compilers/rust/cli
cargo run --release

# PowerShell
cd /workspace
pwsh -Command "Import-Module ./src/compilers/powershell/BloqrCompiler/BloqrCompiler.psd1; Invoke-BloqrCompiler"
```

### Running Tests

```bash
# TypeScript tests (Deno)
cd /workspace/src/compilers/typescript
deno task test

# Shared .NET library, compiler, and Dashboard tests
cd /workspace/src/common/dotnet
dotnet test CompilerCommon.slnx
cd /workspace/src/compilers/dotnet
dotnet test CompilerDotnet.slnx
cd /workspace/src/apps/dashboard
dotnet test BloqrDashboard.slnx

# Python tests
cd /workspace/src/compilers/python
pytest

# Rust workspace tests (compiler and validator)
cargo test --workspace

# PowerShell tests
cd /workspace
pwsh -Command "Invoke-Pester -Path ./src/compilers/powershell -Recurse"

# Or run all tests via docker compose
docker compose --profile test run --rm test
```

The Docker test profile covers Linux-supported test suites. Run Swift tests
separately on macOS with `cd src/compilers/swift && swift test`.

## CI/CD with Docker

The repository workflow at `.github/workflows/docker-image.yml` validates the
Compose configuration and builds the development image when Docker artifacts
change.

## Troubleshooting

### Permission Issues

If you encounter permission issues with mounted volumes:

```bash
# Run as current user
docker run -it -v $(pwd):/workspace -u $(id -u):$(id -g) ad-blocking-dev
```

### Deno Cache Issues

If Deno cache has issues between host and container:

```bash
# Clear Deno cache and re-cache dependencies
docker run -it -v $(pwd):/workspace ad-blocking-dev bash -c "
  rm -rf /root/.deno/deps
  cd /workspace/src/compilers/typescript
  deno cache src/mod.ts
"
```

### .NET Restore Failures

If .NET restore fails:

```bash
docker run -it -v $(pwd):/workspace ad-blocking-dev bash -c "
  cd /workspace/src/compilers/dotnet
  dotnet nuget locals all --clear
  dotnet restore CompilerDotnet.slnx
"
```

### Cargo/Rust Issues

If Rust compilation fails:

```bash
docker run -it -v $(pwd):/workspace ad-blocking-dev bash -c "
  cd /workspace
  cargo clean -p bloqr-compiler-core -p bloqr-compiler
  cargo build -p bloqr-compiler-core -p bloqr-compiler
"
```

### Container Won't Start

Check if the image exists:

```bash
docker images | grep ad-blocking-dev
```

Rebuild if necessary:

```bash
docker build --no-cache -f Dockerfile.warp -t ad-blocking-dev .
```

### Volume Caching Issues

Reset the named caches if dependency caching causes problems:

```bash
# Remove the repository's named dependency caches
docker volume rm ad-blocking-deno-cache ad-blocking-cargo-registry ad-blocking-cargo-git ad-blocking-nuget

# Or remove all unused volumes
docker volume prune
```
