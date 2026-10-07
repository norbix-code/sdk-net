# Using `Norbix.Api` / `Norbix.Hub` with ASP.NET Core

[← Back to project README](../../README.md)

## Quick wire-up

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNorbixApi(builder.Configuration); // reads "Norbix" section
                                                     // + NORBIX_* env vars
builder.Services.AddNorbixHub(builder.Configuration); // only if you use Norbix.Hub too
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
```

```jsonc
// appsettings.json
{
  "Norbix": {
    "ProjectId": "proj_123",
    "ApiKey": "sk_live_...",
    "ApiBaseUrl": "https://api.norbix.ai",
    "HubBaseUrl": "https://hub.norbix.ai"
  }
}
```

`AddNorbixApi(...)` registers `NorbixApiClient` as a **singleton** (`AddNorbixHub(...)` does the same for `NorbixHubClient`). Use `AddNorbixApiScoped(...)` / `AddNorbixHubScoped(...)` for a scoped client. Inject it everywhere:

```csharp
[ApiController, Route("orders")]
public sealed class OrdersController(NorbixApiClient norbix) : ControllerBase
{
    [HttpGet]
    public Task<object?> Index(CancellationToken ct)
        => norbix.Database.FindAsync(new() { CollectionName = "orders" }, ct);
}
```

## Per-user requests (acting on behalf of an end user)

If your API forwards a logged-in user's JWT to Norbix, construct a scoped client with that token:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(sp =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
    var jwt = http.Request.Headers.Authorization
        .ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

    return new NorbixApiClient(new NorbixClientOptions
    {
        ProjectId   = "proj_123",
        BearerToken = jwt,
    });
});
```

Now every request handler gets a `NorbixApiClient` already authenticated as the calling user.

## Health checks

```csharp
builder.Services.AddNorbixApi(builder.Configuration);
builder.Services.AddNorbixApiHealthChecks(ping: true);   // health check "norbix-api"
// Norbix.Hub: AddNorbixHub(...) + AddNorbixHubHealthChecks(...) ("norbix-hub")
```

## Common gotchas

- **DI lifetime mismatch**: don't capture the singleton in a scoped service expecting per-request JWTs. Use the scoped factory pattern above.
- **`NORBIX_*` env vars in containers**: pass them through your orchestration; the SDK reads via `Environment.GetEnvironmentVariable`.
- **Logging**: the transport emits optional `ILogger` diagnostics (no auth/header logging). Configure `ILogger` levels in your app as usual.
