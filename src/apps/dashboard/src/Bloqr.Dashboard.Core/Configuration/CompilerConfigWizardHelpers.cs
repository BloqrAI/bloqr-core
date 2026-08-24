using System.Text.RegularExpressions;

namespace Bloqr.Dashboard.Core.Configuration;

/// <summary>
/// Pure, testable logic for the compiler-config generation wizard (#268), kept separate from
/// <c>CompilerConfigWizardMenuService</c>'s interactive prompting so it can be unit tested without
/// a console. Menu services in this codebase are presentation glue and aren't themselves
/// unit-tested; anything with real logic belongs here instead.
/// </summary>
public static partial class CompilerConfigWizardHelpers
{
    /// <summary>
    /// Slugifies a filter-list name into a safe filename stem: lowercased, whitespace runs
    /// collapsed to a single hyphen, and any character outside <c>[a-z0-9-]</c> dropped.
    /// </summary>
    /// <param name="name">The filter-list name.</param>
    /// <returns>The slugified filename stem, or <c>"filter-list"</c> if nothing survives.</returns>
    public static string Slugify(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var lowered = WhitespaceRun().Replace(name.Trim().ToLowerInvariant(), "-");
        var slug = DisallowedFileNameCharacters().Replace(lowered, string.Empty).Trim('-');
        return string.IsNullOrEmpty(slug) ? "filter-list" : slug;
    }

    /// <summary>
    /// Builds the default output filename for a filter-list name: the slugified name with a
    /// <c>.txt</c> extension, per the epic's explicit spec.
    /// </summary>
    /// <param name="name">The filter-list name.</param>
    /// <returns>The default output filename (no directory).</returns>
    public static string DefaultOutputFileName(string name) => $"{Slugify(name)}.txt";

    /// <summary>
    /// Validates that a version string is strict <c>MAJOR.MINOR.PATCH</c> - all-digit components
    /// only, no <c>v</c> prefix and no prerelease/build suffix - per the epic's explicit
    /// requirement, which is narrower than <c>schemas/compiler-config.schema.json</c>'s full
    /// semver pattern (that pattern also accepts prerelease/build metadata; the wizard
    /// deliberately enforces the stricter subset at input time).
    /// </summary>
    /// <param name="version">The version string to validate.</param>
    /// <returns><c>true</c> if it matches <c>int.int.int</c>; otherwise, <c>false</c>.</returns>
    public static bool IsValidVersion(string version) =>
        !string.IsNullOrWhiteSpace(version) && StrictSemVer().IsMatch(version);

    /// <summary>
    /// Infers a local source file's type by peeking at its content: predominantly IP-prefixed
    /// lines (<c>0.0.0.0 example.com</c>, <c>127.0.0.1 example.com</c>) indicate a hosts file;
    /// anything else defaults to adblock syntax. Only usable for local files already on disk -
    /// remote (URL) sources have no content to peek at without fetching them, so the wizard asks
    /// the user directly for those instead of calling this method.
    /// </summary>
    /// <param name="localFilePath">Path to a local source file.</param>
    /// <returns><c>"hosts"</c> or <c>"adblock"</c>.</returns>
    public static string InferLocalSourceType(string localFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localFilePath);

        if (!File.Exists(localFilePath))
        {
            return SourceTypeHelper.DefaultSourceType;
        }

        const int sampleLineCount = 50;
        var sampled = 0;
        var hostsLike = 0;

        foreach (var line in File.ReadLines(localFilePath))
        {
            if (sampled >= sampleLineCount)
            {
                break;
            }

            var trimmed = line.AsSpan().Trim();
            if (trimmed.IsEmpty || trimmed[0] is '!' or '#')
            {
                continue;
            }

            sampled++;
            if (trimmed.StartsWith("0.0.0.0") || trimmed.StartsWith("127.0.0.1") || trimmed.StartsWith("::1"))
            {
                hostsLike++;
            }
        }

