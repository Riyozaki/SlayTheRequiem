namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Deterministic spell-defense units. Queries never spend charges; commands commit only actual effects.</summary>
public static class SpellDefenseRules
{
    public static bool CanAbsorb(bool hostileSpell, DamageType type, int charges) =>
        hostileSpell && type != DamageType.Light && charges > 0;

    /// <summary>Ward uses whole damage/stack units, after resistance and ordinary Block. Fractions stay native.</summary>
    public static int WardUsed(decimal amount, int ward) =>
        amount <= 0m || ward <= 0 ? 0 : (int)Math.Min(ward, decimal.Floor(amount));

    public static int ManaWardUsed(decimal amount, int remaining, int mana) => WardUsed(amount, Math.Min(remaining, mana));

    /// <summary>One cast per target, not one charge per hit/status; Replay creates another cast.</summary>
    public sealed class Cast<T> where T : notnull
    {
        private readonly HashSet<T> _absorbed = new();
        public bool IsAbsorbed(T target) => _absorbed.Contains(target);
        public bool Absorb(T target) => _absorbed.Add(target);
    }
}
