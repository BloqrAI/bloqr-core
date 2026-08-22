function Get-RuleCount {
    <#
    .SYNOPSIS
        Counts non-empty, non-comment lines in a compiled rules file.

    .DESCRIPTION
        Blank lines and lines starting with `!` or `#` don't count as rules.
        Returns 0 if the file doesn't exist.

    .PARAMETER Path
        Path to the compiled rules file.

    .OUTPUTS
        System.Int32
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return 0
    }

    $count = 0
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if (-not $trimmed) { continue }
        if ($trimmed.StartsWith('!')) { continue }
        if ($trimmed.StartsWith('#')) { continue }
        $count++
    }

    return $count
}
