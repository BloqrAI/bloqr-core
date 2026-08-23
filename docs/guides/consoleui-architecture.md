# AdGuard ConsoleUI Architecture

This document describes the architecture and design patterns used in the AdGuard.ConsoleUI application.

## Overview

AdGuard.ConsoleUI is a menu-driven console application that provides a user-friendly interface for the AdGuard DNS API. It follows a service-oriented architecture with dependency injection for loose coupling and testability.

## Architecture Diagram

```mermaid
flowchart TD
    Program["Program.cs (Main Entry Point)\nBuildConfiguration()\nConfigureServices()\nRuns ConsoleApplication"]
    App["ConsoleApplication\nDisplays welcome banner\nHandles API key configuration\nMain menu loop\nRoutes to menu services"]
    Device["DeviceMenuService\nList/view/create/delete devices"]
    DnsServer["DnsServerMenuService\nList/view/create/delete servers"]
    Stats["StatisticsMenuService\n24h/7d/30d/custom-range stats"]
    Factory["ApiClientFactory\nManages API configuration\nCreates API client instances\nTests API connectivity"]
    ApiClient["AdGuard.ApiClient\nAccountApi, DevicesApi, DNSServersApi,\nStatisticsApi, QueryLogApi, FilterListsApi,\nWebServicesApi, DedicatedIPAddressesApi"]

    Program --> App
    App --> Device
    App --> DnsServer
    App --> Stats
    Device --> Factory
    DnsServer --> Factory
    Stats --> Factory
    Factory --> ApiClient
```

## Design Patterns

### 1. Dependency Injection (DI)

All services are registered in the DI container and injected via constructors:

```csharp
// Registration in Program.cs
services.AddSingleton<ApiClientFactory>();
services.AddSingleton<ConsoleApplication>();
services.AddSingleton<DeviceMenuService>();

// Injection in ConsoleApplication.cs
public ConsoleApplication(
    ApiClientFactory apiClientFactory,
    DeviceMenuService deviceMenu,
    DnsServerMenuService dnsServerMenu,
    // ... other services
)
```

**Benefits:**
- Loose coupling between components
- Easy unit testing with mocks
- Centralized service configuration

### 2. Factory Pattern

`ApiClientFactory` implements the Factory pattern for creating API client instances:

```csharp
public class ApiClientFactory
{
    public AccountApi CreateAccountApi()
    {
        return new AccountApi(GetConfiguration());
    }

    public DevicesApi CreateDevicesApi()
    {
        return new DevicesApi(GetConfiguration());
    }
    // ... other factory methods
}
```

**Benefits:**
- Centralized API client creation
- Consistent configuration across all clients
- Easy to modify client creation logic

### 3. Service Pattern

Each menu service encapsulates a specific domain area:

```csharp
public class DeviceMenuService
{
    private readonly ApiClientFactory _apiClientFactory;

    public DeviceMenuService(ApiClientFactory apiClientFactory)
    {
        _apiClientFactory = apiClientFactory;
    }

    public async Task ShowAsync()
    {
        // Menu loop implementation
    }
}
```

**Benefits:**
- Single Responsibility Principle
- Reusable components
- Easy to extend with new features

## Component Descriptions

### Program.cs

The entry point of the application responsible for:
- Building configuration from multiple sources
- Configuring the DI container
- Setting up logging
- Running the application

### ConsoleApplication

The main application orchestrator that:
- Displays the welcome banner
- Handles initial API key configuration
- Manages the main menu loop
- Routes user selections to appropriate menu services

### ApiClientFactory

Central factory for API operations:
- Stores and manages API configuration
- Creates configured API client instances
- Provides connection testing functionality
- Supports both settings-based and manual configuration

### Menu Services

Each service handles a specific domain:

| Service | Responsibility |
|---------|----------------|
| DeviceMenuService | Device CRUD operations |
| DnsServerMenuService | DNS server CRUD operations |
| StatisticsMenuService | Statistics retrieval and display |
| QueryLogMenuService | Query log viewing and clearing |
| AccountMenuService | Account limits display |
| FilterListMenuService | Filter list display |

## Configuration Flow

