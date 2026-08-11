namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Placeholder settings for wiring the future <c>adguard-api-dotnet</c> client into the
/// Dashboard, per the epic: "Dashboard will support the adguard-api-dotnet API library so
/// ensure stubs or whatever else are needed to support it are written, but the detailed
/// spec for Dashboard supporting the AdGuard API will be in a separate GitHub issue" (#272).
/// This intentionally holds only enough to report configuration status (see
/// <c>DiagnosticsMenuService</c>'s "AdGuard API" row) - it does not construct or call an
/// actual API client, since that client lives in the private
/// <c>BloqrAI/bloqr-apiclients</c> repository and its real shape is out of scope here.
/// </summary>
public sealed class AdGuardApiSettings
{
    /// <summary>
    /// Gets or sets whether AdGuard DNS API integration is enabled. Defaults to
    /// <c>false</c> - nothing in this repo depends on it being on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the AdGuard DNS API base URL to use once a client is wired in.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the name of the environment variable an API key/token will be read
    /// from. Never store the credential itself in the Dashboard's <c>.jsonc</c> config.
    /// </summary>
    public string? ApiKeyEnvironmentVariable { get; set; }
}
