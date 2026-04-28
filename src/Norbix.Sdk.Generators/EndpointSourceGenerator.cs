using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Norbix.Sdk.Generators;

/// <summary>
/// Walks every type in the compilation that carries [NorbixRoute(...)] and
/// emits a partial Norbix class plus per-group module classes (e.g.
/// AccountModule, DatabaseModule). Wires them directly onto the client as
/// flat modules so consumer code looks like <c>norbix.Database.FindAsync(...)</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class EndpointSourceGenerator : IIncrementalGenerator
{
    private const string RouteAttributeFullName = "Norbix.Sdk.Types.NorbixRouteAttribute";
    private const string RequestInterfaceFullName = "Norbix.Sdk.Types.INorbixRequest`1";
    private const string AccountScopedInterfaceFullName = "Norbix.Sdk.Types.INorbixAccountScoped";

    private static DiagnosticDescriptor BothTargetsNotSupportedDescriptor { get; } =
        new DiagnosticDescriptor(
            id: "NORBIX001",
            title: "Both API and Hub SDKs referenced",
            messageFormat:
                "This SDK is split into two packages. Reference either Norbix.Api or Norbix.Hub (not both) in the same project.",
            category: "Norbix.Sdk",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find every class with [NorbixRoute] anywhere in the compilation
        // (the project itself + its references).
        var endpointTypes = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax,
                transform: static (ctx, ct) => ExtractEndpoint(ctx, ct)
            )
            .Where(static e => e is not null)
            .Select(static (e, _) => e!.Value);

        var sourceEndpoints = endpointTypes.Collect();
        var referencedEndpoints = context.CompilationProvider.Select(
            static (compilation, ct) => ExtractReferencedEndpoints(compilation, ct)
        );
        var collected = sourceEndpoints.Combine(referencedEndpoints);

        context.RegisterSourceOutput(
            collected,
            static (ctx, pair) =>
            {
                var byName = new Dictionary<string, EndpointModel>(StringComparer.Ordinal);
                foreach (var endpoint in pair.Left)
                {
                    byName[endpoint.FullTypeName] = endpoint;
                }
                foreach (var endpoint in pair.Right)
                {
                    byName[endpoint.FullTypeName] = endpoint;
                }
                GenerateAll(ctx, byName.Values.ToImmutableArray());
            }
        );
    }

    // ─── Extraction ────────────────────────────────────────────────────

    private static EndpointModel? ExtractEndpoint(
        GeneratorSyntaxContext ctx,
        System.Threading.CancellationToken ct
    )
    {
        var classNode = (Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(classNode, ct) is not INamedTypeSymbol symbol)
        {
            return null;
        }

        var routes = ImmutableArray.CreateBuilder<RouteModel>();
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != RouteAttributeFullName)
                continue;
            var path =
                attr.ConstructorArguments.Length > 0
                    ? attr.ConstructorArguments[0].Value as string
                    : null;
            var verbs =
                attr.ConstructorArguments.Length > 1
                    ? attr.ConstructorArguments[1].Value as string
                    : "POST";
            if (string.IsNullOrEmpty(path))
                continue;
            routes.Add(
                new RouteModel(
                    path!,
                    (verbs ?? "POST")
                        .Split(',')
                        .Select(v => v.Trim().ToUpperInvariant())
                        .ToImmutableArray()
                )
            );
        }
        if (routes.Count == 0)
            return null;

        var responseType = ResolveResponseType(symbol);
        var isAccountScoped =
            ImplementsInterface(symbol, AccountScopedInterfaceFullName)
            || symbol.GetMembers("AccountId").Any(); // gateway DTOs commonly expose `AccountId` directly
        var fullName = symbol.ToDisplayString();
        var ns = ResolveTargetNamespace(symbol);

        return new EndpointModel(
            symbol.Name,
            fullName,
            routes.ToImmutable(),
            responseType,
            isAccountScoped,
            ns
        );
    }

    private static string ResolveResponseType(INamedTypeSymbol symbol)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            if (
                iface.IsGenericType
                && iface.ConstructedFrom?.ToDisplayString()
                    == "Norbix.Sdk.Types.INorbixRequest<TResponse>"
            )
            {
                return iface.TypeArguments[0].ToDisplayString();
            }
            // Match whatever ServiceStack-derived shape sneaks through.
            if (
                iface.IsGenericType
                && iface.ConstructedFrom?.Name == "INorbixRequest"
                && iface.TypeArguments.Length == 1
            )
            {
                return iface.TypeArguments[0].ToDisplayString();
            }
        }
        return "Norbix.Sdk.Types.EmptyResponse";
    }

    private static bool ImplementsInterface(INamedTypeSymbol symbol, string fullName)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.ToDisplayString() == fullName)
                return true;
        }
        return false;
    }

    private static ImmutableArray<EndpointModel> ExtractReferencedEndpoints(
        Compilation compilation,
        System.Threading.CancellationToken ct
    )
    {
        var builder = ImmutableArray.CreateBuilder<EndpointModel>();
        AddNamespaceEndpoints(compilation.GlobalNamespace, builder, ct);
        return builder.ToImmutable();
    }

    private static void AddNamespaceEndpoints(
        INamespaceSymbol ns,
        ImmutableArray<EndpointModel>.Builder builder,
        System.Threading.CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        foreach (var type in ns.GetTypeMembers())
        {
            var endpoint = ExtractEndpoint(type);
            if (endpoint is not null)
            {
                builder.Add(endpoint.Value);
            }
        }
        foreach (var child in ns.GetNamespaceMembers())
        {
            AddNamespaceEndpoints(child, builder, ct);
        }
    }

    private static EndpointModel? ExtractEndpoint(INamedTypeSymbol symbol)
    {
        var routes = ImmutableArray.CreateBuilder<RouteModel>();
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != RouteAttributeFullName)
                continue;
            var path =
                attr.ConstructorArguments.Length > 0
                    ? attr.ConstructorArguments[0].Value as string
                    : null;
            var verbs =
                attr.ConstructorArguments.Length > 1
                    ? attr.ConstructorArguments[1].Value as string
                    : "POST";
            if (string.IsNullOrEmpty(path))
                continue;
            routes.Add(
                new RouteModel(
                    path!,
                    (verbs ?? "POST")
                        .Split(',')
                        .Select(v => v.Trim().ToUpperInvariant())
                        .ToImmutableArray()
                )
            );
        }
        if (routes.Count == 0)
            return null;

        return new EndpointModel(
            symbol.Name,
            symbol.ToDisplayString(),
            routes.ToImmutable(),
            ResolveResponseType(symbol),
            ImplementsInterface(symbol, AccountScopedInterfaceFullName)
                || symbol.GetMembers("AccountId").Any(),
            ResolveTargetNamespace(symbol)
        );
    }

    private static EndpointTarget ResolveTargetNamespace(INamedTypeSymbol symbol)
    {
        var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (ns.Contains(".Hub", StringComparison.Ordinal))
            return EndpointTarget.Hub;
        return EndpointTarget.Api;
    }

    // ─── Emission ──────────────────────────────────────────────────────

    private static void GenerateAll(
        SourceProductionContext ctx,
        ImmutableArray<EndpointModel> endpoints
    )
    {
        if (endpoints.IsDefaultOrEmpty)
        {
            // No endpoints discovered; nothing to generate.
            return;
        }

        var apiEndpoints = endpoints.Where(e => e.Target == EndpointTarget.Api).ToImmutableArray();
        var hubEndpoints = endpoints.Where(e => e.Target == EndpointTarget.Hub).ToImmutableArray();

        var apiGroups = GroupByPath(apiEndpoints);
        var hubGroups = GroupByPath(hubEndpoints);

        if (apiGroups.Count > 0 && hubGroups.Count > 0)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(BothTargetsNotSupportedDescriptor, Location.None));
            return;
        }

        // Per-group module classes.
        foreach (var entry in apiGroups)
        {
            ctx.AddSource(
                $"Norbix.Api.{Pascal(entry.Key)}Module.g.cs",
                BuildModuleClass("Api", entry.Key, entry.Value)
            );
        }
        foreach (var entry in hubGroups)
        {
            ctx.AddSource(
                $"Norbix.Hub.{Pascal(entry.Key)}Module.g.cs",
                BuildModuleClass("Hub", entry.Key, entry.Value)
            );
        }

        ctx.AddSource(
            "Norbix.Init.g.cs",
            BuildClientInitializer(
                apiGroups.Keys.ToImmutableArray(),
                hubGroups.Keys.ToImmutableArray()
            )
        );
        ctx.AddSource("Norbix.EndpointCatalog.g.cs", BuildEndpointCatalog(apiGroups, hubGroups));
    }

    private static SortedDictionary<string, ImmutableArray<EndpointModel>> GroupByPath(
        ImmutableArray<EndpointModel> eps
    )
    {
        var result = new SortedDictionary<string, ImmutableArray<EndpointModel>>(
            StringComparer.Ordinal
        );
        var working = new Dictionary<string, List<EndpointModel>>();
        foreach (var ep in eps)
        {
            var group = DeriveGroup(ep.Routes[0].Path);
            if (!working.TryGetValue(group, out var list))
            {
                list = new List<EndpointModel>();
                working[group] = list;
            }
            list.Add(ep);
        }
        foreach (var entry in working)
            result[entry.Key] = entry.Value.ToImmutableArray();
        return result;
    }

    private static string DeriveGroup(string path)
    {
        var stripped = path.TrimStart('/');
        var parts = stripped.Split('/');
        var head =
            parts.Length > 0 && parts[0] == "{version}" && parts.Length > 1 ? parts[1] : parts[0];
        if (string.IsNullOrEmpty(head))
            head = "misc";
        return ToIdent(head);
    }

    private static string ToIdent(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
        }
        return sb.ToString().Trim('_');
    }

    private static string Pascal(string ident) =>
        string.Concat(
            ident
                .Split('_')
                .Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p.Substring(1))
        );

    private static string PickVerb(ImmutableArray<string> verbs)
    {
        foreach (var v in new[] { "POST", "PUT", "PATCH", "DELETE", "GET" })
            if (verbs.Contains(v))
                return v;
        return "POST";
    }

    private static string MethodNameFromClass(string className)
    {
        var stripped = className.EndsWith("Request", StringComparison.Ordinal)
            ? className.Substring(0, className.Length - "Request".Length)
            : className;
        return char.ToUpperInvariant(stripped[0]) + stripped.Substring(1) + "Async";
    }

    private static string Header =>
        "// <auto-generated> Norbix.Sdk source generator </auto-generated>\n#nullable enable\n";

    private static string BuildEmptyNamespace(string nsName) =>
        Header
        + $@"
