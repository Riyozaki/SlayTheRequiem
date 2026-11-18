namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// Латы, Пробитие, Раскол и сопротивления (docs/design/02 §4, 03).
/// Порядок в пайплайне урона: база → Сила/Слабость (ваниль) → выносливость → сопротивление/уязвимость → Латы/Раскол → native cap → Блок.
/// </summary>
public static class CombatMath
{
    /// <summary>
    /// Применить Латы к одному удару. Латы уменьшают удар (не ниже 1), Пробитие игнорирует положительные Латы,
    /// Раскол снижает Латы и может увести их в минус — тогда каждый удар усиливается.
    /// </summary>
    public static int ApplyPlating(int damage, int plating, int pierce = 0, int sunder = 0) =>
        (int)Math.Min(int.MaxValue, ApplyPlating((decimal)damage, plating, pierce, sunder));

    /// <summary>
    /// Decimal stage shared by preview/hit. Do not truncate before armor/caps: native HP/block loss performs
    /// the final integer conversion. Positive armor never raises a fractional hit to 1. A zero/immune hit
    /// remains zero even with negative armor; negative Sunder/Pierce cannot manufacture a second bonus.
    /// </summary>
    public static decimal ApplyPlating(decimal damage, int plating, int pierce = 0, int sunder = 0)
    {
        if (damage <= 0m) return 0m;
        var effective = (decimal)plating - Math.Max(0, sunder);
        if (effective <= 0m) return damage - effective;
        effective = Math.Max(0m, effective - Math.Max(0, pierce));
        return Math.Min(damage, Math.Max(1m, damage - effective));
    }

    /// <summary>
    /// D-06: powered attacks use armor after ALL multipliers, before native caps. True damage ignores both
    /// armor and Sunder, not ordinary Block/caps/special invulnerability. Spells/unpowered damage skip armor.
    /// </summary>
    public static decimal ArmorStage(decimal damage, bool poweredAttack, DamageType type, int plating, int pierce = 0, int sunder = 0) =>
        poweredAttack && type != DamageType.True ? ApplyPlating(damage, plating, pierce, sunder) : Math.Max(0m, damage);

    /// <summary>Ступень, начиная с которой сопротивление считается иммунитетом.</summary>
    public const int ImmuneTier = 4;

    /// <summary>
    /// Ступень сопротивления: −1 уязвимость (×1.5), 0 нет, 1/2/3 → −25/−50/−75 % (кап), 4+ — иммунитет.
    /// </summary>
    public static decimal ResistanceMultiplier(int tier, bool immune = false)
    {
        if (immune || tier >= ImmuneTier) return 0m;
        if (tier < 0) return 1.5m;
        return 1m - 0.25m * Math.Min(tier, 3);
    }

    /// <summary>
    /// Множитель стихии с учётом ванильной Уязвимости (03, ревью F-10): уязвимость стихии (×1.5) и ванильная Уязвимость
    /// (×1.5 к атакам) не складываются — если на цель уже действует ванильная, стихия добавляет ×1. Сопротивления
    /// (ступени 1+) с Уязвимостью складываются как обычно.
    /// </summary>
    public static decimal ElementalMultiplier(int tier, bool vanillaVulnerableApplies, bool immune = false)
    {
        if (tier < 0 && vanillaVulnerableApplies && !immune) return 1m;
        return ResistanceMultiplier(tier, immune);
    }

    /// <summary>Сопротивление статусам уменьшает накладываемые стаки (а не урон тика).</summary>
    public static int ResistStacks(int stacks, int tier, bool immune = false)
    {
        if (immune || stacks <= 0 || tier >= ImmuneTier) return 0;
        if (tier < 0) return (int)Math.Ceiling(stacks * 1.5m); // уязвимость: стаков больше
        return (int)Math.Floor(stacks * ResistanceMultiplier(tier));
    }
}
