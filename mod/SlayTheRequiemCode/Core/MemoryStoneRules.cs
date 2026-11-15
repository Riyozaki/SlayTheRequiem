namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Цена возврата к Камню Памяти (docs/design/15 §3). Порядок = сохраняемый индекс.</summary>
public enum Scar
{
    Flesh,  // −6 % макс. ♥ (складывается)
    Soul,   // проклятие «Предсмертное эхо» в колоду
    Purse,  // −40 % золота Отпечатка
}

/// <summary>Камень Памяти: Свечи, Шрамы, утешение, счёт (docs/design/15). Чистые функции — зеркало в tools/sim.</summary>
public static class MemoryStoneRules
{
    /// <summary>Значение «бесконечно» для числа Свечей.</summary>
    public const int Infinite = int.MaxValue;

    /// <summary>Свечей за забег по уровню «Реквием» (§2): 0 → ∞, 1–4 → 3, 5–7 → 2, 8–9 → 1, 10+ → 0.</summary>
    public static int CandlesFor(int requiemLevel) => requiemLevel switch
    {
        <= 0 => Infinite,
        <= 4 => 3,
        <= 7 => 2,
        <= 9 => 1,
        _ => 0,
    };

    /// <summary>
    /// Сколько Свечей осталось. <paramref name="configOverride"/> ≥ 0 — своё число из настроек (−1 — по уровню).
    /// <paramref name="bonus"/> — дополнительные Свечи (супербосс, событие).
    /// </summary>
    public static int CandlesLeft(int requiemLevel, int used, int bonus = 0, int configOverride = -1)
    {
        var total = configOverride >= 0 ? configOverride : CandlesFor(requiemLevel);
        if (total == Infinite) return Infinite;
        return Math.Max(0, total + Math.Max(0, bonus) - Math.Max(0, used));
    }

    public const double FleshScarFactor = 0.94;
    public const double PurseScarFactor = 0.60;

    /// <summary>Шрам плоти: −6 % макс. ♥ (округление вниз, но минимум −1 и не ниже 1).</summary>
    public static int FleshScarMaxHp(int maxHp)
    {
        if (maxHp <= 1) return maxHp;
        var reduced = (int)Math.Floor(maxHp * FleshScarFactor);
        return Math.Clamp(reduced, 1, maxHp - 1);
    }

    /// <summary>Шрам кошелька: остаётся 60 % золота.</summary>
    public static int PurseScarGold(int gold) => gold <= 0 ? 0 : (int)Math.Floor(gold * PurseScarFactor);

    /// <summary>Месть (§4): +10 % урона по тому, кто убил героя, до конца повторной попытки акта.</summary>
    public const decimal RevengeMultiplier = 1.10m;

    /// <summary>Счёт: −15 % за каждый откат (§6), не ниже 0.</summary>
    public static double ScoreMultiplier(int rollbacks) => Math.Max(0, 1 - 0.15 * Math.Max(0, rollbacks));

    /// <summary>Снимок пригоден, только если версия схемы сохранения совпадает (§8, риски миграций EA).</summary>
    public static bool IsSnapshotCompatible(int snapshotSchema, int currentSchema) => snapshotSchema == currentSchema && currentSchema > 0;

    /// <summary>
    /// Сколько Шрамов вида наложить после загрузки Отпечатка: Шрамы погибшей попытки (+1, если этот вид выбран сейчас)
    /// минус уже записанные в Отпечатке. Повторный возврат к тому же Камню повторяет и прежние Шрамы.
    /// </summary>
    public static int ScarsToApply(int inDyingRun, bool chosenNow, int inImprint) =>
        Math.Max(0, inDyingRun + (chosenNow ? 1 : 0) - inImprint);

    /// <summary>Удаление «Предсмертного эха» стоит вдвое дороже.</summary>
    public const int DeathEchoRemovalFactor = 2;
}
