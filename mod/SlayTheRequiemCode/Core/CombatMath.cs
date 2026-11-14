namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// Латы, Пробитие, Раскол и сопротивления (docs/design/02 §4, 03).
/// Порядок в пайплайне урона: база → Сила/Слабость (ваниль) → выносливость → сопротивление/уязвимость → Латы → Блок.
/// </summary>
public static class CombatMath
{
    /// <summary>
    /// Применить Латы к одному удару. Латы уменьшают удар (не ниже 1), Пробитие игнорирует положительные Латы,
    /// Раскол снижает Латы и может увести их в минус — тогда каждый удар усиливается.
    /// </summary>
    public static int ApplyPlating(int damage, int plating, int pierce = 0, int sunder = 0)
    {
        if (damage <= 0) return 0;
        var effective = plating - sunder;
        if (effective > 0)
        {
            effective = Math.Max(0, effective - Math.Max(0, pierce));
            return effective == 0 ? damage : Math.Max(1, damage - effective);
        }
        return damage - effective; // effective ≤ 0 → +|effective|
    }

    /// <summary>
    /// Ступень сопротивления: −1 уязвимость (×1.5), 0 нет, 1/2/3 → −25/−50/−75 % (кап).
    /// </summary>
    public static decimal ResistanceMultiplier(int tier, bool immune = false)
    {
        if (immune) return 0m;
        if (tier < 0) return 1.5m;
        return 1m - 0.25m * Math.Min(tier, 3);
    }

    /// <summary>Ванильная Уязвимость и наша уязвимость стихии не складываются: берётся максимум.</summary>
    public static decimal CombineVulnerability(decimal vanillaVulnerableMultiplier, int resistanceTier) =>
        resistanceTier < 0 ? Math.Max(vanillaVulnerableMultiplier, 1.5m) : vanillaVulnerableMultiplier;

    /// <summary>Сопротивление статусам уменьшает накладываемые стаки (а не урон тика).</summary>
    public static int ResistStacks(int stacks, int tier, bool immune = false)
    {
        if (immune || stacks <= 0) return 0;
        return (int)Math.Floor(stacks * ResistanceMultiplier(Math.Max(0, tier)));
    }
}
