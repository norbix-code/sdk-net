# Norbix .NET SDK

[![CI](https://github.com/norbix-code/sdk-net/actions/workflows/ci.yml/badge.svg)](https://github.com/norbix-code/sdk-net/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Norbix.Api.svg?logo=nuget)](https://www.nuget.org/packages/Norbix.Api)
[![NuGet](https://img.shields.io/nuget/v/Norbix.Hub.svg?logo=nuget)](https://www.nuget.org/packages/Norbix.Hub)
[![License](https://img.shields.io/nuget/l/Norbix.Api.svg)](./LICENSE)

Official .NET SDK for [Norbix](https://norbix.ai). There are **two packages**:

- **`Norbix.Api`**: project-scoped data (collections, users)
- **`Norbix.Hub`**: project/account configuration (schemas, integrations, team, billing)

Each package exposes the same ergonomic surface (e.g. `client.Database`, `client.Membership`) but targets only its gateway (API or Hub). Targets .NET 10.

The client is **`NorbixApiClient`** in `Norbix.Api` and **`NorbixHubClient`** in `Norbix.Hub`. `NorbixClientOptions`, `NorbixException` and the login types are shared, so one project can reference both packages.

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
using var client = new NorbixApiClient(new NorbixClientOptions
{
    ApiKey = "<api_key>",
    ProjectId = "proj_123",
});

await client.Database.FindAsync(new FindRequest { CollectionName = "orders" });
```

```csharp
// User mode — exchange credentials for a JWT
using var client = new NorbixApiClient(new NorbixClientOptions
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

using var client = new NorbixHubClient(new NorbixClientOptions
{
    ApiKey = "<api_key>",
    ProjectId = "proj_123",
    AccountId = "acc_456", // only required for account-scoped Hub endpoints
});

await client.Database.GetDatabaseSchemasAsync(new GetDatabaseSchemas());
```

### Both packages in one project

```csharp
using Norbix.Sdk;

var options = new NorbixClientOptions("<api_key>", "proj_123");

using var api = new NorbixApiClient(options); // project data
using var hub = new NorbixHubClient(options); // project configuration
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
using var client = new NorbixApiClient(); // reads everything from env
```

The SDK does not load `.env` files itself. Load them in your app bootstrap or deployment environment before constructing the client.

You can also override gateways for self-hosted or local deployments:

```csharp
var client = new NorbixApiClient(new NorbixClientOptions
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
using var client = new NorbixApiClient(new NorbixClientOptions
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
using var client = new NorbixApiClient(); // picks up NORBIX_REGION
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

With the `Norbix.Hub` package, the account module lists the regions available to your account (no token needed — the sign-up form calls it before there is a session):

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

Every other project setting (name, CORS, languages, admin URL, legal, Admin Portal, AI) is on [docs/hub/project.md](./docs/hub/project.md).

## Project vs Account Scope

- `ProjectId` is required. The SDK works at project scope by default.
- `AccountId` is optional. When set, account-scoped endpoints become callable. Calling one without `AccountId` throws `NORBIX_ACCOUNT_SCOPE_REQUIRED` before the request leaves your machine.
- Four account calls need no token and no `AccountId`, and are always sent with no `Authorization` header: `CreateAccountAsync` (sign-up), `CreateTeamMemberFromInvitationAsync` (join a team from an invitation), `GetAccountRegionsAsync` and `VerifyAccountAsync`. `VerifyAccountAsync` takes the account id once, in the request (`new VerifyAccount { AccountId = ..., Token = ... }`), and sends it in the query.

## Integration Guides

- [**Using with ASP.NET Core**](./docs/integrations/aspnet-core.md) — register `NorbixApiClient` / `NorbixHubClient`, bind configuration, inject into controllers/services.
- [**Using with Generic Host / DI**](./docs/integrations/di.md) — lifetime patterns, retries (Polly), and advanced options.

## ASP.NET Core / DI

```csharp
// Program.cs
builder.Services.AddNorbixApi(builder.Configuration); // singleton, reads the "Norbix" section

// or configure explicitly
builder.Services.AddNorbixApi(o =>
{
    o.ProjectId = "proj_123";
    o.ApiKey = "<api_key>";
});

// Scoped — for per-request auth (derive with WithBearerToken):
builder.Services.AddNorbixApiScoped(o => o.ProjectId = "proj_123");

// Norbix.Hub has the same helpers: AddNorbixHub, AddNorbixHubScoped.
// Each client reads its own named options, so both can live in one container.
builder.Services.AddNorbixHub(builder.Configuration);
```

```csharp
public sealed class OrdersController(NorbixApiClient norbix) : ControllerBase
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

### API — project-scoped data (98 endpoints)

| Module | Endpoints | Description |
| --- | ---: | --- |
| `ai` | 18 | End-user AI chat for a signed-in project user: availability, sessions, entries, feedback, attachments, memory, `StartEndUserChatTurnAsync` (answers at once with a turn id; the answer streams on the user's channel `ai-chat:{projectId}:{authId}`), plus the end-user tools. |
| `database` | 22 | Collection CRUD (`FindAsync`, `FindOneAsync`, `FindOwnAsync`, insert / update / replace / delete one or many), count, distinct, aggregate, saved aggregate execution (`ExecuteAggregateAsync`), `ChangeResponsibilityAsync`, schema reads, taxonomy and term reads (`FindTermsAsync`, `FindTermsChildrenAsync`, `FindTaxonomyTreeAsync`, `FindTermTreeAsync`, `FindMergedTermTreeAsync`). |
| `echo` | 1 | Smoke-test echo endpoint. |
| `files` | 13 | List, info, signed URL, upload URL + commit, download, delete one / many, public links, and a file by its stored id (`GetFileByIdAsync`). |
| `membership` | 18 | User CRUD, registration, preferences, roles, and permissions. |
| `public` | 2 | The project's public config and legal documents, read by the Admin Portal before sign-in (`GetPublicProjectConfigAsync`, `GetPublicProjectLegalAsync`). See [docs/hub/project.md](./docs/hub/project.md#public-config-and-legal-no-sign-in). |

### Hub — project & account configuration (526 endpoints)

| Module | Endpoints | Description |
| --- | ---: | --- |
| `account` | 110 | Account profile, status, projects, regions, team invites, billing, verification; your own account user (`GetMyAccountUserProfileAsync`, `UpdateMyAccountUserPhoneAsync` — the phone "Account users" SMS campaigns send to); the team list (`GetAccountCollaboratorsAsync`: flat `PageSize` / `StartingAfter` / `EndingBefore`, optional `ProjectId`); project AI settings and assistants (`GetProjectAiSettingsAsync`, `UpdateProjectAiSettingsAsync`, `CreateProjectAiAssistantAsync`, `UpdateProjectAiAssistantAsync`, `DeleteProjectAiAssistantAsync`), AI wallet usage (`GetProjectAiUsageAsync`), the Admin Portal switch (`SetAdminPortalEnabledAsync`), `CheckProjectLanguagesAsync` (which email, push and SMS templates miss a proposed project language — call it before changing the languages). Project settings, CORS, admin portal, legal, AI service users: [docs/hub/project.md](./docs/hub/project.md). |
| `ai` | 20 | LLM, MCP and embedding integration configuration and tests (`SaveEmbeddingIntegrationAsync`, `TestEmbeddingIntegrationAsync`, …); `SetLlmIntegrationAsDefaultAsync`. LLM and MCP: [docs/hub/project.md](./docs/hub/project.md#llm-integrations). |
| `database` | 73 | Module on / off (`EnableDatabaseAsync`, `DisableDatabaseAsync`); schemas with drafts, versions and diff, publish, rename, settings, list settings, embed (`UpdateDatabaseSchemaEmbedAsync`) and `ApplyDatabaseSchemaBundleAsync`; collection records from the dashboard (`FindRecordsAsync`, `InsertRecordAsync`, `UpdateManyRecordsAsync`, `AggregateRecordsAsync`, `SeedCollectionRecordsAsync`, `GetCollectionIndexesAsync`, …); taxonomies and terms with trees (`GetDatabaseTaxonomyTreeAsync`, `GetDatabaseTaxonomyTermTreeAsync`, `GetDatabaseMergedTermTreeAsync`); schema triggers; database integrations (including `GetAllowedFlexTiersAsync`, `TestDatabaseIntegrationAsync`, `RevealManagedFlexConnectionStringAsync`); saved aggregates (`TestDatabaseAggregateAsync`); collection imports. |
| `echo` | 1 | Smoke-test echo endpoint. |
| `email` | 2 | Public e-mail link endpoints: `OneClickUnsubscribeAsync` and `GetEmailPreferencesByLinkAsync` (reads the marketing preferences behind a signed unsubscribe link; pass the link's `Token`). Both are public links: they work on a client with no API key (the key is sent only when there is one), like the three `Preview*NotificationAsync` methods. |
| `files` | 23 | File storage integrations, triggers, public links, a file by its stored id (`GetFileByIdAsync`), and module settings. |
| `internal` | 1 | Internal type-generation endpoint. |
| `logs` | 9 | Logging integrations and module settings. |
| `membership` | 25 | Roles, policies, users, preferences, integrations, triggers. |
| `notifications` | 123 | Email, push and SMS templates, integrations, campaigns, devices, settings. Push: [docs/hub/push.md](./docs/hub/push.md). SMS: [docs/hub/sms.md](./docs/hub/sms.md). |
| `oauth` | 5 | The gateway's OAuth server routes (register, authorize, token, revoke). |
| `payments` | 16 | Payment integrations, triggers, tests, and module settings. |
| `scheduler` | 8 | Scheduler module on / off (`EnableSchedulerAsync`, `DisableSchedulerAsync` — `PUT`) and cron tasks: list, read, save, enable, disable, delete. Only email campaign tasks today (`EmailCampaignSchedulerTaskRequest`). Every method and a typed save example: [docs/hub/scheduler.md](./docs/hub/scheduler.md). |
| `triggers` | 1 | `GetTriggersNeedingAttentionAsync` — triggers of one type (`TriggerType`) whose last run needs attention, with the reason and the time. |
| `webhooks` | 8 | Webhook integrations, destinations, tests, and module settings. |
| `wellKnown` | 2 | OAuth discovery documents under `/.well-known`. |

**Module on / off is `PUT`.** Every module switch — `Enable`/`Disable` for
Database, Files, Email, SMS, Push, Payments, Logging, Membership, Code and
Scheduler — is sent as `PUT` with `env` in the body. Older SDK versions sent the
first nine as `GET`; the gateway still answers that `GET` for a while (it logs a
deprecation warning) and will drop it after 0.2, so update the package. Your
code does not change.

`GetDatabaseSchemasAsync` (Hub and Api) returns only the schemas of the request
environment (`norbix-env` header or `Env`, `PROD` when neither is set); each row
carries its `Env`. The paging cursors (`StartingAfter` / `EndingBefore`) are now
schema view ids (`sch_…`), so a cursor saved from an older gateway no longer
matches.

### Database writes, triggers and taxonomies (gateway 2026-10-05)

**Update many / delete many need `AllRecords` to touch every record.** An empty
filter `{}` matches the whole collection, so the gateway refuses it unless the
request says so:

```csharp
// Api
await client.Database.UpdateManyAsync(new UpdateManyRequest
{
    CollectionName = "orders",
    Filter = "{}",
    AllRecords = true,                       // without it: CM-ERRORS-DATABASE-037
    Update = "{\"status\":\"archived\"}",
});

// Hub (dashboard)
await hub.Database.DeleteManyRecordsAsync(new DeleteManyRecords
{
    CollectionName = "orders",
    Filter = "{}",
    AllRecords = true,
});
```

A missing filter on update many counts as `{}`. Callers with only "own" rights
(create as user, update own, delete own) may now call insert / update / delete
many; those calls touch only the caller's own records.

**What else changed for callers:**

- `TaxonomyListProjection.DependencyNames` is gone. Read `DependencyRefs`
  instead: one `TaxonomyRef { Id, Name }` per entry of `Dependencies`, in the
  same order. A dependency that no longer exists keeps its place with
  `Name = null`.
- `RenameDatabaseSchemaRequest.RenameUniqueName` is gone. A rename to a name
  another schema in the same environment already uses is always refused
  (`CM-ERRORS-SCHEMA-002`).
- Schema triggers are per environment. `SchemaTriggerDto` and the
  `GetSchemaTriggersAsync` rows carry `Env`; the list shows only the request
  environment (`PROD` when none is sent); enable / disable / delete act on the
  copy in the request environment. `SchemaTriggerDto.SchemaId` now holds the
  owning schema id (`sch_…`) — it used to hold the trigger's own id.
- A saved aggregate (`MongoDbAggregateDto`) lists the collections its pipeline
  joins in `JoinedCollections`. Deleting a schema that a saved aggregate joins
  is refused (`CM-ERRORS-SCHEMA-018`, the blocking aggregates are in its
  metadata `BlockerAggregateNames`).
- `TestDatabaseAggregateAsync` (Hub) needs create or update rights on the
  aggregate, not only read.
- Update, replace and change-owner no longer match soft-deleted records: such a
  record is "not found", and update many skips it.
- `ChangeResponsibilityAsync` (Api) and `ChangeRecordResponsibilityAsync` (Hub) refuse a new owner who is not a user of the
  project in the request environment (`CM-ERRORS-MEMBERSHIP-USERS-012`).
- `GetDatabaseTaxonomyTreeAsync` with `IncludeTerms = true` now fails when the
  term read fails (it used to return the taxonomies without terms).

**Error codes you may now see** (they arrive on `NorbixException.ErrorCode`):

| Code | When |
| --- | --- |
| `CM-ERRORS-DATABASE-031` | `FindTermsAsync` / `FindTermsChildrenAsync` filter uses `$where`, `$function` or `$accumulator`. |
| `CM-ERRORS-DATABASE-035` | An update one / update many body contains `$` operators (`$inc`, `$set`, …). Send the fields to set; the server applies them with `$set`. |
| `CM-ERRORS-DATABASE-036` | Invalid record document on insert one / insert many / replace one (insert many says which `Index`). Before this it was `-005` "Invalid filter document". |
| `CM-ERRORS-DATABASE-037` | Empty filter `{}` on update many / delete many without `AllRecords = true`. |
| `CM-ERRORS-MEMBERSHIP-USERS-012` | Change owner to a user who is not in the project (request environment). |
| `CM-ERRORS-SCHEMA-002` | Rename to a name another schema in the same environment uses. |
| `CM-ERRORS-SCHEMA-017` | Delete a schema that a schema trigger uses (the blocking trigger ids are in the metadata). |
| `CM-ERRORS-SCHEMA-018` | Delete a schema that a saved aggregate starts on or joins. |
| `CM-ERRORS-TAXONOMIES-005` | A taxonomy name longer than 40 characters on a term read. |
| `CM-ERRORS-TAXONOMIES-010` | The taxonomy name is unknown (term tree, merged tree). |
| `CM-ERRORS-TAXONOMIES-011` | The term tree has more than 5000 terms (whole taxonomy, merged tree, `IncludeTerms`). |
| `CM-ERRORS-TRIGGERS-002` | Schema trigger not found in the request environment, or a save that moves an existing trigger id to another schema. |

### Schema delete drops the records; webhook `eventId` (gateway 2026-10-06)

**Deleting a schema also deletes its records.** `DeleteDatabaseSchemaAsync`
(Hub, `DELETE /{version}/database/schemas/{Id}`) now also drops the schema's
collection (its records and indexes) in the request environment, in every
active database integration of that environment. For a schema with AI embed
on, its records are removed from the AI knowledge too. There is no undo, so
export the records first if you need them. The delete is still refused while a
schema trigger (`CM-ERRORS-SCHEMA-017`) or a saved aggregate
(`CM-ERRORS-SCHEMA-018`) uses the schema; then nothing is dropped. A retry is
safe. The request and response did not change.

**Outbound webhooks carry `eventId`.** The JSON body Norbix POSTs to your
webhook destination is:

```json
{ "id": "…", "eventId": "…", "event": "…", "createdOn": "…",
  "accountId": "…", "projectId": "…", "triggerId": null, "data": { } }
```

This SDK has no type for it (it is not one of the generated DTOs); read it with
`System.Text.Json` in your receiver.

- `id` is one per delivery. A retry of the same delivery keeps its `id` — use
  it to drop retries.
- `eventId` is one per change. Every delivery made for ONE record change
  carries the same `eventId`: the plain webhook delivery and each schema
  Webhook-trigger delivery. A destination that is subscribed to the event AND
  targeted by a schema Webhook trigger gets two deliveries (one with
  `triggerId` null, one with `triggerId` set): two `id`s, one `eventId`.
- Where there is no shared event (Files, Membership, Payments, AI triggers),
  `eventId` equals `id`. Older gateways do not send `eventId`; fall back to
  `id`.

De-duplicate on `eventId`:

```csharp
using var doc = JsonDocument.Parse(rawBody);
var root = doc.RootElement;
var eventId = root.TryGetProperty("eventId", out var e) && e.ValueKind == JsonValueKind.String
    ? e.GetString()!
    : root.GetProperty("id").GetString()!;   // older gateway: no eventId
if (!processedEventIds.Add(eventId)) return Results.Ok();   // same change, already handled
```

### Nested forms, reference expansion, files by id (gateway campaign `audit/schema-content`, 2026-10-07)

> Ships with the gateway campaign branch `audit/schema-content`; the methods
> below answer only once that branch is on the gateway you call.

**A schema read is typed.** `GetDatabaseSchemaAsync` (Api and Hub) returns every
field as its own class — the gateway writes a `$fieldType` discriminator and
this SDK reads it:

```csharp
var schema = await client.Database.GetDatabaseSchemaAsync(new GetDatabaseSchemaRequest { Id = "sch_orders" });
foreach (var field in schema!.Item!.DataSchema.Fields)
{
    switch (field)
    {
        case StringFieldDto s:   Console.WriteLine($"{s.FieldName}: text, default {s.Default}, unique {s.Unique}"); break;
        case ObjectFieldDto o:   Console.WriteLine($"{o.FieldName}: nested form with {o.Properties.Count} members, required {string.Join(",", o.Required ?? [])}"); break;
        case ArrayFieldDto a:    Console.WriteLine($"{a.FieldName}: list of {a.Items.GetType().Name}, {a.MinItems}..{a.MaxItems}"); break;
        case JsonFieldDto j:     Console.WriteLine($"{j.FieldName}: free JSON object, max {j.MaxBytes} bytes"); break;
        case UnknownSchemaFieldDto u: Console.WriteLine($"{u.FieldName}: kind {u.FieldType} this SDK does not know yet"); break;
    }
}
```

The kinds: `StringFieldDto`, `DecimalFieldDto`, `IntegerFieldDto`, `DateFieldDto`,
`BooleanFieldDto`, `CurrencyFieldDto` (`MultipleOf`, `Minimum`, `Maximum`,
`Default: CurrencyDefaultDto { Value, Currency }`), `GeolocationFieldDto`,
`TagsFieldDto` (`MinItems`, `MaxItems`, `Default`), `FileFieldDto` (`MinItems`,
`MaxItems`, `AllowedFileType`, `MaxSizeMb`), `EnumSelectionFieldDto`,
`TaxonomySelectionFieldDto` / `CollectionSelectionFieldDto` /
`UserSelectionFieldDto` / `RoleSelectionFieldDto` (each with `DisplayField`,
the target property shown instead of the id), and the three new ones:
`ObjectFieldDto` (a nested form: `Properties`, `Required`), `ArrayFieldDto`
(`Items` of any kind, `MinItems`, `MaxItems`, `UniqueItems`) and `JsonFieldDto`
(a free-form object, `MaxBytes`). `Default` and `Unique` exist on the primitive
kinds. A kind this SDK does not know yet arrives as `UnknownSchemaFieldDto`
(name + raw `FieldType`), never as an exception. Nesting is recursive:
`ArrayFieldDto.Items` can be an `ObjectFieldDto` whose `Properties` hold another
`ArrayFieldDto`.

**Nested documents.** Records are extended-JSON strings, so a nested form or a
list of objects is just the JSON you write. Reads, filters, sorts and updates
reach into it with dotted paths:

```csharp
// insert a record with a nested form, a list of objects and a free JSON object
await client.Database.InsertOneAsync(new InsertOneRequest
{
    CollectionName = "orders",
    Document = """{"customer":"rec_7","address":{"city":"Vilnius"},"lines":[{"sku":"A-1","qty":2}],"settings":{"theme":"dark"}}""",
});

// filter on a nested member or inside a list; sort on a nested member (not on or through a list)
await client.Database.FindAsync(new FindRequest
{
    CollectionName = "orders",
    Filter = """{"address.city":"Vilnius","lines":{"$elemMatch":{"sku":"A-1","qty":{"$gte":2}}}}""",
    SortBy = "address.city",
});

// update a nested member, one element, every element, or the elements a filter matches
await client.Database.UpdateOneAsync(new UpdateOneRequest
{
    CollectionName = "orders",
    Id = "ord_1",
    Update = """{"address.city":"Kaunas","lines.0.qty":3,"lines.$[].checked":true,"lines.$[line].qty":4}""",
    ArrayFilters = """[{"line.sku":"A-1"}]""",   // one filter per $[name] in the update
});
```

`ArrayFilters` (new on `UpdateOneRequest` / `UpdateManyRequest`, Hub
`UpdateOneRecord` / `UpdateManyRecords`) is MongoDB's `arrayFilters`: a JSON
array with one filter document per `$[name]` identifier used in the update.
`$and` / `$or` / `$nor` are allowed inside a filter. A sort on a list or through
one (`lines.qty`, `tags.0`) is refused — a list cannot be paged on.

**Reference expansion.** A reference field (user, role, taxonomy term,
collection record, file) stores an id. Set `ExpandReferences = true` on
`FindAsync`, `FindOneAsync`, `FindOwnAsync` (Api) or `FindRecordsAsync`,
`FindOneRecordAsync` (Hub) and every reference comes back as `{ id, display }`,
where `display` is the target's display field per the schema (a user's e-mail,
a term's title or slug, a role's name, a record's named field, a file's path)
and `null` when the target is gone. `ReferenceValue` reads it out of the
record:

```csharp
var page = await client.Database.FindAsync(new FindRequest { CollectionName = "orders", ExpandReferences = true });
var record = (JsonElement)page!.List!.Items[0];

var customer = ReferenceValue.Read(record, "customer");            // { Id = "rec_7", Display = "Ann Example" }
var roles = ReferenceValue.ReadMany(record.GetProperty("visibleTo")); // a `multiple` field
var product = ReferenceValue.Read(record, "lines.0.product");      // inside a list of nested forms
```

Expansion needs read permission on every source the schema links to; a
caller without it is refused with `CM-ERRORS-DATABASE-056` (the message names
the source and the fields). Without the flag the read returns the stored ids,
as before. A `display` that is not a string (a term whose title is a language
map) lands in `ReferenceValue.RawDisplay`.

**A file by its stored id.** A file field stores file ids (`nbfl_…`, or the
bare UUID). Read the file behind an id without knowing its path:

```csharp
// Api: GET /{version}/files/{filesIntegrationId}/by-id/{id}
var file = await client.Files.GetFileByIdAsync(new GetFileByIdRequest { FilesIntegrationId = integrationId, Id = "nbfl_…" });
// Hub: GET /{version}/files/item/by-id?filesIntegrationId=…&id=…
var same = await hub.Files.GetFileByIdAsync(new GetFileById { FilesIntegrationId = integrationId, Id = "nbfl_…" });
Console.WriteLine($"{file!.File!.Path} public={file.IsPublic} url={file.PublicUrl}");
```

File ids are stable now (the same id on every listing, `info`, upload commit and
by-id read).

**Terms carry a slug.** `TermDto.Slug` / `TermTreeDto.Slug` is the URL-safe
name (`"Côte d'Ivoire"` → `cote-d-ivoire`), unique inside the taxonomy. The
server derives it from `name` on every save; a save may send an explicit
`"slug"` in the document (Hub `SaveDatabaseTaxonomyTermRequest.Document`,
`UpdateDatabaseTaxonomyTermRequest.Update`). A derived slug another term has
gets a `-2`, `-3`… suffix; an explicit one another term has is refused
(`CM-ERRORS-TAXONOMIES-012`). A taxonomy reference with `DisplayField = "slug"`
expands to the slug.

**What else changed for callers:**

- A record is checked against every rule of its schema on write, at any depth:
  `format` (email / uri), `pattern`, `multipleOf`, `minimum` / `maximum`,
  enum values, geolocation coordinates, `minItems` / `maxItems` on tags, files
  and lists, repeated entries where `uniqueItems`, the `{ value, currency }`
  shape, and — new — a nested form is closed (an undeclared member is refused),
  while the root stays open. The error names the full path
  (`customer.address.zip`, `lines[1].qty`).
- Every referenced user / role / term / record / file must exist in the
  declared target on write (`CM-ERRORS-DATABASE-050` … `-055`). A role
  reference stores the role **id**; a role name is refused (`-051`).
- A translatable string must be a `{ language: text }` object; a plain string
  is refused (`CM-ERRORS-DATABASE-049`).
- `SortBy` accepts a dotted path through nested forms (`address.city`); an
  array position or a path on / through a list is refused.
- Two update keys that overlap (`address` and `address.city`) are refused
  (`CM-ERRORS-DATABASE-014`) instead of failing in MongoDB.

**Error codes you may now see** (on `NorbixException.ErrorCode`; record errors
carry `FieldName` = the full path and `Keyword` in their metadata):

| Code | Keyword | When |
| --- | --- | --- |
| `CM-ERRORS-DATABASE-014` | — | `ArrayFilters` malformed or not paired with the `$[name]` identifiers of the update; two update paths overlap. Wrapped in `CM-ERRORS-PROPERTY-002` on the property. |
| `CM-ERRORS-DATABASE-039` | `type` | Wrong JSON type: a scalar on a list, a list on a single value, a non-object on a nested form / currency / geolocation / JSON field, an empty reference id. |
| `CM-ERRORS-DATABASE-040` | `length` | `minLength` / `maxLength`; `minItems` / `maxItems` on tags, files and lists; a JSON field above `maxBytes` (or a member write into a capped JSON field). |
| `CM-ERRORS-DATABASE-041` | `pattern` | The string does not match `pattern`. |
| `CM-ERRORS-DATABASE-042` | `format` | `format` email / uri; a file id that is not a UUID / `nbfl_…`; a currency code that is not three upper-case letters. |
| `CM-ERRORS-DATABASE-043` | `range` | `minimum` / `maximum` on integer, decimal, date and the currency amount. |
| `CM-ERRORS-DATABASE-044` | `multipleOf` | Decimal or currency amount not on the step. |
| `CM-ERRORS-DATABASE-045` | `enum` | Value outside the enum values, the allowed currencies, the allowed geometry types. |
| `CM-ERRORS-DATABASE-046` | `uniqueItems` | A repeated entry in tags, a multiple enum, file ids, multiple references, a list with `uniqueItems`. |
| `CM-ERRORS-DATABASE-047` | `properties` | A nested form, currency or geolocation object with an unknown member or a missing one. |
| `CM-ERRORS-DATABASE-048` | `coordinates` | Not a `[longitude, latitude]` pair (or list of pairs); longitude outside -180..180, latitude outside -90..90. |
| `CM-ERRORS-DATABASE-049` | `translateOptions` | A translatable string that is not a `{ language: text }` object. |
| `CM-ERRORS-DATABASE-050` … `-054` | `reference` | A referenced user (050), role (051), term (052), record (053) or file (054) does not exist in the declared target. `MissingId` in the metadata. |
| `CM-ERRORS-DATABASE-055` | `reference` | The declared target itself cannot be read (unknown taxonomy, collection without a repository, files integration that cannot open). |
| `CM-ERRORS-DATABASE-056` | — | `ExpandReferences = true` without read permission on a linked source (`SourceKind`, `Source`, `Fields`, `MissingPermissions` in the metadata). |
| `CM-ERRORS-SCHEMA-036` | — | A schema nests deeper than 5 levels. |
| `CM-ERRORS-SCHEMA-037` | — | A field `default` breaks the field's own rules. |
| `CM-ERRORS-SCHEMA-038` | — | A nested `required` names a member the form does not declare. |
| `CM-ERRORS-SCHEMA-039` | — | A collection reference's `displayField` is not a field of the target schema. |
| `CM-ERRORS-SCHEMA-040` | — | A draft or rename would remove a field another schema's collection reference shows (`displayField`). |
| `CM-ERRORS-SCHEMA-041` | — | A schema delete while another schema's collection reference points at it. |
| `CM-ERRORS-TAXONOMIES-012` | — | An explicit term slug another term of the taxonomy has. |
| `CM-ERRORS-TAXONOMIES-013` | — | An explicit term slug with no letter or digit left after slugifying. |


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
| File upload/download bytes | Out of scope for the generated endpoint client; file metadata flows through normal DTOs. A file that arrives as raw bytes (download, public link) is returned as `byte[]`. |
| Server Events / SSE | Requires a hand-written streaming module once the public stream contract is finalized. |

## Public File Links

A file, or a whole folder, can be made readable by anyone holding a link — no
sign-in, no project id, no account. Norbix keeps a record and mints an
unguessable id that looks like `nbpf_7hK2…`; the link is then
`https://<your api host>/v3/files/public/nbpf_7hK2…/invoice.pdf`.

```csharp
// Publishing is a dashboard operation, on the Hub side.
var published = await hub.Files.MakeFilePublicAsync(new MakeFilePublicRequest
{
    FilesIntegrationId = integrationId,
    Path = "invoices/invoice.pdf",
});
string publicId = published!.Id!;          // "nbpf_7hK2abc"

// Reading the link is on the API side — and carries no session at all.
byte[]? pdf = await api.Files.GetPublicFileAsync(new GetPublicFileRequest
{
    PublicId = publicId,
    Name = "invoice.pdf",
});

// Take it back:
await hub.Files.MakeFilePrivateAsync(new MakeFilePrivateRequest
{
    FilesIntegrationId = integrationId,
    Path = "invoices/invoice.pdf",
});
```

`GetPublicFileAsync` is the SDK's first endpoint outside `/auth` that sends
**no** `Authorization` header, whatever the client is holding — its request
carries `INorbixUnauthenticated`. That is what public means: the link has to
work in an e-mail or in a browser on a stranger's phone, and the unguessable id
in the URL is the whole credential. It works on a client with no API key at
all.

Folders work the same way through `MakeFolderPublicAsync` /
`MakeFolderPrivateAsync`. A published folder is **one** record however many
files sit under it, and a file inside it is read with the path inside the
folder as `Name` (`"2026/q1/report.pdf"` — the slashes stay slashes).

Four rules worth knowing:

- Asking twice gives the same id back — the first link is already in somebody's
  hands.
- A file cannot be made private on its own while a folder above it is public
  (`CM-ERRORS-FILES-021`); switch the folder off instead.
- The root cannot be published, and a folder link with nothing after it is a
  `404` — publishing a prefix must not publish its listing.
- Every miss is the same plain `404`: unknown id, wrong name, made private
  again, gone from storage. A more precise answer would tell a stranger that
  the file is there.

After publishing, `ListFilesAsync` and `GetFileInfoAsync` report `IsPublic` and
`PublicUrl` on each file, and a listing carries `PublicFolders`.

## Testing a Files Integration

`TestFilesIntegrationAsync` on the API client runs a live probe against a files
integration — it uploads a small file, reads it, lists the folder and deletes
the file again — and answers one result per step. It needs the `files:create`
permission, because the probe writes to the storage.

```csharp
var probe = await api.Files.TestFilesIntegrationAsync(new TestFilesIntegrationRequest
{
    FilesIntegrationId = integrationId,   // POST /{version}/files/{filesIntegrationId}/test
});

foreach (var step in probe!.Items!)
    Console.WriteLine($"{step.Operation}: {step.Result} {string.Join("; ", step.Errors ?? [])}");
```

The steps are `UploadFile`, `GetFile`, `GetAllFiles` and `DeleteFile`, in that
order. A step that fails does not throw — it comes back with `Result = "FAILED"`
and its `Errors`, and the steps after it come back `"NOT_TESTED"`. Only a refused request (unknown integration, missing permission)
throws `NorbixException`. The Hub client keeps its own
`TestFilesIntegrationAsync` (`POST /{version}/files/integrations/test`, id in
the body) for the dashboard.

## Error Handling

```csharp
try
{
    await client.Database.FindAsync(new FindRequest { CollectionName = "orders" });
}
catch (NorbixException ex)
{
    // HttpStatus / ErrorCode / Errors are the names every Norbix SDK uses.
    // StatusCode / Code / FieldErrors are the same values, kept for older code.
    Console.WriteLine($"{ex.HttpStatus} {ex.ErrorCode}: {ex.Message}");

    foreach (var error in ex.Errors)
    {
        Console.WriteLine($"{error.ErrorCode} {error.FieldName}: {error.Message}");
    }
}
```

`Message` and `ErrorCode` are the gateway's own. The gateway puts them inside
`responseStatus.errors[]`, so `NorbixException` reads that list first, takes the
first entry for `Message` / `ErrorCode`, and keeps every entry in `Errors`. Only
when the body has no `responseStatus` are the top-level `message` and
`errorCode` read. `Request failed (HTTP <status>)` is the last fallback, used
when the body says nothing (for example a 500 page that is not JSON).

| Code | Meaning |
| --- | --- |
| `NORBIX_NOT_AUTHENTICATED` | No `ApiKey`, `BearerToken`, or env var, and `LoginAsync` was not called. |
| `NORBIX_ACCOUNT_SCOPE_REQUIRED` | Account-scoped endpoint called without `AccountId`. |
| `NORBIX_MISSING_PATH_PARAM` | A `{token}` in the route was not provided on the request DTO. |
| `NORBIX_NETWORK_ERROR` | HTTP failed (timeout, connection reset, DNS). |

### Breaking change — a refused call now throws

The gateway answers a business refusal (an unknown id, a rule that says no)
with **HTTP 200** and `responseStatus.isSuccess = false`. The SDK used to hand
that answer back as a normal value, so code carried on as if the call had
worked. It now throws a `NorbixException` with `HttpStatus = 200` and the
gateway's message and error code.

If your code checked `res.ResponseStatus.IsSuccess` itself, move that check into
a `try / catch`. Endpoints that answer with raw bytes rather than a document
(file download, the public file link) are not JSON and are unchanged.

## How It Stays in Sync With the Backend

The source of truth is `src/Norbix.Sdk.Types/Generated/Api.dtos.cs` and `src/Norbix.Sdk.Types/Generated/Hub.dtos.cs`. They are regenerated from a running gateway with `python3 scripts/sync-types.py --api <api url> --hub <hub url>` (see [CONTRIBUTING.md](./CONTRIBUTING.md#regenerate-the-dto-files)); a non-empty `git diff` after a run is a real contract change.

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
