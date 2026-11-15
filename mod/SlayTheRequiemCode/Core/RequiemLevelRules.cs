namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// Уровни сложности «Реквием» 0–10 (docs/design/11 §7) — поверх Возвышения. Чистые функции — зеркало в tools/sim.
/// По умолчанию уровень берётся из Возвышения забега: Возвышение N → «Реквием» N + 1 (максимум 10), так что
/// ступени 2–4 («Ascension-эквиваленты») дают сами правила Возвышения. Уровень 0 «Странник» — только из настроек.
/// </summary>
public static class RequiemLevelRules
{
    public const int Min = 0;
    public const int Max = 10;

    public const int VeteranLevel = 5;
    public const int LegendLevel = 7;
    public const int DamnedLevel = 10;

    /// <summary>Уровень по Возвышению: 0 → 1 «Наёмник», …, 9+ → 10 «Проклятый».</summary>
    public static int FromAscension(int ascension) => Math.Clamp(ascension + 1, 1, Max);

    /// <summary>Действующий уровень: настройка ≥ 0 — своё число, −1 — по Возвышению.</summary>
    public static int Resolve(int configOverride, int ascension) =>
        configOverride >= 0 ? Math.Clamp(configOverride, Min, Max) : FromAscension(ascension);

    /// <summary>♥ врагов: «Странник» −25 %.</summary>
    public static decimal EnemyHpMultiplier(int level) => level <= 0 ? 0.75m : 1m;

    /// <summary>Урон по героям: «Странник» −25 %, «Легенда» (7+) +25 %.</summary>
    public static decimal DamageTakenMultiplier(int level) => level <= 0 ? 0.75m : level >= LegendLevel ? 1.25m : 1m;

    /// <summary>Урон героев: «Проклятый» (10) −20 %.</summary>
    public static decimal DamageDealtMultiplier(int level) => level >= DamnedLevel ? 0.8m : 1m;

    /// <summary>Усталость похода (docs/design/10) — с «Ветерана» (5).</summary>
    public static bool FatigueEnabled(int level) => level >= VeteranLevel;

    /// <summary>Базовое число аффиксов элиты: 1, на «Легенде» и выше — 2–3 (бросок 0..99 &lt; 50 → 3).</summary>
    public static int EliteAffixBase(int level, int roll) => level >= LegendLevel ? (roll < 50 ? 3 : 2) : 1;

    /// <summary>«Проклятый»: Багровый портал обязателен.</summary>
    public static bool SuperbossesMandatory(int level) => level >= DamnedLevel;

    /// <summary>«Проклятый»: праведность начинается с «Запятнанного».</summary>
    public static bool StartsTainted(int level) => level >= DamnedLevel;

    /// <summary>«Проклятый»: Свечей нет совсем — и супербосс их не даёт.</summary>
    public static bool CandlesAllowed(int level) => level < DamnedLevel;

    /// <summary>Название ступени (ключ локализации SLAYTHEREQUIEM-REQUIEM_LEVEL_N).</summary>
    public static string TitleKey(int level) => $"REQUIEM_LEVEL_{Math.Clamp(level, Min, Max)}";
}
