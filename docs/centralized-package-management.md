# Centralized Package Management

## Overview

The repository uses **Central Package Management (CPM)** to manage all NuGet package versions from a single `Directory.Packages.props` file located at the repository root. This provides:

- ✅ Single source of truth for all package versions
- ✅ Consistent versions across all projects
- ✅ Easier dependency updates (change once, applies everywhere)
- ✅ Reduced version conflicts
- ✅ Better security (easier to track and update vulnerable packages)

## Structure

```
/home/runner/work/bloqr-core/bloqr-core/
├── Directory.Build.props           # Global MSBuild properties
├── Directory.Packages.props        # ⭐ CENTRALIZED PACKAGE VERSIONS
└── src/
    └── compilers/dotnet/
        └── src/
            └── ... (all reference packages without versions)
```

> The AdGuard DNS API .NET client (formerly `src/adguard-api-dotnet/`) moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) and manages its own package versions there.

## How It Works

### Root Configuration (`Directory.Packages.props`)

The root-level `Directory.Packages.props` file defines all package versions:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup>
    <PackageVersion Include="Polly" Version="8.6.5" />
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="10.0.1" />
    <!-- ... all other packages -->
  </ItemGroup>
</Project>
```

### Project Files (`.csproj`)

Individual projects reference packages **without specifying versions**:

```xml
<ItemGroup>
  <!-- ✅ Correct: No Version attribute -->
  <PackageReference Include="Polly" />
  <PackageReference Include="Microsoft.Extensions.Logging" />
</ItemGroup>
```

**DON'T** specify versions in project files:

```xml
<ItemGroup>
  <!-- ❌ Wrong: Version will conflict with CPM -->
  <PackageReference Include="Polly" Version="8.6.5" />
</ItemGroup>
```

## Package Categories

### Core API Client Dependencies
- `JsonSubTypes` - JSON polymorphic serialization
- `Newtonsoft.Json` - JSON serialization
- `Polly` - Resilience and transient fault handling

### Microsoft.Extensions (v10.0.2)
All Microsoft.Extensions packages are standardized to version **10.0.2** (latest for .NET 10):
- Configuration (base, abstractions, JSON, environment variables, user secrets, command line)
- Dependency Injection (base, abstractions)
- Hosting
- Logging (base, abstractions, console)
- Options (base, configuration extensions)

### Entity Framework Core (v10.0.2)
- `Microsoft.EntityFrameworkCore` (base)
- `Microsoft.EntityFrameworkCore.Design`
- `Microsoft.EntityFrameworkCore.InMemory` (testing)
- `Microsoft.EntityFrameworkCore.Sqlite`
- `Microsoft.EntityFrameworkCore.SqlServer`
- `Npgsql.EntityFrameworkCore.PostgreSQL`

### UI Dependencies
- `Spectre.Console` - Console UI framework

### Rules Compiler Dependencies
- `YamlDotNet` - YAML parsing
- `Tomlyn` - TOML parsing

### PowerShell Dependencies
- `PowerShellStandard.Library` - PowerShell module development

### Testing Dependencies
- `xunit` (v2.9.3) - Test framework
- `xunit.runner.visualstudio` (v3.1.5) - Test runner
- `Microsoft.NET.Test.Sdk` (v18.0.1) - Test SDK
- `Moq` (v4.20.72) - Mocking framework
- `FluentAssertions` (v6.12.1) - Assertion library
- `coverlet.collector` (v6.0.4) - Code coverage

### Benchmark Dependencies
- `BenchmarkDotNet` (v0.15.8)
- `BenchmarkDotNet.Diagnostics.Windows` (v0.15.8)
- `Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers` (v18.0.36525.3)

## Common Tasks

### Adding a New Package

1. **Add version to `Directory.Packages.props`**:
   ```xml
   <PackageVersion Include="NewPackage" Version="1.2.3" />
   ```

2. **Reference in project file** (without version):
   ```xml
   <PackageReference Include="NewPackage" />
   ```

3. **Restore packages**:
   ```bash
   dotnet restore
   ```

### Updating a Package Version

1. **Update version in `Directory.Packages.props`** only:
   ```xml
   <!-- Before -->
   <PackageVersion Include="Polly" Version="8.6.5" />
   
   <!-- After -->
   <PackageVersion Include="Polly" Version="8.7.0" />
   ```

2. **All projects automatically use the new version** after restore:
   ```bash
   dotnet restore
   ```

### Checking Package Versions

View all centralized package versions:
```bash
cat Directory.Packages.props
```

Check what version a project is using:
```bash
dotnet list package
```

## Troubleshooting

### Error: "Package 'X' doesn't have a version defined"

**Cause**: Package is referenced in a `.csproj` file but not defined in `Directory.Packages.props`.

**Solution**: Add the package version to `Directory.Packages.props`:
```xml
<PackageVersion Include="X" Version="1.0.0" />
```

### Error: "Version conflict detected"

**Cause**: Project file has explicit `Version` attribute on `<PackageReference>`.

**Solution**: Remove the `Version` attribute from the project file:
```xml
<!-- Before (wrong) -->
<PackageReference Include="Polly" Version="8.6.5" />

<!-- After (correct) -->
<PackageReference Include="Polly" />
```

### Build fails after CPM migration

1. **Clean the solution**:
   ```bash
   dotnet clean
   ```

2. **Restore packages**:
   ```bash
   dotnet restore
   ```

3. **Build again**:
   ```bash
   dotnet build
   ```

## Version Consistency Rules

1. **Microsoft.Extensions packages**: All should use the same version (currently **10.0.2**)
2. **Entity Framework Core packages**: All should use the same version (currently **10.0.2**)
3. **Test packages**: Keep xunit, Moq, and test SDK versions aligned
4. **Benchmark packages**: Keep BenchmarkDotNet packages at the same version

## Migration History

**Before**: Three separate `Directory.Packages.props` files:
- `src/adguard-api-dotnet/src/Directory.Packages.props`
- `src/adguard-api-dotnet/src/api-client/Directory.Packages.props`
- `src/compilers/dotnet/Directory.Packages.props`

**Issues**:
- Duplicate definitions
- Version inconsistencies (Microsoft.Extensions: v9.0.0 vs v10.0.1)
- Some projects used explicit versions in `.csproj` files

**After**: Single root-level `Directory.Packages.props`
- All packages centralized
- All versions consistent
- All projects reference packages without versions

## References

- [Microsoft Docs: Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
- [NuGet Documentation](https://docs.microsoft.com/en-us/nuget/)
