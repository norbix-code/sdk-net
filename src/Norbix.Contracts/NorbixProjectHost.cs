namespace Norbix.Sdk;

/// <summary>
/// A project's own API host. Norbix serves every project on
/// <c>pr-&lt;32 hex&gt;.api.norbix.ai</c>. The label is the project id
/// (<c>pr_</c> + base62) written as the hex of the same GUID, so it is built
/// here with no call. The gateway's side of this is <c>ProjectId.HostLabel</c>.
/// </summary>
public static class NorbixProjectHost
{
    private const string ProjectIdPrefix = "pr_";

    // 62^22 is just above 2^128: a GUID never needs more than 22 digits.
    private const int MaxDigits = 22;

    // .NET "N" format: the first three groups are stored little-endian.
    private static readonly int[] HexOrder = [3, 2, 1, 0, 5, 4, 7, 6, 8, 9, 10, 11, 12, 13, 14, 15];

    /// <summary>
    /// The DNS label of a project: <c>pr_5XvmrTA6JVggZdmEE5bGdm</c> →
    /// <c>pr-022c4aca9f9e4df787ab0a20c83c44b6</c>. Returns <c>null</c> for
    /// anything that is not a Norbix project id (a test id, another prefix, a
    /// value that is not a version-4 GUID) — it never throws.
    /// </summary>
    public static string? Label(string? projectId)
    {
        if (projectId is null || !projectId.StartsWith(ProjectIdPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var digits = projectId.AsSpan(ProjectIdPrefix.Length);
        if (digits.Length == 0 || digits.Length > MaxDigits)
        {
            return null;
        }

        // The base-62 number (most significant digit first) is the GUID's
        // bytes, least significant first — .NET's Guid.ToByteArray().
        Span<byte> b = stackalloc byte[16];
        b.Clear();
        foreach (var ch in digits)
        {
            var digit = Base62Digit(ch);
            if (digit < 0)
            {
                return null;
            }

            var carry = digit;
            for (var i = 0; i < b.Length; i++)
            {
                var v = b[i] * 62 + carry;
                b[i] = (byte)v;
                carry = v >> 8;
            }

            if (carry != 0)
            {
                return null; // 2^128 or more
            }
        }

        // Only a version-4 GUID is a Norbix id (the gateway refuses the rest).
        if (b[7] >> 4 != 4 || b[8] >> 6 != 2)
        {
            return null;
        }

        Span<char> label = stackalloc char[3 + 32];
        label[0] = 'p';
        label[1] = 'r';
        label[2] = '-';
        const string hex = "0123456789abcdef";
        for (var i = 0; i < HexOrder.Length; i++)
        {
            var value = b[HexOrder[i]];
            label[3 + i * 2] = hex[value >> 4];
            label[4 + i * 2] = hex[value & 0xF];
        }

        return new string(label);
    }

    /// <summary>
    /// The placeholder an install's address template carries
    /// (<c>GET /v3/echo</c> → <c>apiUrlTemplate</c>), e.g.
    /// <c>https://{projectHostLabel}.api.customer.com</c>.
    /// </summary>
    public const string LabelPlaceholder = "{projectHostLabel}";

    private const string NorbixApiDomain = "api.norbix.ai";

    /// <summary>
    /// Where a project's API calls go, from the API base URL the client has
    /// (devops docs/tasks/clients-project-host.md, round 2):
    /// <list type="number">
    ///   <item><description>a template with <see cref="LabelPlaceholder"/> (an
    ///   install on its own domain that serves project hosts) is filled with the
    ///   project's label; <c>Shared</c> is the template without
    ///   <c>{projectHostLabel}.</c>;</description></item>
    ///   <item><description>an address that is already a project's host
    ///   (<c>https://pr-&lt;32 hex&gt;.…</c>, a pasted <c>apiHost</c>) gets the
    ///   current project's label, so host and <c>norbix-project-id</c> always
    ///   name the same project; <c>Shared</c> drops the label;</description></item>
    ///   <item><description>a norbix.ai API address over https —
    ///   <c>https://api.norbix.ai</c> or <c>https://&lt;region&gt;.api.norbix.ai</c>,
    ///   built in or written out — becomes <c>https://pr-&lt;hex&gt;.api.norbix.ai</c>
    ///   (its DNS record follows the project's region); <c>Shared</c> is the
    ///   address as given;</description></item>
    ///   <item><description>any other address (staging, localhost, a plain own
    ///   domain, <c>http://</c>) is used as given for both.</description></item>
    /// </list>
    /// The social sign-in link is built on <c>Shared</c>. With a project id
    /// that is not a Norbix id, <c>Project</c> is <c>Shared</c>. Scheme, port
    /// and path are kept as given (no trailing slash is added). Never throws.
    /// </summary>
    public static (string Project, string Shared) ApiBaseUrl(string baseUrl, string? projectId)
    {
        baseUrl ??= string.Empty;
        var label = Label(projectId);

        if (baseUrl.Contains(LabelPlaceholder, StringComparison.Ordinal))
        {
            var shared = baseUrl.Replace(LabelPlaceholder + ".", string.Empty, StringComparison.Ordinal);
            return (label is null ? shared : baseUrl.Replace(LabelPlaceholder, label, StringComparison.Ordinal), shared);
        }

        if (!TryFindHost(baseUrl, out var hostStart, out var hostEnd))
        {
            return (baseUrl, baseUrl);
        }

        var host = baseUrl[hostStart..hostEnd].ToLowerInvariant();
        var labels = host.Split('.');

        if (labels.Length > 1 && IsProjectLabel(labels[0]))
        {
            var rest = host[(labels[0].Length + 1)..];
            var shared = WithHost(baseUrl, hostStart, hostEnd, rest);
            return (label is null ? shared : WithHost(baseUrl, hostStart, hostEnd, label + "." + rest), shared);
        }

        var isNorbixApi = host == NorbixApiDomain
            || (labels.Length == 4 && host.EndsWith("." + NorbixApiDomain, StringComparison.Ordinal));
        if (isNorbixApi && baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && label is not null)
        {
            return (WithHost(baseUrl, hostStart, hostEnd, label + "." + NorbixApiDomain), baseUrl);
        }

        return (baseUrl, baseUrl);
    }

    /// <summary>The host name's span in an absolute <c>scheme://[user@]host[:port][/…]</c> URL.</summary>
    private static bool TryFindHost(string url, out int start, out int end)
    {
        start = end = 0;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return false;
        }

        var authorityStart = schemeEnd + 3;
        var authorityEnd = url.IndexOfAny(['/', '?', '#'], authorityStart);
        if (authorityEnd < 0)
        {
            authorityEnd = url.Length;
        }

        var at = url.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart);
        start = at < 0 ? authorityStart : at + 1;
        if (start < url.Length && url[start] == '[')
        {
            return false; // an IPv6 literal is never a project or norbix.ai host
        }

        var colon = url.IndexOf(':', start, authorityEnd - start);
        end = colon < 0 ? authorityEnd : colon;
        return end > start;
    }

    private static string WithHost(string url, int start, int end, string host) =>
        string.Concat(url.AsSpan(0, start), host, url.AsSpan(end));

    // pr-<32 hex>, any case.
    private static bool IsProjectLabel(string value)
    {
        if (value.Length != 35 || !value.StartsWith("pr-", StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 3; i < value.Length; i++)
        {
            if (!char.IsAsciiHexDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static int Base62Digit(char ch) => ch switch
    {
        >= '0' and <= '9' => ch - '0',
        >= 'A' and <= 'Z' => ch - 'A' + 10,
        >= 'a' and <= 'z' => ch - 'a' + 36,
        _ => -1,
    };
}
