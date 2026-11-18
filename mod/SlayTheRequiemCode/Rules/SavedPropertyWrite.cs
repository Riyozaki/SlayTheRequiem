using MegaCrit.Sts2.Core.Saves.Runs;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Change only named fields this operation owns. Rebuilding Props via From(model) would silently drop unknown
/// SpireFields carried by a saved checkpoint. Keep every unrelated list/entry and reject ambiguous owned fields.
/// </summary>
internal static class SavedPropertyWrite
{
    internal static void Int(SavedProperties props, string name, int value) =>
        props.ints = Put(props.ints, name, value);
    internal static void Bool(SavedProperties props, string name, bool value) =>
        props.bools = Put(props.bools, name, value);
    internal static void Text(SavedProperties props, string name, string value) =>
        props.strings = Put(props.strings, name, value);

    private static List<SavedProperties.SavedProperty<T>> Put<T>(List<SavedProperties.SavedProperty<T>>? entries, string name, T value)
    {
        entries ??= new();
        var indices = entries.Select((entry, index) => (entry, index)).Where(p => p.entry.name == name).Select(p => p.index).ToList();
        if (indices.Count > 1) throw new FormatException($"Ambiguous saved field {name}; checkpoint not rewritten");
        var replacement = new SavedProperties.SavedProperty<T>(name, value);
        if (indices.Count == 1) entries[indices[0]] = replacement;
        else entries.Add(replacement);
        return entries;
    }
}
