namespace Bloqr.Dashboard.Console.Services;

/// <summary>
/// Abstract base class for menu services, implementing the Template Method pattern for the
/// common show-menu-until-back loop and error handling. Unlike the AdGuard.ConsoleUI template
/// this is derived from, this base class never touches <c>AnsiConsole</c> directly — it goes
/// through the injected <see cref="IConsoleRenderer"/>/<see cref="IConsolePrompter"/>, so menu
/// services stay testable and rendering-library-agnostic.
/// </summary>
public abstract class MenuServiceBase : IMenuService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MenuServiceBase"/> class.
    /// </summary>
    /// <param name="renderer">The console renderer.</param>
    /// <param name="prompter">The console prompter.</param>
    /// <param name="logger">The logger for this menu service.</param>
    protected MenuServiceBase(IConsoleRenderer renderer, IConsolePrompter prompter, ILogger logger)
    {
        Renderer = renderer;
        Prompter = prompter;
        Logger = logger;
    }

    /// <summary>
    /// Gets the console renderer.
    /// </summary>
    protected IConsoleRenderer Renderer { get; }

    /// <summary>
    /// Gets the console prompter.
    /// </summary>
    protected IConsolePrompter Prompter { get; }

    /// <summary>
    /// Gets the logger for this menu service.
    /// </summary>
    protected ILogger Logger { get; }

    /// <inheritdoc />
    public abstract string Title { get; }

    /// <summary>
    /// Gets the menu's choices and their corresponding actions. Called once per loop iteration,
    /// so implementations may vary the choices based on current state.
    /// </summary>
    /// <returns>A dictionary mapping choice text to the async action it triggers.</returns>
    protected abstract Dictionary<string, Func<Task>> GetMenuActions();

    /// <inheritdoc />
    public virtual async Task ShowAsync()
    {
        var running = true;

        while (running)
        {
            var actions = GetMenuActions();
            var choices = actions.Keys.Append("Back").ToArray();
            var choice = Prompter.Select(Title, choices);
            Renderer.WriteLine();

            if (choice == "Back")
            {
                running = false;
                continue;
            }

            try
            {
                if (actions.TryGetValue(choice, out var action))
                {
                    await action().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }
    }

    /// <summary>
    /// Handles an exception thrown by a menu action. The default implementation logs it and
    /// renders a friendly error message, then returns control to the menu loop — it never lets
    /// the exception propagate and crash the Dashboard. Override for custom handling.
    /// </summary>
    /// <param name="ex">The exception.</param>
    protected virtual void HandleError(Exception ex)
    {
        Logger.LogError(ex, "Unhandled error in menu {MenuTitle}", Title);
        Renderer.WriteStyled($"Error: {ex.Message}", TextStyle.Error);
        Renderer.WriteLine();
    }
}
