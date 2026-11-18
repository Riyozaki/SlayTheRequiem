namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Форма-трансформация (docs/design/06 §1, §1.5). Заводное сердце — не мировоззрение, а «сердце».</summary>
public enum FormKind
{
    None,
    Righteous,
    Vampire,
    Werewolf,
    DarkPact,
    Herald,
    Lich,
    Clockwork,
}

/// <summary>Путь Героя (06 §2). Порядок = порядок таблицы.</summary>
public enum HeroPath
{
    None,
    Paladin,
    BlackKnight,
    Champion,
    Barbarian,
    Monk,
    Assassin,
    Blightbringer,
    MysticArcher,
    ElementLord,
    Battlemage,
    Reflector,
    Medium,
    Reborn,
}

/// <summary>Стихия (Повелитель стихии, Мистический лучник, сферы Заводного сердца).</summary>
public enum Element { Fire, Frost, Shock }

/// <summary>Стойка Вестника (06 §1.4).</summary>
public enum HeraldStance { Plague, Discord, Decay }

/// <summary>Ветка Носителя Зверя (06 §1.3).</summary>
public enum BeastBranch { Hunter, Stalker, Alpha }

/// <summary>Чары карт (07 §6.2).</summary>
public enum CardEnchant { Flaming, Frost, Storm, Lightened, Breathing, Double }

/// <summary>Строка таблицы Жажды вампира (06 §1.2).</summary>
public readonly record struct ThirstRow(
    int Regen,
    int FireResistance,
    int AttackBonus,
    decimal FireLightTakenMultiplier,
    int HealPercent,
    int LifestealPercent,
    bool HealingOnlyByFeeding);

/// <summary>
/// Трансформации и Пути (docs/design/06): совместимость, Жажда, Обращение, Вестник/Лич, Заводное сердце,
/// предложение Пути Героя, числа Сущностей, сложение процентов лечения, чары карт. Чистые функции — зеркало в tools/sim.
/// </summary>
public static class TransformationRules
{
    // ═════════════ совместимость (06 §3) ═════════════

    /// <summary>Мировоззрение (одно на забег): всё, кроме Заводного сердца.</summary>
    public static bool IsWorldview(FormKind kind) => Enum.IsDefined(kind) && kind is not (FormKind.None or FormKind.Clockwork);

    /// <summary>Формы, делающие Отступником (пока активны): вампир, оборотень, путь Договора.</summary>
    public static bool IsApostateForm(FormKind kind) =>
        kind is FormKind.Vampire or FormKind.Werewolf or FormKind.DarkPact or FormKind.Herald or FormKind.Lich;

    /// <summary>Ступень пути Договора: Договор → Вестник → Лич.</summary>
    public static bool IsChainStep(FormKind from, FormKind to) =>
        (from, to) is (FormKind.DarkPact, FormKind.Herald) or (FormKind.Herald, FormKind.Lich);

    /// <summary>
    /// Можно ли принять мировоззрение <paramref name="next"/> при текущем <paramref name="current"/>.
    /// Праведник — только при праведности «Праведник»; Вестник и Лич — только ступенью Договора;
    /// Ординатор (предыстория) совместим только с Праведником; Заводное сердце — только с Праведником.
    /// </summary>
    public static bool CanTakeWorldview(FormKind current, FormKind next, bool righteousPiety, bool ordinator, bool clockwork)
    {
        if (!IsWorldview(next)) return false;
        if (ordinator && OrdinatorForbids(next)) return false;
        if (clockwork && next != FormKind.Righteous) return false;
        if (next is FormKind.Herald or FormKind.Lich) return IsChainStep(current, next);
        if (current != FormKind.None) return false;
        return next != FormKind.Righteous || righteousPiety;
    }

    /// <summary>Ординатор (04, 06 §3): запрещены Праведник, вампиризм, ликантропия и лич. Договор и Вестник — не запрещены.</summary>
    public static bool OrdinatorForbids(FormKind form) =>
        form is FormKind.Righteous or FormKind.Vampire or FormKind.Werewolf or FormKind.Lich;

    /// <summary>Заводное сердце совместимо только с Праведником (06 §1.5).</summary>
    public static bool CanTakeClockwork(FormKind worldview, bool hasClockwork) =>
        !hasClockwork && worldview is FormKind.None or FormKind.Righteous;

    // ═════════════ Праведник (06 §1.1) ═════════════

