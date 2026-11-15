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
// поэтому вид лавки меняет СОДЕРЖИМОЕ слотов: префиксы на приватных Populate*-методах MerchantInventory.
// Подписка PurchaseCompleted → UpdateEntries делается в CreateForNormalMerchant ПОСЛЕ Populate*, так что наши записи
// получают её как ванильные.
//
// | Вид        | Карты героя (5)     | Бесцветные (2) | Реликвии (3)                          | Зелья (3)              |
// |------------|---------------------|----------------|---------------------------------------|------------------------|
// | Странник   | ваниль              | ваниль         | 2 ванильные + 1 снаряжение I–II       | ваниль                 |
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
[HarmonyPatch(typeof(MerchantInventory), "PopulateCharacterCardEntries")]
public static class SpellShopCardsPatch
{
    private static readonly AccessTools.FieldRef<MerchantInventory, List<MerchantCardEntry>> CharacterCards =
        AccessTools.FieldRefAccess<MerchantInventory, List<MerchantCardEntry>>("_characterCardEntries");

    public const int Slots = 5;

    [HarmonyPrefix]
    public static bool StockSpells(MerchantInventory __instance)
    {
        var player = __instance.Player;
        if (ShopTypePatch.ShopFor(player) != ShopType.Arcanist) return true;
        var pool = ShopStock.SpellCandidates(player);
        if (pool.Count < Slots) return true;

        var list = CharacterCards(__instance);
        try
        {
            var rng = player.PlayerRng.Shops;
            var saleIndex = rng.NextInt(Slots);
            // Заказ «заклинание»: первый слот — заклинание высшей доступной ступени.
            if (ShopTypePatch.OrderFor(player) == OrderCategory.Spell)
            {
                var top = pool.OfType<SlayTheRequiem.SlayTheRequiemCode.Spells.SpellCard>().OrderByDescending(c => (int)c.Tier)
                    .ThenBy(c => c.Id.Entry, StringComparer.Ordinal).FirstOrDefault();
                if (top != null)
                {
                    var ordered = new MerchantCardEntry(player, __instance, new List<CardModel> { top }, top.Rarity);
                    list.Add(ordered);
                    ordered.Populate();
                }
            }
            for (var i = list.Count; i < Slots; i++)
            {
                // Редкость берём у случайного ещё не выставленного заклинания — так CreateForMerchant(rarity)
                // гарантированно найдёт карту (ванильный вариант по редкости не умеет откатываться).
                var shown = list.Select(e => e.CreationResult?.Card.CanonicalInstance).OfType<CardModel>().ToHashSet();
                var remaining = pool.Where(c => !shown.Contains(c)).ToList();
                var sample = rng.NextItem(remaining);
                if (sample == null) break;
                var entry = new MerchantCardEntry(player, __instance, pool, sample.Rarity);
                list.Add(entry);
                entry.Populate();
                if (i == saleIndex) entry.SetOnSale();
            }
            if (list.Count == Slots) return false;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Чародейская лавка: витрина заклинаний не собралась ({e.Message}) — ванильные карты.");
        }
        list.Clear();
        return true;
    }
}

/// <summary>Реликвии: снаряжение у Странника и Оружейника, Трактат в Чародейской.</summary>
[HarmonyPatch(typeof(MerchantInventory), "PopulateRelicEntries")]
public static class ShopRelicsPatch
{
    private static readonly AccessTools.FieldRef<MerchantInventory, List<MerchantRelicEntry>> Relics =
        AccessTools.FieldRefAccess<MerchantInventory, List<MerchantRelicEntry>>("_relicEntries");

    public const int Slots = 3;

