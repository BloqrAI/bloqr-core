# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

Project scope
- This repo houses multiple sub-projects: TypeScript/Deno filter compiler and API client, AdGuard DNS API clients (C#, TypeScript, Rust), console UIs for the API clients, and automation scripts.
- CI pipelines (GitHub Actions) validate the .NET API client solution, TypeScript/Deno projects, and various compilers using Deno 2.0+ and .NET 10. Keep local commands aligned with the workflows below.

Common commands (build, lint, test)
TypeScript/Deno – rules compiler (src/adblock-compiler-core)
- Cache deps: cd src/adblock-compiler-core && deno cache src/mod.ts
- Type-check: deno check src/mod.ts
- Lint: deno task lint
- Unit tests: deno task test
- Coverage: deno task test:coverage
- Compile rules: deno task compile
  Notes
  - Reads compiler configuration and writes compiled rules. The canonical list in ../bloqr-blocklists/output/adguard_user_filter.txt is tracked under ../bloqr-blocklists/output/.

TypeScript/Deno – API client (src/adguard-api-typescript)
- Cache deps: cd src/adguard-api-typescript && deno cache src/mod.ts
- Run interactive CLI: deno task start
- Unit tests: deno task test
- Coverage: deno task test:coverage

.NET – API client + console UI (src/adguard-api-dotnet)
- Restore/build/test: cd src/adguard-api-dotnet; dotnet restore src/AdGuard.ApiClient.sln; dotnet build src/AdGuard.ApiClient.sln; dotnet test src/AdGuard.ApiClient.sln
- Run the console UI: dotnet run --project src/AdGuard.ConsoleUI/AdGuard.ConsoleUI.csproj
  Notes
  - The client targets net10.0 and includes helpers for configuration and Polly-based retry policies (see Helpers/ConfigurationHelper.cs and Helpers/RetryPolicyHelper.cs).


PowerShell scripts
- Static analysis (same as CI): Invoke-ScriptAnalyzer -Path src/adguard-api-powershell -Recurse

Running a single test
- TypeScript/Deno
  - By file: cd src/adblock-compiler-core && deno test src/cli.test.ts
  - All tests: deno task test
- .NET (xUnit under src/adguard-api-dotnet)
  - By class pattern: cd src/adguard-api-dotnet && dotnet test src/AdGuard.ApiClient.sln --filter "FullyQualifiedName~DevicesApiTests"
  - By method pattern: dotnet test src/AdGuard.ApiClient.sln --filter "Name~GetAccountLimits"

Environment and secrets used by code
- The API client expects an AdGuard API credential. The console UI can prompt for a key or read it from configuration (AdGuard:ApiKey).

High-level architecture and structure
- Filter rules (../bloqr-blocklists/output/)
  - ../bloqr-blocklists/output/adguard_user_filter.txt is the tracked output list consumed by AdGuard DNS.
- Filter compiler (src/adblock-compiler-core/)
  - Deno/TypeScript wrapper around @bloqr/compiler-core. Reads configuration, compiles sources, and writes compiled rules. Deno tests cover config parsing and output writing.
- API clients
  - src/adguard-api-dotnet/: Auto-generated C# SDK for AdGuard DNS API v1.15. Targets net10.0; uses Newtonsoft.Json and JsonSubTypes. Includes Helpers for configuration and Polly-based retry policies. Console UI uses Spectre.Console.
  - src/adguard-api-typescript/: TypeScript/Deno SDK with full API coverage, repository pattern, and interactive CLI using inquirer/ora.
  - src/adguard-api-rust/: Rust SDK with Tokio async runtime and dialoguer-based CLI.
- Scripts (src/)
  - src/linear: Deno-based tool to import the repo's documentation into Linear. Reads .env for ADGUARD_LINEAR_API_KEY, etc.
  - src/adguard-api-powershell: PowerShell module scaffolding and tests; CI runs PSScriptAnalyzer against the folder.

Notes pulled from existing docs
- Root README lists prerequisites: .NET 10, Deno 2.0+, and PowerShell 7+. It also documents the typical steps to compile filters with the Deno/TypeScript tools.
- The API client READMEs document OpenAPI version (1.11) and usage patterns for C#, TypeScript, and Rust implementations.

Alignment with CI
- .github/workflows/dotnet.yml builds and tests the API client solution with .NET 10.
- .github/workflows/typescript.yml validates TypeScript/Deno projects with deno check, deno lint, and deno test.