    public const int HaloThresholdPercent = 50;
    public const int HaloHeal = 10;
    public const int HaloSpellShield = 10;
    public const int SmiteDamage = 8;
    public const int SmiteDamageUpgrade = 3;
    public const int SmiteStagger = 5;
    /// <summary>Ступени Праведника: 0 Ореол, 1 Аура стойкости (Латы +1), 2 Небесный щит (открывает Дух божества).</summary>
    public const int MaxRighteousStep = 2;
    public const int AuraPlating = 1;

    public static bool HaloTriggers(int hp, int maxHp, bool usedThisCombat) =>
        !usedThisCombat && maxHp > 0 && hp > 0 && hp * 100 < maxHp * HaloThresholdPercent;

    /// <summary>Обет выполнен (босс акта побеждён Праведником без Бездонных вещей) — следующая ступень.</summary>
    public static int RighteousStepAfterAct(int step, bool vowKept) =>
        vowKept ? Math.Min(MaxRighteousStep, step + 1) : step;

    // ═════════════ Дитя Ночи (06 §1.2) ═════════════

    public const int MaxThirst = 4;
    public const int BiteDamage = 6;
    public const int BiteDamageUpgrade = 3;
    public const int NightWingsThresholdPercent = 25;
    public const int BloodLordFeedings = 3;
    public const int BloodLordBlight = 1;
    public const int BloodLordLifestealPercent = 25;

    /// <summary>Таблица Жажды: 0 — сыт; 1 — только минус сытости; 2–3 — «жажда»; 4 — голод.</summary>
    public static ThirstRow Thirst(int thirst) => Math.Clamp(thirst, 0, MaxThirst) switch
    {
        0 => new ThirstRow(1, -1, 0, 1m, 0, 0, false),
        1 => new ThirstRow(0, -1, 0, 1m, 0, 0, false),
        2 or 3 => new ThirstRow(0, 0, 2, 1.5m, -50, 0, false),
        _ => new ThirstRow(0, 0, 4, 2m, -100, 25, true),
    };

    /// <summary>После боя: без кормления Жажда +1 (не выше 4), с кормлением — 0.</summary>
    public static int ThirstAfterCombat(int thirst, bool fedThisCombat) =>
        fedThisCombat ? 0 : Math.Min(MaxThirst, Math.Max(0, thirst) + 1);

    public static bool NightWingsTrigger(int hp, int maxHp, bool usedThisCombat) =>
        !usedThisCombat && maxHp > 0 && hp > 0 && hp * 100 < maxHp * NightWingsThresholdPercent;

    public static bool BecomesBloodLord(int feedings) => feedings >= BloodLordFeedings;

    /// <summary>Вампиризм N %: лечение от нанесённого урона (вниз).</summary>
    public static int Lifesteal(int dealt, int percent) => Math.Max(0, dealt) * Math.Max(0, percent) / 100;

    // ═════════════ Носитель Зверя (06 §1.3) ═════════════

    public const int MoonCallTurns = 3;
    public const int MoonCallEnergy = 2;
    public const int BeastEntryBlock = 15;
    public const int BeastAttackBonus = 4;
    public const int HitsPerExtension = 3;
    public const int DevourHeal = 6;
    public const int DevourTurns = 1;
    public const int BeastRestHealPercent = -25;
    public const int NodesPerBranch = 3;
    /// <summary>Каждые N Обращений (в выигранных боях) — новый узел ветки.</summary>
    public const int TransformationsPerNode = 2;

    /// <summary>Ходы формы при Обращении: 3, «Ловчий» I и III — ещё по 1.</summary>
    public static int MoonCallDuration(int stalkerNodes) =>
        MoonCallTurns + (stalkerNodes >= 1 ? 1 : 0) + (stalkerNodes >= 3 ? 1 : 0);

    /// <summary>Ударов на +1 ход формы: 3, «Ловчий» II — 2.</summary>
    public static int HitsPerExtensionFor(int stalkerNodes) => stalkerNodes >= 2 ? 2 : HitsPerExtension;

    /// <summary>Сколько ходов добавить: каждые N ударов — +1 (счётчик ударов копится за всю форму).</summary>
    public static int ExtensionTurns(int hitsBefore, int hitsAfter, int perExtension) =>
        perExtension <= 0 ? 0 : Math.Max(0, hitsAfter / perExtension - hitsBefore / perExtension);

    /// <summary>Надбавка урона атак в форме: +4, «Охотник» I и III — ещё по +2.</summary>
    public static int BeastAttackBonusFor(int hunterNodes) =>
        BeastAttackBonus + (hunterNodes >= 1 ? 2 : 0) + (hunterNodes >= 3 ? 2 : 0);

