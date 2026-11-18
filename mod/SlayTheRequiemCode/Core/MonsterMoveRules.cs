namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Current MOVE type, not faction/default room type. Used by enemy intents and actual hits.</summary>
public interface IEnemyDamageTypeSource
{
    DamageType CurrentAttackType { get; }
}

public static class MonsterMoveRules
{
    private static readonly IReadOnlyDictionary<string, DamageType> Elements = Build();

    public static DamageType TypeOf(string move)
    {
        // Repeated IDs name the same move; a suffix is not a different damage type.
        if (move.EndsWith("_2", StringComparison.Ordinal) || move.EndsWith("_3", StringComparison.Ordinal)) move = move[..^2];
        return Elements.TryGetValue(move, out var type) ? type : DamageType.Physical;
    }

    public static bool IsUnpowered(string move) => move is "SMELTING_BLAST" or "METEOR_RAIN" or "SOUL_BURST" or "DISCHARGE" or "MASK_AURA";

    private static Dictionary<string, DamageType> Build()
    {
        var map = new Dictionary<string, DamageType>(StringComparer.Ordinal);
        Add(DamageType.Frost, "WRAITH_TOUCH FROST_SCYTHE WINTER_BLADE KINGS_JUDGEMENT GRAVE_COLD FROST_BREATH GLACIAL_ROAR GRAVE_TOUCH DEATH_SHRIEK LAST_LAMENT ICE_SHARD SNOW_BLADE BLIZZARD FROST_LANCE");
        Add(DamageType.Fire, "STEAM_EXHALE STEAM_VOLLEY BOILER_BURST SCALDING_BITE PRESSURE_RELEASE FLAME_LASH FIREBOLT METEOR_SHOWER SMELTING_BLAST METEOR_RAIN");
        Add(DamageType.Shock, "ARC STORM_FIST SENTRY_BOLT LEY_SURGE CHAIN_LIGHTNING DISCHARGE");
        Add(DamageType.Poison, "WEB_SPIT ROT_TOUCH");
        Add(DamageType.Magic, "ABYSS_BLADE ABYSS_CLEAVE ABYSS_HAMMER SANGUINE_DRAIN BLACK_TOME UMBRA_STRIKE SOUL_DRAIN STARFALL MIND_ECLIPSE ARCANE_COIL LEY_RUPTURE ARCANE_BARRAGE PRISM_RAY PRISM_BEAM SHARD_BOLT DEVOUR_GHOST_TOUCH BLOOD_BEAM SANGUINE_JUDGEMENT CHALICE_FLOOD GRADE");
        Add(DamageType.Light, "DAWN_RAY");
        return map;

        void Add(DamageType type, string moves)
        {
            foreach (var move in moves.Split(' ', StringSplitOptions.RemoveEmptyEntries)) map.Add(move, type);
        }
    }

    /// <summary>Enemy action price uses the declared action, not damage after Block/resistance or party size.</summary>
    public static int ActionCost(bool caster, int hits, decimal baseTotal) => caster
        ? EnemyRules.SpellCost(Math.Max(1, hits), (int)Math.Clamp(baseTotal, 0m, int.MaxValue))
        : EnemyRules.AttackCost(Math.Max(1, hits), (int)Math.Clamp(baseTotal, 0m, int.MaxValue));
}
