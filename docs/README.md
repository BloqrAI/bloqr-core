# Documentation

This directory contains comprehensive documentation for the ad-blocking repository.

## Quick Links

| Document | Description |
|----------|-------------|
| [**Why Validation Matters**](WHY_VALIDATION_MATTERS.md) | 🔒 **Start Here** - Understand why security validation is essential |
| [Getting Started](getting-started.md) | Installation and first steps |
| [Configuration Reference](configuration-reference.md) | Complete configuration schema |
| [Docker Guide](docker-guide.md) | Docker development environment |
| [Compiler Comparison](compiler-comparison.md) | Compare TypeScript, .NET, Python, Rust compilers |
| [Release Guide](release-guide.md) | Creating releases with automatic binary builds |

## Security Documentation

| Document | Description |
|----------|-------------|
| [**Why Validation Matters**](WHY_VALIDATION_MATTERS.md) | User-friendly explanation of validation and security (everyone should read this!) |
| [Runtime Enforcement](RUNTIME_ENFORCEMENT.md) | Technical details on runtime validation enforcement |
| [Validation Enforcement](VALIDATION_ENFORCEMENT.md) | CI/CD enforcement mechanisms |
| [Security Policy](../SECURITY.md) | Security vulnerability reporting |

## Contents

### Guides

