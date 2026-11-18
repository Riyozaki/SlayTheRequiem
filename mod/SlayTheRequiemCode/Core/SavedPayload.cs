using System.Text.Json;

namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// Lossless model-snapshot comparison, not SerializableCard.Equals (which ignores Props/floor).
/// Object ordering and SavedProperties' named-entry ordering are not state. All other arrays, unknown fields,
/// amounts and nested card contents ARE state. An unrepresentable field keeps the opaque chest source intact.
/// </summary>
public static class SavedPayload
{
    private static readonly HashSet<string> NamedPropertyLists = new(StringComparer.Ordinal)
        { "ints", "bools", "strings", "int_arrays", "model_ids", "cards", "card_arrays" };

    public static bool Equivalent(string expected, string actual) => Compare(expected, actual, allowAddedDefaults: false);

    /// <summary>
    /// Directional migration: old snapshots predate newly registered SpireFields. Permit ADDED default named
    /// fields only; never a dropped/changed field, even an unknown field whose saved value happens to be zero.
    /// This is not a general merge, and never ignores arbitrary arrays or newly invented model/enchantment data.
    /// </summary>
    public static bool Preserves(string expected, string actual) => Compare(expected, actual, allowAddedDefaults: true);

    private static bool Compare(string expected, string actual, bool allowAddedDefaults)
    {
        using var a = JsonDocument.Parse(expected);
        using var b = JsonDocument.Parse(actual);
        return Compare(a.RootElement, b.RootElement, allowAddedDefaults);
    }

    private static bool Compare(JsonElement expected, JsonElement actual, bool allowAddedDefaults,
        bool savedProperties = false, string? namedList = null)
    {
        // Explicit null props occur in old/manual JSON; current game serialization normally omits them.
        if (allowAddedDefaults && savedProperties && expected.ValueKind == JsonValueKind.Null && DefaultProps(actual)) return true;
        if (expected.ValueKind != actual.ValueKind) return false;
        if (expected.ValueKind == JsonValueKind.Object)
        {
            var source = expected.EnumerateObject().ToList();
            var destination = actual.EnumerateObject().ToList();
            if (!UniqueObject(source) || !UniqueObject(destination)
                || savedProperties && (!UniquePropertyNames(source) || !UniquePropertyNames(destination))) return false;
            foreach (var property in source)
                if (!actual.TryGetProperty(property.Name, out var value)
                    || !Compare(property.Value, value, allowAddedDefaults, property.Name == "props",
                        savedProperties && NamedPropertyLists.Contains(property.Name) ? property.Name : null)) return false;
            foreach (var added in destination.Where(p => !expected.TryGetProperty(p.Name, out _)))
            {
                if (!allowAddedDefaults) return false;
                if (added.Name == "props" && DefaultProps(added.Value)) continue;
                if (savedProperties && NamedPropertyLists.Contains(added.Name) && DefaultNamedList(added.Value, added.Name)) continue;
                return false;
            }
            return true;
        }
        if (expected.ValueKind == JsonValueKind.Array)
        {
            var source = expected.EnumerateArray().ToList();
            var destination = actual.EnumerateArray().ToList();
            if (namedList == null)
                return source.Count == destination.Count && source.Zip(destination).All(p => Compare(p.First, p.Second, allowAddedDefaults));
            if (!UniqueEntries(source) || !UniqueEntries(destination)) return false;
            return source.All(s => destination.Any(d => Name(s) == Name(d) && Compare(s, d, allowAddedDefaults)))
                && destination.Where(d => source.All(s => Name(s) != Name(d)))
                    .All(d => allowAddedDefaults && DefaultNamedEntry(d, namedList));
        }
        return expected.ValueKind == JsonValueKind.String
            ? expected.GetString() == actual.GetString() : expected.GetRawText() == actual.GetRawText();
    }

    private static bool UniqueObject(List<JsonProperty> properties) =>
        properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() == properties.Count;

    private static string? Name(JsonElement entry) => entry.ValueKind == JsonValueKind.Object
        && entry.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;

    private static bool UniqueEntries(List<JsonElement> entries) => entries.All(e => !string.IsNullOrEmpty(Name(e)))
        && entries.Select(Name).Distinct(StringComparer.Ordinal).Count() == entries.Count;

    private static bool UniquePropertyNames(List<JsonProperty> properties)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in properties.Where(p => NamedPropertyLists.Contains(p.Name)))
        {
            if (property.Value.ValueKind != JsonValueKind.Array) return false;
            foreach (var entry in property.Value.EnumerateArray())
                if (Name(entry) is not { Length: > 0 } name || !names.Add(name)) return false;
        }
        return true;
    }

    private static bool DefaultProps(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return false;
        var properties = value.EnumerateObject().ToList();
        return UniqueObject(properties) && UniquePropertyNames(properties)
            && properties.All(p => NamedPropertyLists.Contains(p.Name) && DefaultNamedList(p.Value, p.Name));
    }

    private static bool DefaultNamedList(JsonElement value, string kind) => value.ValueKind == JsonValueKind.Array
        && value.EnumerateArray().All(e => DefaultNamedEntry(e, kind));

    private static bool DefaultNamedEntry(JsonElement entry, string kind) => entry.ValueKind == JsonValueKind.Object
        && entry.EnumerateObject().Count() == 2 && !string.IsNullOrEmpty(Name(entry))
        && entry.TryGetProperty("value", out var value) && (kind switch
        {
            "ints" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n) && n == 0,
            "bools" => value.ValueKind == JsonValueKind.False,
            "strings" => value.ValueKind == JsonValueKind.String && value.GetString() == "",
            "int_arrays" or "card_arrays" => value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0,
            "model_ids" or "cards" => value.ValueKind == JsonValueKind.Null,
            _ => false,
        });
}
