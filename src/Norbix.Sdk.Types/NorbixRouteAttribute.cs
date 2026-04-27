namespace Norbix.Sdk.Types;

/// <summary>
/// Marks a request DTO with the gateway route it maps to. The Norbix.Sdk
/// source generator scans for this attribute to emit per-endpoint methods
/// on the client.
/// </summary>
/// <remarks>
/// Defined here (and not pulled from ServiceStack) so the public SDK surface
/// never leaks transport-specific types. The sync-types script rewrites
/// <c>[ServiceStack.Route(...)]</c> from <c>x csharp</c> output into this
/// attribute.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class NorbixRouteAttribute : Attribute
{
    /// <summary>Path template, e.g. <c>/{version}/database/schemas/{id}</c>.</summary>
    public string Path { get; }

    /// <summary>
    /// Comma-separated list of HTTP verbs the route accepts (e.g. <c>"GET,POST"</c>).
    /// The generator picks the most idiomatic verb when multiple are listed.
    /// </summary>
    public string Verbs { get; }

    public NorbixRouteAttribute(string path, string verbs = "POST")
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Verbs = verbs ?? throw new ArgumentNullException(nameof(verbs));
    }
}
