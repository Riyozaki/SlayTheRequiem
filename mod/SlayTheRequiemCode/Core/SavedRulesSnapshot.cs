namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Read-only selection of shared rules. Called before the game increments/writes a loaded run's counter.</summary>
public sealed record SavedRulesSnapshot(RunSettingsData? Rules, bool MissingCopies)
{
    public static SavedRulesSnapshot Read(IReadOnlyList<string?> grimoireCodes)
    {
        if (grimoireCodes.Count == 0) return new(null, false); // a vanilla/disabled run must stay disabled
        var decoded = new List<RunSettingsData>();
        foreach (var code in grimoireCodes.Where(c => !string.IsNullOrEmpty(c)))
            decoded.Add(RunSettingsData.Decode(code)
                ?? throw new FormatException("Invalid or newer saved Requiem rules; no local-menu fallback"));
        if (decoded.Count == 0) throw new FormatException("Legacy Requiem save has no reconstructible rules snapshot");
        if (decoded.Select(s => s.Encode()).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new FormatException("Saved Requiem snapshots disagree between players");
        return new(decoded[0], decoded.Count != grimoireCodes.Count);
    }
}
