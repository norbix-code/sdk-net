using System.Reflection;
using System.Text.Json;
using Norbix.Sdk.Tests.Helpers;
using NUnit.Framework;

namespace Norbix.Sdk.Tests;

internal static class EndpointCoverageDriver
{
    private static readonly HashSet<string> InfrastructureRequestProperties = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "ProjectId",
        "CultureCode",
        "TimeZoneId",
        "Version",
        "CorrelationId",
    };

    public static IEnumerable<TestCaseData> GetModuleCases()
    {
        return NorbixEndpointCatalog
            .All.GroupBy(e => new { e.Target, e.Group })
            .OrderBy(g => g.Key.Target, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Group, StringComparer.Ordinal)
            .Select(g =>
                new TestCaseData(g.Key.Target, g.Key.Group).SetName(
                    $"EndpointCoverage.{g.Key.Target}.{ToPascal(g.Key.Group)}"
                )
            );
    }

    public static async Task<EndpointCoverageResult> CoverModuleAsync(string target, string group)
    {
        using var fixture = NorbixTestFixture.Create(o =>
        {
            o.AccountId = "test-account";
            o.BearerToken = "test-bearer";
        });
        fixture.RespondNoContentDefault();

        var endpoints = NorbixEndpointCatalog
            .All.Where(e => e.Target == target && e.Group == group)
            .ToList();

        var missingPathParameters = FindMissingPathParameters(endpoints);
        if (missingPathParameters.Count > 0)
        {
            return new EndpointCoverageResult(
                SnapshotFileName: $"EndpointCoverageTests.{target}.{ToPascal(group)}.MissingPathParameters",
                Snapshot: new { MissingPathParameters = missingPathParameters }
            );
        }

        var invoked = new List<object>();
        foreach (var endpoint in endpoints)
        {
            invoked.Add(await InvokeEndpointAsync(fixture, endpoint));
        }

        return new EndpointCoverageResult(
            SnapshotFileName: $"EndpointCoverageTests.{target}.{ToPascal(group)}",
            Snapshot: new
            {
                Target = target,
                Group = group,
                Count = invoked.Count,
                Endpoints = invoked,
            }
        );
    }

    private static async Task<object> InvokeEndpointAsync(
        NorbixTestFixture fixture,
        NorbixEndpointInfo endpoint
    )
    {
        var request =
            Activator.CreateInstance(endpoint.RequestType)
            ?? throw new InvalidOperationException(
                $"Could not create {endpoint.RequestType.FullName}."
            );
        FillRequestProperties(request, endpoint.PathParams);

        var module = ResolveModule(fixture.Client, endpoint);
        var method = ResolveMethod(module, endpoint);
        var responseType = method.ReturnType.GenericTypeArguments.Single();
        fixture.RespondNext(CreateResponsePayload(responseType));

        var response = await InvokeGeneratedMethodAsync(module, method, request, endpoint);
        var sent =
            fixture.LastRequest
            ?? throw new InvalidOperationException(
                $"{endpoint.MethodName} did not send a request."
            );

        return new
        {
            endpoint.Target,
            endpoint.Group,
            endpoint.MethodName,
            RequestType = endpoint.RequestType.Name,
            endpoint.HttpMethod,
            endpoint.Path,
            endpoint.PathParams,
            endpoint.IsAccountScoped,
            endpoint.IsUnauthenticated,
            Sent = sent,
            ResponseType = responseType.Name,
            Response = response,
        };
    }

    private static async Task<object?> InvokeGeneratedMethodAsync(
        object module,
        MethodInfo method,
        object request,
        NorbixEndpointInfo endpoint
    )
    {
        try
        {
            var task = (Task?)method.Invoke(module, new[] { request, CancellationToken.None });
            if (task is null)
            {
                throw new InvalidOperationException(
                    $"{endpoint.MethodName} did not return a Task."
                );
            }

            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"Endpoint invocation failed for {endpoint.Target}.{endpoint.Group}.{endpoint.MethodName}.",
                ex.InnerException
            );
        }
    }

    private static List<object> FindMissingPathParameters(IEnumerable<NorbixEndpointInfo> endpoints)
    {
        var missing = new List<object>();
        foreach (var endpoint in endpoints)
        {
            foreach (var pathParam in endpoint.PathParams)
            {
                var prop = endpoint.RequestType.GetProperty(
                    pathParam,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
                );
                if (prop is null || !prop.CanWrite)
                {
                    missing.Add(
                        new
                        {
                            endpoint.Target,
                            endpoint.Group,
                            endpoint.MethodName,
                            RequestType = endpoint.RequestType.Name,
                            endpoint.Path,
                            PathParam = pathParam,
                        }
                    );
                }
            }
        }

        return missing;
    }

    private static object ResolveModule(NorbixClient client, NorbixEndpointInfo endpoint)
    {
        // This test suite targets the Norbix.Api package only.
        if (endpoint.Target != "Api")
        {
            throw new InvalidOperationException(
                $"Hub endpoint encountered in API-only coverage run: {endpoint.Group}.{endpoint.MethodName}"
            );
        }
        var prop =
            client.GetType().GetProperty(endpoint.ModuleProperty) ?? throw new MissingMemberException(
                client.GetType().FullName,
                endpoint.ModuleProperty
            );

        return prop.GetValue(client)
            ?? throw new InvalidOperationException(
                $"{endpoint.Target}.{endpoint.ModuleProperty} was null."
            );
    }

    private static MethodInfo ResolveMethod(object module, NorbixEndpointInfo endpoint)
    {
        return module
                .GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(m =>
                {
                    if (m.Name != endpoint.MethodName)
                    {
                        return false;
                    }

                    var parameters = m.GetParameters();
                    return parameters.Length == 2
                        && parameters[0].ParameterType == endpoint.RequestType
                        && parameters[1].ParameterType == typeof(CancellationToken);
                })
            ?? throw new MissingMethodException(module.GetType().FullName, endpoint.MethodName);
    }

    private static void FillRequestProperties(object request, IReadOnlyList<string> pathParams)
    {
        var pathParamSet = new HashSet<string>(pathParams, StringComparer.OrdinalIgnoreCase);
        foreach (
            var prop in request.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
        )
        {
            if (
                !prop.CanWrite
                || prop.GetIndexParameters().Length > 0
                || InfrastructureRequestProperties.Contains(prop.Name)
            )
            {
                continue;
            }

            prop.SetValue(request, RequestValue(prop.PropertyType, prop.Name));
        }

        // Validate route token properties after filling the DTO, so path
        // interpolation and POST/PUT bodies are covered in the same run.
        foreach (var pathParam in pathParamSet)
        {
            var prop = request
                .GetType()
                .GetProperty(
                    pathParam,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
                );
            if (prop is null || !prop.CanWrite)
            {
                throw new MissingMemberException(request.GetType().FullName, pathParam);
            }

            if (prop.GetValue(request) is null)
            {
                prop.SetValue(request, RequestValue(prop.PropertyType, pathParam));
            }
        }
    }

    private static object? RequestValue(Type type, string name, int depth = 0)
    {
        var targetType = Nullable.GetUnderlyingType(type) ?? type;
        if (targetType == typeof(string))
        {
            var jsonName = JsonNamingPolicy.CamelCase.ConvertName(name);
            if (
                name.Contains("Url", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Uri", StringComparison.OrdinalIgnoreCase)
            )
            {
                return $"https://example.test/{jsonName}";
            }

            if (name.Contains("Email", StringComparison.OrdinalIgnoreCase))
            {
                return "user@example.test";
            }

            return name.EndsWith("Id", StringComparison.Ordinal)
                ? "11111111-1111-1111-1111-111111111111"
                : $"test-{jsonName}";
        }

        if (targetType == typeof(Guid))
            return Guid.Parse("11111111-1111-1111-1111-111111111111");
        if (targetType == typeof(int))
            return 1;
        if (targetType == typeof(long))
            return 1L;
        if (targetType == typeof(float))
            return 1.5f;
        if (targetType == typeof(double))
            return 1.5d;
        if (targetType == typeof(decimal))
            return 1.5m;
        if (targetType == typeof(bool))
            return true;
        if (targetType == typeof(DateTime))
            return new DateTime(2026, 04, 27, 12, 0, 0, DateTimeKind.Utc);
        if (targetType == typeof(DateTimeOffset))
            return new DateTimeOffset(2026, 04, 27, 12, 0, 0, TimeSpan.Zero);
        if (targetType == typeof(Uri))
            return new Uri("https://example.test/request");
        if (targetType.IsEnum)
            return Enum.GetValues(targetType).GetValue(0);

        if (targetType.IsArray)
        {
            var elementType = targetType.GetElementType() ?? typeof(object);
            var array = Array.CreateInstance(elementType, 1);
            array.SetValue(RequestValue(elementType, elementType.Name, depth + 1), 0);
            return array;
        }

        if (targetType.IsGenericType)
        {
            return RequestGenericValue(targetType, depth);
        }

        if (
            targetType.IsClass
            && targetType != typeof(object)
            && !targetType.IsAbstract
            && depth < 1
        )
        {
            return RequestObjectValue(targetType, depth);
        }

        return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
    }

    private static object? RequestGenericValue(Type targetType, int depth)
    {
        var genericType = targetType.GetGenericTypeDefinition();
        if (
            genericType == typeof(List<>)
            || genericType == typeof(IList<>)
            || genericType == typeof(IReadOnlyList<>)
            || genericType == typeof(IEnumerable<>)
        )
        {
            var elementType = targetType.GetGenericArguments()[0];
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
            list.Add(RequestValue(elementType, elementType.Name, depth + 1));
            return list;
        }

        if (genericType == typeof(IReadOnlySet<>))
        {
            var elementType = targetType.GetGenericArguments()[0];
            var setType = typeof(HashSet<>).MakeGenericType(elementType);
            var value = Activator.CreateInstance(setType)!;
            setType
                .GetMethod("Add")!
                .Invoke(value, new[] { RequestValue(elementType, elementType.Name, depth + 1) });
            return value;
        }

        if (
            genericType == typeof(Dictionary<,>)
            || genericType == typeof(IDictionary<,>)
            || genericType == typeof(IReadOnlyDictionary<,>)
        )
        {
            var args = targetType.GetGenericArguments();
            var dictType = typeof(Dictionary<,>).MakeGenericType(args[0], args[1]);
            var dict = (System.Collections.IDictionary)Activator.CreateInstance(dictType)!;
            dict.Add(DictionaryKeyObject(args[0]), RequestValue(args[1], args[1].Name, depth + 1));
            return dict;
        }

        return null;
    }

    private static object? RequestObjectValue(Type targetType, int depth)
    {
        var value = Activator.CreateInstance(targetType);
        if (value is null)
        {
            return null;
        }

        foreach (
            var prop in targetType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => prop.CanWrite && prop.GetIndexParameters().Length == 0)
                .Take(8)
        )
        {
            prop.SetValue(value, RequestValue(prop.PropertyType, prop.Name, depth + 1));
        }

        return value;
    }

    private static object DictionaryKeyObject(Type keyType)
    {
        var targetType = Nullable.GetUnderlyingType(keyType) ?? keyType;
        if (targetType == typeof(string))
            return "test-key";
        if (targetType == typeof(Guid))
            return Guid.Parse("11111111-1111-1111-1111-111111111111");
        if (targetType == typeof(int))
            return 1;
        if (targetType == typeof(long))
            return 1L;
        if (targetType.IsEnum)
            return Enum.GetValues(targetType).GetValue(0)!;
        return "test-key";
    }

    private static Dictionary<string, object?> CreateResponsePayload(Type responseType)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["responseStatus"] = ResponseStatusValue(),
        };

        foreach (
            var prop in responseType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        )
        {
            if (!prop.CanWrite || prop.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var name = JsonNamingPolicy.CamelCase.ConvertName(prop.Name);
            payload[name] = ResponseValue(prop.PropertyType, prop.Name);
        }

        return payload;
    }

    private static object? ResponseValue(Type type, string name, int depth = 0)
    {
        var targetType = Nullable.GetUnderlyingType(type) ?? type;
        if (name == "ResponseStatus")
            return ResponseStatusValue();
        if (targetType == typeof(string))
            return ResponseStringValue(name);
        if (targetType == typeof(Guid))
            return Guid.Parse("11111111-1111-1111-1111-111111111111");
        if (targetType == typeof(int))
            return 7;
        if (targetType == typeof(long))
            return 42L;
        if (targetType == typeof(float))
            return 3.14f;
        if (targetType == typeof(double))
            return 3.14d;
        if (targetType == typeof(decimal))
            return 3.14m;
        if (targetType == typeof(bool))
            return true;
        if (targetType == typeof(DateTime))
            return new DateTime(2026, 04, 27, 12, 0, 0, DateTimeKind.Utc);
        if (targetType == typeof(DateTimeOffset))
            return new DateTimeOffset(2026, 04, 27, 12, 0, 0, TimeSpan.Zero);
        if (targetType == typeof(Uri))
            return "https://example.test/response";
        if (targetType.IsEnum)
            return Enum.GetValues(targetType).GetValue(0);

        if (targetType.IsGenericType)
        {
            return ResponseGenericValue(targetType, name, depth);
        }

        if (targetType.IsArray)
        {
            var elementType = targetType.GetElementType() ?? typeof(object);
            return new[] { CollectionItemValue(elementType) };
        }

        if (
            typeof(System.Collections.IEnumerable).IsAssignableFrom(targetType)
            && targetType != typeof(string)
        )
        {
            return Array.Empty<object>();
        }

        if (
            targetType.IsClass
            && targetType != typeof(object)
            && !targetType.IsAbstract
            && depth < 1
        )
        {
            return ResponseObjectValue(targetType, depth);
        }

        return new Dictionary<string, object?>();
    }

    private static Dictionary<string, object?> ResponseStatusValue()
    {
        return new Dictionary<string, object?>
        {
            ["isSuccess"] = true,
            ["errors"] = Array.Empty<object>(),
        };
    }

    private static string ResponseStringValue(string name)
    {
        var jsonName = JsonNamingPolicy.CamelCase.ConvertName(name);
        if (
            name.Contains("Url", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Uri", StringComparison.OrdinalIgnoreCase)
        )
        {
            return $"https://example.test/{jsonName}";
        }

        if (name.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            return "user@example.test";
        }

        return name.EndsWith("Id", StringComparison.Ordinal)
            ? "11111111-1111-1111-1111-111111111111"
            : $"test-{jsonName}";
    }

    private static object? ResponseGenericValue(Type targetType, string name, int depth)
    {
        if (targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return ResponseValue(targetType.GetGenericArguments()[0], name, depth);
        }

        if (targetType.GetGenericTypeDefinition() == typeof(IReadOnlySet<>))
        {
            return null;
        }

        if (targetType.Name.StartsWith("PaginatedResponse", StringComparison.Ordinal))
        {
            return new Dictionary<string, object?>
            {
                ["items"] = new[] { CollectionItemValue(targetType.GetGenericArguments()[0]) },
                ["hasMore"] = false,
                ["hasPrevious"] = false,
                ["startingAfter"] = "cursor-start",
                ["endingBefore"] = "cursor-end",
            };
        }

        var genericType = targetType.GetGenericTypeDefinition();
        if (
            genericType == typeof(List<>)
            || genericType == typeof(IList<>)
            || genericType == typeof(IReadOnlyList<>)
            || genericType == typeof(IEnumerable<>)
        )
        {
            return new[] { CollectionItemValue(targetType.GetGenericArguments()[0]) };
        }

        if (
            genericType == typeof(Dictionary<,>)
            || genericType == typeof(IDictionary<,>)
            || genericType == typeof(IReadOnlyDictionary<,>)
        )
        {
            var args = targetType.GetGenericArguments();
            return new Dictionary<string, object?>
            {
                [DictionaryKeyValue(args[0])] = ResponseValue(args[1], args[1].Name, depth + 1),
            };
        }

        return null;
    }

    private static Dictionary<string, object?> ResponseObjectValue(Type targetType, int depth)
    {
        return targetType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(prop => prop.CanWrite && prop.GetIndexParameters().Length == 0)
            .Take(8)
            .ToDictionary(
                prop => JsonNamingPolicy.CamelCase.ConvertName(prop.Name),
                prop => ResponseValue(prop.PropertyType, prop.Name, depth + 1),
                StringComparer.Ordinal
            );
    }

    private static object? CollectionItemValue(Type elementType)
    {
        var targetType = Nullable.GetUnderlyingType(elementType) ?? elementType;
        if (targetType == typeof(object))
        {
            return new Dictionary<string, object?>
            {
                ["id"] = "11111111-1111-1111-1111-111111111111",
                ["name"] = "test-item",
            };
        }

        return ResponseValue(targetType, targetType.Name, 1);
    }

    private static string DictionaryKeyValue(Type keyType)
    {
        var targetType = Nullable.GetUnderlyingType(keyType) ?? keyType;
        if (targetType == typeof(string))
            return "sample-key";
        if (targetType == typeof(Guid))
            return "11111111-1111-1111-1111-111111111111";
        if (targetType == typeof(int) || targetType == typeof(long))
            return "1";
        if (targetType.IsEnum)
            return Enum.GetNames(targetType).FirstOrDefault() ?? "0";
        return "sample-key";
    }

    private static string ToPascal(string value)
    {
        return string.Concat(
            value
                .Split('_')
                .Select(part =>
                    part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]
                )
        );
    }
}

internal sealed record EndpointCoverageResult(string SnapshotFileName, object Snapshot);
