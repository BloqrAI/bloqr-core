namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Result of benchmarking one canned dataset size, unchunked vs chunked, through the real
/// compiler pipeline. Shared by every consumer of <see cref="IBenchmarkService"/> (the
/// <c>--benchmark</c> CLI mode and the Dashboard's Diagnostics menu) and serialized as the
/// cross-language benchmark JSON contract documented in <c>benchmarks/README.md</c>.
/// </summary>
public sealed class BenchmarkRunResult
{
    /// <summary>Canned dataset size ("small", "medium", "large", or "xlarge").</summary>
    [JsonPropertyName("size")]
    public string Size { get; set; } = string.Empty;

    /// <summary>Number of identical duplicated sources compiled in this run.</summary>
    [JsonPropertyName("sources")]
    public int Sources { get; set; }

    /// <summary>Maximum parallel chunk workers used for the chunked run.</summary>
    [JsonPropertyName("maxParallel")]
    public int MaxParallel { get; set; }

    /// <summary>Whether the unchunked (single-invocation) run succeeded.</summary>
    [JsonPropertyName("unchunkedSuccess")]
    public bool UnchunkedSuccess { get; set; }

    /// <summary>Elapsed milliseconds for the unchunked run.</summary>
    [JsonPropertyName("unchunkedMs")]
    public long UnchunkedMs { get; set; }

    /// <summary>Resulting rule count from the unchunked run.</summary>
    [JsonPropertyName("unchunkedRuleCount")]
    public int UnchunkedRuleCount { get; set; }

    /// <summary>Whether the chunked (parallel) run succeeded.</summary>
    [JsonPropertyName("chunkedSuccess")]
    public bool ChunkedSuccess { get; set; }

    /// <summary>Elapsed milliseconds for the chunked run.</summary>
    [JsonPropertyName("chunkedMs")]
    public long ChunkedMs { get; set; }

    /// <summary>Resulting rule count from the chunked run.</summary>
    [JsonPropertyName("chunkedRuleCount")]
    public int ChunkedRuleCount { get; set; }

    /// <summary>
    /// <c>unchunkedMs / chunkedMs</c>, or <c>null</c> if either run failed or
    /// <see cref="ChunkedMs"/> was zero.
    /// </summary>
    [JsonPropertyName("speedup")]
    public double? Speedup { get; set; }

    /// <summary>Error message if either run failed, or the dataset file was missing.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
