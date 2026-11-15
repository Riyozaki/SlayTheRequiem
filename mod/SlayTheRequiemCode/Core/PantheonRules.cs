namespace SlayTheRequiem.SlayTheRequiemCode.Core;

// Пантеон и Знамения (docs/design/05): чистые правила без зависимостей от игры — зеркало в tools/sim/requiem_rules.py.

/// <summary>Знамения (Камни-Хранители) — одно на забег, выбирается у Нео.</summary>
public enum Omen { Blade, Bastion, Eye, Sponge, Rite, Burden, Trickster, Voice, Shade, Serpent }

/// <summary>Осколки отлива — временно, до конца акта («Шесть камней у воды»).</summary>
public enum EbbShard { Earth, Root, Beast, Water, Wind, Sun }

/// <summary>Праведность: Праведник (старт) → Запятнанный → Отступник.</summary>
public enum Piety { Righteous = 0, Tainted = 1, Apostate = 2 }

/// <summary>Небесные (аэдра) — одно благословение за раз.</summary>
public enum Celestial
{
    WingedHour, AnvilFather, ScrollKeeper, MirrorMaiden, WindMistress,
    HearthMother, ThresholdWarden, MercyShield, ThunderCrowned, SunArcher,
}

/// <summary>Князья Бездны (даэдра) — один дар за раз. Ловчий и Владыка Цепей — пути этапа 6.</summary>
public enum AbyssPrince { PlagueLord, TwilightWidow, WebWeaver, GateBreaker, Treachery, Revel, Outcast, DuskStar }

/// <summary>Болезни Повелителя Мора: до Обета — только минус, после — плюс и минус.</summary>
public enum Disease { ShakyGait, StoneGout, MindRot, Wheeze, Bonebreak, Babble }

/// <summary>Сила благословения Небесного при текущей праведности.</summary>
public enum BlessingStrength { None = 0, Dimmed = 1, Full = 2 }

/// <summary>
/// Набор числовых бонусов/штрафов, которыми описываются Знамения, Осколки, благословения, дары и болезни.
/// Игровая сторона применяет его единообразно (BoonRelic): пулы — через IResourceSource, силы — в начале боя,
/// урон — хуками. Отрицательные значения — штрафы (уязвимость = сопротивление −1).
/// </summary>
public sealed record Boon
{
    public static readonly Boon None = new();

    public int MaxHp { get; init; }
    public int StaminaPool { get; init; }
    public int StaminaRegen { get; init; }
    public int ManaPool { get; init; }
    public int ManaRegen { get; init; }
    public int ManaStart { get; init; }
    public int Poise { get; init; }
    public int Burden { get; init; }

    /// <summary>Латы в начале боя (отрицательные — каждый удар по вам сильнее).</summary>
    public int Plating { get; init; }
    /// <summary>Покров N.</summary>
    public int Ward { get; init; }
    public int SpellAbsorption { get; init; }
    /// <summary>Регенерация N: лечение в начале каждого хода.</summary>
    public int Regen { get; init; }

    /// <summary>Урон ваших атак (картой).</summary>
    public int AttackDamage { get; init; }
    /// <summary>Урон «тяжёлых» атак (цена ≥ 2 ◆).</summary>
    public int HeavyAttackDamage { get; init; }
    /// <summary>Урон Кличей.</summary>
    public int ShoutDamage { get; init; }
    /// <summary>Перезарядка Кличей короче на N (полка Кличей — Cards/Shouts.cs).</summary>
    public int ShoutCooldown { get; init; }
    /// <summary>Урон атак по нежити и Бездне.</summary>
    public int UndeadDamage { get; init; }
    /// <summary>Сияние N: атака по нежити/Бездне добавляет N урона светом.</summary>
    public int UndeadLight { get; init; }
    /// <summary>Пробитие N: ваши атаки игнорируют N Лат цели.</summary>
    public int Pierce { get; init; }
    /// <summary>Физический урон атак по вам за удар (+ — штраф).</summary>
    public int PhysicalTaken { get; init; }

    /// <summary>Карт в первой руке боя.</summary>
    public int OpeningHand { get; init; }

    public int FireRes { get; init; }
    public int FrostRes { get; init; }
    public int ShockRes { get; init; }
    public int PoisonRes { get; init; }
    public int MagicRes { get; init; }
    public int BlightRes { get; init; }

