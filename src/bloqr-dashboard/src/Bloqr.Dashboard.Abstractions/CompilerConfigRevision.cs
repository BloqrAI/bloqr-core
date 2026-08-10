namespace Bloqr.Dashboard.Abstractions;

/// <summary>
/// A single commit in a compiler config's git history, as surfaced by
/// <see cref="ICompilerConfigVersionHistoryService"/>.
/// </summary>
/// <param name="ShortSha">The abbreviated commit hash.</param>
/// <param name="Author">The commit author's name.</param>
/// <param name="Date">The commit's authored date.</param>
/// <param name="Message">The commit's subject line.</param>
public sealed record CompilerConfigRevision(string ShortSha, string Author, DateTimeOffset Date, string Message);
