using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

// Витрины лавок Реквиема (docs/design/14 §2). Ванильная сцена магазина держит фиксированное число слотов
// (5 карт героя, 2 бесцветные, 3 реликвии, 3 зелья) — добавлять записи сверх этого нельзя (GetChild(i) упадёт),
// поэтому вид лавки меняет СОДЕРЖИМОЕ слотов: префиксы на приватных статических GetDefault*Entries(Player) MerchantInventory
// (игра 0.113; в 0.111 это были Populate*-методы экземпляра). Списки записей собираются ДО конструктора витрины, а
// MerchantInventory.Setup() затем раздаёт им Id, подписку PurchaseCompleted → UpdateEntries, вызывает Populate(inventory)
// у карт и ставит скидку на случайную карту героя — так что наши записи обслуживаются как ванильные.
//
// | Вид        | Карты героя (5)     | Бесцветные (2) | Реликвии (3)                          | Зелья (3)              |
// |------------|---------------------|----------------|---------------------------------------|------------------------|
// | Странник   | ваниль              | ваниль         | 2 ванильные + снаряжение по сохранённой EconomyRules | ваниль         |
// | Оружейник  | ваниль              | ваниль         | оружие + броня (класс героя) + щит/любое | ваниль              |
// | Чародейская| 5 заклинаний        | ваниль         | Трактат + посох + ванильная           | 3 свитка               |
// | Травница   | ваниль              | ваниль         | ваниль                                | 2 зелья + 1 яд Реквиема|
// | Ювелир     | ваниль              | ваниль         | амулет (бога — гарантия) + кольцо + кольцо/амулет | ваниль     |
// | Храм       | ваниль              | ваниль         | Амулет + Облачение бога Святилища + ванильная | ваниль         |
// | Скупщик    | ваниль              | ваниль         | Бездонное + «краденое» IV–V + ванильная | 2 яда V + зелье V    |
// | Караван    | ваниль              | ваниль         | снаряжение + кольцо/амулет + экзотика (ступенью выше) | зелье + яд + свиток |

/// <summary>Перед генерацией витрины — выбрать вид лавки для узла (Книга торговца).</summary>
[HarmonyPatch(typeof(MerchantInventory), nameof(MerchantInventory.CreateForNormalMerchant))]
public static class ShopTypePatch
{
    [HarmonyPrefix]
    public static void RollShopType(Player player)
    {
        if (!MerchantLedger.Active(player)) return;
        MerchantLedger.Of(player)?.EnterShop(player.RunState);
    }

    /// <summary>Вид лавки для витрины игрока (null — ванильная витрина).</summary>
    public static ShopType? ShopFor(Player player) =>
        MerchantLedger.Active(player) && MerchantLedger.Of(player) is { } ledger ? ledger.CurrentShop : null;

    /// <summary>Заказ (14 §1), который выполняется на этой витрине.</summary>
    public static OrderCategory? OrderFor(Player player) =>
        MerchantLedger.Active(player) && MerchantLedger.Of(player) is { } ledger ? ledger.ActiveOrder(player.RunState) : null;
}

/// <summary>Чародейская лавка: 5 слотов карт героя — заклинания, доступные по владению.</summary>
[HarmonyPatch(typeof(MerchantInventory), "GetDefaultCharacterCardEntries")]
public static class SpellShopCardsPatch
{
    public const int Slots = 5;

    [HarmonyPrefix]
    public static bool StockSpells(Player player, ref List<MerchantCardEntry> __result)
    {
        if (ShopTypePatch.ShopFor(player) != ShopType.Arcanist) return true;
        var pool = ShopStock.SpellCandidates(player);
        if (pool.Count < Slots) return true;

        try
        {
            var rng = player.PlayerRng.Shops;
            var list = new List<MerchantCardEntry>(Slots);
            var chosen = new HashSet<CardModel>();
            // Заказ «заклинание»: первый слот — заклинание высшей доступной ступени.
            if (ShopTypePatch.OrderFor(player) == OrderCategory.Spell)
            {
                var top = pool.OfType<SlayTheRequiem.SlayTheRequiemCode.Spells.SpellCard>().OrderByDescending(c => (int)c.Tier)
                    .ThenBy(c => c.Id.Entry, StringComparer.Ordinal).FirstOrDefault();
                if (top != null)
                {
                    chosen.Add(top);
                    list.Add(new MerchantCardEntry(player, new List<CardModel> { top }, top.Rarity));
                }
            }
            while (list.Count < Slots)
            {
                // Редкость берём у случайного ещё не выбранного заклинания (без повторов): Setup() наполняет записи по
                // очереди с чёрным списком уже выставленных, и на каждую редкость заклинаний в пуле хватает —
                // CreateForMerchant(rarity) не останется пустым (откатываться по редкости он не умеет).
                var sample = rng.NextItem(pool.Where(c => !chosen.Contains(c)).ToList());
                if (sample == null) break;
                chosen.Add(sample);
                list.Add(new MerchantCardEntry(player, pool, sample.Rarity));
            }
            if (list.Count != Slots) return true;
            __result = list;
            return false;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Чародейская лавка: витрина заклинаний не собралась ({e.Message}) — ванильные карты.");
            return true;
        }
    }
}