    /// <summary>Ауры на врагов в начале боя (−1 = уязвимость I).</summary>
    public int EnemyFireRes { get; init; }
    public int EnemyFrostRes { get; init; }
    public int EnemyShockRes { get; init; }
    public int EnemyPoisonRes { get; init; }
    public int EnemyMagicRes { get; init; }

    /// <summary>Проценты: лечение на отдыхе, цены лавок, золото (±N %).</summary>
    public int RestHealPercent { get; init; }
    public int MerchantPricePercent { get; init; }
    public int GoldPercent { get; init; }

    public int SummonSlots { get; init; }
    public int SummonDamage { get; init; }
    /// <summary>Урон по призывам (+50 = на 50 % больше).</summary>
    public int SummonTakenPercent { get; init; }
    /// <summary>Владение всеми школами магии.</summary>
    public int MasteryAll { get; init; }

    /// <summary>Иммунитеты к контролю (Обледенение, Паралич, Шок).</summary>
    public bool ImmuneFrozen { get; init; }
    public bool ImmuneParalysis { get; init; }
    public bool ImmuneShock { get; init; }

    public static Boon operator +(Boon a, Boon b) => new()
    {
        MaxHp = a.MaxHp + b.MaxHp,
        StaminaPool = a.StaminaPool + b.StaminaPool,
        StaminaRegen = a.StaminaRegen + b.StaminaRegen,
        ManaPool = a.ManaPool + b.ManaPool,
        ManaRegen = a.ManaRegen + b.ManaRegen,
        ManaStart = a.ManaStart + b.ManaStart,
        Poise = a.Poise + b.Poise,
        Burden = a.Burden + b.Burden,
        Plating = a.Plating + b.Plating,
        Ward = a.Ward + b.Ward,
        SpellAbsorption = a.SpellAbsorption + b.SpellAbsorption,
        Regen = a.Regen + b.Regen,
        AttackDamage = a.AttackDamage + b.AttackDamage,
        HeavyAttackDamage = a.HeavyAttackDamage + b.HeavyAttackDamage,
        ShoutDamage = a.ShoutDamage + b.ShoutDamage,
        ShoutCooldown = a.ShoutCooldown + b.ShoutCooldown,
        UndeadDamage = a.UndeadDamage + b.UndeadDamage,
        UndeadLight = a.UndeadLight + b.UndeadLight,
        Pierce = a.Pierce + b.Pierce,
        PhysicalTaken = a.PhysicalTaken + b.PhysicalTaken,
        OpeningHand = a.OpeningHand + b.OpeningHand,
        FireRes = a.FireRes + b.FireRes,
        FrostRes = a.FrostRes + b.FrostRes,
        ShockRes = a.ShockRes + b.ShockRes,
        PoisonRes = a.PoisonRes + b.PoisonRes,
        MagicRes = a.MagicRes + b.MagicRes,
        BlightRes = a.BlightRes + b.BlightRes,
        EnemyFireRes = a.EnemyFireRes + b.EnemyFireRes,
        EnemyFrostRes = a.EnemyFrostRes + b.EnemyFrostRes,
        EnemyShockRes = a.EnemyShockRes + b.EnemyShockRes,
        EnemyPoisonRes = a.EnemyPoisonRes + b.EnemyPoisonRes,
        EnemyMagicRes = a.EnemyMagicRes + b.EnemyMagicRes,
        RestHealPercent = a.RestHealPercent + b.RestHealPercent,
        MerchantPricePercent = a.MerchantPricePercent + b.MerchantPricePercent,
        GoldPercent = a.GoldPercent + b.GoldPercent,
        SummonSlots = a.SummonSlots + b.SummonSlots,
        SummonDamage = a.SummonDamage + b.SummonDamage,
        SummonTakenPercent = a.SummonTakenPercent + b.SummonTakenPercent,
        MasteryAll = a.MasteryAll + b.MasteryAll,
        ImmuneFrozen = a.ImmuneFrozen || b.ImmuneFrozen,
        ImmuneParalysis = a.ImmuneParalysis || b.ImmuneParalysis,
        ImmuneShock = a.ImmuneShock || b.ImmuneShock,
    };

