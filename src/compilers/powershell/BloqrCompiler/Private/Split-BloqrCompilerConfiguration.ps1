using module ..\Classes\CompilerConfiguration.psm1

function Split-BloqrCompilerConfiguration {
    <#
    .SYNOPSIS
        Splits a CompilerConfiguration's sources into up to MaxParallel chunk configs.

    .DESCRIPTION
        Mirrors the source-chunking strategy already implemented in the Rust/.NET/
        TypeScript/Python wrappers' chunking modules: sources are distributed evenly
        (ceiling division) across up to MaxParallel chunks, one config per chunk.

        Each returned chunk is a plain hashtable carrying only the fields
        @bloqr/compiler-core's config schema understands (name/description/version/
        sources/defaultEngine/transformations/inclusions/exclusions) -
        CompilerConfiguration's own ConfigPath/Format bookkeeping is deliberately
        left out, since neither has any meaning for a synthesized chunk config.

        defaultEngine is threaded through so per-chunk sources that rely on it for
        engine resolution keep behaving the same as they would unchunked - but the
        chunked compile path itself never passes -Engine/-BrowserOutputPath (#439,
        matching the Rust/.NET/Python/TypeScript precedent of deferring multi-engine
        support for chunked compilation).

    .PARAMETER Config
        The configuration to split.

    .PARAMETER MaxParallel
        Maximum number of chunks to produce.

    .OUTPUTS
        System.Collections.Hashtable[]
    #>
    [CmdletBinding()]
    [OutputType([hashtable[]])]
    param(
        [Parameter(Mandatory = $true)]
        [CompilerConfiguration]$Config,

        [Parameter(Mandatory = $false)]
        [int]$MaxParallel = 4
    )

    $sources = @($Config.Sources)
    if ($sources.Count -eq 0) {
        return @()
    }

    $effectiveMaxParallel = [Math]::Max(1, $MaxParallel)
    $sourcesPerChunk = [Math]::Max(1, [Math]::Ceiling($sources.Count / [double]$effectiveMaxParallel))
    $totalChunks = [Math]::Ceiling($sources.Count / [double]$sourcesPerChunk)

    $chunks = @()
    for ($i = 0; $i -lt $totalChunks; $i++) {
        $startIndex = [int]($i * $sourcesPerChunk)
        $endIndex = [Math]::Min($startIndex + $sourcesPerChunk - 1, $sources.Count - 1)
        $chunkSources = $sources[$startIndex..$endIndex]

        $chunks += @{
            name            = "$($Config.Name) (chunk $($i + 1)/$totalChunks)"
            description     = $Config.Description
            version         = $Config.Version
            sources         = $chunkSources
            defaultEngine   = $Config.DefaultEngine
            transformations = $Config.Transformations
            inclusions      = $Config.Inclusions
            exclusions      = $Config.Exclusions
        }
    }

    # -NoEnumerate preserves $chunks as a single array value on the output stream
    # regardless of its element count - without it, PowerShell enumerates a 1-element
    # array into a bare Hashtable on return (so a caller capturing the result gets a
    # Hashtable instead of a Hashtable[], and `.Count`/indexing silently misbehave, since
    # a Hashtable's own .Count is its key count, not "1"). A blanket `,$chunks` "fixes"
    # that one case but is wrong for 2+ elements, wrapping the whole already-correct array
    # as a single nested element instead.
    Write-Output -InputObject $chunks -NoEnumerate
}
