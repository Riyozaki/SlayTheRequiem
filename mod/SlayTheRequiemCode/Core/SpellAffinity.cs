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

    public const int MaxMastery = 5;

    /// <summary>Стартовое владение (docs/design/04): Латоносец 0; Безмолвная 1 в Восст/Измен/Иллюз; Регент 2 в Разрушении и 1 в остальных.</summary>
    public static int StartingMastery(string? heroTypeName, SpellSchool school) => heroTypeName switch
    {
        "Silent" => school is SpellSchool.Restoration or SpellSchool.Alteration or SpellSchool.Illusion ? 1 : 0,
        "Regent" => school == SpellSchool.Destruction ? 2 : 1,
        _ => 0,
    };

    /// <summary>Родные школы героя (для цены прокачки).</summary>
    public static bool IsNativeSchool(string? heroTypeName, SpellSchool school) => StartingMastery(heroTypeName, school) > 0;

    /// <summary>
    /// Доля заклинаний в наградах (08 §8.4): Регент 45 %, Безмолвная 20 %, Латоносец 10 % + 5 % за каждые 2 очка суммарного владения.
    /// </summary>
    public static decimal RewardShare(string? heroTypeName, int totalMastery) => heroTypeName switch
    {
        "Regent" => 0.45m,
        "Silent" => 0.20m,
        _ => Math.Min(0.45m, 0.10m + 0.05m * (Math.Max(0, totalMastery) / 2)),
    };

    /// <summary>Школа в наградах — при владении ≥ 1, ступень не выше владение + 1.</summary>
    public static bool AllowedInRewards(SpellTier tier, int mastery) => mastery >= 1 && (int)tier <= mastery + 1;

    /// <summary>Потолок ♥ цели для контроля (Иллюзия, Паралич — 08 §7). null = любые не-боссы.</summary>
    public static int? ControlHpCap(SpellTier tier) => tier switch
    {
        SpellTier.Novice => 20,
        SpellTier.Apprentice => 40,
        SpellTier.Adept => 70,
        SpellTier.Expert => 120,
        _ => null,
    };

    /// <summary>Ослабить число эффекта при «С трудом» (×0.75, вниз; ненулевое остаётся ≥ 1).</summary>
    public static int ScaleEffect(int amount, decimal multiplier)
    {
        if (amount <= 0 || multiplier >= 1m) return amount;
        return Math.Max(1, (int)Math.Floor(amount * multiplier));
    }
}
