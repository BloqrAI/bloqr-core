namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// The outcome of an attempt to load the Dashboard's configuration file, including whether
/// recovery action (quarantine, restore, regenerate) was taken.
/// </summary>
/// <param name="Configuration">The resulting, valid configuration.</param>
/// <param name="WasRecovered">Whether the file on disk was corrupt/invalid and a recovery flow ran.</param>
/// <param name="RecoveryDescription">A human-readable description of the recovery action taken, if any.</param>
public sealed record ConfigurationLoadResult(
    DashboardConfiguration Configuration,
    bool WasRecovered,
    string? RecoveryDescription);