```mermaid
flowchart LR
    Json["appsettings.json"] --> Builder
    Env["Environment Vars\nADGUARD_*"] --> Builder

    subgraph Builder["ConfigurationBuilder"]
        direction TB
        B1["AddJsonFile()"]
        B2["AddEnvironment()"]
    end

    Builder --> Config["IConfiguration\n(AdGuard:ApiKey)"]
    Config --> Factory["ApiClientFactory\nConfigureFrom()"]
```

## UI Library: Spectre.Console

The application uses Spectre.Console for rich terminal UI:

### Key Components Used

| Component | Usage |
|-----------|-------|
| `FigletText` | Welcome banner |
| `SelectionPrompt` | Interactive menus |
| `TextPrompt` | User input (including secrets) |
| `Table` | Data display |
| `Panel` | Detail views |
| `Rule` | Section separators |
| `Status` | Loading indicators |
| `Markup` | Colored text |

### Example Usage

```csharp
// Interactive menu
var choice = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("[green]Main Menu[/]")
        .AddChoices(new[] { "Option 1", "Option 2", "Exit" }));

// Loading indicator
var result = await AnsiConsole.Status()
    .StartAsync("Loading...", async ctx =>
    {
        return await api.GetDataAsync();
    });

// Table display
var table = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("[green]ID[/]")
    .AddColumn("[green]Name[/]");
```

## Error Handling Strategy

### API Exceptions

All menu services catch and display API exceptions:

```csharp
try
{
    // API operation
}
catch (ApiException ex)
{
    AnsiConsole.MarkupLine($"[red]API Error ({ex.ErrorCode}): {ex.Message}[/]");
}
```

### Authentication Errors

The `ApiClientFactory.TestConnectionAsync()` method handles authentication:

```csharp
catch (ApiException ex) when (ex.ErrorCode == 401)
{
    AnsiConsole.MarkupLine("[red]Authentication failed. Invalid API key.[/]");
    return false;
}
```

### General Exceptions

The main menu loop catches and displays unexpected exceptions:

```csharp
catch (Exception ex)
{
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
}
```

## Testing Strategy

### Unit Tests

Focus on testing:
- `ApiClientFactory` configuration and validation
- DI container configuration
- Service registration and resolution

### Integration Tests

For testing:
- Configuration loading from various sources
- Service dependency chains

### Why Some Components Are Not Unit Tested

Menu services heavily depend on `AnsiConsole` for:
- User input prompts
- Console output
- Interactive selections

These console I/O operations make pure unit testing impractical. Instead:
- Business logic is centralized in `ApiClientFactory`
- Menu services are thin wrappers around API calls
- Integration testing covers end-to-end scenarios

## Extending the Application

### Adding a New Menu Service

1. Create a new service class:

```csharp
public class NewMenuService
{
    private readonly ApiClientFactory _apiClientFactory;

    public NewMenuService(ApiClientFactory apiClientFactory)
    {
        _apiClientFactory = apiClientFactory;
    }

    public async Task ShowAsync()
    {
        // Implementation
    }
}
```

2. Register in DI container:

```csharp
services.AddSingleton<NewMenuService>();
```

3. Inject into `ConsoleApplication`:

```csharp
public ConsoleApplication(
    // ... existing services
    NewMenuService newMenu)
{
    _newMenu = newMenu;
}
```

4. Add menu option in `MainMenuLoopAsync()`:

```csharp
case "New Feature":
    await _newMenu.ShowAsync();
    break;
```

### Adding a New API Client Type

1. Add factory method to `ApiClientFactory`:

```csharp
public NewApi CreateNewApi()
{
    _logger.LogDebug("Creating NewApi instance");
    return new NewApi(GetConfiguration());
}
```

2. Use in menu service:

```csharp
using var api = _apiClientFactory.CreateNewApi();
var result = await api.OperationAsync();
```

## Best Practices

1. **Use `using` statements** for API clients to ensure proper disposal
2. **Wrap long operations** with `AnsiConsole.Status()` for loading feedback
3. **Escape user content** with `Markup.Escape()` before display
4. **Handle API exceptions** gracefully with user-friendly messages
5. **Keep menu services focused** on a single domain area
