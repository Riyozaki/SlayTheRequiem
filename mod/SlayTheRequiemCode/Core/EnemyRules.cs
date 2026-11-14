namespace SlayTheRequiem.SlayTheRequiemCode.Core;

public enum EnemyRank
{
    Normal,
    Elite,
    Boss,
}

/// <summary>
/// Ресурсы и защита врагов (docs/design/11 §1, §4; 02 §4.4). Числа — стартовые, под плейтест.
/// </summary>
public static class EnemyRules
{
    /// <summary>Пул ◆ врага. Атакующее намерение стоит ◆; без ◆ враг «Отдышивается».</summary>
    public static int StaminaPool(EnemyRank rank) => rank switch
    {
        EnemyRank.Elite => 4,
        EnemyRank.Boss => 5,
        _ => 3,
    };

    /// <summary>Прирост ◆ в конце хода врага. Равен цене обычной атаки: без Озноба враг атакует бесконечно.</summary>
    public const int StaminaRegen = 1;

    /// <summary>Цена атакующего действия: обычное — 1, многоударное (3+ удара) или тяжёлое (20+ урона суммарно) — 2.</summary>
    public static int AttackCost(int hits, int totalDamage) => hits >= 3 || totalDamage >= 20 ? 2 : 1;

    /// <summary>«Отдышаться»: Блок и полное восстановление ◆ вместо атаки.</summary>
    public const int BreatherBlock = 5;

    /// <summary>Стойкость: порог ошеломления. Обычные 3–20 от ♥, элиты +3. Боссы Непоколебимы (0).</summary>
    public static int Poise(EnemyRank rank, int maxHp)
    {
        if (rank == EnemyRank.Boss) return 0;
        var basePoise = Math.Clamp((int)Math.Round(maxHp / 8.0, MidpointRounding.AwayFromZero), 3, 20);
        return rank == EnemyRank.Elite ? basePoise + 3 : basePoise;
    }

    /// <summary>Порог Слома для Непоколебимых (боссы): 15–50 от ♥.</summary>
    public static int BreakThreshold(int maxHp) => Math.Clamp(maxHp / 10, 15, 50);

    /// <summary>После ошеломления/Слома порог растёт на 50 % (нельзя «станлочить»).</summary>
    public static int RaiseThreshold(int threshold) => (int)Math.Ceiling(threshold * 1.5m);

    /// <summary>Сломленный получает +25 % урона.</summary>
    public const decimal BrokenDamageTakenMultiplier = 1.25m;

    /// <summary>После Обледенения — иммунитет к нему на 2 хода.</summary>
    public const int FrozenImmunityTurns = 2;

    // ───── враги-маги (атака стихией/магией в таблице врагов) ─────

    /// <summary>Пул ✦ врага-мага. Его атакующие намерения — заклинания: тратят ✦ вместо ◆.</summary>
    public static int CasterManaPool(EnemyRank rank) => rank switch
    {
        EnemyRank.Elite => 6,
        EnemyRank.Boss => 8,
        _ => 4,
    };

    /// <summary>Прирост ✦ врага-мага в конце его хода.</summary>
    public const int CasterManaRegen = 2;

    /// <summary>Цена заклинания врага: на 1 дороже атаки (2 или 3).</summary>
    public static int SpellCost(int hits, int totalDamage) => AttackCost(hits, totalDamage) + 1;

    /// <summary>Минимальная ✦ для заклинания; меньше — враг «Сосредотачивается» (Оберег и полная ✦) вместо атаки.</summary>
    public const int MinSpellMana = 2;

    /// <summary>Оберег, который враг-маг получает при «Сосредоточиться».</summary>
    public const int FocusSpellShield = 5;

    /// <summary>Маг ли враг: атака не физическая и не «истинная».</summary>
    public static bool IsCaster(DamageType attackType) => attackType is not (DamageType.Physical or DamageType.True or DamageType.Poison);

    /// <summary>Множитель ♥ ванильных врагов (компенсация новых источников урона), по умолчанию +10 %.</summary>
    public static int ScaledMaxHp(int maxHp, decimal multiplier) => Math.Max(1, (int)Math.Round(maxHp * multiplier, MidpointRounding.AwayFromZero));
}

/// <summary>Шаткость против Стойкости.</summary>
public static class StaggerRules
{
    public readonly record struct Result(bool Triggered, int RemainingStagger, int NewThreshold);

    /// <summary>Накопить шаткость. При достижении порога — срабатывание, шаткость в 0, порог ×1.5.</summary>
    public static Result Accumulate(int stagger, int threshold)
    {
        if (threshold <= 0) return new Result(false, stagger, threshold);
        return stagger >= threshold
            ? new Result(true, 0, EnemyRules.RaiseThreshold(threshold))
            : new Result(false, stagger, threshold);
    }
}
