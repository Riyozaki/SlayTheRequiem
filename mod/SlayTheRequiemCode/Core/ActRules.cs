namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Вид акта забега Реквиема (docs/design/13 §2).</summary>
public enum ActKind
{
    /// <summary>1 — Подножие Шпиля (ванильные Заросли / Подземные доки).</summary>
    Foothills,
    FrozenBarrows,
    ClockworkDepths,
    /// <summary>3 — Улей с оверлеем «Трещины Бездны».</summary>
    Hive,
    BloodCourt,
    StarTower,
    /// <summary>5 — Слава с оверлеем «Вторжение Бездны».</summary>
    Glory,
    /// <summary>★ Безымянный предел — тайный финал после пяти супербоссов.</summary>
    NamelessLimit,
    Other,
}

/// <summary>Правило акта (docs/design/13 §3).</summary>
public enum ActRule
{
    None,
    /// <summary>Акт 1 «Ученичество»: без аффиксов у обычных врагов.</summary>
    Apprenticeship,
    /// <summary>2а: Стужа, Тьма курганов, Запертые склепы.</summary>
    Frost,
    /// <summary>2б: Пар и вентили, Перегрев.</summary>
    Steam,
    /// <summary>3: Трещины Бездны.</summary>
    AbyssRifts,
    /// <summary>4а: День и ночь.</summary>
    DayNight,
    /// <summary>4б: Лей-линии и порталы.</summary>
    LeyLines,
    /// <summary>5: Вторжение Бездны.</summary>
    AbyssInvasion,
}

/// <summary>Супербоссы Багрового портала (docs/design/11 §6).</summary>
public enum Superboss
{
    BlackWanderer,
    BarrowReaper,
    DeepMasterSmith,
    GatebreakerEnvoy,
    BloodCourtLord,
    FirstSummoner,
    WorldEater,
    NamelessShadow,
}

/// <summary>Особые отметки узлов карты (подтипы ванильных MapPointType, docs/design/13 §4.2).</summary>
public enum MapMark
{
    None,
    /// <summary>◉ Врата Бездны (Улей, Слава).</summary>
    AbyssGate,
    /// <summary>⛓ Запертый склеп (Курганы) — сокровищница, открываемая Когтистым ключом.</summary>
    LockedVault,
    /// <summary>⚙ Вентиль (Глубины).</summary>
    Valve,
    /// <summary>Лей-линия (Башня): бой с +✦.</summary>
    LeyLine,
    /// <summary>🐾 Логово (акты 1–2): облегчённая элита-зверь — реагенты и шкура (M5, 13 §4.2).</summary>
    Lair,
    /// <summary>⚔ Испытание (акты 2–5): элита с усилением, награда ступенью выше.</summary>
    Trial,
    /// <summary>⚿ Тайник: сундук с замком — ключ или ловушка.</summary>
    Cache,
    /// <summary>! Засада (акты 2–5): бой, где враги бьют первыми; на карте видна только с Ночным зрением (M6).</summary>
    Ambush,
    /// <summary>☀ Знамя Небесных (Слава): Святилище, дающее Праведнику «Небесный щит» до конца акта (M6).</summary>
    CelestialBanner,
}

/// <summary>
/// Пять актов, карта и правила актов (docs/design/13). Чистые функции без зависимостей от игры — зеркало в tools/sim.
/// </summary>
public static class ActRules
{
    public const int FullRunActs = 5;
    public const int ShortRunActs = 3;

    /// <summary>Слоты забега (0..4): полный — все пять, «Короткий забег» — 1 → 3 → 5.</summary>
    public static int[] Slots(bool shortRun) => shortRun ? new[] { 0, 2, 4 } : new[] { 0, 1, 2, 3, 4 };

