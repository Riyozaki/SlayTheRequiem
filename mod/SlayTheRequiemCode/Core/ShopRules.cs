namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Вид лавки (docs/design/14 §2). Порядок = индекс в массивах Книги торговца (сохраняется).</summary>
public enum ShopType
{
    Wanderer,   // Лавка странника — ванильный магазин с правками
    Armorer,    // Оружейник — снаряжение
    Arcanist,   // Чародейская лавка — заклинания, Трактаты
    Herbalist,  // Травница — зелья и яды Реквиема
    Jeweler,    // Ювелир — кольца, амулеты, Камни душ (M5)
    Temple,     // Храмовая лавка — облачения бога, Очищение, Искупление (M5)
    Fence,      // Скупщик краденого — Бездонное, яды V, скупка по 60 % (M5)
    Caravan,    // Караван — понемногу всего + экзотика, Обмен (M5)
}

/// <summary>Ценовая категория снаряжения (docs/design/14 §2.2, §2.5).</summary>
public enum EquipCategory { Weapon, Shield, Armor, Cloak, Ring, Amulet }

/// <summary>Что можно заказать (docs/design/14 §1, «Заказ» при доверии 3).</summary>
public enum OrderCategory { Weapon, Armor, Shield, Equipment, Spell, Potion, Poison }

/// <summary>
/// Лавки: вид лавки на узле, цены, доверие, продажа, золото за бои (docs/design/14). Чистые функции — зеркало в tools/sim.
/// </summary>
public static class ShopRules
{
    public const int ShopTypeCount = 8;

    // ───── вид лавки ─────

    /// <summary>
    /// Пул лавок по ванильной ступени акта (0–2) — для актов вне сетки Реквиема (<see cref="ActKind.Other"/>).
    /// «Чародейская с 3-го акта» сдвинута на 2-й ванильный акт (индекс 1) — иначе заклинания негде покупать до финала.
    /// </summary>
    public static IReadOnlyList<ShopType> ActPool(int actIndex) => actIndex <= 0
        ? [ShopType.Wanderer, ShopType.Armorer, ShopType.Herbalist]
        : [ShopType.Wanderer, ShopType.Armorer, ShopType.Herbalist, ShopType.Arcanist, ShopType.Jeweler];

    /// <summary>
    /// Какие лавки встречаются в акте Реквиема (docs/design/14 §3). Чародейская — со 2-го акта (как и в ванильной сетке),
    /// в Глубинах Оружейник вдвое чаще; Караван — только 2-й и 5-й акт; Храм — 5-й акт (Святилища).
    /// </summary>
    public static IReadOnlyList<ShopType> ActPool(ActKind kind, int tier = 0) => kind switch
    {
        ActKind.Foothills => [ShopType.Wanderer, ShopType.Armorer, ShopType.Herbalist],
        ActKind.FrozenBarrows => [ShopType.Wanderer, ShopType.Armorer, ShopType.Herbalist, ShopType.Arcanist, ShopType.Jeweler, ShopType.Caravan],
        ActKind.ClockworkDepths => [ShopType.Wanderer, ShopType.Armorer, ShopType.Armorer, ShopType.Herbalist, ShopType.Arcanist, ShopType.Jeweler, ShopType.Caravan],
        ActKind.Hive => [ShopType.Wanderer, ShopType.Armorer, ShopType.Herbalist, ShopType.Arcanist, ShopType.Jeweler],
        ActKind.BloodCourt => [ShopType.Armorer, ShopType.Herbalist, ShopType.Arcanist, ShopType.Jeweler, ShopType.Fence],
        ActKind.StarTower => [ShopType.Wanderer, ShopType.Armorer, ShopType.Herbalist, ShopType.Arcanist, ShopType.Jeweler],
        ActKind.Glory => [ShopType.Wanderer, ShopType.Armorer, ShopType.Herbalist, ShopType.Arcanist, ShopType.Jeweler, ShopType.Temple, ShopType.Caravan],
        ActKind.NamelessLimit => [ShopType.Wanderer, ShopType.Armorer, ShopType.Herbalist, ShopType.Arcanist, ShopType.Jeweler],
        _ => ActPool(tier),
    };

