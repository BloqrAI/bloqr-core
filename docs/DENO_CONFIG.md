# Shared Deno Configuration

This repository uses a centralized Deno configuration pattern to ensure consistency across all Deno-based projects.

## Overview

Similar to how `.NET` projects use `Directory.Build.props` and Rust projects use a workspace `Cargo.toml`, all Deno projects in this repository extend a shared base configuration file.

## Configuration Files

### Base Configuration: `deno.base.json`

Located at the repository root (`/deno.base.json`), this file contains shared settings that apply to all Deno projects:

- **Compiler Options**: Strict TypeScript settings with DOM and Deno APIs
- **Formatting Rules**: Consistent code formatting (2-space indents, 100-char line width, single quotes)
- **Linting Rules**: Recommended rules enabled by default
- **Node Modules**: Automatic node_modules directory management

### Project-Specific Configurations

Each Deno project has its own `deno.json` file that extends the base configuration:

1. **`src/compilers/typescript/deno.json`** - Rules compiler

(The AdGuard API TypeScript SDK and the Linear import tool, formerly `src/adguard-api-typescript/` and `src/linear/`, moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) and maintain their own Deno configuration there.)

These files only contain:
- Project metadata (`name`, `version`)
- Project-specific exports
- Project-specific tasks
- Project-specific dependencies (imports)
- Project-specific overrides (e.g., additional lint exclusions)

## How It Works

Each project's `deno.json` file uses the `"extends"` field to inherit settings from the base configuration:

```json
{
  "extends": "../../deno.base.json",
  "name": "@your-project/name",
  "version": "1.0.0",
  ...
}
```

Settings in the project-specific file override or merge with the base configuration:
- **Merging**: `lint.rules.exclude` arrays are merged (base + project-specific)
- **Overriding**: Other fields like `name`, `version`, `exports` replace base settings

## Benefits

1. **Single Source of Truth**: Compiler options, formatting, and linting rules defined once
2. **Consistency**: All projects use identical TypeScript strict mode, formatting, and linting
3. **Easy Updates**: Change base configuration once to update all projects
4. **Reduced Duplication**: Each project file is smaller and focused on project-specific settings
5. **Familiar Pattern**: Mirrors `.NET`'s `Directory.Build.props` and Rust's workspace pattern

## Shared Settings

The following settings are shared across all Deno projects:

### Compiler Options
```json
{
  "lib": ["deno.ns", "deno.unstable", "dom"],
  "strict": true,
  "noImplicitAny": true,
  "strictNullChecks": true
}
```

### Formatting
```json
{
  "indentWidth": 2,
  "lineWidth": 100,
  "singleQuote": true
}
```

### Linting
```json
{
  "rules": {
    "tags": ["recommended"]
  }
}
```

### Node Modules
```json
{
  "nodeModulesDir": "auto"
}
```

## Adding a New Deno Project

When creating a new Deno project in this repository:

1. Create your project directory under `src/`
2. Create a `deno.json` file with:
   ```json
   {
     "extends": "../../deno.base.json",
     "name": "@your-scope/your-project",
     "version": "1.0.0",
     "exports": "./src/index.ts",
     "tasks": {
       // Your project-specific tasks
     },
     "imports": {
       // Your project-specific dependencies
     }
   }
   ```
3. The base configuration will automatically apply

## Modifying Shared Settings

To change settings that affect all Deno projects:

1. Edit `/deno.base.json`
2. The change will automatically apply to all projects that extend it
3. Test all projects to ensure compatibility

To override a shared setting for a specific project:

1. Add the setting to the project's `deno.json`
2. The project-specific setting will override the base setting

## Related Patterns

This configuration pattern follows the same philosophy as:

- **`.NET`**: `Directory.Build.props` and `Directory.Packages.props`
- **Rust**: Workspace `Cargo.toml` with member packages
- **TypeScript**: `tsconfig.base.json` with project-specific `tsconfig.json` files extending it

## Schema Validation

All Deno configuration files use the official Deno schema for validation and IDE autocomplete:

```json
{
  "$schema": "https://deno.land/x/deno/cli/schemas/config-file.v1.json"
}
```

Note: The `$schema` field is only included in `deno.base.json` and inherited by all projects.

## References

- [Deno Configuration File Documentation](https://deno.land/manual/getting_started/configuration_file)
- [Deno Configuration Schema](https://deno.land/x/deno/cli/schemas/config-file.v1.json)