    /// <summary>Все числа ×k (Отверженный после Обета «бонусы удваиваются»). Иммунитеты не меняются.</summary>
    public Boon Scaled(int k) => new()
    {
        MaxHp = MaxHp * k, StaminaPool = StaminaPool * k, StaminaRegen = StaminaRegen * k, ManaPool = ManaPool * k,
        ManaRegen = ManaRegen * k, ManaStart = ManaStart * k, Poise = Poise * k, Burden = Burden * k, Plating = Plating * k,
        Ward = Ward * k, SpellAbsorption = SpellAbsorption * k, Regen = Regen * k, AttackDamage = AttackDamage * k,
        HeavyAttackDamage = HeavyAttackDamage * k, ShoutDamage = ShoutDamage * k, ShoutCooldown = ShoutCooldown * k,
        UndeadDamage = UndeadDamage * k, UndeadLight = UndeadLight * k, Pierce = Pierce * k, PhysicalTaken = PhysicalTaken * k,
        OpeningHand = OpeningHand * k, FireRes = FireRes * k, FrostRes = FrostRes * k, ShockRes = ShockRes * k,
        PoisonRes = PoisonRes * k, MagicRes = MagicRes * k, BlightRes = BlightRes * k, EnemyFireRes = EnemyFireRes * k,
        EnemyFrostRes = EnemyFrostRes * k, EnemyShockRes = EnemyShockRes * k, EnemyPoisonRes = EnemyPoisonRes * k,
        EnemyMagicRes = EnemyMagicRes * k, RestHealPercent = RestHealPercent * k, MerchantPricePercent = MerchantPricePercent * k,
        GoldPercent = GoldPercent * k, SummonSlots = SummonSlots * k, SummonDamage = SummonDamage * k,
        SummonTakenPercent = SummonTakenPercent * k, MasteryAll = MasteryAll * k,
        ImmuneFrozen = ImmuneFrozen, ImmuneParalysis = ImmuneParalysis, ImmuneShock = ImmuneShock,
    };
}

/// <summary>Знамения и Осколки отлива (05 §1).</summary>
public static class OmenRules
{
    public const int OmenCount = 10;
    public const int ShardCount = 6;
    /// <summary>Сколько Знамений предлагает Нео (на высоких уровнях «Реквиема» — все).</summary>
    public const int OmenChoices = 3;
    /// <summary>Сколько Осколков лежит у воды.</summary>
    public const int ShardChoices = 3;
    /// <summary>С какого уровня «Реквиема» (Вознесения) Нео предлагает все 10 Знамений.</summary>
    public const int AllOmensLevel = 8;

    /// <summary>Сколько Знамений предложить на данном уровне.</summary>
    public static int OmenOfferCount(int requiemLevel) => requiemLevel >= AllOmensLevel ? OmenCount : OmenChoices;

    /// <summary>Числовая часть Знамения; особые свойства — у реликвий.</summary>
    public static Boon OmenBoon(Omen omen) => omen switch
    {
        // +6 ♥; атаки +1; +1 прирост ◆; «Устал» не снижает урон (особое).
        Omen.Blade => new Boon { MaxHp = 6, AttackDamage = 1, StaminaRegen = 1 },
        // Латы +1; сопротивление I огню, холоду, разряду; Стойкость +3; первое Оглушение игнорируется (особое).
        Omen.Bastion => new Boon { Plating = 1, FireRes = 1, FrostRes = 1, ShockRes = 1, Poise = 3 },
        // +2 пул ✦, +1 прирост ✦, +1 ко всем школам; враги — уязвимость к магии I.
        Omen.Eye => new Boon { ManaPool = 2, ManaRegen = 1, MasteryAll = 1, EnemyMagicRes = -1 },
        // +6 пул ✦; Поглощение 1; Покров 2. Минус: прирост ✦ −1.
        Omen.Sponge => new Boon { ManaPool = 6, SpellAbsorption = 1, Ward = 2, ManaRegen = -1 },
        // +1 слот призыва; Колдовство −1 ✦ (особое).
        Omen.Rite => new Boon { SummonSlots = 1 },
        // +10 груза; иммунитет к Обледенению/Параличу/Шоку; атаки не дороже 1 ◆ (особое).
        Omen.Burden => new Boon { Burden = 10, ImmuneFrozen = true, ImmuneParalysis = true, ImmuneShock = true },
        // Кража в лавках (особое); +15 % золота.
        Omen.Trickster => new Boon { GoldPercent = 15 },
        // Кличи −1 перезарядка, +3 урона; карты лавок на ступень редкости выше (особое).
        Omen.Voice => new Boon { ShoutCooldown = 1, ShoutDamage = 3 },
        // Невидимость в первый ход, первая атака врага промахивается, атаки из невидимости ×2 (особое).
        Omen.Shade => Boon.None,
        // Яд +1 за наложение, заряды ядов до 5 (особое); сопротивление ядам II; враги −I к ядам.
        Omen.Serpent => new Boon { PoisonRes = 2, EnemyPoisonRes = -1 },
        _ => Boon.None,
    };

