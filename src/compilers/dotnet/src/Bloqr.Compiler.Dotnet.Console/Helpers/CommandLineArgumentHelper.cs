namespace Bloqr.Compiler.Dotnet.Console.Helpers;

/// <summary>
/// Helpers for pre-processing raw CLI arguments before they reach
/// <see cref="Microsoft.Extensions.Configuration.CommandLineConfigurationExtensions.AddCommandLine(IConfigurationBuilder, string[])"/>.
/// </summary>
public static class CommandLineArgumentHelper
{
    /// <summary>
    /// Every bare boolean CLI switch this app recognizes - i.e. flags with no value of their
    /// own, as opposed to "--key value" pairs like <c>--config</c> or <c>--benchmark-size</c>.
    /// </summary>
    public static readonly string[] BareBooleanFlags =
    [
        "--compile",
        "--copy", "--CopyToRules",
        "--version", "-v",
        "--verbose",
        "--validate",
        "--fail-on-warnings",
        "--no-validate-config", "--validate-config",
        "--benchmark", "--benchmark-json",
    ];

    /// <summary>
    /// Splits <paramref name="args"/> into its bare boolean flags (see
    /// <see cref="BareBooleanFlags"/>) and everything else, so the caller can feed the
    /// booleans' presence into configuration separately from <c>AddCommandLine</c>.
    /// </summary>
    /// <remarks>
    /// .NET's default <c>CommandLineConfigurationProvider</c> unconditionally treats the
    /// token immediately following any <c>--key</c> (with no <c>=</c>) as that key's value -
    /// even when the next token is itself another <c>--key</c>. A bare boolean switch like
    /// <c>--benchmark</c> or <c>--verbose</c> has nothing of its own to consume, so when one
    /// appears before another <c>--key value</c> pair, it silently swallows that flag's name
    /// as its own "value" and everything downstream shifts out of alignment. Stripping the
    /// known bare-boolean flags out before they ever reach <c>AddCommandLine</c> - and feeding
    /// their presence in via <c>AddInMemoryCollection</c> instead - sidesteps the ambiguity
    /// entirely, for every flag combination rather than one option group at a time. See #426.
    /// </remarks>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>
    /// The bare-boolean flags found (as configuration key/"true" pairs, keys matching what
    /// <c>AddCommandLine</c> would have produced), and the remaining arguments with those
    /// flags removed.
    /// </returns>
    public static (Dictionary<string, string?> Flags, string[] RemainingArgs) SplitBareBooleanFlags(string[] args)
    {
        var flags = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var remaining = new List<string>(args.Length);

        foreach (var arg in args)
        {
            var matchedFlag = Array.Find(
                BareBooleanFlags,
                f => string.Equals(f, arg, StringComparison.OrdinalIgnoreCase));

            if (matchedFlag is not null)
            {
                // IConfiguration's command-line provider strips leading "--"/"-" from keys.
                flags[matchedFlag.TrimStart('-')] = "true";
                continue;
            }

            remaining.Add(arg);
        }

        return (flags, [.. remaining]);
    }
}