    /// <summary>
    /// Ступень ванильного кода (0..2) для индекса акта: сундуки, кооп-надбавки ♥ и прочие таблицы игры рассчитаны на
    /// три акта. Пять актов: 1 → 0, 2–3 → 1, 4–5 и Безымянный предел → 2. Иначе — индекс, ограниченный 0..2.
    /// </summary>
    public static int VanillaTier(int actIndex, int actCount)
    {
        if (actCount >= FullRunActs)
            return actIndex <= 0 ? 0 : actIndex <= 2 ? 1 : 2;
        return Math.Clamp(actIndex, 0, 2);
    }

    /// <summary>
    /// Ступень ванильного кода, где «2» значит «последний акт» (ключ-фонарь, эпоха за третий акт): первый акт — 0,
    /// последний — 2, всё между — 1. В трёх актах совпадает с индексом (ревью F-05).
    /// </summary>
    public static int FinalAwareTier(int actIndex, int actCount)
    {
        if (actIndex <= 0) return 0;
        return actIndex >= actCount - 1 ? 2 : 1;
    }

    /// <summary>Слот (0..4) акта по индексу в забеге: в «Коротком забеге» индексы 0/1/2 — это слоты 0/2/4.</summary>
    public static int SlotOf(int actIndex, int actCount)
    {
        if (actCount == ShortRunActs) return Math.Clamp(actIndex, 0, 2) * 2;
        return Math.Clamp(actIndex, 0, FullRunActs);
    }

    /// <summary>Ширина карты: новые акты — 9 колонок, ванильные — 7.</summary>
    public static int MapWidth(ActKind kind) => IsNewAct(kind) ? 9 : 7;

    public const int VanillaMapWidth = 7;

    public static bool IsNewAct(ActKind kind) =>
        kind is ActKind.FrozenBarrows or ActKind.ClockworkDepths or ActKind.BloodCourt or ActKind.StarTower;

    /// <summary>Комнат в акте (без босса и стартового узла).</summary>
    public static int Rooms(ActKind kind) => kind switch
    {
        ActKind.Foothills => 15,
        ActKind.FrozenBarrows or ActKind.ClockworkDepths or ActKind.Hive => 14,
        ActKind.BloodCourt or ActKind.StarTower or ActKind.Glory => 13,
        ActKind.NamelessLimit => 4,
        _ => 15,
    };

    /// <summary>
    /// Надбавка ♥ врагам акта в полном забеге (13 §2.1): новые акты 2 заимствуют врагов акта 1 (+35 %), акты 4 —
    /// врагов Улья (+20 %), Улей на 3-м месте +5 %, Слава на 5-м +15 %, Безымянный предел +25 %. В «Коротком забеге» — 0.
    /// </summary>
    public static int EnemyHpBonusPercent(ActKind kind, bool fullRun)
    {
        if (!fullRun && kind is ActKind.Hive or ActKind.Glory) return 0;
        return kind switch
        {
            ActKind.FrozenBarrows or ActKind.ClockworkDepths => 35,
            ActKind.Hive => 5,
            ActKind.BloodCourt or ActKind.StarTower => 20,
            ActKind.Glory => 15,
            ActKind.NamelessLimit => 25,
            _ => 0,
        };
    }

    /// <summary>Слава в полном забеге: элиты получают +1 аффикс.</summary>
    public static int EliteAffixBonus(ActKind kind, bool fullRun) => fullRun && kind == ActKind.Glory ? 1 : 0;

    public static ActRule RuleOf(ActKind kind) => kind switch
    {
        ActKind.Foothills => ActRule.Apprenticeship,
        ActKind.FrozenBarrows => ActRule.Frost,
        ActKind.ClockworkDepths => ActRule.Steam,
        ActKind.Hive => ActRule.AbyssRifts,
        ActKind.BloodCourt => ActRule.DayNight,
        ActKind.StarTower => ActRule.LeyLines,
        ActKind.Glory => ActRule.AbyssInvasion,
        _ => ActRule.None,
    };

