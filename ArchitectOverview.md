# Norbix .NET SDK — Architect Review

**Reviewer perspective:** .NET Architect (20+ years).
**Scope:** UX of configuration and module API surface.
**Repo path reviewed:** `/Users/djovaisas/Projects/norbix/sdks/norbix-net`

---

## 1. Suggestions (ordered by impact)

1. **Use `IHttpClientFactory` instead of one private `HttpClient`.**
   Today `NorbixClient` owns its own `HttpClient` for the whole app lifetime
   (`src/Norbix.Sdk/NorbixClient.cs:49`, `:66`). Singleton + own `HttpClient`
   is the classic .NET trap: DNS changes are not picked up, and socket
   lifetime is not managed. The standard practice since 2018 is
   `services.AddHttpClient(...)` / `IHttpClientFactory`.

   **Status:** Implemented — DI wiring now uses a named `HttpClient` from `IHttpClientFactory` (`AddNorbixHttpClient()` + `AddNorbix(...)`).

2. **Add `ILogger<NorbixClient>` support.**
   I see no logging anywhere in `HttpTransport.cs` or `NorbixClient.cs`.
   Modern .NET libraries always accept an optional `ILogger`.

   **Status:** Implemented — `HttpTransport` emits optional debug/warn logs (no auth/header logging).

3. **Add a fluent builder for the "1-line" use case.**
   Current "shortest" call is still verbose:
   ```csharp
   await client.Database.FindAsync(new FindRequest { CollectionName = "orders" });
   ```
   Best-in-class SDKs (Stripe, MongoDB, OpenAI .NET) let you write something
   close to the goal you described:
   ```csharp
   var orders = await client.Database.FindAllAsync("orders");
   await client.Files.DownloadAsync("file_123");
   ```
   Keep the generated DTO API as the "advanced" surface, but add small
   hand-written extension methods for the top 5–10 most-used calls.

4. **Make singleton state thread-safe — or stop mutating it.**
   `SetBearerToken`, `SetApiKey`, `SetScope`, and `LoginAsync` mutate
   `_options` (`src/Norbix.Sdk/NorbixClient.cs:84-98, 126`).
   The DI extension registers the client as **singleton**
   (`src/Norbix.Sdk/ServiceCollectionExtensions.cs:32`).
   In ASP.NET Core, two parallel HTTP requests from different users can
   overwrite each other's bearer token. This is a real bug, not a style
   point.

   **Status:** Fixed in default DI path — `AddNorbix(...)` now registers `NorbixClient` as **scoped** (safe for per-request auth). Singleton remains available via `AddNorbixSingleton(...)` for fixed API-key workloads.

5. **Auto-load `.env` files in development.**
   Today the README says "The SDK does not load `.env` files itself"
   (`README.md:85`). For local dev, this is friction. Ship a tiny optional
   package `Norbix.Sdk.DotEnv` or a single
   `NorbixClientOptions.LoadDotEnv(path)` helper. Keep the main SDK clean.

6. **Add `ValidateOnStart()` and `IValidateOptions<>`.**
   The DI extension calls `opts.Validate()` inside `Configure(...)`
   (`src/Norbix.Sdk/ServiceCollectionExtensions.cs:29, 58`). This runs
   **at first resolve**, not at app startup. Use
   `services.AddOptions<NorbixClientOptions>().ValidateOnStart()` so
   misconfigured apps fail fast at boot, not on first user request.

   **Status:** Implemented — `AddNorbix(...)` / `AddNorbixSingleton(...)` call `ValidateOnStart()`.

7. **Provide retry / Polly extensions.**
   No retry policy exists for `429`, `503`, or transient network errors.
   Add `services.AddNorbix(...).WithRetryPolicy()` or document a Polly
   recipe.

   **Status:** Implemented (extension point) — `AddNorbixHttpClient()` returns an `IHttpClientBuilder` so apps can attach Polly policies.

8. **Add a `samples/` folder.**
   The repo has README + `docs/integrations/*.md` but no runnable sample.
   A 30-line console app and a 1-controller ASP.NET Core sample reduce
   time-to-first-call dramatically.

9. **Implement `IAsyncDisposable`.**
   `NorbixClient` only implements `IDisposable`
   (`src/Norbix.Sdk/NorbixClient.cs:31, 138`). HTTP-backed clients should
   also support `await using`.

   **Status:** Implemented — `NorbixClient` now implements `IAsyncDisposable`.

10. **Naming consistency: `NorbixOptions` vs `NorbixClientOptions`.**
    Two option types exist (`src/Norbix.Sdk/NorbixOptions.cs` and
    `src/Norbix.Sdk/NorbixClientOptions.cs`). Pick one. Two near-identical
    names will confuse users in IntelliSense.

    **Status:** Implemented — only `NorbixClientOptions` remains.

