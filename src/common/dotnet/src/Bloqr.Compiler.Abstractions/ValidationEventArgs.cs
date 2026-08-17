namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Represents a validation result with zero-trust principles.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Informational message.</summary>
    Info,

    /// <summary>Warning that doesn't prevent compilation.</summary>
    Warning,

    /// <summary>Error that prevents compilation.</summary>
    Error,

    /// <summary>Critical security issue that must block compilation.</summary>
    Critical
}

/// <summary>
/// Represents a single validation finding.
/// </summary>
public class ValidationFinding
{
    /// <summary>
    /// Gets the severity of the finding.
    /// </summary>
    public ValidationSeverity Severity { get; }

    /// <summary>
    /// Gets the validation code (e.g., "ZT001" for zero-trust violations).
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the message describing the finding.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the source or location of the finding.
    /// </summary>
    public string? Location { get; }

    /// <summary>
    /// Gets additional context for the finding.
    /// </summary>
    public IDictionary<string, object>? Context { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationFinding"/> class.
    /// </summary>
    public ValidationFinding(
        ValidationSeverity severity,
        string code,
        string message,
        string? location = null,
        IDictionary<string, object>? context = null)
    {
        Severity = severity;
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Location = location;
        Context = context;
    }
}

/// <summary>
/// Event arguments for validation stages (zero-trust checkpoints).
/// </summary>
public class ValidationEventArgs : CompilationEventArgs
{
    /// <summary>
    /// Gets the name of the validation stage.
    /// </summary>
    public string StageName { get; }

    /// <summary>
    /// Gets the list of validation findings.
    /// </summary>
    public IList<ValidationFinding> Findings { get; }

    /// <summary>
    /// Gets whether validation passed (no errors or critical findings).
    /// </summary>
    public bool Passed => !Findings.Any(f =>
        f.Severity == ValidationSeverity.Error ||
        f.Severity == ValidationSeverity.Critical);

    /// <summary>
    /// Gets or sets a value indicating whether to abort compilation.
    /// </summary>
    public bool Abort { get; set; }

    /// <summary>
    /// Gets or sets the reason for aborting.
    /// </summary>
    public string? AbortReason { get; set; }

    /// <summary>
    /// Gets the duration of the validation stage.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the number of items validated.
    /// </summary>
    public int ItemsValidated { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationEventArgs"/> class.
    /// </summary>
    /// <param name="options">The compiler options.</param>
    /// <param name="stageName">The name of the validation stage.</param>
    /// <param name="findings">The validation findings.</param>
    /// <param name="duration">The duration of validation.</param>
    /// <param name="itemsValidated">The number of items validated.</param>
    public ValidationEventArgs(
        CompilerOptions options,
        string stageName,
        IList<ValidationFinding> findings,
        TimeSpan duration = default,
        int itemsValidated = 0)
        : base(options)
    {
        StageName = stageName ?? throw new ArgumentNullException(nameof(stageName));
        Findings = findings ?? throw new ArgumentNullException(nameof(findings));
        Duration = duration;
        ItemsValidated = itemsValidated;
    }

    /// <summary>
    /// Adds a finding to the validation results.
    /// </summary>
    public void AddFinding(ValidationSeverity severity, string code, string message, string? location = null)
    {
        Findings.Add(new ValidationFinding(severity, code, message, location));
    }

    /// <summary>
    /// Adds an error finding.
    /// </summary>
    public void AddError(string code, string message, string? location = null)
    {
        AddFinding(ValidationSeverity.Error, code, message, location);
    }

    /// <summary>
    /// Adds a warning finding.
    /// </summary>
    public void AddWarning(string code, string message, string? location = null)
    {
        AddFinding(ValidationSeverity.Warning, code, message, location);
    }

    /// <summary>
    /// Adds a critical finding and automatically sets abort.
    /// </summary>
    public void AddCritical(string code, string message, string? location = null)
    {
        AddFinding(ValidationSeverity.Critical, code, message, location);
        Abort = true;
        AbortReason = message;
    }
}
