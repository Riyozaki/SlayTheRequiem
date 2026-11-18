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

    public static bool CanCaptureImprint(bool ancientUnfinished, bool startingChoicesComplete, bool mutationInFlight) =>
        !ancientUnfinished && startingChoicesComplete && !mutationInFlight;

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

    // ───── кооп (§5) ─────

    /// <summary>
    /// Голосование отряда: хост предложил возврат (его голос — «за»), остальные голосуют; кто не ответил — воздержался.
    /// Большинство; при равенстве решает хост — то есть «за».
    /// </summary>
    public static bool VoteApproved(int yesIncludingHost, int no) => yesIncludingHost >= no;

    /// <summary>Свечи общие для отряда: остаток — наименьший среди Камней игроков (∞ только если у всех ∞).</summary>
    public static int SharedCandlesLeft(IEnumerable<int> perPlayer)
    {
        var any = false;
        var min = Infinite;
        foreach (var c in perPlayer) { any = true; min = Math.Min(min, c); }
        return any ? min : 0;
    }

    /// <summary>Сколько потрачено Свечей у отряда: наибольший счётчик (у всех растёт одновременно).</summary>
    public static int SharedCandlesUsed(IEnumerable<int> perPlayer) => perPlayer.DefaultIfEmpty(0).Max();

    /// <summary>Шрам по умолчанию, если игрок не выбрал (вышел/истёк таймер): самый мягкий — кошелёк.</summary>
    public const Scar DefaultScar = Scar.Purse;

    // ───── новая карта акта (§1.3) ─────

    /// <summary>Имя потока ГСЧ новой карты акта после возврата: своё для каждого отката.</summary>
    public static string MapRngName(int actIndex, int rollbacks) => $"act_{actIndex + 1}_map_requiem_{rollbacks}";

    // ───── «Свеча в склепе» (§2, M5 п. 6) ─────

    /// <summary>Шанс, что редкое событие вообще есть в пуле «?» акта (бросок от зерна забега и индекса акта).</summary>
    public const int CryptCandleChancePercent = 30;

    /// <summary>Свечей из склепа за забег — не больше одной (как и за супербосса).</summary>
    public const int MaxCryptCandles = 1;

    /// <summary>Попадает ли «Свеча в склепе» в пул акта: редкость по зерну.</summary>
    public static bool CryptCandleInPool(uint hash) => hash % 100u < CryptCandleChancePercent;

    /// <summary>
    /// Можно ли зажечь Свечу: Камень включён и есть у героя, Свечи конечны (<paramref name="candlesLeft"/> ≠ ∞),
    /// уровень допускает Свечи (не «Проклятый») и Свеча из склепа ещё не взята.
    /// </summary>
    public static bool CanTakeCryptCandle(bool stoneEnabled, bool hasStone, bool candlesAllowed, int candlesLeft, int taken) =>
        stoneEnabled && hasStone && candlesAllowed && candlesLeft != Infinite && taken < MaxCryptCandles;

    // ───── автопереподключение коопа (§5, M5 п. 6) ─────

    /// <summary>Сколько раз клиент ищет лобби хоста после возврата к Камню, прежде чем оставить экран «Присоединиться».</summary>
    public const int RejoinAttempts = 10;

    /// <summary>Пауза перед попыткой <paramref name="attempt"/> (0, 1, …): хосту нужно время выйти в меню и открыть лобби.</summary>
    public static int RejoinDelayMs(int attempt) => attempt switch
    {
        < 0 => 0,
        0 => 2000,
        1 => 2500,
        2 => 3500,
        3 => 5000,
        _ => 6000,
    };

    /// <summary>Сколько всего клиент ждёт хоста (сумма пауз всех попыток), мс.</summary>
    public static int RejoinTotalMs() => Enumerable.Range(0, RejoinAttempts).Sum(RejoinDelayMs);

    /// <summary>Пробовать ли ещё: попытки не кончились, клиент ещё не подключён и не ушёл с экрана сам.</summary>
    public static bool ShouldRetryRejoin(int attempt, bool connected, bool screenOpen) =>
        attempt < RejoinAttempts && !connected && screenOpen;
}