/// <summary>Реликвии: снаряжение у Странника и Оружейника, Трактат в Чародейской.</summary>
[HarmonyPatch(typeof(MerchantInventory), "GetDefaultRelicEntries")]
public static class ShopRelicsPatch
{
    public const int Slots = 3;

    [HarmonyPrefix]
    public static bool StockRelics(Player player, ref List<MerchantRelicEntry> __result)
    {
        var shelf = new List<MerchantRelicEntry>(Slots);
        var shop = ShopTypePatch.ShopFor(player);
        if (shop is null or ShopType.Herbalist) return true;
        static bool IsRing(EquipmentRelic e) => e.Slot == EquipSlot.Ring;
        static bool IsAmulet(EquipmentRelic e) => e.Slot == EquipSlot.Amulet;

        var list = shelf;
        try
        {
            var economy = ShopEconomy.Context(player.RunState);
            var (min, max) = EconomyRules.EquipmentTiers(shop.Value, economy);
            var taken = new HashSet<Type>();
            var order = ShopTypePatch.OrderFor(player);
            var orderTier = EconomyRules.OrderTier(shop.Value, economy);
            (int, int) Tiers(OrderCategory category) => order == category ? (orderTier, orderTier) : (min, max);
            switch (shop.Value)
            {
                case ShopType.Wanderer:
                {
                    AddVanilla(shelf, player, RelicFactory.RollRarity(player.PlayerRng.Shops));
                    AddVanilla(shelf, player, RelicFactory.RollRarity(player.PlayerRng.Shops));
                    var (lo, hi) = Tiers(OrderCategory.Equipment);
                    AddEquipment(shelf, player, lo, hi, null, taken, fallback: RelicRarity.Shop);
                    break;
                }
                case ShopType.Armorer:
                {
                    var armor = ShopRules.PreferredArmor(player.Character.GetType().Name);
                    var (wLo, wHi) = Tiers(OrderCategory.Weapon);
                    var (aLo, aHi) = Tiers(OrderCategory.Armor);
                    var (sLo, sHi) = Tiers(OrderCategory.Shield);
                    AddEquipment(shelf, player, wLo, wHi, e => e is WeaponRelic, taken, fallback: RelicRarity.Common);
                    AddEquipment(shelf, player, aLo, aHi, e => e is ArmorRelic a && (armor == ArmorClass.None || a.Armor == armor), taken, fallback: RelicRarity.Common);
                    if (order == OrderCategory.Shield)
                        AddEquipment(shelf, player, sLo, sHi, e => e.Slot == EquipSlot.OffHand, taken, fallback: RelicRarity.Shop);
                    else
                        AddEquipment(shelf, player, min, max, e => e.Slot == EquipSlot.OffHand || e is ArmorRelic, taken, fallback: RelicRarity.Shop);
                    break;
                }

                case ShopType.Arcanist:
                {
                    if (ShopStock.PickTreatise(player) is { } treatise) shelf.Add(ShopStock.RelicEntry(treatise, player));
                    else AddVanilla(shelf, player, RelicRarity.Shop);
                    var (stLo, stHi) = EconomyRules.StaffTiers(economy);
                    if (ShopStock.PickStaff(player, stLo, stHi, taken) is { } staff) shelf.Add(ShopStock.RelicEntry(staff, player));
                    else AddVanilla(shelf, player, RelicFactory.RollRarity(player.PlayerRng.Shops));
                    AddVanilla(shelf, player, RelicFactory.RollRarity(player.PlayerRng.Shops));
                    break;
                }
                case ShopType.Jeweler:
                {
                    // Амулет бога вашего благословения — гарантированно, если он есть и ещё не куплен (§2.5).
                    var (lo, hi) = Tiers(OrderCategory.Equipment);
                    var god = Pantheon.CelestialBlessing.Of(player)?.God;
                    if (god != null && ShopStock.GodAmulet(player, god.Value) is { } godAmulet)
                        shelf.Add(ShopStock.RelicEntry(godAmulet, player));
                    else
                        AddEquipment(shelf, player, 1, 5, IsAmulet, taken, fallback: RelicRarity.Shop);
                    AddEquipment(shelf, player, lo, Math.Max(hi, 2), IsRing, taken, fallback: RelicRarity.Common);
                    AddEquipment(shelf, player, 1, 5, e => IsRing(e) || IsAmulet(e), taken, fallback: RelicRarity.Uncommon);
                    break;
                }
                case ShopType.Temple:
                {
                    var god = MerchantLedger.TempleGod(player.RunState);
                    if (ShopStock.GodAmulet(player, god) is { } amulet) shelf.Add(ShopStock.RelicEntry(amulet, player));
                    else AddVanilla(shelf, player, RelicRarity.Shop);
                    if (ShopStock.GodRobe(player, god) is { } robe) shelf.Add(ShopStock.RelicEntry(robe, player));
                    else AddVanilla(shelf, player, RelicRarity.Shop);
                    AddVanilla(shelf, player, RelicFactory.RollRarity(player.PlayerRng.Shops));
                    break;
                }
                case ShopType.Fence:
                {
                    // Бездонное ищем и среди запрещённого в лавках: краденое продаёт только Скупщик.
                    var abyssal = ModelDb.AllRelics.OfType<EquipmentRelic>()
                        .Where(e => e is IAbyssalItem && !player.Relics.Any(r => r.GetType() == e.GetType()) && !taken.Contains(e.GetType()))
                        .OrderBy(e => e.Id.Entry, StringComparer.Ordinal).ToList();
                    if (player.PlayerRng.Shops.NextItem(abyssal) is { } dark)
                    {
                        taken.Add(dark.GetType());
                        shelf.Add(ShopStock.RelicEntry(dark, player));
                    }
                    else AddEquipment(shelf, player, 4, 5, null, taken, fallback: RelicRarity.Rare);
                    AddEquipment(shelf, player, 4, 5, null, taken, fallback: RelicRarity.Rare);
                    AddVanilla(shelf, player, RelicRarity.Shop);
                    break;
                }
                case ShopType.Caravan:
                {
                    var (lo, hi) = Tiers(OrderCategory.Equipment);
                    var (eLo, eHi) = EconomyRules.EquipmentTiers(ShopType.Armorer, economy);
                    AddEquipment(shelf, player, Math.Max(lo, eLo), Math.Max(hi, eHi), e => e is WeaponRelic || e is ArmorRelic, taken, fallback: RelicRarity.Common);
                    AddEquipment(shelf, player, 1, 5, e => IsRing(e) || IsAmulet(e), taken, fallback: RelicRarity.Uncommon);
                    // «Экзотика из другого акта» (§2.8): ступенью выше витрины Оружейника.
                    var exotic = Math.Min(5, eHi + 1);
                    AddEquipment(shelf, player, exotic, exotic, null, taken, fallback: RelicRarity.Rare);
                    break;
                }
            }
            if (list.Count == Slots)
            {
                __result = list;
                return false;
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Лавка {shop}: реликвии не собрались ({e.Message}) — ванильная витрина.");
        }
        return true;
    }

    private static void AddVanilla(List<MerchantRelicEntry> list, Player player, RelicRarity rarity) =>
        list.Add(new MerchantRelicEntry(rarity, player));

    private static void AddEquipment(List<MerchantRelicEntry> list, Player player, int min, int max, Func<EquipmentRelic, bool>? filter, ISet<Type> taken, RelicRarity fallback)
    {
        var pick = ShopStock.PickEquipment(player, min, max, filter, taken);
        if (pick != null) list.Add(ShopStock.RelicEntry(pick, player));
        else AddVanilla(list, player, fallback);
    }
}

/// <summary>
/// Слоты зелий: Травница — 2 зелья и 1 яд Реквиема ступени по акту; Чародейская — 3 свитка; Скупщик — 2 яда V и зелье V;
/// Караван — зелье, яд и свиток.
/// </summary>
[HarmonyPatch(typeof(MerchantInventory), "GetDefaultPotionEntries")]
public static class HerbalistPotionsPatch
{
    public const int Slots = 3;