    /// <summary>«Охотник» II: звериные атаки накладывают Кровотечение 2.</summary>
    public static int BeastBleed(int hunterNodes) => hunterNodes >= 2 ? 2 : 0;

    /// <summary>«Альфа»: ♥ волка при Обращении (0 — без волка).</summary>
    public static int AlphaWolfHp(int alphaNodes) => alphaNodes switch
    {
        <= 0 => 0,
        1 => 8,
        2 => 12,
        _ => 16,
    };

    /// <summary>«Альфа» III: +1 слот призыва.</summary>
    public static int AlphaSummonSlots(int alphaNodes) => alphaNodes >= 3 ? 1 : 0;

    /// <summary>Узлов заработано за Обращения.</summary>
    public static int BeastNodesEarned(int transformations) =>
        Math.Min(NodesPerBranch * 3, Math.Max(0, transformations) / TransformationsPerNode);

    // ═════════════ Договор → Вестник → Лич (06 §1.4) ═════════════

    public const int PactManaPool = 4;
    public const int PactNecromancyDiscount = 1;
    public const int PactMaxHpLoss = 8;
    public const int HeraldBlight = 2;
    public const int HeraldWeaken = 1;
    public const int HeraldSunder = 1;
    public const int LichWinsRequired = 10;
    public const int LichMaxHpPercent = 30;
    public const decimal LichPhysicalTakenMultiplier = 0.75m;
    public const int LichDeathBlast = 5;
    public const int PhylacteryRevivePercent = 30;
    public const int PhylacteryWeight = 3;

    public static HeraldStance NextStance(HeraldStance stance) => (HeraldStance)(((int)stance + 1) % 3);

    /// <summary>Сколько макс. ♥ забирает «кость» Лича (−30 %, вниз, но так, чтобы остался хотя бы 1).</summary>
    public static int LichMaxHpLoss(int maxHp) => Math.Clamp(maxHp * LichMaxHpPercent / 100, 0, Math.Max(0, maxHp - 1));

    public static bool CanBecomeLich(FormKind current, int heraldWins) =>
        current == FormKind.Herald && heraldWins >= LichWinsRequired;

    public static int PhylacteryReviveHp(int maxHp) => Math.Max(1, maxHp * PhylacteryRevivePercent / 100);

    // ═════════════ Заводное сердце (06 §1.5) ═════════════

    public const int ClockworkPlating = 2;
    public const int ClockworkHealPercent = -50;
    public const int MaxOrbs = 3;
    public const int SteamBurning = 3;
    public const int SparkStatic = 1;
    public const int SparkDamage = 3;
    public const int RimeChill = 2;

    public static Element OrbFor(int index) => (Element)(Math.Abs(index) % 3);

    // ═════════════ лечение ═════════════

    /// <summary>Проценты лечения складываются; итог не ниже −100 % (лечение не уходит в минус).</summary>
    public static int HealPercent(IEnumerable<int> modifiers) => Math.Max(-100, modifiers.Sum());

    /// <summary>Итог лечения: запрет (Хворь, голод) — 0; иначе количество × (100 + %) / 100, вниз.</summary>
    public static decimal ApplyHeal(decimal amount, int percent, bool blocked)
    {
        if (blocked || amount <= 0) return 0m;
        return Math.Max(0m, Math.Floor(amount * (100 + Math.Max(-100, percent)) / 100m));
    }

    // ═════════════ события трансформаций ═════════════

    /// <summary>Доля «?», которая станет событием трансформации (в подходящих актах).</summary>
    public const int EventChancePercent = 12;

    /// <summary>«Поздний» акт (Лич, Филактерия): 4–5 при пяти актах; последний — при обычном забеге.</summary>
    public static bool IsLateAct(int actIndex, int totalActs) =>
        actIndex >= (totalActs >= 5 ? 3 : Math.Max(1, totalActs - 1));

    /// <summary>
    /// Какие формы можно встретить событием в акте (0-based): Зверь и Договор — акты 1–2; Праведник и Заводное сердце —
    /// 2–3; Пир в склепе — 2–4; Филактерия — поздние акты.
    /// </summary>
    public static IReadOnlyList<FormKind> EventFormsFor(int actIndex, int totalActs)
    {
        var list = new List<FormKind>();
        if (actIndex <= 1) list.Add(FormKind.Werewolf);
        if (actIndex <= 1) list.Add(FormKind.DarkPact);
        if (actIndex is 1 or 2) list.Add(FormKind.Righteous);
        if (actIndex is 1 or 2) list.Add(FormKind.Clockwork);
        if (actIndex is >= 1 and <= 3) list.Add(FormKind.Vampire);
        if (IsLateAct(actIndex, totalActs)) list.Add(FormKind.Lich);
        return list;
    }