    /// <summary>
    /// Гарантированные лавки (§3): первая лавка акта — Странник, в Кровавом Дворе — Скупщик, в Слава — Храм;
    /// в Звёздной Башне первые две — Чародейские. null — бросок по пулу.
    /// </summary>
    public static ShopType? GuaranteedShop(ActKind kind, int shopsSeenThisAct) => kind switch
    {
        ActKind.BloodCourt when shopsSeenThisAct <= 0 => ShopType.Fence,
        ActKind.StarTower when shopsSeenThisAct <= 1 => ShopType.Arcanist,
        ActKind.Glory when shopsSeenThisAct <= 0 => ShopType.Temple,
        _ when shopsSeenThisAct <= 0 => ShopType.Wanderer,
        _ => null,
    };

    /// <summary>Шанс, что Караван остаётся в пуле броска (§2.8: «редкий»), — если в этом акте его ещё не было.</summary>
    public const int CaravanChancePercent = 35;

    /// <summary>
    /// Вид лавки для узла (ванильная ступень акта, без сетки Реквиема). Первая лавка акта — всегда Странник (гарантия §3);
    /// дальше — по зерну из пула акта, без повтора предыдущего вида подряд.
    /// </summary>
    public static ShopType RollShopType(uint hash, int actIndex, int shopsSeenThisAct, ShopType? previous) =>
        RollShopType(hash, ActKind.Other, actIndex, shopsSeenThisAct, previous);

    /// <summary>
    /// Вид лавки для узла: гарантия акта, иначе бросок по пулу без повтора предыдущего вида. Караван — не чаще раза за акт
    /// и только с шансом <see cref="CaravanChancePercent"/>; Скупщик добавляется в любой акт, если герой «вне закона»
    /// (Запятнанный/Отступник или с Ловкачом, §2.7). Детерминирован (зерно забега + этаж) — одинаков у всех игроков коопа
    /// и после перезагрузки.
    /// </summary>
    public static ShopType RollShopType(uint hash, ActKind kind, int tier, int shopsSeenThisAct, ShopType? previous,
        bool outlaw = false, bool caravanSeen = false)
    {
        if (GuaranteedShop(kind, shopsSeenThisAct) is { } guaranteed) return guaranteed;
        var pool = ActPool(kind, tier).ToList();
        if (outlaw && !pool.Contains(ShopType.Fence)) pool.Add(ShopType.Fence);
        var caravanOk = !caravanSeen && (hash >> 16) % 100 < CaravanChancePercent;
        pool = pool.Where(t => (previous == null || t != previous.Value) && (t != ShopType.Caravan || caravanOk)).ToList();
        if (pool.Count == 0) return ShopType.Wanderer;
        return pool[(int)(hash % (uint)pool.Count)];
    }

    /// <summary>FNV-1a: стабильный хеш строки (string.GetHashCode в .NET меняется между запусками).</summary>
    public static uint Fnv1a(string text, int salt = 0)
    {
        var h = 2166136261u;
        unchecked
        {
            foreach (var ch in text)
            {
                h ^= ch;
                h *= 16777619u;
            }
            h ^= (uint)salt;
            h *= 16777619u;
            h ^= h >> 15;
        }
        return h;
    }

    // ───── доверие ─────

    public const int GoldPerTrust = 150;
    public const int MaxTrust = 3;

    /// <summary>Доверие 0–3: +1 за каждые 150 золотых, потраченных в лавках этого вида.</summary>
    public static int Trust(int goldSpent) => Math.Clamp(goldSpent / GoldPerTrust, 0, MaxTrust);

    /// <summary>Цена с учётом доверия: −5 % за уровень. Не дешевле 1.</summary>
    public static int ApplyTrust(int price, int trust) =>
        price <= 0 ? price : Math.Max(1, (int)Math.Round(price * (1 - 0.05 * Math.Clamp(trust, 0, MaxTrust)), MidpointRounding.AwayFromZero));

