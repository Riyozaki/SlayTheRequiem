using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// The native save object is NOT a lossless backup: its serializer discards fields unknown to this game version.
/// Inspect the original document before native migration/deserialization; refuse unknown top-level fields rather
/// than silently resaving them away. This does not claim to migrate another mod's undocumented native schema.
/// </summary>
public static class RawRunDocument
{
    public const int MaximumBytes = 32 * 1024 * 1024;

    // JSON does not admit a BOM, but some older text writers added one. It stays in the exact-byte backup.
    private static ReadOnlyMemory<byte> WithoutBom(ReadOnlyMemory<byte> original) =>
        original.Span.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? original[3..] : original;

    /// <summary>Also recognizes escaped identifiers; inspect BOTH primary and native backup before choosing a reader.</summary>
    public static bool Mentions(ReadOnlyMemory<byte> original, string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) throw new ArgumentException("Empty save identifier", nameof(identifier));
        if (Encoding.UTF8.GetString(original.Span).Contains(identifier, StringComparison.OrdinalIgnoreCase)) return true;
        try
        {
            using var document = JsonDocument.Parse(WithoutBom(original));
            return Contains(document.RootElement);
            bool Contains(JsonElement node) => node.ValueKind switch
            {
                JsonValueKind.String => node.GetString()?.Contains(identifier, StringComparison.OrdinalIgnoreCase) == true,
                JsonValueKind.Array => node.EnumerateArray().Any(Contains),
                JsonValueKind.Object => node.EnumerateObject().Any(p => p.Name.Contains(identifier, StringComparison.OrdinalIgnoreCase) || Contains(p.Value)),
                _ => false,
            };
        }
        catch (JsonException) { return false; } // malformed native text can still be identified by the literal fast path
    }

    public static int Validate(ReadOnlyMemory<byte> original, IReadOnlySet<string> knownFields, int latestNativeSchema)
    {
        if (original.Length == 0 || original.Length > MaximumBytes) throw new FormatException("Run document is empty or too large; original retained");
        using var doc = JsonDocument.Parse(WithoutBom(original));
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new FormatException("Run document is not an object; original retained");
        RequireUniqueFields(root);
        foreach (var field in root.EnumerateObject())
            if (!knownFields.Contains(field.Name))
                throw new FormatException($"Unknown native run field '{field.Name}'; use the matching game/mod version. Original retained");
        if (!root.TryGetProperty("schema_version", out var schema) || schema.ValueKind != JsonValueKind.Number
            || !schema.TryGetInt32(out var version) || version <= 0 || version > latestNativeSchema)
            throw new FormatException("Missing/future native run schema; no lossy native recovery or save rewrite permitted");
        return version;
    }

    /// <summary>
    /// Preflight with the game's actual serializer/converters, without invoking the native repair/rename reader.
    /// Unmapped typed fields (including nested ones) fail rather than silently disappearing on the next save.
    /// A custom converter/extension-data field retains its own contract; no arbitrary opaque migration is claimed.
    /// </summary>
    public static T DeserializeStrict<T>(ReadOnlyMemory<byte> original, JsonSerializerOptions options) where T : class
    {
        if (original.Length == 0 || original.Length > MaximumBytes) throw new FormatException("Run document is empty or too large; original retained");
        var strict = new JsonSerializerOptions(options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        return JsonSerializer.Deserialize<T>(WithoutBom(original).Span, strict) ?? throw new FormatException("Empty run object; original retained");
    }

    private static void RequireUniqueFields(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var fields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in node.EnumerateObject())
            {
                if (!fields.Add(property.Name)) throw new FormatException($"Duplicate JSON field '{property.Name}'; original retained");
                RequireUniqueFields(property.Value);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var element in node.EnumerateArray()) RequireUniqueFields(element);
    }
}
