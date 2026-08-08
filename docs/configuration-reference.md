# Configuration Reference

All rules compilers in this repository use the same configuration schema, validated against [`schemas/compiler-config.schema.json`](../schemas/compiler-config.schema.json). The canonical implementation is [`@bloqr/compiler-core`](https://jsr.io/@bloqr/compiler-core) (`src/adblock-compiler-core/`); see that package's README for the broader architecture story.

## Supported Formats

**JSON is the documented, recommended format** — all examples below use it, and it's what the schema (`$schema` field) validates directly.

YAML and TOML remain supported by the underlying config readers for backward compatibility (`.yaml`/`.yml` via each language's YAML library, `.toml` via each language's TOML library), but are no longer documented here. Convert an existing YAML/TOML config to JSON if you want schema validation and IDE autocomplete against `compiler-config.schema.json`.

## Configuration Schema

### Root-Level Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `name` | string | **Yes** | - | Name of the compiled filter list |
| `description` | string | No | - | Description of the filter list |
| `homepage` | string | No | - | Homepage URL for the filter list |
| `license` | string | No | - | License identifier (e.g., "GPL-3.0") |
| `version` | string | No | - | Version number of the filter list |
| `output` | object | No | See below | Output file configuration |
| `hashVerification` | object | No | See below | Hash verification configuration |
| `archiving` | object | No | See below | Archiving configuration |
| `sources` | array | **Yes** | - | List of filter sources to compile |
| `transformations` | array | No | `[]` | Global transformations to apply |
| `inclusions` | array | No | `[]` | Global inclusion patterns |
| `inclusions_sources` | array | No | `[]` | Files containing inclusion patterns |
| `exclusions` | array | No | `[]` | Global exclusion patterns |
| `exclusions_sources` | array | No | `[]` | Files containing exclusion patterns |

### Output Configuration

Configure output file path and conflict handling:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `path` | string | No | `data/output/adguard_user_filter.txt` | Full path to output file |
| `fileName` | string | No | - | Output filename (alternative to path) |
| `conflictStrategy` | string | No | `rename` | How to handle existing files: `rename`, `overwrite`, or `error` |

**Example:**
```json
{
  "output": {
    "path": "data/output/my-custom-filter.txt",
    "conflictStrategy": "rename"
  }
}
```

When `conflictStrategy` is `rename` and the output file already exists:
- First conflict: `my-custom-filter.txt` → `my-custom-filter-1.txt`
- Second conflict: `my-custom-filter-1.txt` → `my-custom-filter-2.txt`
- And so on...

### Hash Verification Configuration

Configure integrity verification for both local files (at-rest) and remote downloads (in-flight):

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `mode` | string | No | `warning` | Verification mode: `strict`, `warning`, or `disabled` |
| `requireHashesForRemote` | boolean | No | `false` | Require hashes for all remote sources |
| `failOnMismatch` | boolean | No | `false` | Fail compilation on hash mismatch |
| `hashDatabasePath` | string | No | `data/input/.hashes.json` | Path to hash database file |

**Example:**
```json
{
  "hashVerification": {
    "mode": "strict",
    "requireHashesForRemote": true,
    "failOnMismatch": true,
    "hashDatabasePath": "data/input/.hashes.json"
  }
}
```

**Verification Modes:**

**1. Strict Mode** (recommended for production):
- All remote sources must include hash verification
- Any hash mismatch fails compilation immediately
- Manual hash update required for changed files
- Provides maximum security

**2. Warning Mode** (default):
- Hash mismatches generate warnings but don't fail compilation
- New hashes stored automatically for future verification
- Good for development and testing
- Balances security with convenience

**3. Disabled Mode** (not recommended):
- No hash verification performed
- Security risk - only use for testing
- Files can be modified without detection

**Hash Verification Process:**

**At-Rest (Local Files)**:
1. On first compilation, compute SHA-384 hash for each local file
2. Store in `.hashes.json` database (gitignored)
3. On subsequent compilations, verify files haven't changed
4. Alert if mismatch detected (potential tampering)

**In-Flight (Internet Sources)**:
1. User specifies expected hash in URL or separate config
2. Download file over HTTPS (encrypted)
3. Compute SHA-384 hash of downloaded content
4. Compare with expected hash
5. Reject if mismatch (prevents MITM attacks)

**Hash Database Format** (`.hashes.json`):
```json
{
  "custom-rules.txt": {
    "hash": "abc123def456...",
    "size": 1234,
    "lastModified": "2024-12-27T10:30:00Z",
    "lastVerified": "2024-12-27T14:45:00Z"
  },
  "https://easylist.to/easylist/easylist.txt": {
    "hash": "def456abc789...",
    "size": 567890,
    "lastModified": "2024-12-27T08:00:00Z",
    "lastVerified": "2024-12-27T14:45:00Z"
  }
}
```

### Archiving Configuration

Configure automatic archiving of processed input files:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `enabled` | boolean | No | `true` | Enable or disable archiving |
| `mode` | string | No | `automatic` | Archiving mode: `automatic`, `interactive`, or `disabled` |
| `retentionDays` | number | No | `90` | Days to keep archived files before cleanup |

**Example:**
```json
{
  "archiving": {
    "enabled": true,
    "mode": "automatic",
    "retentionDays": 90
  }
}
```

**Modes:**
- `automatic`: Archive after every successful compilation
- `interactive`: Prompt user before archiving
- `disabled`: Same as `enabled: false`, no archiving occurs

### Source Properties

Each source in the `sources` array supports:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `source` | string | **Yes** | - | URL or local file path to the filter list |
| `name` | string | No | - | Human-readable name for this source |
| `type` | string | No | `"adblock"` | Format: `"adblock"` or `"hosts"` |
| `transformations` | array | No | `[]` | Source-specific transformations |
| `inclusions` | array | No | `[]` | Source-specific inclusion patterns |
| `inclusions_sources` | array | No | `[]` | Files with source inclusion patterns |
| `exclusions` | array | No | `[]` | Source-specific exclusion patterns |
| `exclusions_sources` | array | No | `[]` | Files with source exclusion patterns |

## Transformations

Transformations modify the filter rules during compilation. They are always applied in this fixed order, regardless of configuration order:

| Transformation | Description | Use Case |
|---------------|-------------|----------|
| `RemoveComments` | Removes all comment lines (`!` or `#`) | Reduce file size |
| `Compress` | Converts hosts format to adblock syntax | Hosts file sources |
| `RemoveModifiers` | Removes unsupported modifiers from rules | AdGuard DNS compatibility |
| `Validate` | Removes dangerous or incompatible rules | Safety and compatibility |
| `ValidateAllowIp` | Like Validate but allows IP address rules | When IP rules needed |
| `Deduplicate` | Removes duplicate rules | Reduce file size |
| `InvertAllow` | Converts `@@` exception rules to blocking rules | Strict blocking |
| `RemoveEmptyLines` | Removes blank lines | Clean output |
| `TrimLines` | Trims leading/trailing whitespace | Clean output |
| `InsertFinalNewLine` | Ensures file ends with newline | POSIX compliance |
| `ConvertToAscii` | Converts IDN domains to punycode | Compatibility |

### Recommended Transformation Sets

#### For AdGuard DNS

```json
{
  "transformations": ["Validate", "RemoveModifiers", "Deduplicate", "RemoveEmptyLines", "InsertFinalNewLine"]
}
```

#### For Hosts File Sources

```json
{
  "transformations": ["Compress", "Validate", "Deduplicate", "RemoveEmptyLines", "InsertFinalNewLine"]
}
```

#### For Maximum Compatibility

```json
{
  "transformations": ["RemoveComments", "RemoveModifiers", "Validate", "Deduplicate", "TrimLines", "RemoveEmptyLines", "InsertFinalNewLine", "ConvertToAscii"]
}
```

## Pattern Matching

Inclusion and exclusion patterns filter which rules are kept or removed.

### Pattern Types

| Type | Syntax | Example | Matches |
|------|--------|---------|---------|
| Plain string | `text` | `google.com` | Exact match |
| Wildcard | `*text*` | `*tracking*` | Contains "tracking" |
| Prefix wildcard | `*.text` | `*.example.com` | Ends with ".example.com" |
| Suffix wildcard | `text*` | `analytics*` | Starts with "analytics" |
| Regex | `/pattern/` | `/^ad[0-9]+\./` | Regex match |
| Comment | `! text` | `! ignore this` | Ignored |

### Pattern Examples

#### Exclusions (Remove matching rules)

```json
{
  "exclusions": [
    "*.google.com",
    "*analytics*",
    "/^(www\\.)?facebook\\.com$/"
  ]
}
```

`*.google.com` removes rules for Google subdomains, `*analytics*` removes analytics-related rules, and the regex removes Facebook rules.

#### Inclusions (Keep only matching rules)

```json
{
  "inclusions": ["*ad*", "*tracker*", "*.doubleclick.net"]
}
```

Keeps only ad-related, tracker, and DoubleClick rules.

### Pattern Files

Instead of inline patterns, you can reference external files:

```json
{
  "exclusions_sources": ["whitelist.txt", "my-allowlist.txt"],
  "inclusions_sources": ["blocklist-patterns.txt"]
}
```

Pattern file format (one pattern per line, comments with `!`):

```
! This is a comment
*.google.com
*analytics*
/tracking\d+\./
```

## Data Directory Configuration

The `data/` directory structure supports input/output separation for organized filter management.

### Input Directory (`data/input/`)

**Purpose**: Store source filter files and remote list references before compilation.

**Supported Input Types**:

1. **Local filter files**: `.txt` or `.hosts` files with filter rules
   - Adblock format: `||example.com^`, `@@||allowed.com^`
   - Hosts format: `0.0.0.0 blocked.com`, `127.0.0.1 ads.example.com`
   - Automatic format detection

2. **Internet sources file**: `internet-sources.txt` with one URL per line
   ```
   # Example internet-sources.txt (HTTPS only!)
   https://easylist.to/easylist/easylist.txt
   https://raw.githubusercontent.com/StevenBlack/hosts/master/hosts
   
   # With hash verification for security
   https://easylist.to/easylist/easylist.txt#sha384=abc123...
   ```

**Features**:
- **SHA-384 hash verification**: Detects file tampering
- **Syntax validation**: Validates rules before compilation
- **Multi-format support**: Both adblock and hosts formats accepted
- **Remote fetching**: Downloads and caches internet sources
- **URL security validation**: Enforces HTTPS, validates domains, verifies content

**Internet Source Security**:

All URLs in `internet-sources.txt` undergo comprehensive security validation:

1. **Protocol enforcement**: Only HTTPS URLs allowed (HTTP rejected)
2. **Domain validation**: DNS verification and malicious domain checks
3. **Content-Type verification**: Ensures text/plain format (rejects binaries)
4. **Content validation**: Scans downloaded content for valid filter rule syntax
5. **Hash verification**: Optional SHA-384 hash check (add `#sha384=hash` to URL)
6. **Size limits**: Rejects unexpectedly large files

**Security best practices**:
- ⚠️ Only add URLs from trusted, well-known filter list providers
- ⚠️ Verify domain legitimacy before adding to internet-sources.txt
- ⚠️ Use hash verification for critical production sources
- ⚠️ Review downloaded lists before deploying to production
- ⚠️ Monitor for unexpected changes in list size or content

**Example structure**:
```
data/input/
├── README.md                    # Documentation
├── custom-rules.txt             # Local adblock rules
├── company-blocklist.txt        # Organization-specific rules
├── internet-sources.txt         # URLs to remote lists
└── .gitignore                   # Ignore cache/temp files
```

### Output Directory (`data/output/`)

**Purpose**: Store final compiled filter list.

**Main file**: `adguard_user_filter.txt`
- Always in **adblock syntax** (not hosts format)
- Merged from all input sources
- Deduplicated and validated
- SHA-384 hash computed for verification

**Guarantees**:
- ✅ Output format is always adblock
- ✅ Rules are validated and deduplicated
- ✅ Comments and metadata preserved
- ✅ Hash computed for integrity verification

### Compilation Workflow

```
data/input/          →  Compiler  →  data/output/
├── custom.txt           (Validate,      └── adguard_user_filter.txt
├── blocklist.txt         Hash,              (adblock format)
└── internet-sources.txt  Merge)
```

**Processing steps**:
1. Scan `data/input/` for all `.txt` and `.hosts` files
2. Parse `internet-sources.txt` for remote URLs
3. Validate syntax of each source
4. Compute SHA-384 hashes for tampering detection
5. Fetch internet sources with hash verification
6. Merge all sources using `@bloqr/compiler-core`
7. Apply transformations (deduplicate, validate, etc.)
8. Convert hosts format to adblock if needed
9. Write to `data/output/adguard_user_filter.txt`
10. Compute final output hash
11. Archive input files to `data/archive/` (if enabled)

### Archive Directory (`data/archive/`)

**Purpose**: Preserve processed input files after successful compilation.

**Configuration**:
```json
{
  "archiving": {
    "enabled": true,
    "mode": "automatic",
    "retentionDays": 90
  }
}
```

**Environment Variables**:
| Variable | Values | Default |
|----------|--------|---------|
| `ADGUARD_ARCHIVE_ENABLED` | `true`/`false` | `true` |
| `ADGUARD_ARCHIVE_MODE` | `automatic`/`interactive`/`disabled` | `automatic` |
| `ADGUARD_ARCHIVE_RETENTION_DAYS` | number | `90` |

**CLI Flags**:
```bash
--no-archive                  # Disable archiving
--archive-interactive         # Prompt before archiving
--archive-retention <days>    # Custom retention period
```

**Archive structure**:
```
data/archive/
├── 2024-12-27_14-30-45/
│   ├── manifest.json         # Compilation metadata
│   ├── custom-rules.txt      # Input file snapshot
│   └── internet-sources.txt
└── 2024-12-26_09-15-22/
    ├── manifest.json
    └── custom-rules.txt
```

**Manifest file** (`manifest.json`):
```json
{
  "timestamp": "2024-12-27T14:30:45.123Z",
  "compiler": "typescript",
  "compilerVersion": "1.0.0",
  "inputFiles": [
    {
      "name": "custom-rules.txt",
      "hash": "abc123...",
      "size": 1234,
      "format": "adblock"
    }
  ],
  "outputFile": "data/output/adguard_user_filter.txt",
  "outputHash": "xyz789...",
  "ruleCount": 150,
  "success": true,
  "compilationTimeMs": 1234
}
```

**Retention policy**:
- Archives older than `retentionDays` are automatically deleted
- Cleanup occurs before creating new archives
- Manual cleanup: `find data/archive/ -type d -mtime +90 -exec rm -rf {} \;`

## Example Configurations

### JSON

```json
{
  "name": "My Filter List",
  "description": "Custom ad-blocking filter",
  "version": "1.0.0",
  "homepage": "https://example.com/filters",
  "license": "GPL-3.0",
  "output": {
    "path": "data/output/my-filter.txt",
    "conflictStrategy": "rename"
  },
  "hashVerification": {
    "mode": "strict",
    "requireHashesForRemote": true,
    "failOnMismatch": true
  },
  "archiving": {
    "enabled": true,
    "mode": "automatic",
    "retentionDays": 90
  },
  "sources": [
    {
      "name": "Local Rules",
      "source": "data/local.txt",
      "type": "adblock"
    },
    {
      "name": "EasyList",
      "source": "https://easylist.to/easylist/easylist.txt",
      "type": "adblock",
      "transformations": ["RemoveModifiers", "Validate"]
    },
    {
      "name": "Steven Black Hosts",
      "source": "https://raw.githubusercontent.com/StevenBlack/hosts/master/hosts",
      "type": "hosts",
      "transformations": ["Compress", "Validate"]
    }
  ],
  "transformations": [
    "Deduplicate",
    "RemoveEmptyLines",
    "InsertFinalNewLine"
  ],
  "exclusions": [
    "*.google.com",
    "/analytics/"
  ]
}
```

## Advanced Configuration

### Source-Specific Overrides

Each source can have its own transformations, inclusions, and exclusions that override or extend global settings:

```json
{
  "name": "Advanced Filter",
  "transformations": ["Deduplicate", "InsertFinalNewLine"],
  "exclusions": ["*.google.com"],
  "sources": [
    {
      "name": "Strict Source",
      "source": "https://example.com/strict.txt",
      "transformations": ["RemoveComments", "Validate"],
      "exclusions": ["*.facebook.com"]
    },
    {
      "name": "Permissive Source",
      "source": "https://example.com/permissive.txt",
      "inclusions": ["*ad*", "*tracker*"]
    }
  ]
}
```

Global `transformations`/`exclusions` apply to all sources; each source's own `transformations`/`inclusions`/`exclusions` are in addition to the global ones.

### Using External Pattern Files

```json
{
  "name": "Filter with External Patterns",
  "sources": [{ "name": "Main List", "source": "https://example.com/list.txt" }],
  "exclusions_sources": ["config/global-whitelist.txt", "config/user-whitelist.txt"],
  "inclusions_sources": ["config/must-block.txt"]
}
```

### Multiple Sources with Different Types

```json
{
  "name": "Combined Filter",
  "sources": [
    { "name": "EasyList", "source": "https://easylist.to/easylist/easylist.txt", "type": "adblock" },
    { "name": "EasyPrivacy", "source": "https://easylist.to/easylist/easyprivacy.txt", "type": "adblock" },
    {
      "name": "Steven Black",
      "source": "https://raw.githubusercontent.com/StevenBlack/hosts/master/hosts",
      "type": "hosts",
      "transformations": ["Compress"]
    },
    { "name": "Custom Rules", "source": "./my-rules.txt", "type": "adblock" }
  ],
  "transformations": ["Validate", "Deduplicate", "RemoveEmptyLines", "InsertFinalNewLine"]
}
```

`Steven Black` is a hosts-format source; its `Compress` transformation converts it to adblock syntax.

## Validation

### .NET Compiler Validation

The .NET compiler supports configuration validation before compilation:

```bash
dotnet run --project src/RulesCompiler.Console -- -c config.json --validate
```

This checks:
- Required fields are present
- Sources are accessible
- Transformations are valid
- Pattern syntax is correct

### Common Validation Errors

| Error | Cause | Solution |
|-------|-------|----------|
| `name is required` | Missing `name` field | Add `name` property |
| `sources is required` | Missing `sources` array | Add at least one source |
| `Invalid transformation` | Unknown transformation | Check spelling, use valid name |
| `Source not found` | Invalid file path | Verify file exists |
| `Invalid regex pattern` | Bad regex in pattern | Fix regex syntax |

## CLI Options by Compiler

| Option | TypeScript | .NET | Python | Rust |
|--------|------------|------|--------|------|
| Config file | `-c`, `--config` | `-c`, `--config` | `-c`, `--config` | `-c`, `--config` |
| Output file | `-o`, `--output` | `-o`, `--output` | `-o`, `--output` | `-o`, `--output` |
| Copy to rules | `-r`, `--copy-to-rules` | `--copy` | `-r`, `--copy-to-rules` | `-r`, `--copy-to-rules` |
| Debug output | `-d`, `--debug` | `--verbose` | `-d`, `--debug` | `-d`, `--debug` |
| Validate only | - | `--validate` | - | - |
| Version | `--version` | `-v`, `--version` | `-V`, `--version` | `-V`, `--version` |
| Help | `--help` | `--help` | `--help` | `--help` |
