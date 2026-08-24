namespace Bloqr.Dashboard.Core.Configuration;

/// <summary>
/// Pure, testable rendering of a <see cref="CompilerResult"/> into display lines, kept separate
/// from <c>CompileMenuService</c>'s console glue so it can be unit tested without a console -
/// mirroring the split <c>CompilerConfigWizardHelpers</c> already uses. Shared by the interactive
/// "Compile using active profile"/"Compile using a specific config file" actions (#441) so both
/// report the same dual-artifact summary that <c>DashboardApplication</c>'s <c>--compile</c> CLI
/// branch already prints (#440/#453) - the same look whether the Dashboard is driven
/// interactively or non-interactively.
/// </summary>
public static class CompileResultRenderer
{
    /// <summary>
    /// Builds the display lines for a completed (successful or failed) compilation, including a
    /// second line for the browser-syntax artifact when the config mixed DNS and browser sources.
    /// </summary>
    /// <param name="result">The compilation result to render.</param>
    /// <returns>
    /// One or more lines describing the result, in display order. Does not include a
    /// "Copied to: ..." line for <see cref="CompilerResult.CopiedToRules"/> - that's a distinct,
    /// unstyled line the caller renders separately, same as before this method existed.
    /// </returns>
    public static IReadOnlyList<string> Render(CompilerResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var lines = new List<string>();

        if (result.Success)
        {
            lines.Add(
                $"Compiled '{result.ConfigName}': {result.RuleCount} rules -> {result.OutputPath} " +
                $"({result.ElapsedMs}ms)");

            if (!string.IsNullOrEmpty(result.BrowserOutputPath))
            {
                lines.Add($"  Browser artifact: {result.BrowserRuleCount} rules -> {result.BrowserOutputPath}");
                if (!string.IsNullOrEmpty(result.BrowserOutputHash))
                {
                    lines.Add($"    Hash: {result.BrowserOutputHash}");
                }
            }

            if (!string.IsNullOrEmpty(result.OutputHash))
            {
                lines.Add($"  Hash: {result.OutputHash}");
            }
        }
        else
        {
            lines.Add($"Compilation failed: {result.ErrorMessage}");
        }

        return lines;
    }
}