    [HarmonyPrefix]
    public static bool StockRelics(MerchantInventory __instance)
    {
        var player = __instance.Player;
        var shop = ShopTypePatch.ShopFor(player);
        if (shop is null or ShopType.Herbalist) return true;
        static bool IsRing(EquipmentRelic e) => e.Slot == EquipSlot.Ring;
        static bool IsAmulet(EquipmentRelic e) => e.Slot == EquipSlot.Amulet;

        var list = Relics(__instance);
        try
        {
            var act = Acts.ActKinds.Tier(player.RunState);
            var (min, max) = ShopRules.EquipmentTiers(shop.Value, act);
            var taken = new HashSet<Type>();
            var order = ShopTypePatch.OrderFor(player);
            var orderTier = ShopRules.OrderTier(shop.Value, act);
            (int, int) Tiers(OrderCategory category) => order == category ? (orderTier, orderTier) : (min, max);
            switch (shop.Value)
            {
                case ShopType.Wanderer:
                {
                    AddVanilla(__instance, RelicFactory.RollRarity(player));
                    AddVanilla(__instance, RelicFactory.RollRarity(player));
                    var (lo, hi) = Tiers(OrderCategory.Equipment);
                    AddEquipment(__instance, lo, hi, null, taken, fallback: RelicRarity.Shop);
                    break;
                }
                case ShopType.Armorer:
                {
                    var armor = ShopRules.PreferredArmor(player.Character.GetType().Name);
                    var (wLo, wHi) = Tiers(OrderCategory.Weapon);
                    var (aLo, aHi) = Tiers(OrderCategory.Armor);
                    var (sLo, sHi) = Tiers(OrderCategory.Shield);
                    AddEquipment(__instance, wLo, wHi, e => e is WeaponRelic, taken, fallback: RelicRarity.Common);
                    AddEquipment(__instance, aLo, aHi, e => e is ArmorRelic a && (armor == ArmorClass.None || a.Armor == armor), taken, fallback: RelicRarity.Common);
                    if (order == OrderCategory.Shield)
                        AddEquipment(__instance, sLo, sHi, e => e.Slot == EquipSlot.OffHand, taken, fallback: RelicRarity.Shop);
                    else
                        AddEquipment(__instance, min, max, e => e.Slot == EquipSlot.OffHand || e is ArmorRelic, taken, fallback: RelicRarity.Shop);
                    break;
                }

                case ShopType.Arcanist:
                {
                    if (ShopStock.PickTreatise(player) is { } treatise) __instance.AddRelicEntry(ShopStock.RelicEntry(treatise, player));
                    else AddVanilla(__instance, RelicRarity.Shop);
                    var (stLo, stHi) = ShopRules.StaffTiers(act);
                    if (ShopStock.PickStaff(player, stLo, stHi, taken) is { } staff) __instance.AddRelicEntry(ShopStock.RelicEntry(staff, player));
                    else AddVanilla(__instance, RelicFactory.RollRarity(player));
                    AddVanilla(__instance, RelicFactory.RollRarity(player));
                    break;
                }
                case ShopType.Jeweler:
                {
                    // Амулет бога вашего благословения — гарантированно, если он есть и ещё не куплен (§2.5).
                    var (lo, hi) = Tiers(OrderCategory.Equipment);
                    var god = Pantheon.CelestialBlessing.Of(player)?.God;
                    if (god != null && ShopStock.GodAmulet(player, god.Value) is { } godAmulet)
                        __instance.AddRelicEntry(ShopStock.RelicEntry(godAmulet, player));
                    else
                        AddEquipment(__instance, 1, 5, IsAmulet, taken, fallback: RelicRarity.Shop);
                    AddEquipment(__instance, lo, Math.Max(hi, 2), IsRing, taken, fallback: RelicRarity.Common);
                    AddEquipment(__instance, 1, 5, e => IsRing(e) || IsAmulet(e), taken, fallback: RelicRarity.Uncommon);
                    break;
                }
                case ShopType.Temple:
                {
                    var god = MerchantLedger.TempleGod(player.RunState);
                    if (ShopStock.GodAmulet(player, god) is { } amulet) __instance.AddRelicEntry(ShopStock.RelicEntry(amulet, player));
                    else AddVanilla(__instance, RelicRarity.Shop);
                    if (ShopStock.GodRobe(player, god) is { } robe) __instance.AddRelicEntry(ShopStock.RelicEntry(robe, player));
                    else AddVanilla(__instance, RelicRarity.Shop);
                    AddVanilla(__instance, RelicFactory.RollRarity(player));
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
                        __instance.AddRelicEntry(ShopStock.RelicEntry(dark, player));
                    }
                    else AddEquipment(__instance, 4, 5, null, taken, fallback: RelicRarity.Rare);
                    AddEquipment(__instance, 4, 5, null, taken, fallback: RelicRarity.Rare);
                    AddVanilla(__instance, RelicRarity.Shop);
                    break;
                }
                case ShopType.Caravan:
                {
                    var (lo, hi) = Tiers(OrderCategory.Equipment);
                    var (eLo, eHi) = ShopRules.EquipmentTiers(ShopType.Armorer, act);
                    AddEquipment(__instance, Math.Max(lo, eLo), Math.Max(hi, eHi), e => e is WeaponRelic || e is ArmorRelic, taken, fallback: RelicRarity.Common);
                    AddEquipment(__instance, 1, 5, e => IsRing(e) || IsAmulet(e), taken, fallback: RelicRarity.Uncommon);
                    // «Экзотика из другого акта» (§2.8): ступенью выше витрины Оружейника.
                    var exotic = Math.Min(5, eHi + 1);
                    AddEquipment(__instance, exotic, exotic, null, taken, fallback: RelicRarity.Rare);
                    break;
                }
            }
            if (list.Count == Slots) return false;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Лавка {shop}: реликвии не собрались ({e.Message}) — ванильная витрина.");
        }
        list.Clear();
        return true;
    }

    private static void AddVanilla(MerchantInventory inventory, RelicRarity rarity) =>
        inventory.AddRelicEntry(new MerchantRelicEntry(rarity, inventory.Player));

    private static void AddEquipment(MerchantInventory inventory, int min, int max, Func<EquipmentRelic, bool>? filter, ISet<Type> taken, RelicRarity fallback)
    {
        var pick = ShopStock.PickEquipment(inventory.Player, min, max, filter, taken);
        if (pick != null) inventory.AddRelicEntry(ShopStock.RelicEntry(pick, inventory.Player));
        else AddVanilla(inventory, fallback);
    }
}

