function Get-BloqrBrowserOutputPath {
    <#
    .SYNOPSIS
        Derives the default browser-syntax artifact path from a DNS/primary output path.

    .DESCRIPTION
        Mirrors the `.txt` -> `.browser.txt` suffix logic used by the TypeScript CLI's
        `deriveBrowserOutputPath`, .NET's `FilterCompiler`, the Rust wrapper's
        `derive_browser_output_path`, and the Python wrapper's
        `_derive_browser_output_path`, so every wrapper agrees on the default location
        of the second artifact when a caller doesn't pass an explicit one: a `.txt`
        suffix is replaced with `.browser.txt`; any other extension (or none) has
        `.browser.txt` appended.

    .PARAMETER OutputPath
        The DNS/primary output path to derive the browser-syntax artifact path from.

    .OUTPUTS
        System.String
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputPath
    )

    if ($OutputPath.EndsWith('.txt')) {
        return $OutputPath.Substring(0, $OutputPath.Length - 4) + '.browser.txt'
    }

    return "$OutputPath.browser.txt"
}