    /// <summary>
    /// Бросок «?»: <paramref name="roll"/> 0–99 — шанс; <paramref name="pick"/> — выбор среди невиденных.
    /// <paramref name="seenMask"/> — бит (1 &lt;&lt; FormKind) за каждое уже встреченное событие. None — оставить «?».
    /// </summary>
    public static FormKind UnknownRoomEvent(int actIndex, int totalActs, int roll, int pick, int seenMask)
    {
        if (roll < 0 || roll >= EventChancePercent) return FormKind.None;
        var options = EventFormsFor(actIndex, totalActs).Where(k => (seenMask & (1 << (int)k)) == 0).ToList();
        return options.Count == 0 ? FormKind.None : options[Math.Abs(pick) % options.Count];
    }

    // ═════════════ Путь Героя (06 §2) ═════════════

    public const int PathLevelGate = 8;
    public const int PathOffers = 3;
    public const int PathCardsPerPath = 3;
    /// <summary>Доля наград-карт, заменяемых картой Пути (если у героя есть Путь).</summary>
    public const int PathCardRewardPercent = 25;

    /// <summary>После какого босса предлагается Путь: 3-го акта (пять актов) или предпоследнего (обычный забег).</summary>
    public static int PathOfferActIndex(int totalActs) => totalActs >= 5 ? 2 : Math.Max(0, totalActs - 2);

    public static bool PathOfferDue(int level, int bossActIndex, int totalActs, bool hasPath) =>
        !hasPath && level >= PathLevelGate && bossActIndex >= PathOfferActIndex(totalActs);

    /// <summary>
    /// Требования Пути: Паладин — форма Праведника (или Ординатор, 06 §3); Чёрный рыцарь — Отступник (Ординатору закрыт);
    /// Монах — без оружия.
    /// </summary>
    public static bool PathAllowed(HeroPath path, FormKind worldview, bool apostate, bool unarmed, bool ordinator = false) => path switch
    {
        HeroPath.None => false,
        HeroPath.Paladin => worldview == FormKind.Righteous || ordinator,
        HeroPath.BlackKnight => !ordinator && apostate && worldview != FormKind.Righteous,
        HeroPath.Monk => unarmed,
        _ => true,
    };

    /// <summary>«Свои» Пути героя (колонка «Для кого»).</summary>
    public static IReadOnlyList<HeroPath> NativePaths(string? heroKey, FormKind worldview, bool apostate, bool unarmed, bool ordinator = false)
    {
        var list = new List<HeroPath>();
        if (worldview == FormKind.Righteous || ordinator) list.Add(HeroPath.Paladin);
        if (!ordinator && apostate && worldview != FormKind.Righteous) list.Add(HeroPath.BlackKnight);
        switch (heroKey)
        {
            case "Ironclad":
                list.AddRange([HeroPath.Champion, HeroPath.Barbarian, HeroPath.Battlemage]);
                break;
            case "Silent":
                list.AddRange([HeroPath.Assassin, HeroPath.Blightbringer, HeroPath.MysticArcher]);
                break;
            case "Regent":
                list.AddRange([HeroPath.ElementLord, HeroPath.Battlemage, HeroPath.Reflector, HeroPath.Medium, HeroPath.MysticArcher]);
                break;
        }
        if (unarmed) list.Add(HeroPath.Monk);
        return list.Distinct().ToList();
    }

    public static IReadOnlyList<HeroPath> AllPaths =>
        Enum.GetValues<HeroPath>().Where(p => p != HeroPath.None).ToList();

