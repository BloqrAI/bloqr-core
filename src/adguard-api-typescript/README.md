# AdGuard DNS API TypeScript SDK

A comprehensive TypeScript SDK for the AdGuard DNS API v1.15 with feature parity to `adguard-api-dotnet`.

## Features

- **Full API Coverage**: All 34 AdGuard DNS API endpoints
- **Type-Safe Models**: Complete TypeScript types matching the OpenAPI spec
- **Repository Pattern**: High-level abstractions for common operations
- **Retry Policies**: Automatic retry with exponential backoff for transient errors
- **CLI Application**: Interactive console UI for managing AdGuard DNS
- **Rules Compiler Integration**: Sync compiled filter rules to AdGuard DNS
- **Comprehensive Tests**: Full test coverage with Deno test

## Installation

```bash
cd src/adguard-api-typescript
deno cache src/mod.ts
```

## Quick Start

### Using the Client

```typescript
import { AdGuardDnsClient } from 'adguard-api-typescript';

// Create client with API key
const client = AdGuardDnsClient.withApiKey('your-api-key');

// Or from environment variable
const client = AdGuardDnsClient.fromEnv('ADGUARD_API_KEY');

// Use APIs directly
const devices = await client.devices.listDevices();
const limits = await client.account.getAccountLimits();

// Or use repositories for higher-level operations
const device = await client.deviceRepository.getById('device-id');
const stats = await client.statisticsRepository.getSummary();
```

### Using the Configuration Builder

```typescript
import { AdGuardDnsClient, ConfigurationBuilder } from 'adguard-api-typescript';

const config = new ConfigurationBuilder()
  .withApiKey('your-api-key')
  .withTimeout(60000)
  .withConsoleLogging()
  .build();

const client = new AdGuardDnsClient(config);
```

### Using the CLI

```bash
# Interactive mode
deno task start

# Or with API key
deno task start -- --api-key your-key

# Sync rules from file
deno task start -- sync --file ../bloqr-blocklists/output/adguard_user_filter.txt
```

## Architecture

### Project Structure

```
src/adguard-api-typescript/
├── src/
│   ├── api/              # Low-level API clients
│   │   ├── account.ts
│   │   ├── auth.ts
│   │   ├── devices.ts
│   │   ├── dns-servers.ts
│   │   ├── statistics.ts
│   │   ├── query-log.ts
│   │   ├── filter-lists.ts
│   │   ├── web-services.ts
│   │   └── dedicated-ips.ts
│   ├── cli/              # Interactive CLI application
│   │   ├── menus/
│   │   └── utils.ts
│   ├── errors/           # Custom error classes
│   ├── helpers/          # Utility functions
│   │   ├── configuration.ts
│   │   ├── datetime.ts
│   │   └── retry.ts
│   ├── models/           # TypeScript types from OpenAPI
│   ├── repositories/     # High-level abstractions
│   ├── client.ts         # Main client entry point
│   └── index.ts          # Public exports
├── tests/                # Jest tests
└── api/                  # OpenAPI specification
```

### API Clients

Direct access to all AdGuard DNS API endpoints:

- **AccountApi**: Account limits
- **AuthApi**: OAuth authentication
- **DevicesApi**: Device CRUD operations
- **DnsServersApi**: DNS server management
- **StatisticsApi**: Query statistics
- **QueryLogApi**: DNS query logs
- **FilterListsApi**: Available filter lists
- **WebServicesApi**: Web services catalog
- **DedicatedIpApi**: Dedicated IP management

### Repositories

Higher-level abstractions with error handling:

- **DeviceRepository**: Device management with entity checks
- **DnsServerRepository**: DNS server management
- **UserRulesRepository**: User rules CRUD operations
- **StatisticsRepository**: Statistics with time range presets
- **QueryLogRepository**: Query log with statistics

### Helpers

Utility functions matching .NET implementation:

- **ConfigurationHelper**: Fluent API for client configuration
- **RetryPolicy**: Automatic retry with exponential backoff
- **DateTime**: Unix millisecond timestamp utilities

## API Reference

### Models

All TypeScript types match the AdGuard DNS API v1.15 OpenAPI specification:

```typescript
// Device types
interface Device { id, name, device_type, dns_server_id, dns_addresses, settings }
interface DeviceCreate { name, device_type, dns_server_id }
interface DeviceUpdate { name?, device_type?, dns_server_id? }

// DNS Server types
interface DNSServer { id, name, default, device_ids, settings }
interface DNSServerSettings { protection_enabled, user_rules_settings, ... }

// Statistics types
interface StatsQueryParams { time_from_millis, time_to_millis, devices?, countries? }
interface TimeQueriesStatsList { stats: TimeQueriesStats[] }

// And many more...
```