    [HarmonyPrefix]
    public static bool StockBrews(Player player, ref List<MerchantPotionEntry> __result)
    {
        var shop = ShopTypePatch.ShopFor(player);
        if (shop is not (ShopType.Herbalist or ShopType.Arcanist or ShopType.Fence or ShopType.Caravan)) return true;

        var list = new List<MerchantPotionEntry>(Slots);
        try
        {
            var economy = ShopEconomy.Context(player.RunState);
            var tier = EconomyRules.BrewTier(economy);
            var order = ShopTypePatch.OrderFor(player);
            var scrolls = new HashSet<Type>();
            var scrollTier = EconomyRules.ScrollTier(economy);
            // (вид, ступень): 0 — зелье, 1 — яд, 2 — свиток.
            (int Kind, int Tier)[] plan = shop.Value switch
            {
                // Заказ: зелье/яд высшей ступени (V).
                ShopType.Herbalist => new[] { (0, order == OrderCategory.Potion ? 5 : tier), (0, tier), (1, order == OrderCategory.Poison ? 5 : tier) },
                ShopType.Arcanist => new[] { (2, scrollTier), (2, scrollTier), (2, scrollTier) },
                ShopType.Fence => new[] { (1, 5), (1, 5), (0, 5) },
                _ => new[] { (0, tier), (1, tier), (2, scrollTier) },
            };
            // Курганы (13 §3, M6): у Травницы — зелье Ночного зрения против Тьмы вместо второго зелья.
            if (shop == ShopType.Herbalist && Acts.ActKinds.CurrentRule(player.RunState) == ActRule.Frost) plan[1] = (3, 1);
            for (var i = 0; i < plan.Length; i++)
            {
                var (kind, t) = plan[i];
                var model = kind switch
                {
                    2 => ShopStock.PickScroll(player, t, exactTier: i == 0, scrolls),
                    3 => ModelDb.Potion<Potions.NightSightPotion>().ToMutable(),
                    _ => ShopStock.PickBrew(player, kind == 1, t),
                };
                if (model == null) break;
                list.Add(new MerchantPotionEntry(model, player));
            }
            if (list.Count == Slots)
            {
                __result = list;
                return false;
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Лавка {shop}: зелья не собрались ({e.Message}) — ванильные зелья.");
        }
        return true;
    }
}

/// <summary>
/// Пополнение витрины за доверие (<see cref="MerchantLedger.ShouldRefillMerchantEntry"/>): ванильная запись реликвии
/// пополняется ванильной реликвией, а купленное снаряжение должно смениться снаряжением той же категории.
/// </summary>
[HarmonyPatch(typeof(MerchantRelicEntry), "RestockAfterPurchase")]
public static class EquipmentRestockPatch
{
    private static readonly AccessTools.FieldRef<MerchantEntry, Player> PlayerRef =
        AccessTools.FieldRefAccess<MerchantEntry, Player>("_player");