    /// <summary>
    /// Предложение: 2 «своих» (разных) + 1 случайный чужой из допустимых. Если своих меньше — добор чужими.
    /// <paramref name="next"/>(n) — случайное в [0, n).
    /// </summary>
    public static List<HeroPath> OfferPaths(string? heroKey, FormKind worldview, bool apostate, bool unarmed, Func<int, int> next,
        bool ordinator = false)
    {
        var native = NativePaths(heroKey, worldview, apostate, unarmed, ordinator).ToList();
        var foreign = AllPaths.Where(p => !native.Contains(p) && PathAllowed(p, worldview, apostate, unarmed, ordinator)).ToList();
        var result = new List<HeroPath>();
        for (var i = 0; i < 2 && native.Count > 0; i++)
        {
            var pick = native[next(native.Count)];
            native.Remove(pick);
            result.Add(pick);
        }
        while (result.Count < PathOffers && (foreign.Count > 0 || native.Count > 0))
        {
            var pool = foreign.Count > 0 ? foreign : native;
            var pick = pool[next(pool.Count)];
            pool.Remove(pick);
            result.Add(pick);
        }
        return result;
    }

    // ───── числа Сущностей ─────

    public const int PaladinPlating = 1;
    public const int PaladinUndeadReduction = 1;
    public const int BlackKnightFearInterval = 2;
    public const int BlackKnightFearStrength = 1;
    public const int BlackKnightAttackHeal = 1;
    public const int ChampionHeavyStagger = 2;
    public const int BarbarianRageThresholdPercent = 50;
    public const decimal BarbarianRageMultiplier = 1.5m;
    public const int MonkEveryNthStagger = 3;
    public const int MonkStagger = 3;
    public const decimal AssassinShadowStepMultiplier = 2m;
    public const int BlightbringerPoisonBonus = 1;
    public const int MysticArcherStacks = 2;
    public const int ElementLordStatusBonus = 1;
    public const int BattlemageCloak = 2;
    public const int BattlemageElementalDamage = 2;
    public const int ReflectorPercent = 50;
    public const int MediumSummonSlots = 1;
    public const int MediumSummonDamage = 2;

    /// <summary>Безоружная атака Монаха: 4 + уровень/2.</summary>
    public static int MonkUnarmedDamage(int level) => 4 + Math.Max(1, level) / 2;

    public static bool BarbarianRage(int hp, int maxHp) => maxHp > 0 && hp * 100 < maxHp * BarbarianRageThresholdPercent;

    /// <summary>Чёрный рыцарь: Аура страха в начале каждого 2-го хода (2, 4, 6…).</summary>
    public static bool FearTurn(int round) => round > 0 && round % BlackKnightFearInterval == 0;

    /// <summary>Отражатель: 50 % поглощённого Оберегом урона заклинаний.</summary>
    public static int Reflected(int absorbed) => Math.Max(0, absorbed) * ReflectorPercent / 100;

    /// <summary>Чемпион: Ошеломление по Непоколебимым — половина (вверх).</summary>
    public static int ChampionUnshakeableStagger(int stagger) => (Math.Max(0, stagger) + 1) / 2;

    /// <summary>Статус стихии и его сила у Мистического лучника/Повелителя стихии.</summary>
    public static int ElementStacks(Element element, int bonus) => element switch
    {
        Element.Fire => 2 + bonus,
        Element.Frost => 1 + bonus,
        _ => 1 + bonus,
    };

    // ═════════════ чары карт (07 §6.2) ═════════════

    public const int FlamingBurning = 3;
    public const int FrostChill = 2;
    public const int StormManaBurn = 2;
    public const int LightenedWeight = 1;
    public const int BreathingStamina = 1;

    /// <summary>Пламенная/Морозная/Грозовая/Дыхательная — только атаки; Облегчённая — любая карта; Двойная — по составу.</summary>
    public static bool CanEnchantCard(CardEnchant enchant, bool isAttack, bool doublePerk) => enchant switch
    {
        CardEnchant.Lightened => true,
        CardEnchant.Double => doublePerk && isAttack,
        _ => isAttack,
    };

    /// <summary>Двойные чары: пара видов в одном числе (a·10 + b), обе — не «Двойная» и разные.</summary>
    public static int EncodeDouble(CardEnchant a, CardEnchant b) => (int)a * 10 + (int)b;

    public static (CardEnchant A, CardEnchant B) DecodeDouble(int code) =>
        ((CardEnchant)Math.Clamp(code / 10, 0, 4), (CardEnchant)Math.Clamp(code % 10, 0, 4));

    public static bool ValidDouble(CardEnchant a, CardEnchant b) =>
        a != b && a != CardEnchant.Double && b != CardEnchant.Double;

    public static int CardWeightAfter(int weight, bool lightened) => lightened ? Math.Max(0, weight - LightenedWeight) : weight;

    public static int StaminaAfter(int cost, bool breathing) => breathing ? Math.Max(0, cost - BreathingStamina) : cost;
}