    // ───── продажа ─────

    /// <summary>Продажа: 35 % цены, 50 % при доверии 3. Минимум 1.</summary>
    public static int SellValue(int price, int trust) =>
        price <= 0 ? 0 : Math.Max(1, (int)Math.Floor(price * (trust >= MaxTrust ? 0.50 : 0.35)));

    /// <summary>Доля выручки в процентах для подписи: 35, при доверии 3 — 50, у Скупщика краденого — 60.</summary>
    public static int SellPercent(int trust, bool fence) => fence ? 60 : trust >= MaxTrust ? 50 : 35;

    // ───── цены ─────

    private static int ByTier(int tier, params int[] prices) => prices[Math.Clamp(tier, 1, prices.Length) - 1];

    /// <summary>Снаряжение по ступени I–V (§2.2; кольца и амулеты — §2.5).</summary>
    public static int EquipmentPrice(EquipCategory category, int tier) => category switch
    {
        EquipCategory.Weapon => ByTier(tier, 45, 80, 130, 190, 260),
        EquipCategory.Shield => ByTier(tier, 40, 70, 115, 170, 230),
        EquipCategory.Armor or EquipCategory.Cloak => ByTier(tier, 35, 65, 110, 160, 220),
        EquipCategory.Ring => ByTier(tier, 90, 140, 140, 200, 200),
        EquipCategory.Amulet => ByTier(tier, 120, 180, 180, 250, 250),
        _ => 100,
    };

    /// <summary>Заклинание по ступени (§2.3): фолиант 45/70/110/160/230; отдельное заклинание — −10 %.</summary>
    public static int SpellPrice(SpellTier tier, bool single)
    {
        var folio = ByTier((int)tier, 45, 70, 110, 160, 230);
        return single ? (int)Math.Round(folio * 0.9, MidpointRounding.AwayFromZero) : folio;
    }

    /// <summary>Зелья и яды Травницы по ступени I–V (§2.4). Ступени зелий Реквиема — 1/3/5.</summary>
    public static int BrewPrice(int tier, bool poison) => poison
        ? ByTier(tier, 35, 50, 70, 95, 125)
        : ByTier(tier, 40, 55, 75, 100, 130);

    /// <summary>Трактат владения: 120, +40 за каждый купленный ранее (§2.3).</summary>
    public static int TreatisePrice(int boughtBefore) => 120 + 40 * Math.Max(0, boughtBefore);

    /// <summary>
    /// Ступени снаряжения на витрине: Странник, Чародейская, Храм — I–II; Оружейник, Ювелир и Караван — по акту
    /// (I–II, II–III, III–IV, IV–V); Скупщик краденого — IV–V («краденое»).
    /// </summary>
    public static (int Min, int Max) EquipmentTiers(ShopType shop, int actIndex) => shop switch
    {
        ShopType.Armorer or ShopType.Jeweler or ShopType.Caravan => (Math.Clamp(actIndex + 1, 1, 4), Math.Clamp(actIndex + 2, 2, 5)),
        ShopType.Fence => (4, 5),
        _ => (1, 2),
    };

    /// <summary>Класс брони «по герою» на витрине Оружейника (§2.2): Латоносец — латы, Безмолвная — лёгкая, Регент — одеяния.</summary>
    public static ArmorClass PreferredArmor(string? heroTypeName) => heroTypeName switch
    {
        "Ironclad" => ArmorClass.Heavy,
        "Silent" => ArmorClass.Light,
        "Regent" => ArmorClass.Robe,
        _ => ArmorClass.None,
    };

    /// <summary>Ступень зелий Травницы по акту: I → III → V.</summary>
    public static int BrewTier(int actIndex) => actIndex <= 0 ? 1 : actIndex == 1 ? 3 : 5;

    // ───── золото ─────

    public const double CombatGoldMultiplier = 1.3;

