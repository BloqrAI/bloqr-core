# Input Directory

> 🔒 **Security Note**: All files in this directory are automatically validated for tampering and malicious content. [Learn why validation matters →](../../docs/WHY_VALIDATION_MATTERS.md)

This directory contains local filter rule lists that will be compiled into the final output filter list.

## Purpose

The `data/input/` directory serves as the source location for:
- **Local rule files**: Filter rules in various formats (adblock, hosts, etc.)
- **Internet list references**: Text files containing URLs to remote filter lists

## File Organization

### Local Rule Files

Place your local filter rule files in this directory. Supported formats:

- **AdBlock format** (`.txt`): Standard AdGuard/uBlock syntax
  ```
  ! Comment
  ||example.com^
  @@||allowed.com^
  ```

- **Hosts format** (`.hosts`, `.txt`): Traditional hosts file syntax
  ```
  # Comment
  127.0.0.1 blocked-domain.com
  0.0.0.0 another-blocked.com
  ```

### Internet List References

Create a file named `internet-sources.txt` (or similar) containing one URL per line:

```
https://easylist.to/easylist/easylist.txt
https://raw.githubusercontent.com/StevenBlack/hosts/master/hosts
https://filters.adtidy.org/extension/chromium/filters/2.txt
```

Lines starting with `#` are treated as comments.

## Hash Verification

All input files are verified using SHA-384 hashes to ensure data integrity both at rest and in transit:

### At-Rest Hash Verification (Local Files)

**Purpose**: Detect unauthorized modifications to local filter files

**Process**:
1. On first compilation, compute SHA-384 hash for each local file
2. Store hashes in `.hashes.json` database (gitignored)
3. On subsequent compilations, verify files haven't changed unexpectedly
4. Alert user if hash mismatch detected (potential tampering)

**Hash Database** (`.hashes.json`):
```json
{
  "custom-rules.txt": {
    "hash": "abc123def456...",
    "size": 1234,
    "lastModified": "2024-12-27T10:30:00Z",
    "lastVerified": "2024-12-27T14:45:00Z"
  }
}
```

### In-Flight Hash Verification (Internet Sources)

**Purpose**: Prevent man-in-the-middle attacks and ensure download integrity

**Process**:
1. User specifies expected hash in URL: `https://example.com/list.txt#sha384=hash`
2. Download file over HTTPS (encrypted channel)
3. Compute SHA-384 hash of downloaded content
4. Compare with expected hash from URL
5. Reject if mismatch (compilation fails with security error)

**Hash Format Options**:
```
# Option 1: Inline hash in URL (recommended)
https://easylist.to/easylist/easylist.txt#sha384=abc123...

# Option 2: Separate hash file
https://easylist.to/easylist/easylist.txt
https://easylist.to/easylist/easylist.txt.sha384

# Option 3: Hash database (internet-sources-hashes.json)
{
  "https://easylist.to/easylist/easylist.txt": "abc123..."
}
```

### Automatic Hash Updates

**Behavior**:
- **First download**: Store hash automatically for future verification
- **Subsequent downloads**: Verify against stored hash
- **Hash mismatch**: 
  - Warn user of potential tampering or list update
  - Require explicit confirmation to update hash
  - Log old and new hashes for audit trail

### Hash Verification Modes

Configure via environment variable or config file:

**1. Strict Mode** (production recommended):
```json
{
  "hashVerification": {
    "mode": "strict",
    "requireHashesForRemote": true,
    "failOnMismatch": true
  }
}
```
- All remote sources must have hashes
- Any mismatch fails compilation
- Manual hash update required

**2. Warning Mode** (default):
```json
{
  "hashVerification": {
    "mode": "warning",
    "requireHashesForRemote": false,
    "failOnMismatch": false
  }
}
```
- Hash mismatches generate warnings
- Compilation continues
- New hashes stored automatically

**3. Disabled Mode** (not recommended):
```json
{
  "hashVerification": {
    "mode": "disabled"
  }
}
```
- No hash verification
- Security risk - only for testing

## Syntax Validation

Before compilation, all input files undergo:

1. **Format detection**: Automatic detection of adblock vs hosts syntax
2. **Syntax validation**: Verification of rule syntax according to format
3. **Error reporting**: Clear messages for invalid rules with line numbers

## Example Structure

