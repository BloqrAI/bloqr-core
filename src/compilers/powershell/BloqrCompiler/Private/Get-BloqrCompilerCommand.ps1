function Get-BloqrCompilerCommand {
    <#
    .SYNOPSIS
        Resolves how to invoke hostlist-compiler.

    .DESCRIPTION
        Prefers a native `hostlist-compiler` on PATH; falls back to `npx
        @adguard/hostlist-compiler` when only npx is available. Returns $null
        (rather than throwing) when neither is found, mirroring the bash/zsh
        wrappers' get_compiler_command()/get-compiler-command behavior so callers
        can produce their own actionable error message.

    .OUTPUTS
        Hashtable with `Executable` and `Arguments` keys, or $null if not found.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    $native = Get-Command -Name 'hostlist-compiler' -ErrorAction SilentlyContinue
    if ($native) {
        return @{ Executable = $native.Source; Arguments = @() }
    }

    $npx = Get-Command -Name 'npx' -ErrorAction SilentlyContinue
    if ($npx) {
        return @{ Executable = $npx.Source; Arguments = @('@adguard/hostlist-compiler') }
    }

    return $null
}
