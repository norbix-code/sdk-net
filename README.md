# Norbix.Sdk

[![CI](https://github.com/norbix-dev/norbix-net/actions/workflows/ci.yml/badge.svg)](https://github.com/norbix-dev/norbix-net/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Norbix.Sdk.svg?logo=nuget)](https://www.nuget.org/packages/Norbix.Sdk)
[![License](https://img.shields.io/nuget/l/Norbix.Sdk.svg)](./LICENSE)

Official .NET SDK for [Norbix](https://norbix.dev). One client wraps both the **API** (project-scoped data — collections, users, AI chat) and the **Hub** (project & account configuration — schemas, integrations, team, billing). Targets .NET 10.

## Install

```bash
dotnet add package Norbix.Sdk
```

DTO contracts are also published separately. Use `Norbix.Sdk.Types` if you only need request/response shapes without the client.

## Quickstart

```csharp
using Norbix.Sdk;
using Norbix.Sdk.Types.Api;
using Norbix.Sdk.Types.Hub;

// Service mode — long-lived API key
using var client = new NorbixClient(new NorbixClientOptions
{
    ApiKey = "<api_key>",
    ProjectId = "proj_123",
});

await client.Api.Database.FindAsync(new FindRequest { CollectionName = "orders" });
await client.Hub.Database.GetDatabaseSchemasAsync(new GetDatabaseSchemas());
```

```csharp
// User mode — exchange credentials for a JWT
using var client = new NorbixClient(new NorbixClientOptions
{
    ProjectId = "proj_123",
});

await client.LoginAsync(new()
{
    UserName = "alice@team.io",
    Password = "secret",
});

await client.Api.Database.FindAsync(new FindRequest { CollectionName = "orders" }); // acts as Alice
```

## Authentication

| Mode | When to use | How |
| --- | --- | --- |
| **API key** | Server-to-server, scripts, scheduled jobs | `ApiKey = "..."` or `NORBIX_API_KEY` |
| **JWT bearer** | Logged-in user session | `BearerToken = "..."`, `NORBIX_BEARER_TOKEN`, or `await client.LoginAsync(...)` |

Both are sent as `Authorization: Bearer <token>`. If both are set, JWT wins. With neither set the SDK throws `NORBIX_NOT_AUTHENTICATED` on the first call.

```csharp
var asUser = client.WithBearerToken(userToken);
var asService = client.WithoutBearerToken(); // falls back to ApiKey if configured
var forOtherProject = client.WithScope("proj_456");
```

## Configuration from Environment

Any field you do not set on `NorbixClientOptions` is read from environment variables.

```bash
NORBIX_API_KEY=sk_live_...
NORBIX_PROJECT_ID=proj_123
NORBIX_ACCOUNT_ID=acc_456            # optional
NORBIX_API_URL=https://api.norbix.dev
NORBIX_HUB_URL=https://hub.norbix.dev
NORBIX_API_VERSION=v2
NORBIX_HUB_VERSION=v2
NORBIX_TIMEOUT_MS=30000
```

```csharp
using var client = new NorbixClient(); // reads everything from env
```

The SDK does not load `.env` files itself. Load them in your app bootstrap or deployment environment before constructing `NorbixClient`.

## Project vs Account Scope

- `ProjectId` is required. The SDK works at project scope by default.
- `AccountId` is optional. When set, account-scoped Hub endpoints (team invite, billing portal, account verify) become callable. Calling them without `AccountId` throws `NORBIX_ACCOUNT_SCOPE_REQUIRED` before the request leaves your machine.

## Integration Guides

- [**Using with ASP.NET Core**](./docs/integrations/aspnet-core.md) — register `NorbixClient`, bind configuration, inject into controllers/services.
- [**Using with Generic Host / DI**](./docs/integrations/di.md) — lifetime patterns, retries (Polly), and advanced options.

## ASP.NET Core / DI

```csharp
// Program.cs
builder.Services.AddNorbix(builder.Configuration); // scoped by default (safe for per-request auth)

// or configure explicitly
builder.Services.AddNorbix(o =>
{
    o.ProjectId = "proj_123";
    o.ApiKey = "<api_key>";
});

// Service-to-service (fixed API key) singleton:
builder.Services.AddNorbixSingleton(builder.Configuration);
```

```csharp
public sealed class OrdersController(NorbixClient norbix) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var orders = await norbix.Api.Database.FindAsync(
            new FindRequest { CollectionName = "orders" },
            ct);

        return Ok(orders);
    }
}
```

## Module Reference

The public endpoint surface is generated from gateway DTOs at compile time. The test snapshots in `tests/Norbix.Sdk.Tests/test_results` verify every generated module by sending a request and deserializing a representative response.

### API — project-scoped data (38 endpoints)

| Module | Endpoints | Description |
| --- | ---: | --- |
| `chat` | 1 | AI chat completion. |
| `database` | 18 | Collection CRUD, count, distinct, aggregate, saved aggregate execution, taxonomy reads. |
| `echo` | 1 | Smoke-test echo endpoint. |
| `membership` | 18 | User CRUD, registration, preferences, roles, and permissions. |

### Hub — project & account configuration (244 endpoints)

| Module | Endpoints | Description |
| --- | ---: | --- |
| `account` | 37 | Account profile, status, projects, regions, team invites, billing, verification. |
| `ai` | 14 | LLM and MCP integration configuration and tests. |
| `database` | 41 | Schemas, integrations, saved aggregates, taxonomies, triggers, module settings. |
| `echo` | 1 | Smoke-test echo endpoint. |
| `email` | 1 | Email helper endpoint. |
| `files` | 15 | File storage integrations, triggers, and module settings. |
| `internal` | 1 | Internal type-generation endpoint. |
| `logs` | 9 | Logging integrations and module settings. |
| `membership` | 25 | Roles, policies, users, preferences, integrations, triggers. |
| `notifications` | 68 | Email and push templates, integrations, campaigns, devices, settings. |
| `payments` | 16 | Payment integrations, triggers, tests, and module settings. |
| `scheduler` | 8 | Scheduler module and task management. |
| `webhooks` | 8 | Webhook integrations, destinations, tests, and module settings. |

## Coverage Notes

Generated coverage tracks the API and Hub DTO contract files. Some flows are not generated automatically:

| Area | Status |
| --- | --- |
| File upload/download bytes | Out of scope for the generated endpoint client; file metadata flows through normal DTOs. |
| Server Events / SSE | Requires a hand-written streaming module once the public stream contract is finalized. |

## Error Handling

```csharp
try
{
    await client.Api.Database.FindAsync(new FindRequest { CollectionName = "orders" });
}
catch (NorbixException ex)
{
    Console.WriteLine($"{ex.StatusCode} {ex.Code}: {ex.Message}");

    foreach (var fieldError in ex.FieldErrors)
    {
        Console.WriteLine($"{fieldError.FieldName}: {fieldError.Message}");
    }
}
```

| Code | Meaning |
| --- | --- |
| `NORBIX_NOT_AUTHENTICATED` | No `ApiKey`, `BearerToken`, or env var, and `LoginAsync` was not called. |
| `NORBIX_ACCOUNT_SCOPE_REQUIRED` | Account-scoped endpoint called without `AccountId`. |
| `NORBIX_MISSING_PATH_PARAM` | A `{token}` in the route was not provided on the request DTO. |
| `NORBIX_NETWORK_ERROR` | HTTP failed (timeout, connection reset, DNS). |

## How It Stays in Sync With the Backend

The source of truth is `src/Norbix.Sdk.Types/Generated/Api.dtos.cs` and `src/Norbix.Sdk.Types/Generated/Hub.dtos.cs`.

A Roslyn source generator walks every `[NorbixRoute]` DTO and emits:

- `client.Api.*` and `client.Hub.*` namespace classes
- one module class per endpoint group
- one strongly typed async method per endpoint
- an internal endpoint catalog used by the coverage tests

CI fails if request/response snapshots drift, so stale DTOs or broken generated endpoints are caught during tests.

## Development

```bash
dotnet tool restore
dotnet restore
dotnet build
dotnet test
```

Conventional commits are required. The `release-preview` workflow comments on every PR with the next version it would cut.

## Releases

Pushes to `main` are released to NuGet by [semantic-release](https://github.com/semantic-release/semantic-release) with `@semantic-release/exec` calling `dotnet pack` and `dotnet nuget push`. `next` and `beta` branches publish prereleases.

## License

MIT — see [LICENSE](./LICENSE).
