# TypeScript API SDK Guide

A comprehensive guide to using the AdGuard DNS TypeScript API SDK with Deno 2.0+.

## Overview

The TypeScript API SDK provides a complete, type-safe interface to the AdGuard DNS API v1.15 with feature parity to the .NET SDK. Built for Deno 2.0+, it offers high-level repositories, automatic retry policies, pagination support, and seamless integration with the rules compiler.

## Features

- **Full API Coverage**: Complete implementation of AdGuard DNS API v1.15
- **Type-Safe**: Comprehensive TypeScript types generated from OpenAPI spec
- **Repository Pattern**: High-level abstractions for common operations
- **Automatic Retry**: Built-in exponential backoff for transient failures
- **Pagination Support**: Easy-to-use pagination helpers for list operations
- **Error Handling**: Robust error handling with custom exception types
- **DateTime Helpers**: Utilities for Unix millisecond timestamp conversion
- **Rules Integration**: Seamless sync with rules compiler
- **Deno Native**: Leverages Deno 2.0+ features and security model

## Prerequisites

| Requirement | Version | Installation |
|-------------|---------|--------------|
| Deno | 2.0+ | [deno.land](https://deno.land/) |
| AdGuard DNS Account | - | [adguard-dns.io](https://adguard-dns.io/) |
| API Key | - | Generate from account settings |

## Installation

```bash
# Clone the repository
git clone https://github.com/BloqrAI/bloqr-lists.git
cd bloqr-lists/src/adguard-api-typescript

# Cache dependencies
deno cache src/index.ts
```

## Quick Start

### Basic Setup

```typescript
import { AdGuardDnsClient } from './src/index.ts';

// Create client with API key
const client = AdGuardDnsClient.withApiKey('your-api-key-here');

// Or from environment variable
const client = AdGuardDnsClient.fromEnv('ADGUARD_API_KEY');

// Use the client
const devices = await client.devices.listDevices();
console.log(`You have ${devices.length} devices`);
```

### Using the Builder Pattern

```typescript
import { AdGuardDnsClientBuilder } from './src/lib/index.ts';

const client = AdGuardDnsClientBuilder.create()
  .withApiKey('your-api-key')
  .withTimeout(60000)  // 60 seconds
  .withConsoleLogging()
  .build();
```

## API Reference

### Account API

Manage account information and limits.

```typescript
// Get account limits
const limits = await client.account.getAccountLimits();
console.log(`Devices: ${limits.devices.used}/${limits.devices.limit}`);
console.log(`DNS Servers: ${limits.dnsServers.used}/${limits.dnsServers.limit}`);
console.log(`User Rules: ${limits.userRules.used}/${limits.userRules.limit}`);
```

### Devices API

Manage devices connected to your DNS profiles.

```typescript
// List all devices
const devices = await client.devices.listDevices();

// Create a new device
const newDevice = await client.devices.createDevice({
  name: 'My iPhone',
  deviceType: 'IOS',
  dnsServerId: 'dns-server-id'
});

// Get device details
const device = await client.devices.getDevice('device-id');

// Update device
await client.devices.updateDevice('device-id', {
  name: 'My New iPhone 15'
});

// Delete device
await client.devices.deleteDevice('device-id');
```

### DNS Servers API

Manage DNS server profiles (filtering configurations).

```typescript
// List all DNS servers
const servers = await client.dnsServers.listServers();

// Create DNS server
const newServer = await client.dnsServers.createServer({
  name: 'Family Profile',
  settings: {
    protectionEnabled: true,
    blockAdultWebsitesEnabled: true,
    safeSearchEnabled: true
  }
});

// Update DNS server
await client.dnsServers.updateServer('server-id', {
  settings: {
    protectionEnabled: false
  }
});

// Delete DNS server
await client.dnsServers.deleteServer('server-id');
```

### Query Log API

View and manage DNS query logs.

```typescript
import { daysAgo, toUnixMilliseconds } from './src/helpers/datetime.ts';

// Get query log for last 24 hours
const dayAgo = daysAgo(1);
const now = toUnixMilliseconds(new Date());

const queryLog = await client.queryLog.getQueryLog({
  timeFromMillis: dayAgo,
  timeToMillis: now,
  limit: 100
});

// Process results
for (const item of queryLog.items) {
  console.log(`${item.domain} - ${item.filteringStatus}`);
}

// Clear query log
await client.queryLog.clearQueryLog();
```

### Statistics API

Retrieve DNS query statistics.

```typescript
import { daysAgo, toUnixMilliseconds } from './src/helpers/datetime.ts';

// Get time-based statistics for last 7 days
const weekAgo = daysAgo(7);
const now = toUnixMilliseconds(new Date());

const timeStats = await client.statistics.getTimeQueriesStats({
  timeFromMillis: weekAgo,
  timeToMillis: now
});

// Calculate totals
let totalQueries = 0;
let totalBlocked = 0;

for (const stat of timeStats.stats) {
  totalQueries += stat.value.queries;
  totalBlocked += stat.value.blocked;
}

console.log(`Total Queries: ${totalQueries}`);
console.log(`Total Blocked: ${totalBlocked}`);
console.log(`Block Rate: ${(totalBlocked / totalQueries * 100).toFixed(2)}%`);

// Get device statistics
const deviceStats = await client.statistics.getDevicesQueriesStats({
  timeFromMillis: weekAgo,
  timeToMillis: now
});

// Get company statistics
const companyStats = await client.statistics.getCompaniesStats({
  timeFromMillis: weekAgo,
  timeToMillis: now
});
```

### Filter Lists API

Get available filter lists.

```typescript
// List all available filter lists
const filterLists = await client.filterLists.listFilterLists();

for (const filter of filterLists) {
  console.log(`${filter.name}: ${filter.description}`);
}
```

### Web Services API

Manage blockable web services.

```typescript
// List all web services
const webServices = await client.webServices.listWebServices();

for (const service of webServices) {
  console.log(`${service.name} (${service.id})`);
}
```

### Dedicated IP API

Manage dedicated IPv4 addresses.

```typescript
// List dedicated IPs
const ips = await client.dedicatedIps.listDedicatedIPv4Addresses();

// Allocate a new dedicated IP
const newIp = await client.dedicatedIps.allocateDedicatedIPv4Address();
console.log(`Allocated: ${newIp.ip}`);

// Link dedicated IP to device
await client.dedicatedIps.linkDedicatedIPv4ToDevice('ip-id', {
  deviceId: 'device-id'
});
```

### Authentication API

Generate OAuth tokens (alternative to API key authentication).

```typescript
// Get access token
const tokenResponse = await client.auth.getAccessToken({
  username: 'your-email@example.com',
  password: 'your-password',
  mfaToken: '123456'  // Optional, if 2FA enabled
});

// Use the token
const clientWithToken = AdGuardDnsClient.withBearerToken(tokenResponse.accessToken);

// Refresh token
const newToken = await client.auth.getAccessToken({
  refreshToken: tokenResponse.refreshToken
});
```

## High-Level Repositories

Repositories provide convenient, high-level operations built on top of the base API.

### Device Repository

```typescript
// Get device by ID
const device = await client.deviceRepository.getById('device-id');

// Get all devices
const allDevices = await client.deviceRepository.getAll();

// Find devices by name
const iphones = allDevices.filter(d => d.name.includes('iPhone'));

// Find devices by type
const iosDevices = await client.deviceRepository.findByType('IOS');

// Get devices for a specific DNS server
const serverDevices = await client.deviceRepository.getByServerId('server-id');
```

### DNS Server Repository

```typescript
// Get default server
const defaultServer = await client.dnsServerRepository.getDefault();

// Find server by name
const familyServer = await client.dnsServerRepository.findByName('Family Profile');

// Get all servers with device counts
const servers = await client.dnsServerRepository.getAllWithDevices();
```

### Query Log Repository

```typescript
// Get queries for last N hours
const recentQueries = await client.queryLogRepository.getRecentQueries(24);

// Get blocked queries only
const blocked = await client.queryLogRepository.getBlockedQueries({
  hoursAgo: 24
});

// Get queries for specific domain
const domainQueries = await client.queryLogRepository.getQueriesForDomain(
  'example.com',
  { hoursAgo: 7 * 24 }  // Last 7 days
);
```

### Statistics Repository

```typescript
// Get summary statistics
const summary = await client.statisticsRepository.getSummary({
  hoursAgo: 24
});

console.log(`Queries: ${summary.totalQueries}`);
console.log(`Blocked: ${summary.totalBlocked}`);
console.log(`Block Rate: ${summary.blockRate}%`);

// Get statistics for specific time range
const stats = await client.statisticsRepository.getStatisticsForRange({
  startDate: new Date('2024-01-01'),
  endDate: new Date('2024-01-31')
});
```

### User Rules Repository

```typescript
// Sync rules from file
const result = await client.userRulesRepository.syncFromFile(
  'dns-server-id',
  'data/output/adguard_user_filter.txt'
);

console.log(`Added: ${result.added}`);
console.log(`Removed: ${result.removed}`);
console.log(`Unchanged: ${result.unchanged}`);

// Get current rules
const rules = await client.userRulesRepository.getRules('dns-server-id');

// Clear all rules
await client.userRulesRepository.clearRules('dns-server-id');
```

## Pagination

The SDK provides powerful pagination utilities.

### Using createPagedList

```typescript
import { createPagedList } from './src/lib/index.ts';

// Get all devices as paged list
const pagedDevices = createPagedList(() => client.deviceRepository.getAll());

// Filter and take first 10
const activeDevices = await pagedDevices
  .filter(d => d.settings?.protectionEnabled)
  .take(10)
  .toArray();

// Map to simple list
const deviceNames = await pagedDevices
  .map(d => d.name)
  .toArray();
```

### Manual Pagination

```typescript
// Create paginated result
const devices = await client.devices.listDevices();
const pageResult = createPageResult(devices, { pageSize: 10 });

console.log(`Page 1 of ${pageResult.totalPages}`);
console.log(`Items: ${pageResult.items.length}`);
console.log(`Has More: ${pageResult.hasMore}`);
```

## DateTime Helpers

Work with Unix millisecond timestamps easily.

```typescript
import {
  toUnixMilliseconds,
  fromUnixMilliseconds,
  daysAgo,
  hoursAgo,
  minutesAgo,
  startOfToday,
  endOfToday,
  formatRelative
} from './src/helpers/datetime.ts';

// Convert to Unix milliseconds
const now = toUnixMilliseconds(new Date());
const specificDate = toUnixMilliseconds(new Date('2024-01-01'));

// Convert from Unix milliseconds
const date = fromUnixMilliseconds(1704067200000);

// Relative times
const yesterday = daysAgo(1);
const sixHoursAgo = hoursAgo(6);
const thirtyMinsAgo = minutesAgo(30);

// Today's range
const todayStart = startOfToday();
const todayEnd = endOfToday();

// Format for display
const relative = formatRelative(hoursAgo(2));  // "2 hours ago"
```

## Retry Policies

Automatic retry with exponential backoff for transient failures.

### Using Built-in Retry

```typescript
import { executeWithRetry } from './src/helpers/retry.ts';

// Execute with automatic retry
const devices = await executeWithRetry(
  () => client.devices.listDevices(),
  { maxRetries: 3 }
);
```

### Custom Retry Policy

```typescript
import { RetryPolicy, createRateLimitRetryPolicy } from './src/helpers/retry.ts';

// Create custom retry policy
const customPolicy = new RetryPolicy({
  maxRetries: 5,
  baseDelay: 2000,  // 2 seconds
  maxDelay: 30000,  // 30 seconds
  retryableStatusCodes: [408, 429, 500, 502, 503, 504]
});

// Use with any operation
const result = await customPolicy.execute(() => 
  client.devices.createDevice(newDevice)
);

// Rate limit specific policy
const rateLimitPolicy = createRateLimitRetryPolicy({
  maxRetries: 5,
  baseDelay: 5000  // 5 seconds
});
```

## Error Handling

The SDK provides typed error classes.

```typescript
import { ApiError, RateLimitError, AuthenticationError } from './src/errors/index.ts';

try {
  const devices = await client.devices.listDevices();
} catch (error) {
  if (error instanceof AuthenticationError) {
    console.error('Invalid API key');
  } else if (error instanceof RateLimitError) {
    console.error(`Rate limited. Retry after: ${error.retryAfter}s`);
  } else if (error instanceof ApiError) {
    console.error(`API Error (${error.statusCode}): ${error.message}`);
  } else {
    console.error('Unknown error:', error);
  }
}
```

## Rules Compiler Integration

Seamlessly integrate with the rules compiler.

```typescript
import { RulesCompilerIntegration } from './src/rules-compiler-integration.ts';

const integration = new RulesCompilerIntegration(client);

// Sync rules from compiled output
const result = await integration.syncRulesToServer('dns-server-id', {
  rulesFile: 'data/output/adguard_user_filter.txt',
  clearExisting: true,
  dryRun: false
});

console.log(`Sync Complete:`);
console.log(`- Added: ${result.added}`);
console.log(`- Removed: ${result.removed}`);
console.log(`- Unchanged: ${result.unchanged}`);
console.log(`- Errors: ${result.errors.length}`);

// Compile and sync in one step
const compileResult = await integration.compileAndSync('dns-server-id', {
  configFile: 'compiler-config.yaml',
  clearExisting: true
});
```

## Configuration

### Configuration Builder

```typescript
import { ConfigurationHelper } from './src/helpers/configuration.ts';

// Create with API key
const config = ConfigurationHelper.createWithApiKey('your-api-key');

// Create with bearer token
const config = ConfigurationHelper.createWithBearerToken('your-token');

// Custom configuration
const config = ConfigurationHelper.createCustom()
  .withApiKey('your-api-key')
  .withBaseUrl('https://api.adguard-dns.io')
  .withTimeout(60000)
  .withUserAgent('MyApp/1.0');
```

### Environment Variables

```bash
# Set API key
export ADGUARD_API_KEY="your-api-key-here"

# Set base URL (optional)
export ADGUARD_API_URL="https://api.adguard-dns.io"
```

## Testing

### Run Tests

```bash
# Run all tests
deno task test

# Run specific test file
deno test tests/client.test.ts

# Run with coverage
deno task test:coverage

# Watch mode
deno task dev
```

### Writing Tests

```typescript
import { assertEquals } from 'https://deno.land/std@0.203.0/assert/mod.ts';
import { AdGuardDnsClient } from './src/index.ts';

Deno.test('should list devices', async () => {
  const client = AdGuardDnsClient.withApiKey('test-key');
  
  // Your test logic
  const devices = await client.devices.listDevices();
  
  assertEquals(Array.isArray(devices), true);
});
```

## Best Practices

### 1. Use Environment Variables for API Keys

```typescript
// Bad: Hardcoded key
const client = AdGuardDnsClient.withApiKey('sk_test_123456');

// Good: From environment
const client = AdGuardDnsClient.fromEnv('ADGUARD_API_KEY');
```

### 2. Use Repositories for Complex Operations

```typescript
// Bad: Multiple low-level API calls
const devices = await client.devices.listDevices();
const myDevice = devices.find(d => d.id === 'device-id');

// Good: Use repository
const device = await client.deviceRepository.getById('device-id');
```

### 3. Always Handle Errors

```typescript
// Bad: No error handling
const devices = await client.devices.listDevices();

// Good: With error handling
try {
  const devices = await client.devices.listDevices();
} catch (error) {
  if (error instanceof RateLimitError) {
    // Wait and retry
  } else {
    // Handle other errors
  }
}
```

### 4. Use DateTime Helpers

```typescript
// Bad: Manual timestamp calculation
const now = Date.now();
const yesterday = now - (24 * 60 * 60 * 1000);

// Good: Use helpers
const yesterday = daysAgo(1);
const now = toUnixMilliseconds(new Date());
```

### 5. Leverage Pagination

```typescript
// Bad: Loading all data at once
const devices = await client.devices.listDevices();
const first10 = devices.slice(0, 10);

// Good: Using pagination
const devices = await createPagedList(() => client.deviceRepository.getAll())
  .take(10)
  .toArray();
```

## Interactive CLI

The SDK includes an interactive CLI for testing and exploration.

```bash
# Start interactive mode
deno task start

# With API key argument
deno task start -- --api-key your-key
```

**Features:**
- Account information
- Device management
- DNS server management
- Query log viewing
- Statistics reports
- User rules management

## Performance Tips

1. **Use Repositories**: Higher-level abstractions with optimizations
2. **Enable Retry Policies**: Automatic handling of transient failures
3. **Batch Operations**: Group multiple API calls when possible
4. **Cache Results**: Store frequently-accessed data locally
5. **Use Pagination**: Don't load all data at once for large datasets

## Troubleshooting

### API Key Issues

```bash
# Test API key
export ADGUARD_API_KEY="your-key"
deno task start

# Or test directly
deno run --allow-net --allow-env tests/client.test.ts
```

### Rate Limiting

If you're being rate limited, implement retry delays:

```typescript
const policy = createRateLimitRetryPolicy({ maxRetries: 5, baseDelay: 5000 });
const result = await policy.execute(() => client.devices.listDevices());
```

### Network Errors

Ensure network access is allowed:

```bash
# Grant network permission
deno run --allow-net src/index.ts
```

## Examples

See complete examples in:
- [API Client Usage](./api-client-usage.md)
- [API Client Examples](./api-client-examples.md)
- `tests/` directory for test examples

## Related Documentation

- [AdGuard DNS API Documentation](https://adguard-dns.io/kb/private-dns/api/overview/)
- [TypeScript Rules Compiler](./typescript-rules-compiler.md)
- [Configuration Reference](../configuration-reference.md)
- [.NET API Client](../../src/adguard-api-dotnet/README.md)
- [Rust API Client](../../src/adguard-api-rust/README.md)

## License

GPLv3 - See [LICENSE](../../LICENSE) for details.
