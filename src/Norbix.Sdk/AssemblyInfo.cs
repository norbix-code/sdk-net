using System.Runtime.CompilerServices;

// Tests reach into internal types (NorbixHttpHandler test-seam, transport
// internals). The public surface stays clean — no HttpClient or transport
// internals leak through any public ctor or property.
[assembly: InternalsVisibleTo("Norbix.Sdk.Tests")]
