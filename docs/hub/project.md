# Project — .NET

[← Back to project README](../../README.md)

Every Project-module call of the .NET SDK: the project itself, its settings,
CORS, languages, regions, admin URL, legal documents, the Admin Portal, the
project AI settings, LLM and MCP integrations, AI service users, the developer
MCP endpoint, and the public (no sign-in) config and legal reads.

The methods are generated at build time from the route attributes in
`src/Norbix.Sdk.Types/Generated/Hub.dtos.cs` (method name = request class name
without `Request`, plus `Async`), so the tables below match the shipped surface.
Each one is covered by a snapshot in
`tests/Norbix.Hub.Tests/test_results/EndpointCoverageTests.Hub.Account.verified.txt`
(`Hub.Ai` and `Api.Public` for the AI and public calls): the exact request it
sends and the reply it reads.

Most calls are on the Hub client (`Norbix.Hub` package). Calls with
`{projectId}` in the path need `ProjectId` **on the request** — the client
option only sets the `X-CM-ProjectId` header, and a missing path value throws
`NORBIX_MISSING_PATH_PARAM` before anything is sent.

```csharp
using Norbix.Sdk;
using Norbix.Sdk.Types.Hub;

using var norbix = new NorbixHubClient(new NorbixClientOptions
{
    ApiKey = "<hub_api_key>",
    ProjectId = "proj_123",
});

const string projectId = "proj_123";
```

All examples below use `norbix` and `projectId` from this block.

## Project

| method | verb | path |
|---|---|---|
| `GetProjectsAsync` | `GET` | `/account/projects` |
| `GetProjectAsync` | `GET` | `/account/projects/{projectId}` |
| `CreateProjectAsync` | `POST` | `/account/projects` |
| `WaitForProjectActiveAsync` | `GET` | `/account/projects/{projectId}/wait-active` |
| `EnableProjectAsync` | `PATCH` | `/account/projects/{projectId}/enable` |
| `DisableProjectAsync` | `PATCH` | `/account/projects/{projectId}/disable` |
| `DeleteProjectAsync` | `DELETE` | `/account/projects/{projectId}` |
| `GetProjectTokensAsync` | `GET` | `/account/projects/{projectId}/tokens` |

```csharp
var projects = await norbix.Account.GetProjectsAsync(new GetProjects());

var project = await norbix.Account.GetProjectAsync(new GetProject { ProjectId = projectId });
Console.WriteLine(project!.Item!.Name);

await norbix.Account.DisableProjectAsync(new DisableProject { ProjectId = projectId });
await norbix.Account.EnableProjectAsync(new EnableProject { ProjectId = projectId });

// Key/value token mappings of the project.
var tokens = await norbix.Account.GetProjectTokensAsync(new GetProjectTokens { ProjectId = projectId });
```