11. **`AddNorbixHealthChecks()` extension.**
    A 10-line extension that pings the `Echo` endpoint is cheap and very
    valuable for production users.

    **Status:** Implemented — `services.AddNorbixHealthChecks(ping: true)` optionally pings `GET /{version}/echo`.

12. **Document or implement automatic JWT refresh.**
    Provide a clear pattern for refresh tokens (create a derived client via
    `WithBearerToken(...)` after refresh) or ship middleware.

---

## 2. Summary of observations

The SDK is in good shape overall. The architecture is clean: hand-written
transport and auth in `src/Norbix.Sdk`, generated DTOs and modules in
`src/Norbix.Sdk.Types`, and a Roslyn source generator that keeps endpoint
methods in sync with the backend. Test coverage uses snapshot testing with
Verify, which is exactly what I would recommend for a generated API
surface.

### Configuration UX is the strongest part

The progressive pattern in `src/Norbix.Sdk/NorbixClient.cs:38-53` is
excellent:

```csharp
// Zero-arg → reads NORBIX_* env vars
using var client = new NorbixClient();

// Explicit options → env vars fill any blank field
using var client = new NorbixClient(new NorbixClientOptions {
    ApiKey = "sk_live_...",
    ProjectId = "proj_123",
});
```

The `ApplyEnvironment()` + `Validate()` chain in
`src/Norbix.Sdk/NorbixClientOptions.cs:71-113` is clean and easy to test.
The DI extensions in `src/Norbix.Sdk/ServiceCollectionExtensions.cs` cover
both `appsettings.json` binding and inline lambda configuration, which
matches what users expect from a modern .NET library.

So the answer to the direct question — *"do we have manual init in scripts,
or can we load from env?"* — is: **both work, and the env-var path is
well-designed.** The only gap is that `.env` files in dev still need a
third-party loader.

### Module UX is good but verbose

The grouped sub-client model is right:
`client.Api.Database.FindAsync(...)` and
`client.Hub.Notifications.GetTemplatesAsync(...)`
(`README.md:31-32, 117-119`). This scales to 38 API + 244 Hub endpoints
without becoming a flat soup. But the call site is heavier than the goal
("`db.FindAll()`, `files.Download()`, `code.Execute()`"). Compare:

```csharp
// Norbix today (README.md:31)
await client.Api.Database.FindAsync(new FindRequest { CollectionName = "orders" });

// What "best library" feel looks like
await client.Database.FindAllAsync("orders");
await client.Files.DownloadAsync("file_123", "/tmp/out.pdf");
```

The two-level path `client.Api.Database` is correct for separating **API**
from **Hub**, but for the most-used endpoints, hand-written shortcut
methods would close the gap. Keep the generated `Api/Hub` surface for full
power; add shortcuts for the top 10 calls.

### The biggest real risk: singleton + mutable state

In `src/Norbix.Sdk/ServiceCollectionExtensions.cs:32`, the client is
registered as singleton. In `src/Norbix.Sdk/NorbixClient.cs:84-98, 126`,
methods like `SetBearerToken`, `SetScope`, and `LoginAsync` write to
`_options`.

In a web app where every HTTP request belongs to a different user, this
will cause user-A's bearer token to leak into user-B's request. You
should either:

- (a) make per-user calls go through a scoped factory, or
- (b) accept a `bearerToken` parameter per call, or
- (c) use `AsyncLocal<T>` for the token.

### The second risk: `HttpClient` ownership

`src/Norbix.Sdk/NorbixClient.cs:49` creates `new HttpTransport(_options)`,
which (based on the constructor pattern at line 66) owns its own
`HttpClient`. With a singleton client, this is the "no DNS refresh"
anti-pattern. Switching to `IHttpClientFactory` is a small change with a
big reliability win.

### Smaller things worth a look

- Two option classes (`NorbixOptions.cs` and `NorbixClientOptions.cs`) —
  IntelliSense will offer both and confuse users.
- The client only implements `IDisposable`, not `IAsyncDisposable`.
- No built-in logging, metrics, or retry layer — production users will
  need them and currently must add them manually.
- No `samples/` folder, only docs.

None of these are blockers, but each one is a small friction point that
adds up.

---

## 3. Overall rating: 4 / 5

Configuration UX and project structure are above average for a .NET SDK
at this stage. The two real bugs to fix first are:

1. The singleton-with-mutable-token issue.
2. The `HttpClient` ownership / `IHttpClientFactory` migration.

After those, the next investment should be:

- Shortcut methods (`client.Database.FindAll(...)`).
- `ILogger` integration.
- Runnable samples in a `samples/` folder.

These changes will make the developer experience match the quality of the
underlying architecture.
