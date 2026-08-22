function Find-BloqrBenchmarkDataDirectory {
    <#
    .SYNOPSIS
        Locates the repo's benchmarks/data directory by walking up from the current
        directory.

    .DESCRIPTION
        Mirrors the other four language wrappers' benchmark-data auto-discovery
        (walking up from the current directory looking for benchmarks/data, the
        same strategy configuration-file discovery uses).

    .OUTPUTS
        System.String, or $null if no benchmarks/data directory was found.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    $current = (Get-Location).Path
    while ($true) {
        $candidate = Join-Path $current 'benchmarks' 'data'
        if (Test-Path -LiteralPath $candidate -PathType Container) {
            return $candidate
        }

        $parent = Split-Path -Parent $current
        if (-not $parent -or $parent -eq $current) {
            return $null
        }
        $current = $parent
    }
}
