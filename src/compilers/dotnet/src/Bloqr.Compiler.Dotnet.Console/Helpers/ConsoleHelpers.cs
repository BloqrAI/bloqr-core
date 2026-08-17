namespace Bloqr.Compiler.Dotnet.Console.Helpers;

/// <summary>
/// Helper methods for console UI operations.
/// </summary>
public static class ConsoleHelpers
{
    /// <summary>
    /// Prompts the user to select from a list of choices.
    /// </summary>
    /// <param name="title">The prompt title.</param>
    /// <param name="choices">The available choices.</param>
    /// <returns>The selected choice.</returns>
    public static string SelectChoice(string title, params string[] choices)
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(title)
                .PageSize(10)
                .HighlightStyle(new Style(Color.Green))
                .AddChoices(choices));
    }

    /// <summary>
    /// Executes an action with a status spinner.
    /// </summary>
    /// <param name="status">The status message.</param>
    /// <param name="action">The action to execute.</param>
    public static async Task WithStatusAsync(string status, Func<Task> action)
    {
        await AnsiConsole.Status()
            .StartAsync(status, async ctx =>
            {
                await action();
            });
    }

    /// <summary>
    /// Executes an action with a status spinner and returns a result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="status">The status message.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>The result of the action.</returns>
    public static async Task<T> WithStatusAsync<T>(string status, Func<Task<T>> action)
    {
        return await AnsiConsole.Status()
            .StartAsync(status, async ctx =>
            {
                return await action();
            });
    }

    /// <summary>
    /// Prompts for confirmation.
    /// </summary>
    /// <param name="message">The confirmation message.</param>
    /// <returns>True if confirmed, false otherwise.</returns>
    public static bool Confirm(string message)
    {
        return AnsiConsole.Confirm(message);
    }

    /// <summary>
    /// Displays a success message.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public static void Success(string message)
    {
        AnsiConsole.MarkupLine($"[green]{Markup.Escape(message)}[/]");
    }

    /// <summary>
    /// Displays an error message.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public static void Error(string message)
    {
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
    }

    /// <summary>
    /// Displays a warning message.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public static void Warning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");
    }

    /// <summary>
    /// Displays an info message.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public static void Info(string message)
    {
        AnsiConsole.MarkupLine($"[blue]{Markup.Escape(message)}[/]");
    }
}