| Guide | Description |
|-------|-------------|
| [Getting Started](getting-started.md) | Installation, prerequisites, and quick start |
| [**@bloqr/compiler-core Guide**](guides/adblock-compiler-guide.md) | **Core compilation package with CI/CD examples** |
| [Docker Guide](docker-guide.md) | Using Docker for development |
| [Configuration Reference](configuration-reference.md) | Full configuration schema documentation |
| [Compiler Comparison](compiler-comparison.md) | Feature comparison of all compilers |
| [Release Guide](release-guide.md) | Creating releases with automatic binary builds |
| [API Client Usage Guide](guides/api-client-usage.md) | AdGuard DNS API client usage (C# examples) |
| [API Client Examples](guides/api-client-examples.md) | Code examples with helper classes |
| [ConsoleUI Architecture](guides/consoleui-architecture.md) | Console UI design documentation |

### API Reference (`api/`)

Auto-generated API documentation for the AdGuard DNS API Client (v1.15).

#### API Endpoints

| API Class | Description |
|-----------|-------------|
| [AccountApi](api/AccountApi.md) | Account limits and information |
| [AuthenticationApi](api/AuthenticationApi.md) | OAuth token generation |
| [DevicesApi](api/DevicesApi.md) | Device management (CRUD) |
| [DNSServersApi](api/DNSServersApi.md) | DNS server profile management |
| [DedicatedIPAddressesApi](api/DedicatedIPAddressesApi.md) | Dedicated IPv4 management |
| [FilterListsApi](api/FilterListsApi.md) | Filter list retrieval |
| [QueryLogApi](api/QueryLogApi.md) | Query log operations |
| [StatisticsApi](api/StatisticsApi.md) | DNS query statistics |
| [WebServicesApi](api/WebServicesApi.md) | Web services for blocking |

#### Data Models

| Model | Description |
|-------|-------------|
| [AccessTokenResponse](api/AccessTokenResponse.md) | OAuth token response |
| [AccessTokenErrorResponse](api/AccessTokenErrorResponse.md) | OAuth error response |
| [AccountLimits](api/AccountLimits.md) | Account usage limits |
| [Device](api/Device.md) | Device information |
| [DeviceCreate](api/DeviceCreate.md) | Device creation request |
| [DeviceUpdate](api/DeviceUpdate.md) | Device update request |
| [DNSServer](api/DNSServer.md) | DNS server profile |
| [DNSServerCreate](api/DNSServerCreate.md) | DNS server creation request |
| [DedicatedIPv4Address](api/DedicatedIPv4Address.md) | Dedicated IPv4 address |
| [ErrorResponse](api/ErrorResponse.md) | API error response |
| [FilterList](api/FilterList.md) | Filter list information |
| [Limit](api/Limit.md) | Usage limit details |
| [QueryLogResponse](api/QueryLogResponse.md) | Query log data |
| [TimeQueriesStatsList](api/TimeQueriesStatsList.md) | Time-based statistics |
| [WebService](api/WebService.md) | Web service for blocking |

## Project Documentation

### Main README Files

| Project | Location |
|---------|----------|
| Repository Overview | [README.md](../README.md) |
| .NET API Client | [src/adguard-api-dotnet/README.md](../src/adguard-api-dotnet/README.md) |
| TypeScript API Client | [src/adguard-api-typescript/README.md](../src/adguard-api-typescript/README.md) |
| Rust API Client | [src/adguard-api-rust/README.md](../src/adguard-api-rust/README.md) |
| TypeScript Compiler (`@bloqr/compiler-core`) | [src/adblock-compiler-core/README.md](../src/adblock-compiler-core/README.md) |
| .NET Compiler | [src/rules-compiler-dotnet/README.md](../src/rules-compiler-dotnet/README.md) |
| Python Compiler | [src/rules-compiler-python/README.md](../src/rules-compiler-python/README.md) |
| Rust Compiler | [src/rules-compiler-rust/README.md](../src/rules-compiler-rust/README.md) |
| Shell Scripts | [src/rules-compiler-shell/README.md](../src/rules-compiler-shell/README.md) |

### Development

| Document | Location |
|----------|----------|
| Claude Code Instructions | [CLAUDE.md](../CLAUDE.md) |
| Copilot Instructions | [.github/copilot-instructions.md](../.github/copilot-instructions.md) |
| Security Policy | [SECURITY.md](../SECURITY.md) |
| Release Guide | [release-guide.md](release-guide.md) |

## API Endpoints Reference

All URIs are relative to `https://api.adguard-dns.io`

| Class | Method | HTTP Request | Description |
|-------|--------|--------------|-------------|
| *AccountApi* | **GetAccountLimits** | **GET** /oapi/v1/account/limits | Gets account limits |
| *AuthenticationApi* | **AccessToken** | **POST** /oapi/v1/oauth_token | Generates tokens |
| *DNSServersApi* | **CreateDNSServer** | **POST** /oapi/v1/dns_servers | Creates DNS server |
| *DNSServersApi* | **ListDNSServers** | **GET** /oapi/v1/dns_servers | Lists DNS servers |
| *DedicatedIPAddressesApi* | **AllocateDedicatedIPv4Address** | **POST** /oapi/v1/dedicated_addresses/ipv4 | Allocates IPv4 |
| *DedicatedIPAddressesApi* | **ListDedicatedIPv4Addresses** | **GET** /oapi/v1/dedicated_addresses/ipv4 | Lists IPv4 addresses |
| *DevicesApi* | **CreateDevice** | **POST** /oapi/v1/devices | Creates device |
| *DevicesApi* | **GetDevice** | **GET** /oapi/v1/devices/{device_id} | Gets device |
| *DevicesApi* | **ListDevices** | **GET** /oapi/v1/devices | Lists devices |
| *DevicesApi* | **RemoveDevice** | **DELETE** /oapi/v1/devices/{device_id} | Removes device |
| *DevicesApi* | **UpdateDevice** | **PUT** /oapi/v1/devices/{device_id} | Updates device |
| *FilterListsApi* | **ListFilterLists** | **GET** /oapi/v1/filter_lists | Gets filter lists |
| *QueryLogApi* | **ClearQueryLog** | **DELETE** /oapi/v1/query_log | Clears query log |
| *QueryLogApi* | **GetQueryLog** | **GET** /oapi/v1/query_log | Gets query log |
| *StatisticsApi* | **GetTimeQueriesStats** | **GET** /oapi/v1/stats/time | Gets statistics |
| *WebServicesApi* | **ListWebServices** | **GET** /oapi/v1/web_services | Lists web services |

## Authentication

### ApiKey

- **Type**: API key
- **API key parameter name**: Authorization
- **Location**: HTTP header

### AuthToken

- **Type**: Bearer Authentication

## External Resources

- [AdGuard DNS](https://adguard-dns.io/)
- [AdGuard DNS API Documentation](https://api.adguard-dns.io/static/swagger/swagger.json)
- [@bloqr/compiler-core on JSR](https://jsr.io/@bloqr/compiler-core)
- [AdBlock Tester](https://adblock-tester.com/)
- [AdGuard Tester](https://d3ward.github.io/toolz/adblock.html)
