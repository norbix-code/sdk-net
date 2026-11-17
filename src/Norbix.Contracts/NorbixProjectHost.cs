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

    private static int Base62Digit(char ch) => ch switch
    {
        >= '0' and <= '9' => ch - '0',
        >= 'A' and <= 'Z' => ch - 'A' + 10,
        >= 'a' and <= 'z' => ch - 'a' + 36,
        _ => -1,
    };
}
