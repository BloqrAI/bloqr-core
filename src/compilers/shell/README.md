# Shell Scripts

Consolidated location for all shell script implementations in the ad-blocking repository.

## Structure

```
src/compilers/shell/
├── README.md          # This file
├── bash/              # Bash shell scripts
│   └── compile.sh
└── zsh/               # Zsh shell scripts
    └── compile.zsh
```

## Bash Scripts

### compile.sh
Cross-platform Bash script for compiling AdGuard filter rules.

**Usage:**
```bash
./src/compilers/shell/bash/compile.sh -c config.json
```

## Zsh Scripts

### compile.zsh
Zsh-optimized script with native features.

**Usage:**
```zsh
./src/compilers/shell/zsh/compile.zsh -c config.json
```

## CLI Options

| Option | Short | Description |
|--------|-------|-------------|
| `--config PATH` | `-c` | Path to configuration file |
| `--output PATH` | `-o` | Path to output file |
| `--copy-to-rules` | `-r` | Copy output to rules directory |
| `--version` | `-v` | Show version |
| `--help` | `-h` | Show help |

## Migration Notes

**Current location:** `src/compilers/shell/` ✅  
**Previous:** `src/rules-compiler-shell/` (prior location before the `src/` reorg, #331/#372), `src/shell/`, `src/shell-scripts/`

See [Main README](../../../README.md) for full documentation.