    private static readonly System.Reflection.MethodInfo? SetModel = AccessTools.Method(typeof(MerchantRelicEntry), "SetModel");

    [HarmonyPrefix]
    public static bool RestockEquipment(MerchantRelicEntry __instance, MerchantInventory? inventory)
    {
        if (__instance.Model is not EquipmentRelic bought || SetModel == null) return true;
        var player = PlayerRef(__instance);
        var shop = player == null ? null : ShopTypePatch.ShopFor(player);
        if (player == null || shop == null) return true;
        try
        {
            var (min, max) = EconomyRules.EquipmentTiers(shop.Value, ShopEconomy.Context(player.RunState));
            var taken = new HashSet<Type> { bought.GetType() };
            foreach (var entry in inventory?.RelicEntries ?? [])
                if (entry.Model != null) taken.Add(entry.Model.GetType());
            var category = bought.PriceCategory;
            EquipmentRelic? pick;
            if (bought is Pantheon.ICelestialGarment) return true;
            if (bought is MageStaff)
            {
                var (sLo, sHi) = EconomyRules.StaffTiers(ShopEconomy.Context(player.RunState));
                pick = ShopStock.PickStaff(player, sLo, sHi, taken);
            }
            else pick = ShopStock.PickEquipment(player, min, max, e => e.PriceCategory == category, taken);
            if (pick == null) return true;
            SetModel.Invoke(__instance, [pick.ToMutable()]);
            return false;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Пополнение витрины: {e.Message}");
            return true;
        }
    }
}