Creating a project and regions are shown in the README
([Regions](../../README.md#regions)).

## Name, description, URL, logo, icon, colors

| method | verb | path |
|---|---|---|
| `UpdateProjectNameAsync` | `PATCH` | `/account/projects/{projectId}/settings/name` |
| `UpdateProjectDescriptionAsync` | `PATCH` | `/account/projects/{projectId}/settings/description` |
| `UpdateProjectUrlAsync` | `PATCH` | `/account/projects/{projectId}/settings/url` |
| `UpdateProjectLogoAsync` | `PATCH` | `/account/projects/{projectId}/settings/logo` |
| `UpdateProjectIconAsync` | `PATCH` | `/account/projects/{projectId}/settings/icon` |
| `UpdateProjectMainColorAsync` | `PATCH` | `/account/projects/{projectId}/settings/main-color` |
| `UpdateProjectAccentColorAsync` | `PATCH` | `/account/projects/{projectId}/settings/accent-color` |

```csharp
await norbix.Account.UpdateProjectNameAsync(new UpdateProjectName { ProjectId = projectId, Name = "Shop" });
await norbix.Account.UpdateProjectDescriptionAsync(
    new UpdateProjectDescription { ProjectId = projectId, Description = "Online shop" });
await norbix.Account.UpdateProjectUrlAsync(
    new UpdateProjectUrl { ProjectId = projectId, Url = "https://shop.example.com" });

// Logo and icon point at a file already uploaded to a Files integration.
await norbix.Account.UpdateProjectLogoAsync(new UpdateProjectLogo
{
    ProjectId = projectId,
    FileResource = new FileResourceRefDto
    {
        IntegrationId = "files_integration_id",
        Provider = FileProvider.AwsS3,
        Path = "branding/logo.png",
        Resource = new FileResourceDto { Id = "file_id" },
    },
});
// UpdateProjectIconAsync(new UpdateProjectIcon { ProjectId = projectId, FileResource = … }) has the same shape.

await norbix.Account.UpdateProjectMainColorAsync(new UpdateProjectMainColor { ProjectId = projectId, Color = "#1E40AF" });
await norbix.Account.UpdateProjectAccentColorAsync(new UpdateProjectAccentColor { ProjectId = projectId, Color = "#F59E0B" });
```

## CORS — allowed origins

| method | verb | path |
|---|---|---|
| `UpdateProjectAllowedOriginsAsync` | `PATCH` | `/account/projects/{projectId}/settings/origins` |

The list **replaces** the old one. An entry with no scheme gets `https`. The
project's own Admin Portal origin is kept: a list without it is refused with
`CM-ERRORS-PROJECTS-037` unless `RemoveAdminPortalOrigin = true`.

```csharp
await norbix.Account.UpdateProjectAllowedOriginsAsync(new UpdateProjectAllowedOrigins
{
    ProjectId = projectId,
    Origins = ["https://shop.example.com", "https://admin.example.com"],
});
```

The current list is `ProjectDto.AllowedOrigins` (from `GetProjectAsync`).

## Languages

| method | verb | path |
|---|---|---|
| `UpdateProjectLanguagesAsync` | `PATCH` | `/account/projects/{projectId}/settings/languages` |
| `UpdateProjectDefaultLanguageAsync` | `PATCH` | `/account/projects/{projectId}/settings/default-language` |

```csharp
await norbix.Account.UpdateProjectLanguagesAsync(
    new UpdateProjectLanguages { ProjectId = projectId, Languages = ["en", "lt", "de"] });
await norbix.Account.UpdateProjectDefaultLanguageAsync(
    new UpdateProjectDefaultLanguage { ProjectId = projectId, DefaultLanguage = "en" });
```

## Regions

| method | verb | path |
|---|---|---|
| `GetAccountRegionsAsync` | `GET` | `/account/regions` (no token, no `AccountId`: sent with no `Authorization` header) |
| `UpdateProjectRegionsAsync` | `PATCH` | `/account/projects/{projectId}/settings/regions` |

```csharp
var regions = await norbix.Account.GetAccountRegionsAsync(new GetAccountRegions());

await norbix.Account.UpdateProjectRegionsAsync(new UpdateProjectRegions
{
    ProjectId = projectId,
    PrimaryRegion = "nb-eu-germany",
    AdditionalRegions = ["nb-us-east"],
});
```

## Admin URL

| method | verb | path |
|---|---|---|
| `UpdateProjectAdminUrlAsync` | `PATCH` | `/account/projects/{projectId}/settings/admin-url` |

Your own address for the project's Admin Portal. `null` goes back to the
Norbix one. `ProjectDto.EffectiveAdminUrl` is the one in use.

```csharp
await norbix.Account.UpdateProjectAdminUrlAsync(
    new UpdateProjectAdminUrl { ProjectId = projectId, Url = "https://admin.shop.example.com" });
```

## Legal documents

| method | verb | path |
|---|---|---|
| `UpdateProjectLegalDocumentsAsync` | `PATCH` | `/account/projects/{projectId}/settings/legal` |
| `UpdateProjectExposeLegalAsync` | `PATCH` | `/account/projects/{projectId}/settings/legal/expose` |

Terms and privacy are Markdown. They are public (see
[Public config and legal](#public-config-and-legal-no-sign-in)) only after
`Exposed = true`.

```csharp
await norbix.Account.UpdateProjectLegalDocumentsAsync(new UpdateProjectLegalDocuments
{
    ProjectId = projectId,
    TermsMarkdown = "# Terms\n\n…",
    PrivacyMarkdown = "# Privacy\n\n…",
});
await norbix.Account.UpdateProjectExposeLegalAsync(
    new UpdateProjectExposeLegal { ProjectId = projectId, Exposed = true });
```

## What the public Admin Portal config shows

| method | verb | path |
|---|---|---|
| `UpdateProjectExposeBrandAsync` | `PATCH` | `/account/projects/{projectId}/settings/brand/expose` |
| `UpdateProjectExposeAuthAsync` | `PATCH` | `/account/projects/{projectId}/settings/auth/expose` |

The public config ([Public config and legal](#public-config-and-legal-no-sign-in))
returns the brand (name, colors, logo, icon) by default and hides the sign-in
methods (email / phone / username) and the password policy. Social providers and
the passkey yes/no are always returned. Two switches change that:

```csharp
// Hide the brand from the public config.
await norbix.Account.UpdateProjectExposeBrandAsync(
    new UpdateProjectExposeBrand { ProjectId = projectId, Exposed = false });
// Show the sign-in methods and the password policy.
await norbix.Account.UpdateProjectExposeAuthAsync(
    new UpdateProjectExposeAuth { ProjectId = projectId, Exposed = true });
```

## Admin Portal

| method | verb | path |
|---|---|---|
| `SetAdminPortalEnabledAsync` | `PUT` | `/account/projects/{projectId}/admin-portal/enabled` |
| `GetAdminPortalStructureAsync` | `GET` | `/account/projects/{projectId}/admin-portal/structure` |
| `AssignAdminPortalServiceUserAsync` | `PUT` | `/account/projects/{projectId}/settings/admin-portal/service-user` |

```csharp
await norbix.Account.SetAdminPortalEnabledAsync(
    new SetAdminPortalEnabledRequest { ProjectId = projectId, Enabled = true });

var structure = await norbix.Account.GetAdminPortalStructureAsync(
    new GetAdminPortalStructure { ProjectId = projectId });
foreach (var module in structure!.Modules)
    Console.WriteLine($"{module.Key}: {module.Enabled}");

// The service user the Admin Portal acts as.
await norbix.Account.AssignAdminPortalServiceUserAsync(
    new AssignAdminPortalServiceUserRequest { ProjectId = projectId, ServiceUserId = "service_user_id" });
```

## AI settings, assistants, usage

| method | verb | path |
|---|---|---|
| `GetProjectAiSettingsAsync` | `GET` | `/account/projects/{projectId}/ai/settings` |
| `UpdateProjectAiSettingsAsync` | `PUT` | `/account/projects/{projectId}/ai/settings` |
| `CreateProjectAiAssistantAsync` | `POST` | `/account/projects/{projectId}/ai/assistants` |
| `UpdateProjectAiAssistantAsync` | `PUT` | `/account/projects/{projectId}/ai/assistants/{assistantId}` |
| `DeleteProjectAiAssistantAsync` | `DELETE` | `/account/projects/{projectId}/ai/assistants/{assistantId}` |
| `GetProjectAiUsageAsync` | `GET` | `/account/projects/{projectId}/ai/usage` |

```csharp
await norbix.Account.UpdateProjectAiSettingsAsync(new UpdateProjectAiSettings
{
    ProjectId = projectId,
    Enabled = true,
    DefaultLlmIntegrationId = "llm_integration_id",
    DefaultModel = "gpt-4o-mini",
});

var created = await norbix.Account.CreateProjectAiAssistantAsync(new CreateProjectAiAssistant
{
    ProjectId = projectId,
    Name = "Support",
    WelcomeMessage = "Hi! How can I help?",
    SystemPrompt = "You answer questions about orders.",
    MemoryEnabled = true,
    IsDefault = true,
});

await norbix.Account.UpdateProjectAiAssistantAsync(new UpdateProjectAiAssistant
{
    ProjectId = projectId,
    AssistantId = created!.Id!,
    Name = "Support",
    Model = "gpt-4o",
});
await norbix.Account.DeleteProjectAiAssistantAsync(
    new DeleteProjectAiAssistant { ProjectId = projectId, AssistantId = created.Id! });

var settings = await norbix.Account.GetProjectAiSettingsAsync(new GetProjectAiSettings { ProjectId = projectId });
var usage = await norbix.Account.GetProjectAiUsageAsync(new GetProjectAiUsage { ProjectId = projectId, Top = 10 });
Console.WriteLine(usage!.Result!.Period);
```

## LLM integrations

On `norbix.Ai`. The project id comes from the `X-CM-ProjectId` header (set
`ProjectId` in the client options); `ProjectId` on the request is optional here.

| method | verb | path |
|---|---|---|
| `SaveLlmIntegrationAsync` | `POST` | `/ai/integrations/llms/` |
| `TestLlmIntegrationAsync` | `POST` | `/ai/integrations/llms/test` |
| `GetLlmIntegrationsAsync` | `GET` | `/ai/integrations/llms/integrations` |
| `GetLlmIntegrationAsync` | `GET` | `/ai/integrations/llms/{id}` |
| `DeleteLlmIntegrationAsync` | `DELETE` | `/ai/integrations/llms/{Id}` |
| `EnableLlmIntegrationAsync` | `PUT` | `/ai/integrations/llms/{Id}/enable` |
| `DisableLlmIntegrationAsync` | `PUT` | `/ai/integrations/llms/{Id}/disable` |
| `SetLlmIntegrationAsDefaultAsync` | `PUT` | `/ai/integrations/llms/{Id}/default` |

```csharp
var llm = await norbix.Ai.SaveLlmIntegrationAsync(new SaveLlmIntegration
{
    Integration = new OpenAiLlmIntegrationRequest
    {
        Provider = LlmProvider.OpenAI,
        ApiKey = configuration["OpenAi:ApiKey"]!,
        IntegrationName = "openai",
        IsEnabled = true,
        DefaultModel = "gpt-4o-mini",
    },
});

var test = await norbix.Ai.TestLlmIntegrationAsync(new TestLlmIntegration { IntegrationId = llm!.Id! });
await norbix.Ai.SetLlmIntegrationAsDefaultAsync(new SetLlmIntegrationAsDefaultRequest { Id = llm.Id! });

var llms = await norbix.Ai.GetLlmIntegrationsAsync(new GetLlmIntegrations());
var one = await norbix.Ai.GetLlmIntegrationAsync(new GetLlmIntegration { Id = llm.Id! });

await norbix.Ai.DisableLlmIntegrationAsync(new DisableLlmIntegrationRequest { Id = llm.Id! });
await norbix.Ai.EnableLlmIntegrationAsync(new EnableLlmIntegrationRequest { Id = llm.Id! });
await norbix.Ai.DeleteLlmIntegrationAsync(new DeleteLlmIntegrationRequest { Id = llm.Id! });
```

`Integration` takes the provider's own request type (`OpenAiLlmIntegrationRequest`,
`AnthropicLlmIntegrationRequest`, `OllamaLlmIntegrationRequest`, …); the ones
with a key implement `ILlmApiKeyRequest`. Read the key from configuration, not
from source code.

## MCP integrations

MCP servers the project's assistants may call. Same shape as LLM integrations;
`Integration` takes the server's own request type (`GitHubMcpIntegrationRequest`,
`StripeMcpIntegrationRequest`, `PlaywrightMcpIntegrationRequest`, …).

| method | verb | path |
|---|---|---|
| `SaveMcpIntegrationAsync` | `POST` | `/ai/integrations/mcp/` |
| `TestMcpIntegrationAsync` | `POST` | `/ai/integrations/mcp/test` |
| `GetMcpIntegrationsAsync` | `GET` | `/ai/integrations/mcp/integrations` |
| `GetMcpIntegrationAsync` | `GET` | `/ai/integrations/mcp/{id}` |
| `DeleteMcpIntegrationAsync` | `DELETE` | `/ai/integrations/mcp/{Id}` |
| `EnableMcpIntegrationAsync` | `PUT` | `/ai/integrations/mcp/{Id}/enable` |
| `DisableMcpIntegrationAsync` | `PUT` | `/ai/integrations/mcp/{Id}/disable` |

```csharp
var mcp = await norbix.Ai.SaveMcpIntegrationAsync(new SaveMcpIntegration
{
    Integration = new GitHubMcpIntegrationRequest
    {
        Provider = McpProvider.GitHub,
        Transport = McpTransport.HttpStream,
        ServerUrl = "https://api.githubcopilot.com/mcp/",
        AccessToken = configuration["GitHub:Token"]!,
        IntegrationName = "github",
        Name = "GitHub",
        Category = "developer",
        Description = "Issues and pull requests",
        Icon = "github",
        IsEnabled = true,
    },
});

await norbix.Ai.TestMcpIntegrationAsync(new TestMcpIntegration { IntegrationId = mcp!.Id! });
var servers = await norbix.Ai.GetMcpIntegrationsAsync(new GetMcpIntegrations());
await norbix.Ai.DisableMcpIntegrationAsync(new DisableMcpIntegrationRequest { Id = mcp.Id! });
await norbix.Ai.DeleteMcpIntegrationAsync(new DeleteMcpIntegrationRequest { Id = mcp.Id! });
```

## AI service users

Keys for AI agents (Claude Code, Cursor, …) that talk to the developer MCP
endpoint. Account-level: no `ProjectId` on the request; the scope says which
project and which environments a key may touch.

| method | verb | path |
|---|---|---|
| `CreateAiServiceUserAsync` | `POST` | `/account/ai/service-users` |
| `ListAiServiceUsersAsync` | `GET` | `/account/ai/service-users` |
| `RotateAiServiceUserKeyAsync` | `POST` | `/account/ai/service-users/{Id}/keys` |
| `RevokeAiServiceUserKeyAsync` | `DELETE` | `/account/ai/service-users/{Id}/keys/{KeyId}` |
| `DeleteAiServiceUserAsync` | `DELETE` | `/account/ai/service-users/{Id}` |

```csharp
var agent = await norbix.Account.CreateAiServiceUserAsync(new CreateAiServiceUserRequest
{
    Name = "Claude Code on my laptop",
    Scope = new AiScopeDto { Reach = "project", ProjectId = projectId, Rights = "admin", Envs = ["TEST"] },
});
// agent.Key is shown once. Store it now; it cannot be read back.

var rotated = await norbix.Account.RotateAiServiceUserKeyAsync(
    new RotateAiServiceUserKeyRequest { Id = agent!.Id, RevokeKeyId = agent.KeyId });

var agents = await norbix.Account.ListAiServiceUsersAsync(new ListAiServiceUsersRequest());

await norbix.Account.RevokeAiServiceUserKeyAsync(
    new RevokeAiServiceUserKeyRequest { Id = agent.Id, KeyId = rotated!.KeyId });
await norbix.Account.DeleteAiServiceUserAsync(new DeleteAiServiceUserRequest { Id = agent.Id });
```

`Reach` is `account` or `project` (then set `ProjectId`), `Rights` is `read`
or `admin`, `Envs` is `["TEST"]` or `["TEST", "PROD"]`.

## Developer MCP endpoint

| method | verb | path |
|---|---|---|
| `McpAsync` | `POST` (also `GET`, `DELETE`) | `/account/mcp` |

This is an MCP server (Streamable HTTP: JSON-RPC 2.0 over `POST`, a server
stream over `GET`, session end over `DELETE`). Point an MCP client at
`<hub base URL>/v2/account/mcp` with an AI service user key as the bearer
token — that is the supported way to use it.

`McpAsync` exists because the route exists, but it is not an MCP client: it
sends only `toolsets` as JSON and returns the raw reply text. It cannot send a
JSON-RPC message (the request's `RequestStream` is not serialized).

```csharp
// Not an MCP session — use an MCP client for real work.
string? raw = await norbix.Account.McpAsync(new McpRequest { Toolsets = "database" });
```

## Public config and legal (no sign-in)

Read by the project's Admin Portal before anyone signs in; the gateway does
not ask for a key. In an app use the `Norbix.Api` package.

The .NET SDK does not mark these two requests as public yet, so the client
still needs credentials (an API key or a signed-in user) or it throws
`NORBIX_NOT_AUTHENTICATED` before sending.

| method | verb | path |
|---|---|---|
| `GetPublicProjectConfigAsync` | `GET` | `/public/projects/{ProjectId}/config` |
| `GetPublicProjectLegalAsync` | `GET` | `/public/projects/{ProjectId}/legal/{Kind}` |

```csharp
using Norbix.Sdk;
using Norbix.Sdk.Types.Api;

using var app = new NorbixApiClient(new NorbixClientOptions { ApiKey = "<api_key>", ProjectId = "proj_123" });

var config = await app.Public.GetPublicProjectConfigAsync(
    new GetPublicProjectConfig { ProjectId = "proj_123" });
Console.WriteLine($"{config!.DisplayName}, portal on: {config.AdminPortalEnabled}");

var terms = await app.Public.GetPublicProjectLegalAsync(
    new GetPublicProjectLegal { ProjectId = "proj_123", Kind = "terms" });
if (terms!.Available)
    Console.WriteLine(terms.Body);
```

`Kind` is `terms` or `privacy`. `Available` is `false` until the project
exposes its legal documents (`UpdateProjectExposeLegalAsync`). The `Norbix.Hub`
package has the same two calls on `norbix.Public`.