        return sampled > 0 && hostsLike * 2 > sampled ? "hosts" : SourceTypeHelper.DefaultSourceType;
    }

    /// <summary>
    /// Infers a local source file's compilation engine by sampling its content, ported from the
    /// TypeScript compiler's <c>EngineDetector.classifyLine</c>/<c>detectEngineFromLines</c>
    /// (<c>src/compilers/typescript/src/engines/EngineDetector.ts</c>, #433) - a majority vote
    /// over per-line signals: cosmetic/element-hiding separators (<c>##</c>, <c>#@#</c>, etc.) and
    /// network-rule modifiers meaningful only to a browser (e.g. <c>script</c>, <c>csp</c>,
    /// <c>elemhide</c>) vote "browser"; hosts-file lines and bare/DNS-only modifier rules vote
    /// "dns". Ties (including no classifiable lines) fall back to <c>"dns"</c>, matching the
    /// TypeScript detector's default fallback. Only usable for local files already on disk -
    /// remote (URL) sources have no content to peek at without fetching them, so the wizard asks
    /// the user directly for those instead (mirroring <see cref="InferLocalSourceType"/>'s split).
    /// </summary>
    /// <param name="localFilePath">Path to a local source file.</param>
    /// <returns><c>"dns"</c> or <c>"browser"</c>.</returns>
    public static string InferLocalSourceEngine(string localFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localFilePath);

        if (!File.Exists(localFilePath))
        {
            return "dns";
        }

        const int sampleLineCount = 200;
        var sampled = 0;
        var dnsVotes = 0;
        var browserVotes = 0;

        foreach (var rawLine in File.ReadLines(localFilePath))
        {
            if (sampled >= sampleLineCount)
            {
                break;
            }

            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '!' || (line[0] == '#' && !ContainsCosmeticSeparator(line)))
            {
                continue;
            }

            var signal = ClassifyLine(line);
            if (signal is null)
            {
                continue;
            }

            if (signal == "dns")
            {
                dnsVotes++;
            }
            else
            {
                browserVotes++;
            }

            sampled++;
        }

        if (dnsVotes == 0 && browserVotes == 0)
        {
            return "dns";
        }

        return browserVotes > dnsVotes ? "browser" : "dns";
    }

    private static readonly string[] CosmeticSeparators = ["#@#", "#?#", "#$#", "#@$#", "#%#", "#@%#", "##"];

    private static readonly string[] BrowserOnlyModifiers =
    [
        "script", "stylesheet", "image", "object", "xmlhttprequest", "subdocument", "ping",
        "websocket", "webrtc", "document", "elemhide", "generichide", "genericblock", "jsinject",
        "popup", "csp", "removeparam", "redirect", "replace", "app", "cookie", "permissions",
    ];

    private static bool ContainsCosmeticSeparator(string line) =>
        CosmeticSeparators.Any(line.Contains);

    /// <summary>
    /// Classifies a single trimmed, non-empty, non-comment line as a "dns" signal, a "browser"
    /// signal, or <see langword="null"/> (no strong signal either way) - a direct port of
    /// <c>EngineDetector.classifyLine</c>.
    /// </summary>
    private static string? ClassifyLine(string line)
    {
        if (ContainsCosmeticSeparator(line))
        {
            return "browser";
        }

        if (HostsLine().IsMatch(line))
        {
            return "dns";
        }

        var dollarIndex = line.IndexOf('$');
        if (dollarIndex != -1 && (line.StartsWith("||", StringComparison.Ordinal) ||
            line.StartsWith("@@", StringComparison.Ordinal) || line.StartsWith('|')))
        {
            var modifiers = line[(dollarIndex + 1)..].ToLowerInvariant();
            return BrowserOnlyModifiers.Any(modifiers.Contains) ? "browser" : "dns";
        }

        return DnsAdblockLine().IsMatch(line) ? "dns" : null;
    }

    [GeneratedRegex(@"^(?:\d{1,3}\.){3}\d{1,3}\s+\S+|^::1?\s+\S+")]
    private static partial Regex HostsLine();

    [GeneratedRegex(@"^@{0,2}\|{0,2}[a-z0-9*][a-z0-9.*-]*\^?(\$[a-z0-9_,=~-]*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex DnsAdblockLine();

    /// <summary>
    /// Derives a default source name from its path or URL: the filename without extension for a
    /// local path, or the last non-empty URL path segment (falling back to the host) for a URL.
    /// </summary>
    /// <param name="source">The source path or URL.</param>
    /// <returns>The derived default name.</returns>
    public static string DefaultSourceName(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            var segment = uri.Segments.LastOrDefault(s => s != "/")?.TrimEnd('/');
            return string.IsNullOrEmpty(segment) ? uri.Host : Path.GetFileNameWithoutExtension(segment);
        }

        return Path.GetFileNameWithoutExtension(source);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    [GeneratedRegex("[^a-z0-9-]")]
    private static partial Regex DisallowedFileNameCharacters();

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex StrictSemVer();
}
