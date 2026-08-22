# Docker Guide

This guide covers using Docker for development with the ad-blocking toolkit.

## Docker Development Environment

The repository includes a pre-configured Docker environment with all necessary dependencies for all compilers (TypeScript, .NET, Python, Rust) and tools.

### Dockerfile.warp

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble

# Build arguments for version control
ARG DENO_VERSION=2.x
ARG RUST_VERSION=stable

# Includes installation of:
# - Deno 2.x
# - Python 3.12 with pip
# - Rust stable toolchain
# - PowerShell 7
# - @bloqr/compiler-core (via Deno JSR integration)

WORKDIR /workspace
```

### Included Components

| Component | Version | Purpose |
|-----------|---------|---------|
| .NET SDK | 10.0 | .NET compiler, API client |
| Deno | 2.x | TypeScript compiler, `@bloqr/compiler-core` |
| Python | 3.12 | Python compiler |
| Rust | Stable | Rust compiler |
| PowerShell | 7.x | PowerShell scripts and modules |
| Git | Latest | Version control |
| @bloqr/compiler-core | 1.0.0 | Via Deno JSR integration; the .NET/Python/Rust compilers all shell out to it (`deno run jsr:@bloqr/compiler-core/cli`) |
| Ubuntu | 24.04 (Noble) | Base OS |

### Pre-installed Tools

- **Deno packages**: `@bloqr/compiler-core` (via JSR)
- **Python packages**: `pytest`, `pytest-cov`, `mypy`, `ruff`, `pyyaml`, `tomlkit`
- **Rust components**: `clippy`, `rustfmt`
- **PowerShell modules**: `Pester`, `PSScriptAnalyzer`

## Building the Docker Image

### Standard Build

```bash
docker build -f Dockerfile.warp -t ad-blocking-dev .
```

### With Build Arguments

```bash
docker build -f Dockerfile.warp \
  --build-arg DENO_VERSION=2.x \
  --build-arg RUST_VERSION=stable \
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
  -e "AdGuard__ApiKey=your-api-key" \
  -e "DEBUG=1" \
  ad-blocking-dev
```

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
| `test` | Run all tests | test |

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

# Run all compilers
docker compose --profile compile up

# Run all tests
docker compose --profile test run --rm test
```

> The AdGuard DNS API client console UI moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) and no longer has a service in this repo's `docker-compose.yml`.

### Environment Variables

Create a `.env` file in the project root:

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

# .NET projects
cd /workspace/src/compilers/dotnet
dotnet restore CompilerDotnet.slnx

# Python compiler
cd /workspace/src/compilers/python
pip install -e ".[dev]"

# Rust compiler
cd /workspace/src/compilers/rust
cargo build
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
cd /workspace/src/compilers/rust
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

# .NET tests
cd /workspace/src/compilers/dotnet
dotnet test CompilerDotnet.slnx

# Python tests
cd /workspace/src/compilers/python
pytest

# Rust tests
cd /workspace/src/compilers/rust
cargo test

# PowerShell tests
cd /workspace
pwsh -Command "Invoke-Pester -Path ./src/compilers/powershell -Recurse"

# Or run all tests via docker compose
docker compose --profile test run --rm test
```

## Warp Environment

For [Warp](https://www.warp.dev/) terminal users, a pre-built environment is available.

### Environment Details

| Property | Value |
|----------|-------|
| Docker Image | `jaysonknight/warp-env:ad-blocking` |
| Environment ID | `Egji4sZU4TNIOwNasFU73A` |

### Using with Warp

```bash
# The environment automatically runs these setup commands:
cd bloqr-core/src/compilers/typescript && deno cache src/mod.ts
cd bloqr-core/src/compilers/dotnet && dotnet restore CompilerDotnet.slnx
```

## CI/CD with Docker

### GitHub Actions Example

```yaml
name: Build with Docker

on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Build Docker image
        run: docker build -f Dockerfile.warp -t ad-blocking-dev .

      - name: Run TypeScript tests
        run: |
          docker run --rm \
            -v ${{ github.workspace }}:/workspace \
            ad-blocking-dev \
            bash -c "cd /workspace/src/compilers/typescript && deno task test"

      - name: Run .NET tests
        run: |
          docker run --rm \
            -v ${{ github.workspace }}:/workspace \
            ad-blocking-dev \
            bash -c "cd /workspace/src/compilers/dotnet && dotnet restore && dotnet test"

      - name: Run Python tests
        run: |
          docker run --rm \
            -v ${{ github.workspace }}:/workspace \
            ad-blocking-dev \
            bash -c "cd /workspace/src/compilers/python && pip install -e '.[dev]' && pytest"

      - name: Run Rust tests
        run: |
          docker run --rm \
            -v ${{ github.workspace }}:/workspace \
            ad-blocking-dev \
            bash -c "cd /workspace/src/compilers/rust && cargo test"
```

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
  cd /workspace/src/compilers/rust
  cargo clean
  cargo build
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

Reset named volumes if caching causes problems:

```bash
# Remove all ad-blocking volumes
docker volume rm ad-blocking-deno-cache ad-blocking-node-web ad-blocking-cargo-registry ad-blocking-cargo-git ad-blocking-nuget

# Or remove all unused volumes
docker volume prune
```
