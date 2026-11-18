namespace SlayTheRequiem.SlayTheRequiemCode.Core;

public enum WorldEaterMove { Wings, Bite, Breath, Meteors }
public enum BoilerMove { Bite, Coil, Release }

/// <summary>Finite, visible encounter counterplay. Runtime commands and previews share these policies.
/// Tests of these rules do not execute native monster hooks or prove that a fight is balanced.</summary>
public static class BossCounterplayRules
{
    public const int ValveClosedTurns = 2;
    public const int GroundedTurns = 2;
    public const int SummonerEggs = 3;
    public const int SummonerCultists = 2;
    public const int SummonerAuraTier = -1;
    public const int WorldEaterCycleLength = 4;
    public const int CounterShoutCooldown = 3;
    public const int BindingMaxStacks = 3;
    public const int CocoonGuards = 3;
    public const decimal AerialMeleeMultiplier = 0.5m;

    public static decimal FlightMultiplier(bool powered, bool spell, bool ranged, DamageType type) =>
        powered && !spell && !ranged && type == DamageType.Physical ? AerialMeleeMultiplier : 1m;

    // Original Word of Binding is a resistance-tier debuff, not a second +15% damage multiplier.
    public static int BindingPenalty(bool hero, DamageType type, int stacks) => hero && type == DamageType.Magic
        ? -Math.Clamp(stacks, 0, BindingMaxStacks) : 0;
    public static decimal BindingStacksToApply(int current, decimal added) =>
        Math.Max(0m, Math.Min(added, BindingMaxStacks - Math.Max(0, current)));

    // Mist is a special avoidance window, including single-target unpowered spells. Status ticks are not moves.
    public static decimal MistMultiplier(bool move, bool spell, bool area, DamageType type) =>
        !move || type == DamageType.Light || spell && area ? 1m : 0m;

    public static int SmeltingDamage(int openValves, int damage) => openValves > 0 ? Math.Max(0, damage) : 0;
    public static int MeteorDamage(bool cleared, int damage) => cleared ? 0 : Math.Max(0, damage);
    public static int TurnsAfterEnemyAction(int remaining) => Math.Max(0, remaining - 1);
    public static int RefreshClosure(int remaining) => Math.Max(remaining, ValveClosedTurns); // refresh, never +2 per hit

    // Sunbeam opens a cocoon without killing its guards; the next phase replenishes to three, never adds a fourth.
    public static int MissingCocoonGuards(int living) => Math.Max(0, CocoonGuards - Math.Max(0, living));

    /// <summary>Pure pressure selection; re-evaluating a cold hit/phase does not advance alternation or draw RNG.</summary>
    public static BoilerMove BoilerMoveAt(int pressure, int threshold, int alternate)
    {
        if (threshold < 1) throw new ArgumentOutOfRangeException(nameof(threshold));
        if (alternate < 0) throw new ArgumentOutOfRangeException(nameof(alternate));
        return pressure >= threshold ? BoilerMove.Release : alternate % 2 == 0 ? BoilerMove.Bite : BoilerMove.Coil;
    }

    public static bool CanStartCocoon(bool alreadyCocooned, int packetDamage, int hp, int maxHp, int nextThreshold) =>
        !alreadyCocooned && packetDamage > 0 && BossTimingRules.ThresholdReached(hp, maxHp, nextThreshold);

    public static bool CanDevour(int used, int livingEggs, int hp, int maxHp) =>
        used >= 0 && used < SummonerEggs && livingEggs > 0 && BossTimingRules.ThresholdReached(hp, maxHp, 30);

    public static int AuraPenalty(bool hero, bool livingSource, DamageType type) => hero && livingSource
        && type is DamageType.Fire or DamageType.Frost or DamageType.Shock or DamageType.Poison or DamageType.Blight or DamageType.Magic
            ? SummonerAuraTier : 0;

    /// <summary>One-based enemy action: wings → bite → warning → meteors. Meteors 4/8/12, never an unannounced first-turn cast.</summary>
    public static WorldEaterMove WorldEaterMoveAt(int turn)
    {
        if (turn < 1) throw new ArgumentOutOfRangeException(nameof(turn));
        return ((turn - 1) % WorldEaterCycleLength) switch
        {
            0 => WorldEaterMove.Wings,
            1 => WorldEaterMove.Bite,
            2 => WorldEaterMove.Breath,
            _ => WorldEaterMove.Meteors,
        };
    }
}

/// <summary>Combat-local, per-recipient entitlements. A full hand/failed insertion retains the exact token;
/// requesting the same window or retrying never creates a second one. No save/run ownership is implied.</summary>
public sealed class EncounterGrantQueue<TRecipient, TToken, TCombat>
    where TRecipient : notnull where TToken : class where TCombat : class
{
    public sealed class Entry(TRecipient recipient, string kind, int window, TToken token, TCombat combat)
    {
        public TRecipient Recipient { get; } = recipient;
        public string Kind { get; } = kind;
        public int Window { get; } = window;
        public TToken Token { get; } = token;
        public TCombat Combat { get; } = combat;
        public bool GenerationAttempted { get; set; }
    }

    private readonly Dictionary<(TRecipient, string), Entry> _entries = new();
    public IReadOnlyList<Entry> Entries => _entries.Values.ToArray();

    public Entry Request(TRecipient recipient, string kind, int window, TCombat combat,
        Func<TToken?> findReusable, Func<TToken> create)
    {
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentException.ThrowIfNullOrEmpty(kind);
        if (window < 0) throw new ArgumentOutOfRangeException(nameof(window));
        var key = (recipient, kind);
        if (_entries.TryGetValue(key, out var previous) && ReferenceEquals(previous.Combat, combat))
        {
            if (window < previous.Window) throw new InvalidOperationException("Counterplay window moved backwards");
            if (window == previous.Window) return previous;
        }
        var reusable = findReusable();
        var token = reusable ?? create() ?? throw new InvalidOperationException("Encounter grant factory returned no token");
        var entry = new Entry(recipient, kind, window, token, combat)
        {
            GenerationAttempted = reusable != null && (previous != null && ReferenceEquals(previous.Combat, combat)
                && ReferenceEquals(previous.Token, reusable) ? previous.GenerationAttempted : true),
        };
        _entries[key] = entry;
        return entry;
    }

    public void Clear() => _entries.Clear();
}

/// <summary>Reissuing a combat answer must not bypass its normal shout cooldown when the shelf is full.
/// Uses each recipient's own turn clock (extra turns/co-op can differ), not the enemy round or grant window.
/// This only delays boss reissue/recall; normal shelf capacity and externally drawn cards keep their own rules.</summary>
public sealed class EncounterShoutCooldowns<TRecipient> where TRecipient : notnull
{
    private readonly Dictionary<(TRecipient, string), int> _readyTurns = new();

    public void Played(TRecipient recipient, string kind, int ownerTurn, int cooldown)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);
        if (ownerTurn < 1) throw new ArgumentOutOfRangeException(nameof(ownerTurn));
        var readyAt = checked(ownerTurn + Math.Max(ShoutRules.MinCooldown, cooldown));
        var key = (recipient, kind);
        _readyTurns[key] = Math.Max(_readyTurns.GetValueOrDefault(key), readyAt); // replays never add or shorten
    }

    public bool Ready(TRecipient recipient, string kind, int ownerTurn) =>
        !_readyTurns.TryGetValue((recipient, kind), out var readyAt) || ownerTurn >= readyAt;

    public void Clear() => _readyTurns.Clear();
}