    // ───── 2а: Стужа, Тьма, Запертые склепы ─────

    /// <summary>Стужа: Озноб в начале каждого боя.</summary>
    public const int FrostChill = 1;

    /// <summary>Отогреться у костра: столько следующих боёв без Стужи.</summary>
    public const int WarmthFights = 2;

    /// <summary>Тьма курганов: сколько рядов вперёд видно (с Ночным зрением/факелом — 4).</summary>
    public static int DarknessVisibleRows(bool nightSight) => nightSight ? 4 : 2;

    /// <summary>Узел скрыт тьмой (виден только силуэт).</summary>
    public static bool IsHiddenByDarkness(int nodeRow, int currentRow, bool nightSight) =>
        nodeRow - Math.Max(0, currentRow) > DarknessVisibleRows(nightSight);

    /// <summary>Запертых склепов на карте Курганов: 2–3.</summary>
    public static int LockedVaultCount(int roll) => 2 + Math.Abs(roll % 2);

    /// <summary>Когтистых ключей за акт с элит — не больше 2.</summary>
    public const int MaxClawKeysPerAct = 2;

    /// <summary>Золото из склепа без ключа нет; с ключом — дополнительная реликвия и золото.</summary>
    public const int VaultGold = 40;

    // ───── 2б: Пар и вентили ─────

    /// <summary>Вентилей на карте Глубин: 3–4.</summary>
    public static int ValveCount(int roll) => 3 + Math.Abs(roll % 2);

    public const int OverheatPeriod = 4;
    public const int OverheatBurning = 3;

    /// <summary>Паровой выброс — в начале хода героев каждого 4-го раунда.</summary>
    public static bool IsOverheatRound(int round) => round > 0 && round % OverheatPeriod == 0;

    /// <summary>«Закрыть клапан» приходит в руку за ход до выброса.</summary>
    public static bool IsOverheatWarningRound(int round) => round > 0 && round % OverheatPeriod == OverheatPeriod - 1;

    /// <summary>Перекрытый вентиль: столько следующих боёв без Перегрева.</summary>
    public const int ValveReliefFights = 3;

    // ───── 4а: День и ночь ─────

    public const int DayNightBlock = 3;

    /// <summary>Ряды карты чередуются блоками по 3: ряды 1–3 — день, 4–6 — ночь, …</summary>
    public static bool IsNight(int row) => row >= 1 && (row - 1) / DayNightBlock % 2 == 1;

    public const int NightHpBonusPercent = 25;
    public const int NightGoldBonus = 15;

    // ───── 4б: Лей-линии ─────

    /// <summary>Лей-линий (колонок карты) в Башне: 2–3.</summary>
    public static int LeyLineCount(int roll) => 2 + Math.Abs(roll % 2);

    /// <summary>Бой на лей-линии: +✦ всем в начале боя (и врагам-магам).</summary>
    public const int LeyLineMana = 3;

    // ───── 3 и 5: Бездна ─────

    /// <summary>
    /// Срабатывает ли Бездна после входа в узел ряда <paramref name="row"/>: Трещины — после каждого 3-го узла,
    /// Вторжение — после каждого.
    /// </summary>
    public static bool IsRiftTrigger(ActRule rule, int row) => rule switch
    {
        ActRule.AbyssRifts => row > 0 && row % 3 == 0,
        ActRule.AbyssInvasion => row > 0,
        _ => false,
    };

    /// <summary>
    /// Может ли узел стать Вратами: выше текущего ряда, не магазин/лагерь/старт/босс; Трещины Улья — только бои.
    /// </summary>
    public static bool CanBecomeGate(ActRule rule, int nodeRow, int currentRow, bool isCombat, bool isShopOrRest, bool isSpecial)
    {
        if (nodeRow <= currentRow || isShopOrRest || isSpecial) return false;
        return rule switch
        {
            ActRule.AbyssRifts => isCombat,
            ActRule.AbyssInvasion => true,
            _ => false,
        };
    }

