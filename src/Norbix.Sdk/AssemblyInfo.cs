using System.Runtime.CompilerServices;

// Tests reach into internal types (NorbixHttpHandler test-seam, transport
// internals). The public surface stays clean — no HttpClient or transport
// internals leak through any public ctor or property.
[assembly: InternalsVisibleTo("Norbix.Sdk.Tests")]
// Same for the Hub half of the suite — Norbix.Hub compiles these same sources.
[assembly: InternalsVisibleTo("Norbix.Hub.Tests")]