    /// <summary>Золото за бои ×1.3 (§1). Native 0.107.1 baseline без Poverty/overrides:
    /// обычный 10–20 → 13–26, элита 35–45 → 46–59, босс 100 → 130. Округление AwayFromZero.</summary>
    public static int CombatGold(int vanilla) =>
        vanilla <= 0 ? vanilla : (int)Math.Round(vanilla * CombatGoldMultiplier, MidpointRounding.AwayFromZero);

    // ───── доверие: витрина и Заказ ─────

    /// <summary>«+1 товар за уровень доверия» (§1): сколько раз за визит опустевший слот витрины пополняется.</summary>
    public static int RefillsPerVisit(int trust) => Math.Clamp(trust, 0, MaxTrust);

    /// <summary>«Торговое имя» (Выживание II, 10 §2): ещё +1 товар за визит.</summary>
    public static int RefillsPerVisit(int trust, bool tradeName) => RefillsPerVisit(trust) + (tradeName ? 1 : 0);

    /// <summary>Заказ (§1) доступен при доверии 3 и стоит 50 % цены заказанного предмета.</summary>
    public static bool CanOrder(int trust) => trust >= MaxTrust;

    public static int OrderPrice(int itemPrice) => itemPrice <= 0 ? 0 : (int)Math.Ceiling(itemPrice * 0.5);

    /// <summary>Ступень заказанного предмета — верхняя ступень витрины Оружейника в текущем акте (минимум II) для любой лавки.</summary>
    public static int OrderTier(ShopType shop, int actIndex) => Math.Max(2, EquipmentTiers(ShopType.Armorer, actIndex).Max);

    /// <summary>Варианты Заказа по виду лавки.</summary>
    public static IReadOnlyList<OrderCategory> OrderOptions(ShopType shop) => shop switch
    {
        ShopType.Armorer => [OrderCategory.Weapon, OrderCategory.Armor, OrderCategory.Shield],
        ShopType.Arcanist => [OrderCategory.Spell],
        ShopType.Herbalist => [OrderCategory.Potion, OrderCategory.Poison],
        ShopType.Fence => [OrderCategory.Poison],
        ShopType.Temple => [],
        _ => [OrderCategory.Equipment],
    };

    /// <summary>Полная цена заказанного предмета (Заказ стоит половину): верхняя ступень витрины акта.</summary>
    public static int OrderItemPrice(OrderCategory category, int actIndex)
    {
        var tier = OrderTier(ShopType.Armorer, actIndex);
        return category switch
        {
            OrderCategory.Weapon or OrderCategory.Equipment => EquipmentPrice(EquipCategory.Weapon, tier),
            OrderCategory.Armor => EquipmentPrice(EquipCategory.Armor, tier),
            OrderCategory.Shield => EquipmentPrice(EquipCategory.Shield, tier),
            OrderCategory.Spell => SpellPrice((SpellTier)Math.Clamp(tier, 1, 5), single: true),
            OrderCategory.Potion => BrewPrice(5, poison: false),
            OrderCategory.Poison => BrewPrice(5, poison: true),
            _ => 100,
        };
    }

    // ───── кража ─────

    /// <summary>Кража (§1): 80 % − 10 % за каждые полные 50 золотых цены, не ниже 0.</summary>
    public static int TheftChancePercent(int price) => Math.Clamp(80 - 10 * (Math.Max(0, price) / 50), 0, 80);

    /// <summary>Бросок кражи по детерминированному хешу (одинаков у всех игроков коопа).</summary>
    public static bool TheftSucceeds(uint hash, int price) => (int)(hash % 100) < TheftChancePercent(price);

    /// <summary>
    /// Провал кражи: Стража выставляет вора — потеря 20 % макс. ♥ (не ниже 1 ♥ остатка), доверие вида → 0.
    /// Бой со Стражей (облегчённая элита) — когда появятся элиты Реквиема (этап 7).
    /// </summary>
    public static int TheftFailDamage(int maxHp) => Math.Max(1, (int)Math.Ceiling(Math.Max(0, maxHp) * 0.2));

