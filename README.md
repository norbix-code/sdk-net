# Norbix .NET SDK

[![CI](https://github.com/norbix-dev/norbix-net/actions/workflows/ci.yml/badge.svg)](https://github.com/norbix-dev/norbix-net/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Norbix.Api.svg?logo=nuget)](https://www.nuget.org/packages/Norbix.Api)
[![NuGet](https://img.shields.io/nuget/v/Norbix.Hub.svg?logo=nuget)](https://www.nuget.org/packages/Norbix.Hub)
[![License](https://img.shields.io/nuget/l/Norbix.Api.svg)](./LICENSE)

Official .NET SDK for [Norbix](https://norbix.dev). There are **two packages**:

- **`Norbix.Api`**: project-scoped data (collections, users, AI chat)
- **`Norbix.Hub`**: project/account configuration (schemas, integrations, team, billing)

Each package exposes the same ergonomic surface (e.g. `client.Database`, `client.Membership`) but targets only its gateway (API or Hub). Targets .NET 10.

## Install

```bash
dotnet add package Norbix.Api
```

Or install Hub only:

```bash
dotnet add package Norbix.Hub
```

## Quickstart

### API package (`Norbix.Api`)

```csharp
using Norbix.Sdk;
using Norbix.Sdk.Types.Api;

// Service mode — long-lived API key
using var client = new NorbixClient(new NorbixClientOptions
{
    ApiKey = "<api_key>",
    ProjectId = "proj_123",
});

await client.Database.FindAsync(new FindRequest { CollectionName = "orders" });
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

await client.Database.FindAsync(new FindRequest { CollectionName = "orders" }); // acts as Alice
```

### Hub package (`Norbix.Hub`)

```csharp
using Norbix.Sdk;
using Norbix.Sdk.Types.Hub;

using var client = new NorbixClient(new NorbixClientOptions
{
    ApiKey = "<api_key>",
    ProjectId = "proj_123",
    AccountId = "acc_456", // only required for account-scoped Hub endpoints
});

await client.Database.GetDatabaseSchemasAsync(new GetDatabaseSchemas());
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
NORBIX_REGION=nb-eu-germany          # optional — no default region (see "Regions")
NORBIX_API_URL=https://api.norbix.ai
NORBIX_HUB_URL=https://hub.norbix.ai
NORBIX_API_VERSION=v2
NORBIX_HUB_VERSION=v2
NORBIX_TIMEOUT_MS=30000
```

```csharp
using var client = new NorbixClient(); // reads everything from env
```

The SDK does not load `.env` files itself. Load them in your app bootstrap or deployment environment before constructing `NorbixClient`.

You can also override gateways for self-hosted or local deployments:

```csharp
var client = new NorbixClient(new NorbixClientOptions
{
    ProjectId = "proj_123",
    ApiKey = "<api_key>",
    ApiBaseUrl = "https://api.norbix.isidos.lt",  // or "http://localhost:5000"
    HubBaseUrl = "https://hub.norbix.isidos.lt",  // or "http://localhost:5001"
});
```

## Regions

Norbix can run in multiple regions. The SDK resolves the region in this order — unlike other settings there is **no default region**:

1. Per-call override — `client.WithRegion("...")`
2. `Region` on `NorbixClientOptions` (explicit values win over env vars)
3. `NORBIX_REGION` environment variable
4. Unset — no region header is sent and requests stay byte-identical to a region-less SDK

When a region is resolved, every request carries the `nb-region` header.

```csharp
using var client = new NorbixClient(new NorbixClientOptions
{
    ApiKey = "<api_key>",
    ProjectId = "proj_123",
    Region = "nb-eu-germany",
});
```

```bash
# or from the environment
NORBIX_REGION=nb-eu-germany
```

```csharp
using var client = new NorbixClient(); // picks up NORBIX_REGION
```

`WithRegion(...)` creates a derived client for per-call or per-scope overrides. Like `WithBearerToken` / `WithScope`, the new client shares the underlying `HttpClient`:

```csharp
var eu = client.WithRegion("nb-eu-germany");
var us = client.WithRegion("nb-us-east");   // overrides a region set on options
var unpinned = client.WithRegion(null);     // clears the region — header omitted again
```

### Regional URLs

When a region is resolved **and** the base URL is still the SDK default, the SDK composes the regional endpoint per request:

| Base URL | With `Region = "nb-eu-germany"` |
| --- | --- |
| `https://api.norbix.ai` (default) | `https://nb-eu-germany.api.norbix.ai` |
| `https://hub.norbix.ai` (default) | `https://nb-eu-germany.hub.norbix.ai` |
| Custom `ApiBaseUrl` / `HubBaseUrl` | Never rewritten — the `nb-region` header is still sent |

Self-hosted and local deployments are unaffected: a custom base URL is never rewritten, and with no region configured nothing changes at all.

### Discovering regions

The echo endpoint reports the regions a deployment knows about, with their composed per-region endpoints (empty on SelfHosted deployments in practice):

```csharp
var echo = await client.Echo.EchoAsync(new Echo());

foreach (var region in echo!.Regions ?? [])
{
    // EchoRegionDto: Code, DisplayName, ApiUrl, HubUrl
    Console.WriteLine($"{region.Code} ({region.DisplayName}) → {region.ApiUrl}");
}
```

With the `Norbix.Hub` package, the account module lists the regions available to your account:

```csharp
using Norbix.Sdk.Types.Hub;

var regions = await client.Account.GetAccountRegionsAsync(new GetAccountRegions());
// regions.Items — set of ProjectRegionDto { Id, Name, Continent }
```

### Project regions (`Norbix.Hub`)

A project has a **primary region** (where its main DB / control data live) and optional **additional regions** (where app-data infrastructure may be placed — requires a multi-region deployment). Pass region codes when creating a project, or change them later:

```csharp
// At creation
await client.Account.CreateProjectAsync(new CreateProjectRequest
{
    ProjectName = "my-project",
    Integration = new DatabaseIntegrationRequest { /* ... */ },
    PrimaryRegion = "nb-eu-germany",
    AdditionalRegions = ["nb-us-east"],
});

// Later
await client.Account.UpdateProjectRegionsAsync(new UpdateProjectRegions
{
    ProjectId = "proj_123",
    PrimaryRegion = "nb-eu-germany",
    AdditionalRegions = ["nb-us-east"],
});
```

`ProjectDto` (returned by `client.Account.GetProjectAsync(...)`) exposes the same shape as `PrimaryRegion` / `AdditionalRegions` (`ProjectRegionDto`).

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
        var orders = await norbix.Database.FindAsync(
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

## Working with terms

A **taxonomy** is a named tree of **terms** (labels). A term can have one parent (a clean hierarchy) or several parents (the same item under many categories). Pick the call that matches what you want:

| I want to… | Call | Returns |
| --- | --- | --- |
| Get a taxonomy's terms as a flat list | `FindTermsAsync` | a paginated `List` of terms |
| Get only the children of one term | `FindTermsChildrenAsync` | a `List` of child terms (direct + multi-parent) |
| Get a taxonomy's terms as a ready-made tree | `FindTermTreeAsync` | a `Tree` of nested term nodes |
| Get the taxonomy structure (e.g. Countries → Cities) | `FindTaxonomyTreeAsync` | a `Tree` of taxonomy nodes |

The examples below all use one example `services` taxonomy shaped like this:

```text
Indoors
  └─ Air conditioning
       └─ Wall-mounted
Outdoors
  └─ Solar panels
```

### List a taxonomy's terms (flat)

**Goal:** show every term of `services` in a simple list, in display order.

```csharp
var result = await client.Database.FindTermsAsync(new FindTermsRequest
{
    TaxonomyName = "services",
});
```

```json
{
  "list": {
    "items": [
      { "id": "term_indoors",   "taxonomyName": "services", "parentId": null,           "order": 1, "name": "Indoors" },
      { "id": "term_air_con",   "taxonomyName": "services", "parentId": "term_indoors", "order": 1, "name": "Air conditioning" },
      { "id": "term_wall",      "taxonomyName": "services", "parentId": "term_air_con", "order": 1, "name": "Wall-mounted" },
      { "id": "term_outdoors",  "taxonomyName": "services", "parentId": null,           "order": 2, "name": "Outdoors" },
      { "id": "term_solar",     "taxonomyName": "services", "parentId": "term_outdoors","order": 1, "name": "Solar panels" }
    ],
    "hasMore": false, "hasPrevious": false, "startingAfter": null, "endingBefore": null
  },
  "responseStatus": { "isSuccess": true }
}
```

The list is flat — every term is one row, with its `parentId` telling you where it sits. The nesting is not built for you here (use `FindTermTreeAsync` for that).

### List only top-level terms (filtered)

**Goal:** show just the roots (no parent) — for the first level of a menu.

```csharp
var result = await client.Database.FindTermsAsync(new FindTermsRequest
{
    TaxonomyName = "services",
    Filter = "{ \"parentId\": null }",
});
```

```json
{
  "list": {
    "items": [
      { "id": "term_indoors",  "taxonomyName": "services", "parentId": null, "order": 1, "name": "Indoors" },
      { "id": "term_outdoors", "taxonomyName": "services", "parentId": null, "order": 2, "name": "Outdoors" }
    ],
    "hasMore": false, "hasPrevious": false, "startingAfter": null, "endingBefore": null
  },
  "responseStatus": { "isSuccess": true }
}
```

`Filter` is an optional MongoDB filter, ANDed with the taxonomy. Use it to fetch one level at a time (lazy tree loading) or to find terms by any field.

### Get a term's children

**Goal:** the user expanded *Indoors* — load what is directly under it.

```csharp
var children = await client.Database.FindTermsChildrenAsync(new FindTermsChildrenRequest
{
    TaxonomyName = "services",
    ParentId = "term_indoors",
});
```

```json
{
  "list": {
    "items": [
      {
        "id": "term_air_con",
        "taxonomyName": "services",
        "parentId": "term_indoors",
        "order": 1,
        "name": "Air conditioning",
        "multiParents": [
          { "taxonomyId": "tax_service_types", "parentId": "term_indoors",          "name": "Indoors" },
          { "taxonomyId": "tax_service_types", "parentId": "term_energy_efficient", "name": "Energy efficient" }
        ]
      }
    ],
    "hasMore": false, "hasPrevious": false
  },
  "responseStatus": { "isSuccess": true }
}
```

This returns **both** direct children (their `parentId` is `term_indoors`) **and** multi-parent children (terms that list `term_indoors` in `multiParents`). Parent names are already resolved, so no second lookup.

### Multi-parent: one product in several categories

**Goal:** in a `products` taxonomy, a *Relaxing massage oil* belongs to *For couples*, *Gift ideas*, **and** *Body care*. Listing the children of **any** of those categories returns it.

```csharp
var children = await client.Database.FindTermsChildrenAsync(new FindTermsChildrenRequest
{
    TaxonomyName = "products",
    ParentId = "term_gift_ideas",
});
```

```json
{
  "list": {
    "items": [
      {
        "id": "term_relaxing_oil",
        "taxonomyName": "products",
        "name": "Relaxing massage oil",
        "multiParents": [
          { "taxonomyId": "tax_categories", "parentId": "term_for_couples", "name": "For couples" },
          { "taxonomyId": "tax_categories", "parentId": "term_gift_ideas",  "name": "Gift ideas" },
          { "taxonomyId": "tax_categories", "parentId": "term_body_care",   "name": "Body care" }
        ]
      }
    ],
    "hasMore": false, "hasPrevious": false
  },
  "responseStatus": { "isSuccess": true }
}
```

One product, three category links — no duplicate listings. The same product would also come back from the children of `term_for_couples` and `term_body_care`.

### Get the whole term tree in one call

**Goal:** render the full `services` tree at once, already nested.

```csharp
var tree = await client.Database.FindTermTreeAsync(new FindTermTreeRequest
{
    TaxonomyName = "services",
});
```

```json
{
  "tree": [
    {
      "id": "term_indoors",
      "name": "Indoors",
      "order": 1,
      "children": [
        {
          "id": "term_air_con",
          "name": "Air conditioning",
          "order": 1,
          "children": [
            { "id": "term_wall", "name": "Wall-mounted", "order": 1, "children": null }
          ]
        }
      ]
    },
    {
      "id": "term_outdoors",
      "name": "Outdoors",
      "order": 2,
      "children": [
        { "id": "term_solar", "name": "Solar panels", "order": 1, "children": null }
      ]
    }
  ],
  "responseStatus": { "isSuccess": true }
}
```

Roots are in `Tree`; each node carries its own `Children`; a leaf has `children: null`. The tree arrives ready to render — no client-side tree building.

### Get only a sub-tree, capped by depth

**Goal:** start from *Indoors* and go at most 2 levels deep.

```csharp
var tree = await client.Database.FindTermTreeAsync(new FindTermTreeRequest
{
    TaxonomyName = "services",
    RootTermId = "term_indoors",
    Depth = 2,
});
```

```json
{
  "tree": [
    {
      "id": "term_indoors",
      "name": "Indoors",
      "order": 1,
      "children": [
        { "id": "term_air_con", "name": "Air conditioning", "order": 1, "children": null }
      ]
    }
  ],
  "responseStatus": { "isSuccess": true }
}
```

With `Depth = 2` you get *Indoors* (level 1) and *Air conditioning* (level 2); *Wall-mounted* (level 3) is cut off, so *Air conditioning* shows `children: null`.

### Get the taxonomy structure tree — without terms

**Goal:** see how taxonomies relate to each other (e.g. a `Cities` taxonomy whose parent is `Countries`), structure only.

```csharp
var taxonomyTree = await client.Database.FindTaxonomyTreeAsync(new FindTaxonomyTreeRequest());
```

```json
{
  "tree": [
    {
      "viewId": "txn_countries",
      "taxonomyName": "Countries",
      "taxonomySlug": "countries",
      "parentId": null,
      "children": [
        { "viewId": "txn_cities", "taxonomyName": "Cities", "taxonomySlug": "cities", "parentId": "txn_countries", "children": null, "terms": null }
      ],
      "terms": null
    }
  ],
  "responseStatus": { "isSuccess": true }
}
```

This is the **taxonomy** tree, not the term tree: nodes are taxonomies. Every `terms` is `null` because we did not ask for terms.

### Get the taxonomy structure tree — with terms

**Goal:** same structure, but also pull each taxonomy's terms in the same call.

```csharp
var taxonomyTree = await client.Database.FindTaxonomyTreeAsync(new FindTaxonomyTreeRequest
{
    IncludeTerms = true,
});
```

```json
{
  "tree": [
    {
      "viewId": "txn_countries",
      "taxonomyName": "Countries",
      "taxonomySlug": "countries",
      "parentId": null,
      "terms": [
        { "id": "term_lt", "name": "Lithuania", "order": 1, "children": null },
        { "id": "term_lv", "name": "Latvia",    "order": 2, "children": null }
      ],
      "children": [
        {
          "viewId": "txn_cities",
          "taxonomyName": "Cities",
          "taxonomySlug": "cities",
          "parentId": "txn_countries",
          "terms": [
            { "id": "term_vilnius", "name": "Vilnius", "order": 1, "children": null },
            { "id": "term_kaunas",  "name": "Kaunas",  "order": 2, "children": null }
          ],
          "children": null
        }
      ]
    }
  ],
  "responseStatus": { "isSuccess": true }
}
```

Now each taxonomy node's `Terms` holds that taxonomy's full term tree (same shape as `FindTermTreeAsync`) — *Countries* carries its countries, *Cities* carries its cities.

> Every term-reading request also accepts an optional `DatabaseIntegrationId` to target a non-default database.

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
    await client.Database.FindAsync(new FindRequest { CollectionName = "orders" });
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

- flat modules on the client (`client.Database`, `client.Membership`, ...)
- one module class per endpoint group
- one strongly typed async method per endpoint
- an internal endpoint catalog used by the coverage tests

CI fails if request/response snapshots drift, so stale DTOs or broken generated endpoints are caught during tests.

## Development

```bash
dotnet restore
dotnet build
dotnet test
```

Conventional commits are required. The `release-preview` workflow comments on every PR with the next version it would cut.

## NuGet configuration (.nuget)

This repo contains two NuGet configuration files:

- `nuget.config`: restore sources + package source mapping (CI and local restore)
- `.nuget/NuGet.config`: sets `defaultPushSource` to help avoid accidentally pushing to the wrong feed when running `dotnet nuget push` locally

## Releases

Pushes to `main` are released to NuGet by [semantic-release](https://github.com/semantic-release/semantic-release) with `@semantic-release/exec` calling `dotnet pack` and `dotnet nuget push`. `next` and `beta` branches publish prereleases.

## License

MIT — see [LICENSE](./LICENSE).