namespace Norbix.Sdk;

public sealed partial class {nsName}Namespace
{{
    internal {nsName}Namespace(global::Norbix.Sdk.NorbixClient client) {{ }}
}}

public sealed partial class NorbixClient
{{
    public {nsName}Namespace {nsName} {{ get; private set; }} = null!;
}}
";

    private static string BuildNamespaceClass(string nsName, IEnumerable<string> groups)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);
        sb.AppendLine("using global::Norbix.Sdk.Transport;");
        sb.AppendLine();
        sb.AppendLine("namespace Norbix.Sdk;");
        sb.AppendLine();
        sb.AppendLine($"public sealed partial class {nsName}Namespace");
        sb.AppendLine("{");
        foreach (var g in groups)
        {
            sb.AppendLine($"    public {nsName}{Pascal(g)}Module {Pascal(g)} {{ get; }}");
        }
        sb.AppendLine();
        sb.AppendLine($"    internal {nsName}Namespace(global::Norbix.Sdk.NorbixClient client)");
        sb.AppendLine("    {");
        foreach (var g in groups)
        {
            sb.AppendLine(
                $"        {Pascal(g)} = new {nsName}{Pascal(g)}Module(client.Transport);"
            );
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"public sealed partial class NorbixClient");
        sb.AppendLine("{");
        sb.AppendLine($"    public {nsName}Namespace {nsName} {{ get; private set; }} = null!;");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildModuleClass(
        string nsName,
        string group,
        ImmutableArray<EndpointModel> endpoints
    )
    {
        var className = nsName + Pascal(group) + "Module";
        var sb = new StringBuilder();
        sb.AppendLine(Header);
        sb.AppendLine("using global::Norbix.Sdk.Transport;");
        sb.AppendLine();
        sb.AppendLine("namespace Norbix.Sdk;");
        sb.AppendLine();
        sb.AppendLine(
            $"/// <summary>Auto-generated module: {nsName}.{Pascal(group)} ({endpoints.Length} endpoints).</summary>"
        );
        sb.AppendLine($"public sealed class {className}");
        sb.AppendLine("{");
        sb.AppendLine(
            "    private readonly global::Norbix.Sdk.Transport.INorbixTransport _transport;"
        );
        sb.AppendLine(
            $"    internal {className}(global::Norbix.Sdk.Transport.INorbixTransport transport) {{ _transport = transport; }}"
        );
        sb.AppendLine();

        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ep in endpoints)
        {
            var route = ep.Routes[0];
            var verb = PickVerb(route.Verbs);
            var methodName = MethodNameFromClass(ep.ClassName);
            var suffix = 1;
            while (!seenNames.Add(methodName))
            {
                suffix++;
                methodName = MethodNameFromClass(ep.ClassName).Replace("Async", $"{suffix}Async");
            }

            var pathParams = ExtractPathParams(route.Path);
            var pathParamsLiteral =
                pathParams.Count == 0
                    ? "global::System.Array.Empty<string>()"
                    : "new[] { " + string.Join(", ", pathParams.Select(p => $"\"{p}\"")) + " }";
            var scope = EndpointScope(ep, route);
            var responseType = ep.ResponseType;
            var requestType = ep.FullTypeName;

            sb.AppendLine(
                $"    /// <summary>{verb} {Escape(route.Path)} (DTO: {Escape(ep.ClassName)})</summary>"
            );
            sb.AppendLine(
                $"    public global::System.Threading.Tasks.Task<{responseType}?> {methodName}({requestType} request, global::System.Threading.CancellationToken cancellationToken = default)"
            );
            sb.AppendLine("    {");
            sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(request);");
            sb.AppendLine(
                "        return _transport.SendAsync<"
                    + responseType
                    + ">(new global::Norbix.Sdk.Transport.NorbixRequestSpec"
            );
            sb.AppendLine("        {");
            sb.AppendLine(
                $"            Target = global::Norbix.Sdk.Transport.NorbixTarget.{nsName},"
            );
            sb.AppendLine($"            Path = \"{Escape(route.Path)}\",");
            sb.AppendLine($"            Method = \"{verb}\",");
            sb.AppendLine($"            Request = request,");
            sb.AppendLine($"            PathParams = {pathParamsLiteral},");
            sb.AppendLine($"            Scope = global::Norbix.Sdk.Transport.NorbixScope.{scope},");
            sb.AppendLine("        }, cancellationToken);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildEndpointCatalog(
        SortedDictionary<string, ImmutableArray<EndpointModel>> apiGroups,
        SortedDictionary<string, ImmutableArray<EndpointModel>> hubGroups
    )
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);
        sb.AppendLine("namespace Norbix.Sdk;");
        sb.AppendLine();
        sb.AppendLine("internal sealed class NorbixEndpointInfo");
        sb.AppendLine("{");
        sb.AppendLine(
            "    public NorbixEndpointInfo(string target, string group, string moduleProperty, string methodName, global::System.Type requestType, string path, string httpMethod, global::System.Collections.Generic.IReadOnlyList<string> pathParams, bool isAccountScoped, bool isUnauthenticated)"
        );
        sb.AppendLine("    {");
        sb.AppendLine("        Target = target;");
        sb.AppendLine("        Group = group;");
        sb.AppendLine("        ModuleProperty = moduleProperty;");
        sb.AppendLine("        MethodName = methodName;");
        sb.AppendLine("        RequestType = requestType;");
        sb.AppendLine("        Path = path;");
        sb.AppendLine("        HttpMethod = httpMethod;");
        sb.AppendLine("        PathParams = pathParams;");
        sb.AppendLine("        IsAccountScoped = isAccountScoped;");
        sb.AppendLine("        IsUnauthenticated = isUnauthenticated;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public string Target { get; }");
        sb.AppendLine("    public string Group { get; }");
        sb.AppendLine("    public string ModuleProperty { get; }");
        sb.AppendLine("    public string MethodName { get; }");
        sb.AppendLine("    public global::System.Type RequestType { get; }");
        sb.AppendLine("    public string Path { get; }");
        sb.AppendLine("    public string HttpMethod { get; }");
        sb.AppendLine(
            "    public global::System.Collections.Generic.IReadOnlyList<string> PathParams { get; }"
        );
        sb.AppendLine("    public bool IsAccountScoped { get; }");
        sb.AppendLine("    public bool IsUnauthenticated { get; }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("internal static class NorbixEndpointCatalog");
        sb.AppendLine("{");
        sb.AppendLine(
            "    public static global::System.Collections.Generic.IReadOnlyList<NorbixEndpointInfo> All { get; } = new NorbixEndpointInfo[]"
        );
        sb.AppendLine("    {");
        AppendCatalogEntries(sb, "Api", apiGroups);
        AppendCatalogEntries(sb, "Hub", hubGroups);
        sb.AppendLine("    };");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AppendCatalogEntries(
        StringBuilder sb,
        string nsName,
        SortedDictionary<string, ImmutableArray<EndpointModel>> groups
    )
    {
        foreach (var entry in groups)
        {
            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ep in entry.Value)
            {
                var route = ep.Routes[0];
                var verb = PickVerb(route.Verbs);
                var methodName = MethodNameFromClass(ep.ClassName);
                var suffix = 1;
                while (!seenNames.Add(methodName))
                {
                    suffix++;
                    methodName = MethodNameFromClass(ep.ClassName)
                        .Replace("Async", $"{suffix}Async");
                }

                var pathParams = ExtractPathParams(route.Path);
                var pathParamsLiteral =
                    pathParams.Count == 0
                        ? "global::System.Array.Empty<string>()"
                        : "new[] { " + string.Join(", ", pathParams.Select(p => $"\"{p}\"")) + " }";
                var scope = EndpointScope(ep, route);

                sb.AppendLine("        new NorbixEndpointInfo(");
                sb.AppendLine($"            \"{nsName}\",");
                sb.AppendLine($"            \"{Escape(entry.Key)}\",");
                sb.AppendLine($"            \"{Pascal(entry.Key)}\",");
                sb.AppendLine($"            \"{Escape(methodName)}\",");
                sb.AppendLine($"            typeof(global::{ep.FullTypeName}),");
                sb.AppendLine($"            \"{Escape(route.Path)}\",");
                sb.AppendLine($"            \"{verb}\",");
                sb.AppendLine($"            {pathParamsLiteral},");
                sb.AppendLine($"            {(ep.IsAccountScoped ? "true" : "false")},");
                sb.AppendLine($"            {(scope == "Unauthenticated" ? "true" : "false")}),");
            }
        }
    }

    private static string EndpointScope(EndpointModel ep, RouteModel route)
    {
        return route.Path == "/auth" || route.Path.StartsWith("/auth/", StringComparison.Ordinal)
                ? "Unauthenticated"
            : ep.IsAccountScoped ? "Account"
            : "Project";
    }

    private static string BuildClientInitializer(
        ImmutableArray<string> apiGroups,
        ImmutableArray<string> hubGroups
    )
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);
        sb.AppendLine("namespace Norbix.Sdk;");
        sb.AppendLine();
        sb.AppendLine("public sealed partial class NorbixClient");
        sb.AppendLine("{");

        foreach (var g in apiGroups)
        {
            sb.AppendLine(
                $"    public Api{Pascal(g)}Module {Pascal(g)} {{ get; private set; }} = null!;"
            );
        }
        foreach (var g in hubGroups)
        {
            sb.AppendLine(
                $"    public Hub{Pascal(g)}Module {Pascal(g)} {{ get; private set; }} = null!;"
            );
        }

        sb.AppendLine();
        sb.AppendLine("    partial void InitializeModules()");
        sb.AppendLine("    {");
        foreach (var g in apiGroups)
        {
            sb.AppendLine($"        {Pascal(g)} = new Api{Pascal(g)}Module(Transport);");
        }
        foreach (var g in hubGroups)
        {
            sb.AppendLine($"        {Pascal(g)} = new Hub{Pascal(g)}Module(Transport);");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static List<string> ExtractPathParams(string path)
    {
        var list = new List<string>();
        var i = 0;
        while (i < path.Length)
        {
            if (path[i] == '{')
            {
                var end = path.IndexOf('}', i);
                if (end < 0)
                    break;
                var token = path.Substring(i + 1, end - i - 1);
                if (token != "version")
                    list.Add(token);
                i = end + 1;
            }
            else
                i++;
        }
        return list;
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ─── Models ────────────────────────────────────────────────────────

    private enum EndpointTarget
    {
        Api,
        Hub,
    }

    private readonly struct RouteModel
    {
        public RouteModel(string path, ImmutableArray<string> verbs)
        {
            Path = path;
            Verbs = verbs;
        }

        public string Path { get; }
        public ImmutableArray<string> Verbs { get; }
    }

    private readonly struct EndpointModel
    {
        public EndpointModel(
            string className,
            string fullTypeName,
            ImmutableArray<RouteModel> routes,
            string responseType,
            bool isAccountScoped,
            EndpointTarget target
        )
        {
            ClassName = className;
            FullTypeName = fullTypeName;
            Routes = routes;
            ResponseType = responseType;
            IsAccountScoped = isAccountScoped;
            Target = target;
        }

        public string ClassName { get; }
        public string FullTypeName { get; }
        public ImmutableArray<RouteModel> Routes { get; }
        public string ResponseType { get; }
        public bool IsAccountScoped { get; }
        public EndpointTarget Target { get; }
    }
}
