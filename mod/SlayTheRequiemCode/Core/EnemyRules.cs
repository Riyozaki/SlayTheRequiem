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

    // ───── аффиксы (docs/design/11 §2) ─────

    public const int AffixPlating = 2;
    public const int AffixCloak = 3;
    public const int AffixResistance = 2;
    public const int AffixDrain = 2;
    public const int VampiricPercent = 50;
    public const int AffixPoison = 2;
    /// <summary>Шанс аффикса у обычного врага с акта 2, в процентах.</summary>
    public const int NormalAffixChance = 25;

    /// <summary>Свита элиты (разбойники Атаманши, мелочь в элитных боях) — ниже этого ♥ аффиксов не получает.</summary>
    public const int EliteMinionHp = 40;

    // аффиксы этапа 7
    public const int BannerDamage = 2;
    public const int AbsorberStacks = 1;
    /// <summary>Неистовый: ниже этой доли ♥ (в процентах) — +1 удар в каждой атаке.</summary>
    public const int FrenzyHpPercent = 50;
    /// <summary>Призрачный (аффикс): физический урон не от заклинаний −50 % (у «Призрачного Великана» — −75 %).</summary>
    public const int GhostlyAffixReduction = 50;
    public const int AvengerDamage = 6;

    /// <summary>
    /// Сколько аффиксов получает враг (docs/design/11 §2): элита — 1, на «Реквиеме» 7+ — 2–3 (бросок &lt; 50 → 3),
    /// плюс <paramref name="eliteBonus"/> (Слава в пяти актах: +1); свита элиты с ♥ &lt; <see cref="EliteMinionHp"/> — 0;
    /// обычный с акта 2 — 0/1 (бросок 0..99); босс — 0.
    /// </summary>
    public static int AffixCount(EnemyRank rank, int actIndex, int roll, int maxHp = int.MaxValue, int requiemLevel = 1, int eliteBonus = 0) => rank switch
    {
        EnemyRank.Elite when maxHp < EliteMinionHp => 0,
        EnemyRank.Elite => Math.Min(MaxAffixes, RequiemLevelRules.EliteAffixBase(requiemLevel, roll) + Math.Max(0, eliteBonus)),
        EnemyRank.Normal when actIndex >= 1 && roll < NormalAffixChance => 1,
        _ => 0,
    };

    /// <summary>Больше аффиксов на одном враге не бывает (всего их 11 видов, повторов нет).</summary>
    public const int MaxAffixes = 4;
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

/// <summary>Все 11 аффиксов (docs/design/11 §2; огонь/лёд/гроза — три вида одного «стихийного»).</summary>
public enum EnemyAffix
{
    Hardened,
    Fiery,
    Icy,
    Stormy,
    Draining,
    ManaEater,
    Vampiric,
    PoisonedBlade,
    Bannerman,
    Absorber,
    Frenzied,
    Ghostly,
    Avenger,
}

public static class AffixRules
{
    /// <summary>
    /// Выбрать <paramref name="count"/> разных аффиксов по броскам (каждый бросок — индекс среди ещё не выбранных).
    /// Стихийные аффиксы взаимоисключающие: огненный враг не станет ещё и ледяным.
    /// </summary>
    public static List<EnemyAffix> Pick(int count, IEnumerable<int> rolls)
    {
        var pool = Enum.GetValues<EnemyAffix>().ToList();
        var result = new List<EnemyAffix>();
        using var roll = rolls.GetEnumerator();
        while (result.Count < count && pool.Count > 0 && roll.MoveNext())
        {
            var affix = pool[((roll.Current % pool.Count) + pool.Count) % pool.Count];
            result.Add(affix);
            pool.Remove(affix);
            if (IsElemental(affix)) pool.RemoveAll(IsElemental);
        }
        return result;
    }

    public static bool IsElemental(EnemyAffix affix) => affix is EnemyAffix.Fiery or EnemyAffix.Icy or EnemyAffix.Stormy;

    /// <summary>Неистовый: ♥ ниже порога.</summary>
    public static bool IsFrenzied(int hp, int maxHp) => maxHp > 0 && hp * 100 < maxHp * EnemyRules.FrenzyHpPercent;
}
