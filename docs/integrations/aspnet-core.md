# Using `Norbix.Sdk` with ASP.NET Core

[← Back to project README](../../README.md)

## Quick wire-up

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNorbix(builder.Configuration);   // reads "Norbix" section
                                                     // + NORBIX_* env vars
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
    "ApiBaseUrl": "https://api.norbix.dev",
    "HubBaseUrl": "https://hub.norbix.dev"
  }
}
```

The SDK is registered as a singleton because it's stateless from the consumer's perspective. Inject it everywhere:

```csharp
[ApiController, Route("orders")]
public sealed class OrdersController(NorbixClient norbix) : ControllerBase
{
    [HttpGet]
    public Task<object?> Index(CancellationToken ct)
        => norbix.Api.Database.FindAsync(new() { CollectionName = "orders" }, ct);
}
```

## Per-user requests (acting on behalf of an end user)

If your API forwards a logged-in user's JWT to Norbix, register the SDK as **scoped** instead of singleton, and clone the JWT off the incoming request:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(sp =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
    var jwt = http.Request.Headers.Authorization
        .ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

    return new NorbixClient(new NorbixClientOptions
    {
        ProjectId   = "proj_123",
        BearerToken = jwt,
    });
});
```

Now every request handler gets a `NorbixClient` already authenticated as the calling user.

## Health checks

```csharp
builder.Services.AddHealthChecks()
    .AddAsyncCheck("norbix", async () =>
    {
        try
        {
            var client = sp.GetRequiredService<NorbixClient>();
            await client.Api.Echo.EchoAsync(new() { Message = "hc" });
            return HealthCheckResult.Healthy();
        }
        catch (NorbixException ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    });
```

## Common gotchas

- **DI lifetime mismatch**: don't capture the singleton in a scoped service expecting per-request JWTs. Use the scoped factory pattern above.
- **`NORBIX_*` env vars in containers**: pass them through your orchestration; the SDK reads via `Environment.GetEnvironmentVariable`.
- **Logging**: there's no built-in HTTP logger. Wrap calls or wire `ILogger` around your controllers — the SDK doesn't take an opinion on logging stacks.
