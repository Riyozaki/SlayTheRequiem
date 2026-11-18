using System.Globalization;
using System.Text.Json;

namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// A one-time SOURCE, not a card type, owns an acquisition claim. Retain completed sources after the card was
/// removed/upgraded/obtain-transformed, otherwise a save inside the callback could issue it a second time.
/// Unknown versions/extra fields are refused, not rewritten into a lossy interpretation.
/// </summary>
public static class MandatoryGrantLedger
{
    public const int Version = 1;
    public const int MaxEntries = 4096;
    public static void ValidateSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > 512) throw new FormatException("Invalid mandatory grant source");
    }
    private static bool ValidClaim(string claim) => claim.StartsWith("q:", StringComparison.Ordinal)
        && int.TryParse(claim.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
        && claim == "q:" + value.ToString(CultureInfo.InvariantCulture);

    public static Dictionary<string, string> Read(string text)
    {
        var grants = new Dictionary<string, string>(StringComparer.Ordinal);
        if (text.Length == 0) return grants;
        if (text.Length > 2 * 1024 * 1024) throw new FormatException("Mandatory grant journal too large; original data retained");
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2
            || !root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var v) || v != Version
            || !root.TryGetProperty("sources", out var sources) || sources.ValueKind != JsonValueKind.Object)
            throw new FormatException("Unknown mandatory grant journal; original data retained");
        var seenClaims = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources.EnumerateObject())
        {
            ValidateSource(source.Name);
            if (source.Value.ValueKind != JsonValueKind.String || source.Value.GetString() is not { } claim || !ValidClaim(claim)
                || !grants.TryAdd(source.Name, claim) || !seenClaims.Add(claim) || grants.Count > MaxEntries)
                throw new FormatException("Ambiguous mandatory grant source/claim; original data retained");
        }
        return grants;
    }

    public static string Write(IReadOnlyDictionary<string, string> sources)
    {
        if (sources.Count > MaxEntries) throw new FormatException("Mandatory grant journal capacity reached");
        if (sources.Values.Distinct(StringComparer.Ordinal).Count() != sources.Count) throw new FormatException("Shared mandatory grant claim");
        foreach (var (source, claim) in sources)
        {
            ValidateSource(source);
            if (!ValidClaim(claim)) throw new FormatException("Invalid mandatory grant claim");
        }
        var text = JsonSerializer.Serialize(new { version = Version, sources = sources.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal) });
        if (text.Length > 2 * 1024 * 1024) throw new FormatException("Mandatory grant journal capacity reached");
        return text;
    }
}
