// Spike for #265: proves the "subprocess wrapper" integration path chosen in
// docs/adr/0002-aglint-integration-strategy.md actually works end-to-end from
// .NET. Standalone, throwaway - not wired into any solution or DI container.
// Run: dotnet run -- <path-to-rules-file>  (defaults to bad-rules.txt alongside this file)

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

var targetFile = Path.GetFullPath(args.Length > 0 ? args[0] : "bad-rules.txt");

if (!File.Exists(targetFile))
{
    Console.Error.WriteLine($"Target file not found: {targetFile}");
    return 2;
}

var denoPath = FindOnPath("deno");
if (denoPath is null)
{
    Console.Error.WriteLine("deno not found on PATH. Install from https://deno.com/ to run this spike.");
    return 2;
}

// AGLint refuses to run without a config file present (found during the #265 spike -
// see the ADR). Write a minimal one next to the target file if one isn't already there.
var configDir = Path.GetDirectoryName(targetFile)!;
var configPath = Path.Combine(configDir, ".aglintrc.yaml");
if (!File.Exists(configPath))
{
    File.WriteAllText(configPath, "root: true\nextends:\n    - aglint:recommended\nsyntax:\n    - Common\n");
}

var psi = new ProcessStartInfo
{
    FileName = denoPath,
    WorkingDirectory = configDir,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
};
foreach (var arg in new[]
{
    "run",
    "--allow-read", "--allow-env", "--allow-run", "--allow-sys", "--allow-write",
    "npm:@adguard/aglint@3.0.2",
    "--no-colors",
    Path.GetFileName(targetFile),
})
{
    psi.ArgumentList.Add(arg);
}

using var process = Process.Start(psi)!;
var stdout = await process.StandardOutput.ReadToEndAsync();
var stderr = await process.StandardError.ReadToEndAsync();
await process.WaitForExitAsync();

// Found during this spike: AGLint writes its lint report to STDERR, not stdout - even
// for a normal "problems found" run (not just fatal errors). A non-zero exit code alone
// doesn't mean the invocation failed; it's how AGLint reports "problems found." Only
// treat it as a real failure if neither stream contains any parseable findings.
var findings = ParseFindings(stderr);
if (findings.Count == 0)
{
    findings = ParseFindings(stdout);
}

if (process.ExitCode != 0 && findings.Count == 0)
{
    Console.Error.WriteLine($"aglint failed to run (exit {process.ExitCode}):");
    Console.Error.WriteLine(stderr);
    return process.ExitCode;
}
Console.WriteLine(JsonSerializer.Serialize(findings, new JsonSerializerOptions { WriteIndented = true }));

return process.ExitCode;

static string? FindOnPath(string command)
{
    var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
    var extensions = OperatingSystem.IsWindows() ? [".exe", ".cmd", ".bat"] : new[] { "" };
    foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
    {
        foreach (var ext in extensions)
        {
            var candidate = Path.Combine(dir, command + ext);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
    return null;
}

// Parses AGLint's tabular CLI text output. There is no --json/--format flag (confirmed
// via `aglint --help` during the #265 spike), so this is regex-over-text - deliberately
// isolated in one small function per the ADR's "Consequences" section, so switching to
// a JSON output source later only touches this function.
static List<AglintFinding> ParseFindings(string stdout)
{
    var findings = new List<AglintFinding>();
    var lineRegex = new Regex(@"^\s*(\d+):(\d+)\s+(\w+)\s+(.+?)\s{2,}(\S+)\s*$");

    foreach (var line in stdout.Split('\n'))
    {
        var match = lineRegex.Match(line.TrimEnd('\r'));
        if (!match.Success)
        {
            continue;
        }

        findings.Add(new AglintFinding(
            Line: int.Parse(match.Groups[1].Value),
            Column: int.Parse(match.Groups[2].Value),
            Severity: match.Groups[3].Value,
            Message: match.Groups[4].Value.Trim(),
            RuleId: match.Groups[5].Value));
    }

    return findings;
}

internal sealed record AglintFinding(int Line, int Column, string Severity, string Message, string RuleId);
