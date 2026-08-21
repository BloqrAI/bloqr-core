namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// Thrown when the Dashboard's configuration file (or a compiler config it references) is
/// invalid or missing and cannot be automatically recovered — e.g. because non-interactive mode
/// disallows the auto-recovery flow.
/// </summary>
public sealed class DashboardConfigurationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DashboardConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public DashboardConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
