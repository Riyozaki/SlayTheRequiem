namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Вид лавки (docs/design/14 §2). Порядок = индекс в массивах Книги торговца (сохраняется).</summary>
public enum ShopType
{
    Wanderer,   // Лавка странника — ванильный магазин с правками
    Armorer,    // Оружейник — снаряжение
    Arcanist,   // Чародейская лавка — заклинания, Трактаты
    Herbalist,  // Травница — зелья и яды Реквиема
}

/// <summary>Ценовая категория снаряжения (docs/design/14 §2.2, §2.5).</summary>
public enum EquipCategory { Weapon, Shield, Armor, Cloak, Ring, Amulet }

/// <summary>
/// Лавки: вид лавки на узле, цены, доверие, продажа, золото за бои (docs/design/14). Чистые функции — зеркало в tools/sim.
/// </summary>
public static class ShopRules
{
    public const int ShopTypeCount = 4;

    // ───── вид лавки ─────

    /// <summary>
    /// Какие лавки встречаются в акте (docs/design/14 §3). В ванильной сетке 3 акта, поэтому «Чародейская с 3-го акта»
    /// сдвинута на 2-й ванильный акт (индекс 1) — иначе заклинания негде покупать до финала.
    /// </summary>
    public static IReadOnlyList<ShopType> ActPool(int actIndex) => actIndex <= 0
        ? [ShopType.Wanderer, ShopType.Armorer, ShopType.Herbalist]
        : [ShopType.Wanderer, ShopType.Armorer, ShopType.Herbalist, ShopType.Arcanist];

    /// <summary>
    /// Вид лавки для узла. Первая лавка акта — всегда Странник (гарантия §3); дальше — по зерну из пула акта,
    /// без повтора предыдущего вида подряд. Детерминирован (зерно забега + этаж), поэтому одинаков у всех игроков коопа
    /// и после перезагрузки.
    /// </summary>
    public static ShopType RollShopType(uint hash, int actIndex, int shopsSeenThisAct, ShopType? previous)
    {
        if (shopsSeenThisAct <= 0) return ShopType.Wanderer;
        var pool = ActPool(actIndex).Where(t => previous == null || t != previous.Value).ToList();
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

    /// <summary>Ступени снаряжения на витрине: Странник — I–II; Оружейник — по акту (I–II, II–III, III–IV, IV–V).</summary>
    public static (int Min, int Max) EquipmentTiers(ShopType shop, int actIndex) => shop switch
    {
        ShopType.Armorer => (Math.Clamp(actIndex + 1, 1, 4), Math.Clamp(actIndex + 2, 2, 5)),
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

    /// <summary>Золото за бои ×1.3 (§1): 13–20 → 17–26, элита 25–35 → 33–45, босс 95–105 → 124–137.</summary>
    public static int CombatGold(int vanilla) =>
        vanilla <= 0 ? vanilla : (int)Math.Round(vanilla * CombatGoldMultiplier, MidpointRounding.AwayFromZero);
}