    /// <summary>Индекс среди кандидатов по броску (без отрицательных).</summary>
    public static int PickIndex(int roll, int count) => count <= 0 ? -1 : ((roll % count) + count) % count;

    /// <summary>Выбрать <paramref name="count"/> разных индексов из <paramref name="n"/> по броскам.</summary>
    public static List<int> PickDistinct(int n, int count, IEnumerable<int> rolls)
    {
        var pool = Enumerable.Range(0, Math.Max(0, n)).ToList();
        var result = new List<int>();
        using var roll = rolls.GetEnumerator();
        while (result.Count < count && pool.Count > 0 && roll.MoveNext())
        {
            var i = PickIndex(roll.Current, pool.Count);
            result.Add(pool[i]);
            pool.RemoveAt(i);
        }
        result.Sort();
        return result;
    }

    // ───── элиты ─────

    /// <summary>Редкая элита акта: шанс 15 % на каждый элитный узел (максимум одна за акт).</summary>
    public const int RareEliteChancePercent = 15;

    public static bool RollRareElite(int roll, bool alreadyMetThisAct) => !alreadyMetThisAct && roll < RareEliteChancePercent;

    // ───── супербоссы ─────

    public static int SuperbossHp(Superboss boss) => boss switch
    {
        Superboss.BlackWanderer => 220,
        Superboss.BarrowReaper => 300,
        Superboss.DeepMasterSmith => 320,
        Superboss.GatebreakerEnvoy => 420,
        Superboss.BloodCourtLord => 450,
        Superboss.FirstSummoner => 430,
        Superboss.WorldEater => 600,
        Superboss.NamelessShadow => 700,
        _ => 300,
    };

    /// <summary>Супербосс Багрового портала акта (у Безымянного предела Тень — главный босс, портала нет).</summary>
    public static Superboss? SuperbossFor(ActKind kind) => kind switch
    {
        ActKind.Foothills => Superboss.BlackWanderer,
        ActKind.FrozenBarrows => Superboss.BarrowReaper,
        ActKind.ClockworkDepths => Superboss.DeepMasterSmith,
        ActKind.Hive => Superboss.GatebreakerEnvoy,
        ActKind.BloodCourt => Superboss.BloodCourtLord,
        ActKind.StarTower => Superboss.FirstSummoner,
        ActKind.Glory => Superboss.WorldEater,
        _ => null,
    };

    /// <summary>Побед над супербоссами (маска бит Superboss) для открытия Безымянного предела.</summary>
    public const int SuperbossesForNameless = 5;

    public static int CountSuperbosses(int mask)
    {
        var count = 0;
        for (var b = 0; b < (int)Superboss.NamelessShadow; b++)
            if ((mask & (1 << b)) != 0) count++;
        return count;
    }

    /// <summary>Безымянный предел открывается в полном забеге после пяти супербоссов.</summary>
    public static bool NamelessUnlocked(int defeatedMask, bool fullRun) => fullRun && CountSuperbosses(defeatedMask) >= SuperbossesForNameless;

    /// <summary>Портал обязателен: настройка «Всегда» или «Реквием 10».</summary>
    public static bool PortalMandatory(bool alwaysSetting, int requiemLevel) =>
        alwaysSetting || RequiemLevelRules.SuperbossesMandatory(requiemLevel);

    /// <summary>Свечей Реквиема за супербоссов — не больше одной за забег.</summary>
    public const int MaxSuperbossCandles = 1;
}

/// <summary>Код отметки на карте (Врата, склепы, вентили, лей-линии) — одно число для сохранения в Дорожной карте.</summary>
public static class MapMarks
{
    public static int Encode(int actIndex, int col, int row) => actIndex * 10000 + col * 100 + row;

    public static (int Act, int Col, int Row) Decode(int code) => (code / 10000, code / 100 % 100, code % 100);
}
