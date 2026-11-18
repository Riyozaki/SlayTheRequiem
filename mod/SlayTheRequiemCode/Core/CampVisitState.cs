namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Visit identity survives Continue. Re-entering the same camp must not refill points or one-shot actions.</summary>
public sealed record CampVisitState(string Key, int Points, bool ArtisanUsed, string Completed)
{
    public static string Encode(IEnumerable<string> options) => string.Join(",", options.Where(s => s.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    public IReadOnlySet<string> CompletedOptions => Completed.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
    public CampVisitState Enter(string visit, int budget, IReadOnlyCollection<string> nativeHistory)
    {
        if (visit == Key || Key.Length == 0 && nativeHistory.Count > 0)
            return this with { Key = visit, Completed = Encode(CompletedOptions.Concat(nativeHistory)) };
        return new(visit, budget, false, "");
    }
    public CampVisitState Complete(string option) => this with { Completed = Encode(CompletedOptions.Append(option)) };
}
