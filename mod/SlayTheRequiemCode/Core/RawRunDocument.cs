using System.Text.Json;

namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// The native save object is NOT a lossless backup: its serializer discards fields unknown to this game version.
/// Inspect the original document before native migration/deserialization; refuse unknown top-level fields rather
/// than silently resaving them away. This does not claim to migrate another mod's undocumented native schema.
/// </summary>
public static class RawRunDocument
{
    public const int MaximumBytes = 32 * 1024 * 1024;

    public static void Validate(ReadOnlyMemory<byte> original, IReadOnlySet<string> knownFields, int latestNativeSchema)
    {
        if (original.Length == 0 || original.Length > MaximumBytes) throw new FormatException("Run document is empty or too large; original retained");
        // JSON does not admit a BOM, but some older text writers added one. It stays in the exact-byte backup.
        var json = original.Span.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? original[3..] : original;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new FormatException("Run document is not an object; original retained");
        RequireUniqueFields(root);
        foreach (var field in root.EnumerateObject())
            if (!knownFields.Contains(field.Name))
                throw new FormatException($"Unknown native run field '{field.Name}'; use the matching game/mod version. Original retained");
        if (!root.TryGetProperty("schema_version", out var schema) || schema.ValueKind != JsonValueKind.Number
            || !schema.TryGetInt32(out var version) || version <= 0 || version > latestNativeSchema)
            throw new FormatException("Missing/future native run schema; no lossy native recovery or save rewrite permitted");
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