### Error Handling

```typescript
import {
  ApiError,
  EntityNotFoundError,
  ValidationError,
  RateLimitError
} from 'adguard-api-typescript';

try {
  const device = await client.devices.getDevice('invalid-id');
} catch (error) {
  if (error instanceof EntityNotFoundError) {
    console.log(`Device not found: ${error.entityId}`);
  } else if (error instanceof ValidationError) {
    console.log(`Validation failed: ${error.fieldErrors}`);
  } else if (error instanceof RateLimitError) {
    console.log(`Rate limited, retry after: ${error.retryAfter}s`);
  }
}
```

### DateTime Helpers

```typescript
import { DateTime, daysAgo, hoursAgo, startOfToday } from 'adguard-api-typescript';

// Get statistics for the last 7 days
const stats = await client.statistics.getTimeQueriesStats({
  time_from_millis: daysAgo(7),
  time_to_millis: DateTime.now(),
});

// Or use repository with presets
const summary = await client.statisticsRepository.getTimeStatsByRange('7d');
```

### Rules Compiler Integration

Sync compiled filter rules from `adblock-compiler-core`:

```typescript
import { RulesCompilerIntegration } from 'adguard-api-typescript';

const integration = new RulesCompilerIntegration(
  client.userRulesRepository,
  client.dnsServerRepository
);

// Sync from compiled rules file
const result = await integration.syncCompiledRules('server-id');
console.log(`Synced ${result.rulesCount} rules`);

// Sync from custom file
await integration.syncRules('server-id', {
  rulesPath: 'path/to/rules.txt',
  append: false,
  enable: true,
});
```

## Testing

```bash
# Run all tests
deno task test

# Run with coverage
deno task test:coverage

# Run specific test file
deno test tests/helpers/datetime.test.ts
```

## CLI Commands

### Interactive Mode

```bash
deno task start
```

Navigate through menus:
- Devices: List, view, create, delete, toggle protection
- DNS Servers: Manage DNS servers/profiles
- User Rules: Add, remove, sync rules
- Statistics: View query statistics
- Query Log: Search and analyze queries
- Account: View limits and filter lists

### Non-Interactive Commands

```bash
# Sync rules from file
deno task start -- sync --file rules.txt --server server-id

# With verbose logging
deno task start -- sync --file rules.txt --verbose

# Append to existing rules
deno task start -- sync --file rules.txt --append
```

## Feature Parity with adguard-api-dotnet

This SDK provides equivalent functionality to the .NET implementation:

| Feature | .NET | TypeScript |
|---------|------|------------|
| API Clients | ✓ | ✓ |
| Repository Pattern | ✓ | ✓ |
| Configuration Helper | ✓ | ✓ |
| Retry Policies (Polly) | ✓ | ✓ (axios-retry) |
| DateTime Extensions | ✓ | ✓ |
| Console UI (Spectre) | ✓ | ✓ (inquirer/ora) |
| Fluent Configuration | ✓ | ✓ |
| Error Handling | ✓ | ✓ |
| Comprehensive Tests | ✓ | ✓ |

## Environment Variables

| Variable | Description |
|----------|-------------|
| `ADGUARD_API_KEY` | Default API key for CLI |

## Dependencies

- **axios**: HTTP client
- **axios-retry**: Retry policies
- **commander**: CLI argument parsing
- **inquirer**: Interactive prompts
- **ora**: Spinners
- **chalk**: Terminal colors
- **cli-table3**: Table formatting

## Type Definitions

This project uses Deno, which works with TypeScript natively. For compatibility with other tools or for publishing, type definition files (`.d.ts`) can be generated using:

```bash
deno task generate:types
```

The generated files are placed in the `dist/` directory and re-export all types from the source files. These files provide type information for consumers of this library.

**Note**: The `.d.ts` files are automatically generated and should not be edited manually. They are excluded from version control.

### Available Tasks

- `deno task start` - Run the CLI application
- `deno task sync` - Sync rules to AdGuard DNS
- `deno task cli` - Run the interactive CLI
- `deno task test` - Run tests
- `deno task check` - Type check the code
- `deno task lint` - Lint the code
- `deno task fmt` - Format the code
- `deno task generate:types` - Generate `.d.ts` type definition files

## License

MIT