    /// <summary>Заряды яда на оружии со Знамением Змея (3 → 5).</summary>
    public const int SerpentExtraCharges = 2;

    /// <summary>Осколок целиком — плюс и минус вместе.</summary>
    public static Boon ShardBoon(EbbShard shard) => shard switch
    {
        EbbShard.Earth => new Boon { StaminaPool = 1, Plating = 1, OpeningHand = -1 },
        EbbShard.Root => new Boon { MagicRes = 1, PoisonRes = 2, FireRes = -1 },
        EbbShard.Beast => new Boon { AttackDamage = 2, Pierce = 1, PhysicalTaken = 1 },
        EbbShard.Water => new Boon { ManaPool = 3, ManaRegen = 1, ShockRes = -1 },
        EbbShard.Wind => new Boon { OpeningHand = 1, StaminaRegen = 1, Plating = -1 },
        EbbShard.Sun => new Boon { MaxHp = 8, Regen = 1, FrostRes = -1 },
        _ => Boon.None,
    };

    /// <summary>
    /// Выбрать <paramref name="count"/> разных индексов из <paramref name="total"/> (частичный Фишер–Йетс).
    /// <paramref name="nextInt"/>(n) → [0, n) — ГСЧ события, чтобы в коопе у всех вышло одно и то же.
    /// </summary>
    public static List<int> PickDistinct(int total, int count, Func<int, int> nextInt, ICollection<int>? exclude = null)
    {
        var pool = Enumerable.Range(0, Math.Max(0, total)).Where(i => exclude == null || !exclude.Contains(i)).ToList();
        count = Math.Clamp(count, 0, pool.Count);
        for (var i = 0; i < count; i++)
        {
            var j = i + Math.Clamp(nextInt(pool.Count - i), 0, pool.Count - i - 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.Take(count).ToList();
    }
}

/// <summary>Праведность (05 §2).</summary>
public static class PietyRules
{
    /// <summary>Кража в лавке, грабёж в событии, дар Князя: −1 ступень (из Запятнанного — Отступник).</summary>
    public static Piety Sin(Piety p) => p == Piety.Righteous ? Piety.Tainted : Piety.Apostate;

    /// <summary>Убийство мирного, Тёмный договор: сразу Отступник.</summary>
    public static Piety GraveSin(Piety p) => Piety.Apostate;

    /// <summary>Искупление: +1 ступень, но не выше Праведника.</summary>
    public static Piety Redeem(Piety p) => p == Piety.Apostate ? Piety.Tainted : Piety.Righteous;

    /// <summary>
    /// Действующая праведность: записанная ступень, но не лучше Запятнанного при Бездонной вещи в инвентаре
    /// и не лучше Отступника при трансформации (вампир/оборотень/лич) или вечном отступничестве (Разрушитель Врат).
    /// </summary>
    public static Piety Effective(Piety recorded, bool holdsAbyssal, bool transformed, bool permanentApostate)
    {
        var p = recorded;
        if (holdsAbyssal && p < Piety.Tainted) p = Piety.Tainted;
        if (transformed || permanentApostate) p = Piety.Apostate;
        return p;
    }

    /// <summary>Праведник — всё (благословение + амулет + облачение), Запятнанный — только благословение, Отступник — ничего.</summary>
    public static BlessingStrength Strength(Piety effective, bool pietyForbidden) =>
        pietyForbidden ? BlessingStrength.None : effective switch
        {
            Piety.Righteous => BlessingStrength.Full,
            Piety.Tainted => BlessingStrength.Dimmed,
            _ => BlessingStrength.None,
        };

    /// <summary>Пожертвование за Искупление: 50 золотых + 25 за каждый акт после первого.</summary>
    public static int RedemptionCost(int actIndex) => 50 + 25 * Math.Max(0, actIndex);

    /// <summary>Искупление раз в акт, если есть что искупать и праведность не запрещена.</summary>
    public static bool CanRedeem(Piety recorded, int lastRedeemedAct, int actIndex, bool forbidden) =>
        !forbidden && recorded != Piety.Righteous && lastRedeemedAct != actIndex;

    /// <summary>Грабёж святилища: 40 золотых + 20 за акт.</summary>
    public static int RobberyGold(int actIndex) => 40 + 20 * Math.Max(0, actIndex);
}

/// <summary>Небесные и Князья Бездны (05 §3–4).</summary>
public static class PantheonRules
{
    public const int CelestialCount = 10;
    public const int PrinceCount = 8;
    public const int DiseaseCount = 6;

    /// <summary>Цена амулета и облачения бога в Святилище.</summary>
    public const int AmuletPrice = 120;
    public const int RobePrice = 150;

    /// <summary>Шанс (в %), что «?» в актах 2+ окажется Святилищем / Тёмным святилищем / Камнями отлива.</summary>
    public const int ShrinePercent = 14;
    public const int DarkShrinePercent = 8;
    public const int EbbStonesPercent = 8;

    /// <summary>Что выпало в «?» (0 — оставить ванильное событие). <paramref name="roll"/> ∈ [0, 100).</summary>
    public static int UnknownRoomRoll(int actIndex, int roll)
    {
        if (actIndex < 1) return 0;
        if (roll < ShrinePercent) return 1;
        if (roll < ShrinePercent + DarkShrinePercent) return 2;
        if (actIndex <= 2 && roll < ShrinePercent + DarkShrinePercent + EbbStonesPercent) return 3;
        return 0;
    }

    /// <summary>Благословение (первый бонус; при Запятнанном действует только оно).</summary>
    public static Boon Blessing(Celestial god) => god switch
    {
        Celestial.WingedHour => new Boon { StaminaPool = 2, FireRes = 1 },
        Celestial.AnvilFather => new Boon { MaxHp = 6, StaminaRegen = 1 },
        Celestial.ScrollKeeper => new Boon { ManaPool = 2, ManaRegen = 1 },
        Celestial.MirrorMaiden => new Boon { ManaRegen = 1, SpellAbsorption = 1 },
        Celestial.WindMistress => new Boon { StaminaRegen = 1, ManaRegen = 1 },
        // Регенерация 2 на 3 хода в начале боя (особое); иммунитет к Параличу.
        Celestial.HearthMother => new Boon { ImmuneParalysis = true },
        Celestial.ThresholdWarden => new Boon { Plating = 1, FrostRes = 1 },
        Celestial.MercyShield => new Boon { AttackDamage = 1, UndeadDamage = 2 },
        Celestial.ThunderCrowned => new Boon { ShoutDamage = 3, ShockRes = 1 },
        Celestial.SunArcher => new Boon { UndeadLight = 2 },
        _ => Boon.None,
    };

    /// <summary>Второй бонус — Амулет бога (бижутерия), только у Праведника с благословением того же бога.</summary>
    public static Boon Amulet(Celestial god) => god switch
    {
        Celestial.WingedHour => new Boon { StaminaRegen = 1 },
        Celestial.AnvilFather => new Boon { Plating = 1 },
        Celestial.ScrollKeeper => new Boon { ManaStart = 2 },
        Celestial.MirrorMaiden => new Boon { Ward = 2 },
        Celestial.WindMistress => new Boon { Poise = 2 },
        Celestial.HearthMother => new Boon { MaxHp = 5 },
        Celestial.ThresholdWarden => new Boon { FrostRes = 1, UndeadDamage = 1 },
        Celestial.MercyShield => new Boon { UndeadDamage = 2 },
        Celestial.ThunderCrowned => new Boon { ShockRes = 1, StaminaPool = 1 },
        Celestial.SunArcher => new Boon { UndeadLight = 1 },
        _ => Boon.None,
    };

    /// <summary>Третий бонус — Облачение бога (тело), там же.</summary>
    public static Boon Robe(Celestial god) => god switch
    {
        Celestial.WingedHour => new Boon { FireRes = 1 },
        Celestial.AnvilFather => new Boon { HeavyAttackDamage = 2 },
        Celestial.ScrollKeeper => new Boon { ManaPool = 2 },
        Celestial.MirrorMaiden => new Boon { SpellAbsorption = 1 },
        Celestial.WindMistress => new Boon { PoisonRes = 1 },
        Celestial.HearthMother => new Boon { RestHealPercent = 25 },
        Celestial.ThresholdWarden => new Boon { Plating = 1 },
        Celestial.MercyShield => new Boon { Plating = 1 },
        Celestial.ThunderCrowned => new Boon { AttackDamage = 1 },
        Celestial.SunArcher => new Boon { MagicRes = 1 },
        _ => Boon.None,
    };

    /// <summary>Всё, что даёт бог при данной силе благословения и надетых вещах его комплекта.</summary>
    public static Boon CelestialTotal(Celestial god, BlessingStrength strength, bool amulet, bool robe)
    {
        if (strength == BlessingStrength.None) return Boon.None;
        var boon = Blessing(god);
        if (strength == BlessingStrength.Full)
        {
            if (amulet) boon += Amulet(god);
            if (robe) boon += Robe(god);
        }
        return boon;
    }

    /// <summary>Дух божества: благословение + амулет + облачение + ступень «Небесный щит» Праведника (этап 6).</summary>
    public static bool HasDivineSpirit(BlessingStrength strength, bool amulet, bool robe, bool heavenlyShield) =>
        strength == BlessingStrength.Full && amulet && robe && heavenlyShield;

    /// <summary>Небесные, несовместимые с некромантией (Страж Порога, Щит Милосердия).</summary>
    public static bool ForbidsNecromancy(Celestial god) => god is Celestial.ThresholdWarden or Celestial.MercyShield;

    // ───── Князья Бездны ─────

    /// <summary>Числовая часть Дара и Цены (до Обета). Особые свойства — у реликвий.</summary>
    public static Boon Gift(AbyssPrince prince) => prince switch
    {
        // Атаки — Хворь 1 (особое); +1 прирост ◆. Цена: болезни (особое).
        AbyssPrince.PlagueLord => new Boon { StaminaRegen = 1 },
        // Невидимость в первый ход (особое). Цена: −5 макс. ♥.
        AbyssPrince.TwilightWidow => new Boon { MaxHp = -5 },
        // Вампиризм по отравленным (особое). Цена: лечение на отдыхе −30 %.
        AbyssPrince.WebWeaver => new Boon { RestHealPercent = -30 },
        // Призывы и Бездонное оружие +2 (призывы — здесь), первое заклинание хода −1 ✦ (особое). Цена: вечный Отступник.
        AbyssPrince.GateBreaker => new Boon { SummonDamage = 2 },
        // Атаки +2, Пробитие 1. Цена: призывы получают +50 % урона.
        AbyssPrince.Treachery => new Boon { AttackDamage = 2, Pierce = 1, SummonTakenPercent = 50 },
        // Две Сытости одновременно (FoodRules.Slots). Цена: статус в руку в начале боя (особое).
        AbyssPrince.Revel => Boon.None,
        // Лечение 2 после боя (особое); тяжёлые атаки +2. Цена: праведность недоступна; лавки +15 %.
        AbyssPrince.Outcast => new Boon { HeavyAttackDamage = 2, MerchantPricePercent = 15 },
        // +6 ♥, +2 пул ✦; сопротивление огню и холоду I. Цена: звёздные метки на картах (особое).
        AbyssPrince.DuskStar => new Boon { MaxHp = 6, ManaPool = 2, FireRes = 1, FrostRes = 1 },
        _ => Boon.None,
    };

    /// <summary>Добавка после выполнения Обета.</summary>
    public static Boon VowReward(AbyssPrince prince) => prince switch
    {
        AbyssPrince.TwilightWidow => new Boon { ManaRegen = 1 },
        AbyssPrince.GateBreaker => new Boon { EnemyFireRes = -1 },
        AbyssPrince.Treachery => new Boon { OpeningHand = 1 },
        _ => Boon.None,
    };

    /// <summary>Сколько нужно для Обета.</summary>
    /// <summary>Второе свойство напитка (Обет Хмельного Князя).</summary>
    public enum DrinkBonus { None, Stamina, Mana, Heal, Block }

    /// <summary>
    /// Вторые свойства напитков после Обета Хмельного Князя (05 §4): восполнения ♥ дают ещё ◆, восполнения ◆ — ✦,
    /// восполнения ✦ — лечение, усиления и Панацея — блок. Сила растёт со ступенью (I/III/V). Яды не пьют.
    /// </summary>
    public static (DrinkBonus Kind, int Amount) RevelSecondProperty(Brew brew, int tier)
    {
        var step = Math.Max(1, (tier + 1) / 2); // I → 1, III → 2, V → 3
        return brew switch
        {
            Brew.Health or Brew.Rejuvenation => (DrinkBonus.Stamina, step),
            Brew.Stamina or Brew.SecondWind => (DrinkBonus.Mana, step),
            Brew.Mana or Brew.Clarity => (DrinkBonus.Heal, 2 * step),
            _ when AlchemyRules.IsPoison(brew) => (DrinkBonus.None, 0),
            _ => (DrinkBonus.Block, 3 * step),
        };
    }

    public static int VowTarget(AbyssPrince prince) => prince switch
    {
        AbyssPrince.PlagueLord => 3,   // убить 3 врагов под Хворью
        AbyssPrince.TwilightWidow => 2, // 2 боя без урона в первый ход
        AbyssPrince.WebWeaver => 3,    // отравить 3 элиты
        AbyssPrince.GateBreaker => 1,  // убить элиту Бездны
        AbyssPrince.Treachery => 1,    // элита без призывов
        AbyssPrince.Revel => 3,        // выпить 3 зелья
        AbyssPrince.Outcast => 1,      // босс без зелий
        AbyssPrince.DuskStar => 1,     // акт без смерти союзников
        _ => 1,
    };

    /// <summary>Тройственные (Пеплорождённые): Измена, Паутина, Сумеречная Звезда — без потери праведности.</summary>
    public static bool IsTribunal(AbyssPrince prince) =>
        prince is AbyssPrince.Treachery or AbyssPrince.WebWeaver or AbyssPrince.DuskStar;

    /// <summary>Отдача Ткачихи: 1 ♥ за удар по отравленной цели, не больше 5 за ход.</summary>
    public const int WebLeechPerTurnCap = 5;

    /// <summary>Отверженный после Обета: бонусы ×2.</summary>
    public static int OutcastMultiplier(bool vowDone) => vowDone ? 2 : 1;

    /// <summary>Лечение Отверженного после боя.</summary>
    public static int OutcastAfterCombatHeal(bool vowDone) => 2 * OutcastMultiplier(vowDone);

    /// <summary>Ткачиха после Обета: атаки по целям с ♥ &lt; 50 % — +2.</summary>
    public static bool BelowHalf(int hp, int maxHp) => maxHp > 0 && hp * 2 < maxHp;

    // ───── болезни ─────

    public static Boon DiseasePlus(Disease d) => d switch
    {
        Disease.ShakyGait => new Boon { Poise = 3 },
        Disease.StoneGout => new Boon { Plating = 1 },
        Disease.MindRot => new Boon { ManaPool = 3, SpellAbsorption = 1 },
        Disease.Wheeze => new Boon { Regen = 1 },
        Disease.Bonebreak => new Boon { StaminaRegen = 1 },
        Disease.Babble => new Boon { ManaRegen = 1 },
        _ => Boon.None,
    };

    /// <summary>Числовая часть минуса. Особые минусы: Размягчение разума (+1 энергия первому заклинанию хода),
    /// Костолом (−3 макс. ♥ в начале акта), Заумь (первое заклинание боя дороже на 2 ✦).</summary>
    public static Boon DiseaseMinus(Disease d) => d switch
    {
        Disease.ShakyGait => new Boon { OpeningHand = -1 },
        Disease.StoneGout => new Boon { AttackDamage = -1 },
        Disease.Wheeze => new Boon { RestHealPercent = -50 },
        _ => Boon.None,
    };

    public const int BonebreakHpPerAct = 3;
    public const int BabbleSurcharge = 2;

    /// <summary>Все болезни разом: минусы всегда, плюсы — после Обета Повелителя Мора.</summary>
    public static Boon Diseases(IEnumerable<int> diseases, bool vowDone)
    {
        var total = Boon.None;
        foreach (var raw in diseases)
        {
            if (raw < 0 || raw >= DiseaseCount) continue;
            var d = (Disease)raw;
            total += DiseaseMinus(d);
            if (vowDone) total += DiseasePlus(d);
        }
        return total;
    }

    /// <summary>Сколько раз болезнь есть в списке.</summary>
    public static int Count(IEnumerable<int> diseases, Disease d) => diseases.Count(x => x == (int)d);

    /// <summary>Сумеречная Звезда: число отмеченных карт растёт на 1 за акт, пока Цена не снята.</summary>
    public static int StarMarksAfterAct(int marks, bool vowDone) => vowDone ? 0 : marks + 1;
}