```
data/input/
├── README.md                    # This file
├── custom-rules.txt             # Your custom adblock rules
├── company-blocklist.txt        # Organization-specific rules
├── hosts-additions.hosts        # Additional hosts entries
├── internet-sources.txt         # URLs to remote lists
└── .gitignore                   # Ignore sensitive/large files
```

## Compilation Process

1. **Discovery**: Scan `data/input/` for all supported files
2. **Validation**: Lint and verify syntax of each file
3. **Hashing**: Compute SHA-384 hash for integrity verification
4. **Remote fetch** (if applicable): Download internet lists with hash verification
5. **Compilation**: Merge all sources using `@bloqr/compiler-core`
6. **Output**: Write final adblock-format list to `data/output/adguard_user_filter.txt`

## Security

- **Hash verification**: Detects file tampering
- **Syntax validation**: Prevents injection of malformed rules
- **Format enforcement**: Final output is always in adblock syntax
- **Source tracking**: Maintains provenance of each rule
- **URL validation**: Internet sources undergo security checks before download

### Internet Source URL Security

When using `internet-sources.txt`, all URLs are validated for security:

**1. Protocol Verification**
- ✅ **HTTPS required**: Only `https://` URLs are allowed (enforced)
- ❌ **HTTP blocked**: Insecure `http://` URLs are rejected
- ❌ **Other protocols blocked**: `ftp://`, `file://`, etc. are rejected

**2. Domain Validation**
- Verify domain is resolvable via DNS
- Check against known malicious domain lists
- Ensure domain is not an IP address (prefer named domains)
- Validate domain follows proper DNS naming conventions

**3. Content-Type Verification**
- Verify HTTP response has `Content-Type: text/plain` or similar text format
- Reject binary content, executables, or unexpected MIME types
- Check `Content-Length` header for reasonable file sizes

**4. Content Validation**
- Download and scan first 1KB to verify it contains filter rules
- Check for valid rule syntax (adblock or hosts format)
- Reject files that don't match expected patterns
- Scan for suspicious patterns (scripts, embedded content, etc.)

**5. Hash Verification**
- Optionally specify expected SHA-384 hash for each URL
- Fail if downloaded content doesn't match expected hash
- Store hash database for known-good sources

**Example with hash verification:**
```
# internet-sources.txt with hashes
https://easylist.to/easylist/easylist.txt#sha384=abc123...
https://raw.githubusercontent.com/StevenBlack/hosts/master/hosts#sha384=def456...
```

**Security warnings:**
- ⚠️ Untrusted sources may contain malicious or overly aggressive rules
- ⚠️ Always verify the legitimacy of the source domain
- ⚠️ Review downloaded lists before deploying to production
- ⚠️ Use hash verification for critical sources
- ⚠️ Monitor for unexpected changes in list content or size

## Usage

### Adding Local Rules

1. Create a `.txt` file in `data/input/`
2. Add your filter rules in adblock or hosts format
3. Run the compiler - it will automatically discover and include the file

### Adding Internet Sources

1. Create or edit `internet-sources.txt`
2. Add one URL per line
3. Run the compiler with internet source support enabled

### Running Compilation

```bash
# TypeScript compiler
cd src/adblock-compiler-core
deno task compile

# .NET compiler
cd src/rules-compiler-dotnet
dotnet run

# Python compiler
cd src/rules-compiler-python
rules-compiler --input-dir ../../data/input --output ../../data/output/adguard_user_filter.txt

# Rust compiler
cd src/rules-compiler-rust
cargo run --release
```

## Best Practices

1. **Organize by purpose**: Group related rules in separate files
2. **Add comments**: Use `!` or `#` to document rule purposes
3. **Test incrementally**: Add rules gradually and verify behavior
4. **Keep backups**: Maintain copies before making bulk changes
5. **Track hashes**: Note hash values for important reference files
6. **Review internet sources**: Verify legitimacy of remote list URLs

## Troubleshooting

### "Hash mismatch detected"
- File was modified since last compilation
- Verify changes were intentional
- Recompile to update hash database

### "Syntax error at line X"
- Check rule format matches file type
- Ensure proper adblock or hosts syntax
- See format examples above

### "File too large"
- Input file exceeds size limit
- Split into multiple smaller files
- Review and remove unnecessary rules

## Related Documentation

- [Configuration Reference](../../docs/configuration-reference.md)
- [Compiler Comparison](../../docs/compiler-comparison.md)
- [AdGuard Filter Syntax](https://adguard.com/kb/general/ad-filtering/create-own-filters/)