/// <summary>
/// Слоты зелий: Травница — 2 зелья и 1 яд Реквиема ступени по акту; Чародейская — 3 свитка; Скупщик — 2 яда V и зелье V;
/// Караван — зелье, яд и свиток.
/// </summary>
[HarmonyPatch(typeof(MerchantInventory), "PopulatePotionEntries")]
public static class HerbalistPotionsPatch
{
    private static readonly AccessTools.FieldRef<MerchantInventory, List<MerchantPotionEntry>> Potions =
        AccessTools.FieldRefAccess<MerchantInventory, List<MerchantPotionEntry>>("_potionEntries");

    public const int Slots = 3;

    [HarmonyPrefix]
    public static bool StockBrews(MerchantInventory __instance)
    {
        var player = __instance.Player;
        var shop = ShopTypePatch.ShopFor(player);
        if (shop is not (ShopType.Herbalist or ShopType.Arcanist or ShopType.Fence or ShopType.Caravan)) return true;

        var list = Potions(__instance);
        try
        {
            var act = Acts.ActKinds.Tier(player.RunState);
            var tier = ShopRules.BrewTier(act);
            var order = ShopTypePatch.OrderFor(player);
            var scrolls = new HashSet<Type>();
            var scrollTier = ShopRules.ScrollTier(act);
            // (вид, ступень): 0 — зелье, 1 — яд, 2 — свиток.
            (int Kind, int Tier)[] plan = shop.Value switch
            {
                // Заказ: зелье/яд высшей ступени (V).
                ShopType.Herbalist => new[] { (0, order == OrderCategory.Potion ? 5 : tier), (0, tier), (1, order == OrderCategory.Poison ? 5 : tier) },
                ShopType.Arcanist => new[] { (2, scrollTier), (2, scrollTier), (2, scrollTier) },
                ShopType.Fence => new[] { (1, 5), (1, 5), (0, 5) },
                _ => new[] { (0, tier), (1, tier), (2, scrollTier) },
            };
            for (var i = 0; i < plan.Length; i++)
            {
                var (kind, t) = plan[i];
                var model = kind == 2
                    ? ShopStock.PickScroll(player, t, exactTier: i == 0, scrolls)
                    : ShopStock.PickBrew(player, kind == 1, t);
                if (model == null) break;
                list.Add(new MerchantPotionEntry(model, player));
            }
            if (list.Count == Slots) return false;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Лавка {shop}: зелья не собрались ({e.Message}) — ванильные зелья.");
        }
        list.Clear();
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
            var (min, max) = ShopRules.EquipmentTiers(shop.Value, Acts.ActKinds.Tier(player.RunState));
            var taken = new HashSet<Type> { bought.GetType() };
            foreach (var entry in inventory?.RelicEntries ?? [])
                if (entry.Model != null) taken.Add(entry.Model.GetType());
            var category = bought.PriceCategory;
            EquipmentRelic? pick;
            if (bought is Pantheon.ICelestialGarment) return true;
            if (bought is MageStaff)
            {
                var (sLo, sHi) = ShopRules.StaffTiers(Acts.ActKinds.Tier(player.RunState));
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
