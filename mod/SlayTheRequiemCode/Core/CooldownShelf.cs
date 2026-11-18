namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// A physical card belongs to one combat shelf, not a class-name/cooldown slot. Tick never consumes a due entry:
/// only an observed return (or an explicit external move/cleanup) completes it. Comparisons are reference identity.
/// This state is combat-local and is never serialized into the next combat/run.
/// </summary>
public sealed class CooldownShelf<TCard, TCombat> where TCard : class where TCombat : class
{
    public sealed class Entry(TCard card, TCombat combat, int turns)
    {
        public TCard Card { get; } = card;
        public TCombat Combat { get; } = combat;
        public int Turns { get; internal set; } = Math.Max(1, turns);
    }

    private readonly List<Entry> _entries = new();
    public int Count => _entries.Count;
    public int NextTurns => _entries.Count == 0 ? 0 : _entries.Min(e => e.Turns);
    public bool Contains(TCard card) => _entries.Any(e => ReferenceEquals(e.Card, card));

    public bool Put(TCard card, TCombat combat, int turns)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(combat);
        if (Contains(card)) return false;
        _entries.Add(new(card, combat, turns));
        return true;
    }

    /// <summary>A failed return stays due at zero. A different combat never returns an earlier combat's card.</summary>
    public IReadOnlyList<Entry> Tick(TCombat combat)
    {
        ArgumentNullException.ThrowIfNull(combat);
        _entries.RemoveAll(e => !ReferenceEquals(e.Combat, combat));
        foreach (var entry in _entries) entry.Turns = Math.Max(0, entry.Turns - 1);
        return _entries.Where(e => e.Turns == 0).ToList();
    }

    public bool Complete(TCard card) => _entries.RemoveAll(e => ReferenceEquals(e.Card, card)) > 0;
    public void Clear() => _entries.Clear();
}
