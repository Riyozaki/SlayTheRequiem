namespace SlayTheRequiem.SlayTheRequiemCode.Core;

public enum SpellSchool
{
    Destruction,
    Restoration,
    Alteration,
    Conjuration,
    Illusion,
}

/// <summary>Ступень заклинания: Новичок 1 … Мастер 5.</summary>
public enum SpellTier
{
    Novice = 1,
    Apprentice = 2,
    Adept = 3,
    Expert = 4,
    Master = 5,
}

public enum CastState
{
    Ease,
    Free,
    Clumsy,
    Strained,
    Impossible,
}

/// <summary>
/// Предрасположенность: любой герой может колдовать, но цена зависит от владения школой (docs/design/08 §8).
/// </summary>
public static class SpellAffinity
{
    public readonly record struct CastCost(CastState State, bool Castable, int ManaCost, decimal EffectMultiplier);

    public static int Gap(SpellTier tier, int mastery) => (int)tier - Math.Clamp(mastery, 0, 5);

    public static CastCost Evaluate(int baseManaCost, SpellTier tier, int mastery, bool fromScroll = false)
    {
        baseManaCost = Math.Max(0, baseManaCost);
        if (fromScroll) return new CastCost(CastState.Free, true, 0, 1m);

        var gap = Gap(tier, mastery);
        return gap switch
        {
            <= -2 => new CastCost(CastState.Ease, true, Math.Max(0, baseManaCost - 1), 1m),
            <= 0 => new CastCost(CastState.Free, true, baseManaCost, 1m),
            1 => new CastCost(CastState.Clumsy, true, (int)Math.Ceiling(baseManaCost * 1.5m), 1m),
            2 => new CastCost(CastState.Strained, true, baseManaCost * 2, 0.75m),
            _ => new CastCost(CastState.Impossible, false, baseManaCost, 0m),
        };
    }

    /// <summary>Сколько очков стоит +1 владения: родная школа — 1, чужая — 2.</summary>
    public static int PointsPerMastery(bool nativeSchool) => nativeSchool ? 1 : 2;
}
