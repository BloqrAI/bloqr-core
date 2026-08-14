function Get-RulesFileHash {
    <#
    .SYNOPSIS
        Computes the lowercase SHA-384 hex hash of a compiled rules file.

    .DESCRIPTION
        Wraps Get-FileHash and lowercases its output so the result matches the
        other wrappers' hash format (Rust's hex::encode, Python's hexdigest(),
        TypeScript's crypto digest('hex'), and sha384sum/shasum -a 384 all
        produce lowercase hex).

    .PARAMETER Path
        Path to the file to hash.

    .OUTPUTS
        System.String
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA384).Hash.ToLowerInvariant()
}
