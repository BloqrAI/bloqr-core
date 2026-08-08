# Linear Documentation Import Tool

Import ad-blocking repository documentation into Linear project management system.

## Overview

This tool parses the repository's Linear documentation (`docs/LINEAR_DOCUMENTATION.md`) and imports it into Linear, creating:

- A project to track the ad-blocking system
- Issues for each roadmap item
- Documentation issues for each component
- A main documentation issue with the full content

## Prerequisites

- Deno 2.0+
- Linear account with API access

## Installation

```bash
cd src/linear
# No installation needed - Deno handles dependencies automatically
```

## Configuration

1. Copy the environment example file:
   ```bash
   cp .env.example .env
   ```

2. Get your Linear API key:
   - Go to **Linear Settings** > **API** > **Personal API keys**
   - Create a new key with appropriate permissions
   - Copy the key to your `.env` file

3. Edit `.env` with your configuration:
   ```env
   ADGUARD_LINEAR_API_KEY=lin_api_your_key_here
   ADGUARD_LINEAR_TEAM_ID=optional_team_id
   ADGUARD_LINEAR_PROJECT_NAME=Ad-Blocking Documentation
   ```

## Usage

### Import Documentation

```bash
# Full import with default settings
deno task import

# Dry run (preview without making changes)
deno task import --dry-run

# Custom file path
deno task import --file /path/to/documentation.md

# Specify project name
deno task import --project "My Project Name"

# Use specific team
deno task import --team team_id_here
```

### List Available Resources

```bash
# List available teams
deno task import --list-teams

# List existing projects
deno task import --list-projects
```

### Command Options

| Option | Description |
|--------|-------------|
| `-f, --file <path>` | Path to markdown documentation file |
| `-t, --team <id>` | Linear team ID (defaults to first team) |
| `-p, --project <name>` | Linear project name |
| `--dry-run` | Preview import without making changes |
| `--no-project` | Skip project creation |
| `--no-issues` | Skip issue creation |
| `--no-docs` | Skip documentation issue creation |
| `--list-teams` | List available teams and exit |
| `--list-projects` | List existing projects and exit |
| `-v, --verbose` | Verbose output |

## What Gets Imported

### Roadmap Items

Roadmap items (checkbox lists in the documentation) are converted to Linear issues:

```markdown
- [ ] Additional filter sources integration
- [ ] Real-time filter update notifications
```

Becomes:
- Issue: "Additional filter sources integration"
- Issue: "Real-time filter update notifications"

### Components

Each documented component creates a detailed documentation issue:

- Filter Rules (`/../bloqr-blocklists/output/`)
- Rules Compiler TypeScript (`/src/adblock-compiler-core/`)
- API Client (`/src/adguard-api-dotnet/`)
- etc.

### Main Documentation

The full documentation content is imported as a comprehensive reference issue.

## Project Structure

```
src/linear/
├── src/
│   ├── types.ts          # TypeScript interfaces
│   ├── parser.ts         # Markdown parsing logic
│   ├── linear-client.ts  # Linear API wrapper
│   ├── linear-import.ts  # Node.js entry point
│   └── mod.ts            # Deno entry point
├── .env.example          # Environment configuration template
├── deno.json             # Deno configuration and tasks
└── README.md             # This file
```

## Development

### Type Check

```bash
deno task check
```

### Lint

```bash
deno task lint
```

### Run directly

```bash
deno run --allow-read --allow-write --allow-env --allow-net src/mod.ts --help
```

## Troubleshooting

### "ADGUARD_LINEAR_API_KEY environment variable is required"

Ensure you've created a `.env` file with your API key:
```bash
cp .env.example .env
# Edit .env and add your API key
```

Note: The legacy `LINEAR_API_KEY` is also supported for backward compatibility.

### "No teams found in Linear workspace"

Make sure your API key has access to at least one team in your Linear workspace.

### "Failed to create project"

Check that your API key has permissions to create projects. You may need to use the `--team` option to specify a valid team ID.

### Rate Limiting

Linear has API rate limits. If you encounter rate limiting issues, wait a few minutes and try again.

## Type Definitions

This project uses Deno, which works with TypeScript natively. For compatibility with other tools or for publishing, type definition files (`.d.ts`) can be generated using:

```bash
deno task generate:types
```

The generated files are placed in the `dist/` directory and re-export all types from the source files.

**Note**: The `.d.ts` files are automatically generated and should not be edited manually. They are excluded from version control.

### Available Tasks

- `deno task import` - Import issues from markdown
- `deno task import:docs` - Import from LINEAR_DOCUMENTATION.md
- `deno task import:dry-run` - Dry run import
- `deno task cli` - Run the CLI
- `deno task test` - Run tests
- `deno task check` - Type check the code
- `deno task lint` - Lint the code
- `deno task fmt` - Format the code
- `deno task generate:types` - Generate `.d.ts` type definition files

## License

GPL-3.0 - See [LICENSE](../../LICENSE) for details.