    // ───── услуги ─────

    /// <summary>Максимум улучшений предмета (07 §2: +2; «Легендарный» +3 — узел «Кузнечное дело», см. LegendaryUpgrade).</summary>
    public const int MaxUpgrade = 2;

    /// <summary>«Легендарный» предмет: +3 (узел «Кузнечное дело», ветка Ремесла).</summary>
    public const int LegendaryUpgrade = 3;

    /// <summary>Заточка Оружейника (§2.2): 60 (I–II), 90 (III), 130 (IV–V).</summary>
    public static int SharpenPrice(int tier) => tier <= 2 ? 60 : tier == 3 ? 90 : 130;

    /// <summary>Перековка (§2.2): 50.</summary>
    public const int ReforgePrice = 50;

    /// <summary>Варка у Травницы (§2.4): 25, без очков лагеря.</summary>
    public const int BrewServicePrice = 25;

    /// <summary>Панацея (§2.4): снять Хворь/Болезнь — 60.</summary>
    public const int PanaceaPrice = 60;

    /// <summary>Набор из 3 реагентов (§2.4): 20 в 1-м акте, +5 за акт, не дороже 35.</summary>
    public static int ReagentKitPrice(int actIndex) => Math.Clamp(20 + 5 * Math.Max(0, actIndex), 20, 35);

    public const int ReagentKitSize = 3;

    /// <summary>Скупка реагентов (§2.4): 50 % цены реагента (цена реагента — треть набора 1-го акта), минимум 1.</summary>
    public static int ReagentSellValue(int count) => Math.Max(0, count) * 3;

    /// <summary>Удаление «Предсмертного эха» (15 §3) стоит вдвое: доплата = цена удаления, но не больше остатка золота.</summary>
    public static int DeathEchoSurcharge(int removalPrice, int goldLeft) => Math.Clamp(removalPrice, 0, Math.Max(0, goldLeft));

    /// <summary>Цена удаления карты у торговца (ваниль: 75, +25 за каждое следующее) — для доплаты за «эхо».</summary>
    public static int RemovalPrice(int removalsUsed) => 75 + 25 * Math.Max(0, removalsUsed);

    // ───── Чародейская: свитки, посохи, камни, зачарование (§2.3) ─────

    /// <summary>Свиток I / II / III: 25 / 45 / 70.</summary>
    public static int ScrollPrice(int tier) => ByTier(tier, 25, 45, 70);

    /// <summary>Посох по ступени I–V: 70 / 120 / 180 / 240 / 320.</summary>
    public static int StaffPrice(int tier) => ByTier(tier, 70, 120, 180, 240, 320);

    /// <summary>Ступень свитков на витрине по ванильной ступени акта: I → II → III.</summary>
    public static int ScrollTier(int actIndex) => Math.Clamp(actIndex + 1, 1, 3);

    /// <summary>Ступень посоха на витрине: акт 1 — I, 2 — II–III, 3 — IV–V (по ванильной ступени акта).</summary>
    public static (int Min, int Max) StaffTiers(int actIndex) => actIndex <= 0 ? (1, 1) : actIndex == 1 ? (2, 3) : (4, 5);

    /// <summary>Камень душ (малый / большой / чёрный): 40 / 80 / 140.</summary>
    public static int SoulStonePrice(int power) => ByTier(power, 40, 80, 140);

    /// <summary>Сила камня душ в лавке по ванильной ступени акта: 1 → 2 → 3.</summary>
    public static int SoulStonePower(int actIndex) => Math.Clamp(actIndex + 1, 1, 3);

    /// <summary>Зачарование снаряжения у Чародея: 75 / 110 / 150 по силе чар; без своего камня — +60 за камень лавки.</summary>
    public static int EnchantServicePrice(int power, bool ownStone) => ByTier(power, 75, 110, 150) + (ownStone ? 0 : 60);

