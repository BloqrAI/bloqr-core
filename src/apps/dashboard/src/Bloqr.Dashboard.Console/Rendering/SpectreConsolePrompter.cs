namespace Bloqr.Dashboard.Console.Rendering;

/// <summary>
/// <see cref="IConsolePrompter"/> implementation backed by Spectre.Console.
/// </summary>
public sealed class SpectreConsolePrompter : IConsolePrompter
{
    /// <inheritdoc />
    public string Prompt(string prompt, string? defaultValue = null)
    {
        var textPrompt = new TextPrompt<string>(Markup.Escape(prompt));
        if (defaultValue is not null)
        {
            textPrompt.DefaultValue(defaultValue);
        }

        return AnsiConsole.Prompt(textPrompt);
    }

    /// <inheritdoc />
    public Task<string> PromptAsync(
        string prompt,
        string? defaultValue = null,
        CancellationToken cancellationToken = default)
    {
        var textPrompt = new TextPrompt<string>(Markup.Escape(prompt));
        if (defaultValue is not null)
        {
            textPrompt.DefaultValue(defaultValue);
        }

        return AnsiConsole.PromptAsync(textPrompt, cancellationToken);
    }

    /// <inheritdoc />
    public string PromptSecret(string prompt) =>
        AnsiConsole.Prompt(new TextPrompt<string>(Markup.Escape(prompt)).Secret());

    /// <inheritdoc />
    public bool Confirm(string prompt, bool defaultValue = false) =>
        AnsiConsole.Confirm(Markup.Escape(prompt), defaultValue);

    /// <inheritdoc />
    public T Select<T>(string prompt, IEnumerable<T> choices) where T : notnull =>
        AnsiConsole.Prompt(new SelectionPrompt<T>().Title(Markup.Escape(prompt)).AddChoices(choices));

    /// <inheritdoc />
    public T Select<T>(string prompt, IEnumerable<T> choices, Func<T, string> displaySelector) where T : notnull =>
        AnsiConsole.Prompt(
            new SelectionPrompt<T>()
                .Title(Markup.Escape(prompt))
                .UseConverter(item => Markup.Escape(displaySelector(item)))
                .AddChoices(choices));

    /// <inheritdoc />
    public IEnumerable<T> MultiSelect<T>(string prompt, IEnumerable<T> choices) where T : notnull =>
        AnsiConsole.Prompt(new MultiSelectionPrompt<T>().Title(Markup.Escape(prompt)).AddChoices(choices));
}
