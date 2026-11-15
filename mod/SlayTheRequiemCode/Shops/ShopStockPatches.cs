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
// | Чародейская| 5 заклинаний        | ваниль         | Трактат + 2 ванильные                 | ваниль                 |
// | Травница   | ваниль              | ваниль         | ваниль                                | 2 зелья + 1 яд Реквиема|

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
            for (var i = 0; i < Slots; i++)
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

        var list = Relics(__instance);
        try
        {
            var act = player.RunState.CurrentActIndex;
            var (min, max) = ShopRules.EquipmentTiers(shop.Value, act);
            var taken = new HashSet<Type>();
            switch (shop.Value)
            {
                case ShopType.Wanderer:
                    AddVanilla(__instance, RelicFactory.RollRarity(player));
                    AddVanilla(__instance, RelicFactory.RollRarity(player));
                    AddEquipment(__instance, min, max, null, taken, fallback: RelicRarity.Shop);
                    break;

                case ShopType.Armorer:
                    var armor = ShopRules.PreferredArmor(player.Character.GetType().Name);
                    AddEquipment(__instance, min, max, e => e is WeaponRelic, taken, fallback: RelicRarity.Common);
                    AddEquipment(__instance, min, max, e => e is ArmorRelic a && (armor == ArmorClass.None || a.Armor == armor), taken, fallback: RelicRarity.Common);
                    AddEquipment(__instance, min, max, e => e.Slot == EquipSlot.OffHand || e is ArmorRelic, taken, fallback: RelicRarity.Shop);
                    break;

                case ShopType.Arcanist:
                    if (ShopStock.PickTreatise(player) is { } treatise) __instance.AddRelicEntry(ShopStock.RelicEntry(treatise, player));
                    else AddVanilla(__instance, RelicRarity.Shop);
                    AddVanilla(__instance, RelicFactory.RollRarity(player));
                    AddVanilla(__instance, RelicFactory.RollRarity(player));
                    break;
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

/// <summary>Травница: 3 слота зелий — 2 зелья и 1 яд Реквиема ступени по акту.</summary>
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
        if (ShopTypePatch.ShopFor(player) != ShopType.Herbalist) return true;

        var list = Potions(__instance);
        try
        {
            var tier = ShopRules.BrewTier(player.RunState.CurrentActIndex);
            foreach (var poison in new[] { false, false, true })
            {
                var brew = ShopStock.PickBrew(player, poison, tier);
                if (brew == null) break;
                list.Add(new MerchantPotionEntry(brew, player));
            }
            if (list.Count == Slots) return false;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Травница: зелья не собрались ({e.Message}) — ванильные зелья.");
        }
        list.Clear();
        return true;
    }
}
