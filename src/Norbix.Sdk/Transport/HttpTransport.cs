using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

using Norbix.Sdk.Types;

namespace Norbix.Sdk.Transport;

/// <summary>
/// Internal native-<see cref="HttpClient"/> transport. Not part of the
/// public surface — consumers see only <see cref="NorbixClient"/>.
/// </summary>
internal sealed class HttpTransport : INorbixTransport, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly NorbixClientOptions _options;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    public HttpTransport(NorbixClientOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _http = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;
        _http.Timeout = _options.Timeout;
    }

    public async Task<TResponse?> SendAsync<TResponse>(
        NorbixRequestSpec spec,
        CancellationToken cancellationToken = default)
    {
        if (spec.Scope == NorbixScope.Account && string.IsNullOrEmpty(_options.AccountId))
        {
            throw new NorbixException(
                "This endpoint is account-scoped. Set AccountId on NorbixClientOptions before calling it.",
                code: NorbixErrorCodes.AccountScopeRequired);
        }

        var (url, body) = BuildUrlAndBody(spec);

        using var request = new HttpRequestMessage(new HttpMethod(spec.Method), url);

        if (spec.Scope != NorbixScope.Unauthenticated)
        {
            var token = !string.IsNullOrEmpty(_options.BearerToken)
                ? _options.BearerToken
                : _options.ApiKey;
            if (string.IsNullOrEmpty(token))
            {
                throw new NorbixException(
                    "Norbix is not authenticated. Provide ApiKey, BearerToken, or call LoginAsync(...) first.",
                    code: NorbixErrorCodes.NotAuthenticated,
                    url: url);
            }
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (!string.IsNullOrEmpty(_options.ProjectId))
        {
            request.Headers.TryAddWithoutValidation("X-CM-ProjectId", _options.ProjectId);
        }
        if (!string.IsNullOrEmpty(_options.AccountId))
        {
            request.Headers.TryAddWithoutValidation("X-CM-AccountId", _options.AccountId);
        }

        foreach (var (k, v) in _options.DefaultHeaders)
        {
            request.Headers.TryAddWithoutValidation(k, v);
        }

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new NorbixException(
                $"Request timed out after {_options.Timeout.TotalMilliseconds}ms",
                code: NorbixErrorCodes.NetworkError,
                url: url,
                innerException: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new NorbixException(
                ex.Message,
                code: NorbixErrorCodes.NetworkError,
                url: url,
                innerException: ex);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            ms.Position = 0;
            throw await BuildExceptionAsync(ms, (int)response.StatusCode, url, cancellationToken)
                .ConfigureAwait(false);
        }

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return default;
        }

        if (typeof(TResponse) == typeof(EmptyResponse))
        {
            return default;
        }

        if (response.Content.Headers.ContentLength == 0)
        {
            return default;
        }

        try
        {
            return await JsonSerializer.DeserializeAsync<TResponse>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new NorbixException(
                "Failed to deserialize response from Norbix gateway.",
                statusCode: (int)response.StatusCode,
                url: url,
                innerException: ex);
        }
    }

    private static async Task<NorbixException> BuildExceptionAsync(
        Stream body,
        int statusCode,
        string url,
        CancellationToken cancellationToken)
    {
        ResponseStatus? status = null;
        object? raw = null;
        try
        {
            raw = await JsonSerializer.DeserializeAsync<JsonElement>(body, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (raw is JsonElement el && el.ValueKind == JsonValueKind.Object)
            {
                if (el.TryGetProperty("responseStatus", out var rs))
                {
                    status = rs.Deserialize<ResponseStatus>(JsonOptions);
                }
                else if (el.TryGetProperty("ResponseStatus", out var rs2))
                {
                    status = rs2.Deserialize<ResponseStatus>(JsonOptions);
                }
                else
                {
                    status = el.Deserialize<ResponseStatus>(JsonOptions);
                }
            }
        }
        catch
        {
            // Best-effort; non-JSON bodies fall back to a generic message.
        }

        return new NorbixException(
            status?.Message ?? $"Request failed with status {statusCode}",
            statusCode: statusCode,
            code: status?.ErrorCode,
            fieldErrors: status?.Errors,
            url: url,
            rawBody: raw);
    }

    private (string Url, string? Body) BuildUrlAndBody(in NorbixRequestSpec spec)
    {
        var baseUrl = spec.Target == NorbixTarget.Api ? _options.ApiBaseUrl : _options.HubBaseUrl;
        var version = spec.Target == NorbixTarget.Api ? _options.ApiVersion : _options.HubVersion;

        var path = spec.Path.Replace("{version}", Uri.EscapeDataString(version), StringComparison.Ordinal);
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        path = ReplaceTokens(path, spec.Request, consumed);

        if (spec.PathParams is not null)
        {
            foreach (var p in spec.PathParams) consumed.Add(p);
        }

        var url = JoinUrl(baseUrl, path);

        if (spec.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) ||
            spec.Method.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            var qs = ToQueryString(spec.Request, consumed);
            return (string.IsNullOrEmpty(qs) ? url : $"{url}?{qs}", null);
        }

        var body = SerializeBodyOrNull(spec.Request, consumed);
        return (url, body);
    }

    private static string ReplaceTokens(string path, object? request, HashSet<string> consumed)
    {
        if (request is null) return path;

        var props = GetWritableProperties(request.GetType());
        return System.Text.RegularExpressions.Regex.Replace(
            path,
            @"\{([^/{}]+)\}",
            match =>
            {
                var token = match.Groups[1].Value;
                if (token.Equals("version", StringComparison.OrdinalIgnoreCase)) return match.Value;
                if (!props.TryGetValue(token, out var prop) || prop.GetValue(request) is null)
                {
                    throw new NorbixException(
                        $"Missing path parameter \"{token}\" for {path}",
                        code: NorbixErrorCodes.MissingPathParam);
                }
                consumed.Add(prop.Name);
                return Uri.EscapeDataString(prop.GetValue(request)!.ToString()!);
            });
    }

    private static string ToQueryString(object? request, HashSet<string> consumed)
    {
        if (request is null) return string.Empty;
        var props = GetWritableProperties(request.GetType());
        var parts = new List<string>();
        foreach (var (_, prop) in props)
        {
            if (consumed.Contains(prop.Name)) continue;
            var v = prop.GetValue(request);
            if (v is null) continue;
            var key = HttpUtility.UrlEncode(JsonNamingPolicy.CamelCase.ConvertName(prop.Name));
            switch (v)
            {
                case string s:
                    parts.Add($"{key}={HttpUtility.UrlEncode(s)}");
                    break;
                case System.Collections.IEnumerable e and not string:
                    foreach (var item in e)
                        if (item is not null)
                            parts.Add($"{key}={HttpUtility.UrlEncode(item.ToString())}");
                    break;
                case DateTime dt:
                    parts.Add($"{key}={HttpUtility.UrlEncode(dt.ToString("o", System.Globalization.CultureInfo.InvariantCulture))}");
                    break;
                default:
                    parts.Add($"{key}={HttpUtility.UrlEncode(v.ToString())}");
                    break;
            }
        }
        return string.Join('&', parts);
    }

    private static string? SerializeBodyOrNull(object? request, HashSet<string> consumed)
    {
        if (request is null) return null;
        var props = GetWritableProperties(request.GetType());
        var dict = new Dictionary<string, object?>();
        foreach (var (_, prop) in props)
        {
            if (consumed.Contains(prop.Name)) continue;
            var v = prop.GetValue(request);
            if (v is null) continue;
            dict[JsonNamingPolicy.CamelCase.ConvertName(prop.Name)] = v;
        }
        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict, JsonOptions);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> PropertyCache = new();

    private static IReadOnlyDictionary<string, PropertyInfo> GetWritableProperties(Type t)
    {
        return PropertyCache.GetOrAdd(t, type =>
        {
            var dict = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                dict[p.Name] = p;
            }
            return dict;
        });
    }

    private static string JoinUrl(string baseUrl, string path)
    {
        var b = baseUrl.EndsWith('/') ? baseUrl[..^1] : baseUrl;
        var p = path.StartsWith('/') ? path : "/" + path;
        return b + p;
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _http.Dispose();
    }
}
