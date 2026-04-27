# Dependency injection patterns

[← Back to project README](../../README.md)

`AddNorbix(...)` covers the 90 % case. The recipes below cover the rest.

## Multiple Norbix scopes in one app

If your app has several Norbix projects (multi-tenant SaaS, separate staging / production) and you want one client per scope, use **keyed services**:

```csharp
builder.Services.AddKeyedSingleton<NorbixClient>("tenant-a",
    (_, _) => new NorbixClient(new NorbixClientOptions
    {
        ProjectId = "proj_a", ApiKey = "...",
    }));

builder.Services.AddKeyedSingleton<NorbixClient>("tenant-b",
    (_, _) => new NorbixClient(new NorbixClientOptions
    {
        ProjectId = "proj_b", ApiKey = "...",
    }));
```

```csharp
public sealed class TenantSwitcher(
    [FromKeyedServices("tenant-a")] NorbixClient a,
    [FromKeyedServices("tenant-b")] NorbixClient b)
{
    public Task<object?> A(CancellationToken ct) => a.Api.Echo.EchoAsync(new() { Message = "a" }, ct);
    public Task<object?> B(CancellationToken ct) => b.Api.Echo.EchoAsync(new() { Message = "b" }, ct);
}
```

## Strong-typed module injection

If you want to inject just one module instead of the whole client:

```csharp
public sealed class NorbixDatabase(NorbixClient norbix)
{
    public DatabaseModule Hub => norbix.Hub.Database;
    public DatabaseModule Api => norbix.Api.Database;
}

builder.Services.AddSingleton<NorbixDatabase>();
```

This keeps your application service signatures focused — `OrdersRepository(NorbixDatabase db)` says exactly what it needs.

## Auto-refreshing the JWT

If your auth provider returns refresh tokens, wrap your domain calls with a small retry helper that calls `client.SetBearerToken(...)` after a refresh:

```csharp
public sealed class NorbixWithRefresh(NorbixClient norbix, ITokenRefresher refresher)
{
    public async Task<T> CallAsync<T>(Func<NorbixClient, Task<T>> call, CancellationToken ct = default)
    {
        try { return await call(norbix); }
        catch (NorbixException ex) when (ex.StatusCode == 401)
        {
            var fresh = await refresher.RefreshAsync(ct);
            norbix.SetBearerToken(fresh.AccessToken);
            return await call(norbix);
        }
    }
}
```

Now domain code does `await wrapper.CallAsync(c => c.Api.Database.FindAsync(...))` and gets transparent re-auth.

## Testing

The SDK's public API is `NorbixClient` only — `HttpClient`, `HttpMessageHandler`, and the transport layer are all internal. For your own unit tests, mock `NorbixClient` behind a domain abstraction:

```csharp
public interface IOrdersRepository
{
    Task<IEnumerable<Order>> ListAsync(CancellationToken ct);
}

public sealed class NorbixOrdersRepository(NorbixClient norbix) : IOrdersRepository
{
    public async Task<IEnumerable<Order>> ListAsync(CancellationToken ct)
    {
        var resp = await norbix.Api.Database.FindAsync(
            new() { CollectionName = "orders" }, ct);
        return resp?.Result ?? [];
    }
}

// In tests, mock IOrdersRepository — not NorbixClient. Domain abstractions
// are easier to fake and don't tie tests to SDK internals.
```

If you genuinely need to integration-test against a stubbed gateway, use [WireMock.Net](https://github.com/WireMock-Net/WireMock.Net) and point `NorbixClientOptions.ApiBaseUrl` / `HubBaseUrl` at the stub.
