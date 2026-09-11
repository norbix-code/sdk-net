using System.Runtime.CompilerServices;

// Tests reach into internal types (NorbixHttpHandler test-seam, transport
// internals). The public surface stays clean — no HttpClient or transport
// internals leak through any public ctor or property.
[assembly: InternalsVisibleTo("Norbix.Sdk.Tests")]

// Norbix.Api and Norbix.Hub both compile these sources but cannot be referenced
// from one test project (they share type names), so the Hub has its own suite.
[assembly: InternalsVisibleTo("Norbix.Hub.Tests")]
