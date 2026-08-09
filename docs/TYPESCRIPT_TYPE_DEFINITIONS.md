# TypeScript Type Definitions (.d.ts) Generation

## Overview

This document describes the implementation of `.d.ts` type definition file generation for all TypeScript projects in the repository.

## Why .d.ts Files?

While Deno uses TypeScript natively and doesn't require `.d.ts` files for its own operation, they are useful for:

1. **npm Publishing**: If these packages are published to npm, consumers will need `.d.ts` files
2. **IDE Support**: Better autocomplete and type checking in editors for external consumers
3. **Documentation**: Type definitions serve as documentation of the public API
4. **Compatibility**: Non-Deno tools (like tsc, webpack) may require declaration files

## Implementation

### Type Generation Script

Each TypeScript project has a `generate-types.ts` script that:

- Uses only built-in Deno APIs (no external dependencies)
- Recursively walks through the `src/` directory
- Excludes test files (`*.test.ts`)
- Generates `.d.ts` files that re-export from the original `.ts` files
- Places all output in a `dist/` directory

**Location**: 
- `src/adblock-compiler-core/generate-types.ts`

The AdGuard DNS API TypeScript client and the Linear import tool (formerly `src/adguard-api-typescript/` and `src/linear/`) moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients) and have their own `generate-types.ts` scripts there.

### Generated Files

The `.d.ts` files are simple re-exports that leverage TypeScript's type resolution:

```typescript
/**
 * Type definitions for index
 * AUTO-GENERATED - do not edit manually
 * 
 * This file re-exports types from the TypeScript source file.
 * For Deno projects, the .ts files themselves serve as type definitions.
 */

export * from '../src/index.ts';
```

This approach:
- Maintains a single source of truth (the `.ts` files)
- Automatically stays in sync when source files change (when regenerated)
- Provides proper type information to consumers

## Usage

### Generate Type Definitions

From any TypeScript project directory:

```bash
deno task generate:types
```

This command is available in:
- `src/adblock-compiler-core/`

(The AdGuard DNS API TypeScript client and Linear import tool have their own copies in [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients).)

### Output

The command will:
1. Clean the existing `dist/` directory
2. Walk through all `.ts` files in `src/`
3. Generate corresponding `.d.ts` files
4. Report the number of files generated

Example output:
```
🔨 Generating TypeScript declaration files...

📁 Source: /path/to/src
📁 Output: /path/to/dist

✓ index.d.ts
✓ types.d.ts
✓ models/user.d.ts
...

✅ Generated 48 declaration files successfully!
📁 Output: /path/to/dist
```

## Keeping Type Definitions Updated

### Manual Updates

When you modify TypeScript source files, regenerate the type definitions:

```bash
# From the project directory
deno task generate:types
```

### CI/CD Integration

The GitHub Actions workflow (`.github/workflows/typescript.yml`) automatically:

1. Generates type definitions for each project
2. Verifies the `dist/` directory was created
3. Counts the generated `.d.ts` files
4. Fails the build if no files are generated

This ensures that type definitions are always validated in CI but are not committed to the repository (since they're generated).

### Best Practices

1. **Regenerate After Changes**: Always run `deno task generate:types` after modifying source files
2. **Don't Edit Manually**: Never edit `.d.ts` files directly - they are auto-generated
3. **Check in CI**: The CI workflow will verify type definitions can be generated successfully
4. **Exclude from Git**: The `dist/` directories are in `.gitignore` to avoid committing generated files

## Project-Specific Details

### adblock-compiler-core
- **Files**: 17 `.d.ts` files
- **Entry Point**: `dist/index.d.ts`
- **Main Exports**: RulesCompiler, ConfigurationBuilder, types

> `adguard-api-typescript` and `linear` moved to [`BloqrAI/bloqr-apiclients`](https://github.com/BloqrAI/bloqr-apiclients); see that repo for their type-definition details.

## Troubleshooting

### "Permission denied" Error

Make sure the script has execute permissions:
```bash
chmod +x generate-types.ts
```

Or run with explicit permissions:
```bash
deno run --allow-read --allow-write --no-npm generate-types.ts
```

### Network Errors

If you see npm registry errors, ensure the `--no-npm` flag is used:
```bash
deno run --allow-read --allow-write --no-npm generate-types.ts
```

### Empty dist/ Directory

Check that:
1. The `src/` directory exists and contains `.ts` files
2. No syntax errors in TypeScript files prevent them from being read
3. File permissions allow reading source files

## Future Enhancements

Possible improvements to consider:

1. **Watch Mode**: Auto-regenerate on file changes during development
2. **Bundle Generation**: Create a single bundled `.d.ts` file for simpler distribution
3. **JSDoc Extraction**: Include JSDoc comments in generated files
4. **Type Checking**: Validate generated types compile correctly
5. **npm Package**: Create npm package structure with proper `types` field in package.json

## References

- [TypeScript Declaration Files](https://www.typescriptlang.org/docs/handbook/declaration-files/introduction.html)
- [Deno TypeScript Support](https://deno.land/manual/typescript)
- [Publishing to npm from Deno](https://deno.land/manual/node/npm_specifiers)