    /// <summary>Переписать свиток в карту-заклинание: 40.</summary>
    public const int RewriteScrollPrice = 40;

    /// <summary>Зачаровать карту: 80.</summary>
    public const int CardEnchantPrice = 80;

    // ───── Ювелир (§2.5) ─────

    /// <summary>Гравировка: чары силы 1 на кольцо/амулет — 70.</summary>
    public const int EngravePrice = 70;

    /// <summary>Огранка: перенести чары с одного предмета на другой — 50.</summary>
    public const int FacetPrice = 50;

    // ───── Храм (§2.6) ─────

    /// <summary>Амулет бога Святилища: 150 (дороже, чем у алтаря/события — храмовая наценка).</summary>
    public const int TempleAmuletPrice = 150;

    /// <summary>Облачение бога Святилища: 200.</summary>
    public const int TempleRobePrice = 200;

    /// <summary>Сменить благословение на бога Святилища: 60.</summary>
    public const int BlessingChangePrice = 60;

    /// <summary>Очищение (удаление карты): 50, Праведнику — 35.</summary>
    public static int CleansePrice(bool righteous) => righteous ? 35 : 50;

    /// <summary>Исцеление в Храме: 30 % макс. ♥ за 40.</summary>
    public const int TempleHealPercent = 30;
    public const int TempleHealPrice = 40;

    /// <summary>Искупление: +1 ступень праведности (раз за акт) — 100.</summary>
    public const int AtonementPrice = 100;

    // ───── Скупщик краденого (§2.7) ─────

    /// <summary>Скупщик берёт всё по 60 % цены (минимум 1).</summary>
    public static int FenceSellValue(int price) => price <= 0 ? 0 : Math.Max(1, (int)Math.Floor(price * 0.60));

    /// <summary>Снять проклятие — не золотом, а −6 макс. ♥.</summary>
    public const int CurseRemovalMaxHp = 6;

    /// <summary>Снять проклятие можно, если после потери останется хотя бы 1 макс. ♥.</summary>
    public static bool CanPayCurseRemoval(int maxHp) => maxHp > CurseRemovalMaxHp;

    /// <summary>Отмыть имя: вернуть доверие одной лавки к 1 — 80.</summary>
    public const int ClearNamePrice = 80;

    /// <summary>Золото «доверия 1» после «Отмыть имя».</summary>
    public const int ClearNameSpent = GoldPerTrust;

    /// <summary>Какую лавку «отмывать»: вид (кроме самого Скупщика) с наименьшим доверием ниже 1; при равенстве — первый. −1 — нечего.</summary>
    public static int ClearNameTarget(IReadOnlyList<int> spent)
    {
        var best = -1;
        for (var i = 0; i < Math.Min(spent.Count, ShopTypeCount); i++)
        {
            if (i == (int)ShopType.Fence || Trust(spent[i]) >= 1) continue;
            if (best < 0 || spent[i] < spent[best]) best = i;
        }
        return best;
    }

    // ───── Караван (§2.8) ─────

    /// <summary>Обмен: за отданный предмет — предмет той же ступени или на одну ниже (не ниже I).</summary>
    public static (int Min, int Max) TradeTiers(int givenTier) => (Math.Max(1, givenTier - 1), Math.Max(1, givenTier));

    // ───── Наставник (§2.9) ─────

    public const int MentorMaxSkillPoints = 3;

    /// <summary>Очко навыка у Наставника: 100 / 150 / 200 (по числу уже купленных), чужая ветка Наставника — +50 %. null — лимит.</summary>
    public static int? MentorSkillPrice(int bought, bool nativeBranch)
    {
        if (bought < 0 || bought >= MentorMaxSkillPoints) return null;
        var price = 100 + 50 * bought;
        return nativeBranch ? price : (int)Math.Round(price * 1.5, MidpointRounding.AwayFromZero);
    }

    /// <summary>Урок владения оружием (снимает «Неумелый хват» для одного класса): 90.</summary>
    public const int WeaponLessonPrice = 90;
}
