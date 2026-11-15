using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Net;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

/// <summary>Карта-болезнь (Хворь/Болезнь, этап 4 — Повелитель Мора): снимается Панацеей Травницы.</summary>
public interface IAilment { }

/// <summary>Источник кражи в лавках (Знамение Ловкача, Кольцо Безликого Вора, Лунная порода-Плут — этапы 4–6).</summary>
public interface ITheftEnabler { }

// ───────────── карты прилавка (только для показа, в колоду не попадают) ─────────────

/// <summary>
/// Услуга прилавка в виде карты на экране «выбери карту». Цена и количество — переменные описания.
/// Создаётся на время выбора и сразу удаляется из состояния забега.
/// </summary>
public abstract class CounterCard : RequiemCard
{
    public const string PriceVar = "Price";
    public const string AmountVar = "Amount";

    protected CounterCard() : base(-1, CardType.Status, CardRarity.Token, TargetType.None)
    {
        WithKeyword(CardKeyword.Unplayable);
        WithVar(PriceVar, 0);
        WithVar(AmountVar, 0);
    }

    public override int MaxUpgradeLevel => 0;

    public void SetDisplay(int price, int amount)
    {
        if (DynamicVars.TryGetValue(PriceVar, out var p)) p.BaseValue = price;
        if (DynamicVars.TryGetValue(AmountVar, out var a)) a.BaseValue = amount;
    }
}

[Pool(typeof(TokenCardPool))] public sealed class CounterSell : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterSharpenWeapon : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterSharpenArmor : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterReforge : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterBrewPotion : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterBrewPoison : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterReagentKit : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterSellReagents : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterPanacea : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterOrder : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterMore : CounterCard { }

/// <summary>Вариант Заказа (14 §1).</summary>
public abstract class OrderCard : CounterCard
{
    public abstract OrderCategory Category { get; }
}

[Pool(typeof(TokenCardPool))] public sealed class OrderWeapon : OrderCard { public override OrderCategory Category => OrderCategory.Weapon; }
[Pool(typeof(TokenCardPool))] public sealed class OrderArmor : OrderCard { public override OrderCategory Category => OrderCategory.Armor; }
[Pool(typeof(TokenCardPool))] public sealed class OrderShield : OrderCard { public override OrderCategory Category => OrderCategory.Shield; }
[Pool(typeof(TokenCardPool))] public sealed class OrderEquipment : OrderCard { public override OrderCategory Category => OrderCategory.Equipment; }
[Pool(typeof(TokenCardPool))] public sealed class OrderSpell : OrderCard { public override OrderCategory Category => OrderCategory.Spell; }
[Pool(typeof(TokenCardPool))] public sealed class OrderPotion : OrderCard { public override OrderCategory Category => OrderCategory.Potion; }
[Pool(typeof(TokenCardPool))] public sealed class OrderPoison : OrderCard { public override OrderCategory Category => OrderCategory.Poison; }

/// <summary>
/// Прилавок торговца (docs/design/14 §1–2): продажа, услуги, Заказ. В ванильной сцене лавки нет кнопок для этого,
/// поэтому прилавок открывается, когда игрок закрывает витрину: экран «выбери карту», где каждая карта — услуга
/// с ценой (до 3 на экране, «Ещё…» листает). Выбор локальный, а эффект — синхронное действие
/// <see cref="ShopActions"/>, поэтому всё работает и в коопе.
/// </summary>
public static class ShopServices
{
    private sealed record Option(Func<RunState, Player, CounterCard> Make, int Price, int Amount, Func<Task> Run);

    private static bool _open;

    // ───── что можно сделать ─────

    public static AlchemyRules.BrewOption? BestBrew(Player player, bool poison)
    {
        if (HerbalistPouch.Of(player) is not { } pouch) return null;
        // Сильнейший доступный рецепт нужного вида; при равной ступени — первый по таблице рецептов (одинаково у всех).
        foreach (var option in AlchemyRules.Available(pouch.Reagents).OrderByDescending(o => o.Tier))
            if (option.Recipe.IsPoison == poison) return option;
        return null;
    }

    public static IEnumerable<CardModel> Ailments(Player player) => player.Deck.Cards.Where(c => c is IAilment).ToList();

    /// <summary>Что точить вторым: надетая броня/щит/плащ с наименьшим улучшением (при равенстве — старшей ступени).</summary>
    public static EquipmentRelic? ArmorToSharpen(Player player) =>
        Loadout.Equipped(player)
            .Where(r => r.CanUpgrade && r.Slot is not (EquipSlot.MainHand or EquipSlot.Ring or EquipSlot.Amulet))
            .OrderBy(r => r.Upgrades).ThenByDescending(r => r.Tier).ThenBy(r => r.Id.Entry, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>Перековка: оружие того же класса ступени, но другого вида (свойство оружия задаётся видом, 07 §3.1).</summary>
    public static WeaponRelic? ReforgeResult(Player player, EquipmentRelic weapon)
    {
        var owned = player.Relics.Select(r => r.GetType()).ToHashSet();
        var options = ModelDb.AllRelics.OfType<WeaponRelic>()
            .Where(w => w.IsAllowedInShops && w.Tier == weapon.Tier && !owned.Contains(w.GetType()))
            .OrderBy(w => w.Id.Entry, StringComparer.Ordinal)
            .ToList();
        if (options.Count == 0) return null;
        var hash = ShopRules.Fnv1a($"{player.RunState.Rng.StringSeed}|reforge|{weapon.Id.Entry}", player.RunState.TotalFloor);
        return options[(int)(hash % (uint)options.Count)];
    }

    /// <summary>Набор реагентов Травницы: 3 обычных реагента (без драконьей чешуи) по зерну, этажу и содержимому сумки.</summary>
    public static List<Reagent> ReagentKit(Player player)
    {
        var basic = Enum.GetValues<Reagent>().Where(r => r != Reagent.DragonScale).ToList();
        var total = HerbalistPouch.Of(player)?.Total ?? 0;
        var kit = new List<Reagent>();
        for (var i = 0; i < ShopRules.ReagentKitSize; i++)
        {
            var hash = ShopRules.Fnv1a($"{player.RunState.Rng.StringSeed}|kit|{player.NetId}|{total}|{i}", player.RunState.TotalFloor);
            kit.Add(basic[(int)(hash % (uint)basic.Count)]);
        }
        return kit;
    }

    private static List<Option> Options(Player player)
    {
        var list = new List<Option>();
        var ledger = MerchantLedger.Of(player);
        if (ledger == null || !MerchantLedger.Active(player)) return list;
        var trust = ledger.CurrentTrust;
        var gold = player.Gold;
        var act = player.RunState.CurrentActIndex;
        int Price(int basePrice) => ShopRules.ApplyTrust(basePrice, trust);

        void Paid<T>(int price, int amount, Func<Task> run) where T : CounterCard
        {
            if (price <= gold) list.Add(new Option((s, p) => s.CreateCard<T>(p), price, amount, run));
        }

        // Продажа — в любой лавке.
        var sellable = SellService.Sellable(player);
        if (sellable.Count > 0)
        {
            var total = sellable.Sum(i => SellService.Value(i, trust));
            list.Add(new Option((s, p) => s.CreateCard<CounterSell>(p), total, sellable.Count, () => SellService.Sell(player, sellable, trust)));
        }

        switch (ledger.CurrentShop)
        {
            case ShopType.Armorer:
            {
                var weapon = Loadout.InSlot(player, EquipSlot.MainHand);
                if (weapon is { CanUpgrade: true })
                {
                    var price = Price(ShopRules.SharpenPrice(weapon.Tier));
                    Paid<CounterSharpenWeapon>(price, weapon.Upgrades + 1,
                        () => ShopActions.Perform(player, ShopAction.Sharpen, price, 0, weapon.Id.Entry));
                }
                if (ArmorToSharpen(player) is { } armor)
                {
                    var price = Price(ShopRules.SharpenPrice(armor.Tier));
                    Paid<CounterSharpenArmor>(price, armor.Upgrades + 1,
                        () => ShopActions.Perform(player, ShopAction.Sharpen, price, 0, armor.Id.Entry));
                }
                if (weapon != null && ReforgeResult(player, weapon) is { } next)
                {
                    var price = Price(ShopRules.ReforgePrice);
                    Paid<CounterReforge>(price, weapon.Tier,
                        () => ShopActions.Perform(player, ShopAction.Reforge, price, 0, $"{weapon.Id.Entry}|{next.Id.Entry}"));
                }
                break;
            }
            case ShopType.Herbalist:
            {
                var brew = Price(ShopRules.BrewServicePrice);
                if (BestBrew(player, poison: false) is { } potion)
                    Paid<CounterBrewPotion>(brew, potion.Tier, () => ShopActions.Perform(player, ShopAction.Brew, brew, 0));
                if (BestBrew(player, poison: true) is { } poison)
                    Paid<CounterBrewPoison>(brew, poison.Tier, () => ShopActions.Perform(player, ShopAction.Brew, brew, 1));

                if (HerbalistPouch.Of(player) is { } pouch)
                {
                    var kitPrice = Price(ShopRules.ReagentKitPrice(act));
                    var kit = string.Join(",", ReagentKit(player).Select(r => ((int)r).ToString()));
                    Paid<CounterReagentKit>(kitPrice, ShopRules.ReagentKitSize,
                        () => ShopActions.Perform(player, ShopAction.BuyReagents, kitPrice, 0, kit));
                    if (pouch.Total > 0)
                    {
                        var value = ShopRules.ReagentSellValue(pouch.Total);
                        list.Add(new Option((s, p) => s.CreateCard<CounterSellReagents>(p), value, pouch.Total,
                            () => ShopActions.Perform(player, ShopAction.SellReagents, value, 0)));
                    }
                }

                var ailments = Ailments(player).Count();
                if (ailments > 0)
                {
                    var price = Price(ShopRules.PanaceaPrice);
                    Paid<CounterPanacea>(price, ailments, () => ShopActions.Perform(player, ShopAction.Panacea, price, 0));
                }
                break;
            }
        }

        if (ShopRules.CanOrder(trust) && !ledger.HasOrder)
            list.Add(new Option((s, p) => s.CreateCard<CounterOrder>(p), 0, 0, () => OrderMenu(player, ledger)));
        return list;
    }

    public static bool HasOptions(Player player) => Options(player).Count > 0;

    // ───── экран прилавка ─────

    public static async Task OpenCounter(Player player)
    {
        if (_open || !LocalContext.IsMe(player)) return;
        _open = true;
        try
        {
            // Несколько услуг подряд: после каждой прилавок открывается снова, пока игрок не пропустит.
            for (var guard = 0; guard < 16; guard++)
            {
                var options = Options(player);
                if (options.Count == 0) return;
                var picked = await Choose(player, options);
                if (picked == null) return;
                await picked.Run();
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Прилавок: {e.Message}");
        }
        finally
        {
            _open = false;
        }
    }

    private static async Task OrderMenu(Player player, MerchantLedger ledger)
    {
        var shop = ledger.CurrentShop;
        var act = player.RunState.CurrentActIndex;
        var options = new List<Option>();
        foreach (var category in ShopRules.OrderOptions(shop))
        {
            var price = ShopRules.ApplyTrust(ShopRules.OrderPrice(ShopRules.OrderItemPrice(category, act)), ledger.CurrentTrust);
            if (price > player.Gold) continue;
            Func<RunState, Player, CounterCard> make = category switch
            {
                OrderCategory.Weapon => (s, p) => s.CreateCard<OrderWeapon>(p),
                OrderCategory.Armor => (s, p) => s.CreateCard<OrderArmor>(p),
                OrderCategory.Shield => (s, p) => s.CreateCard<OrderShield>(p),
                OrderCategory.Spell => (s, p) => s.CreateCard<OrderSpell>(p),
                OrderCategory.Potion => (s, p) => s.CreateCard<OrderPotion>(p),
                OrderCategory.Poison => (s, p) => s.CreateCard<OrderPoison>(p),
                _ => (s, p) => s.CreateCard<OrderEquipment>(p),
            };
            var cat = category;
            options.Add(new Option(make, price, ShopRules.OrderTier(shop, act),
                () => ShopActions.Perform(player, ShopAction.PlaceOrder, (int)shop, (int)cat, price.ToString())));
        }
        if (options.Count == 0) return;
        var picked = await Choose(player, options);
        if (picked != null) await picked.Run();
    }

    private static async Task<Option?> Choose(Player player, IReadOnlyList<Option> options)
    {
        var paged = options.Count > 3;
        var perPage = paged ? 2 : 3;
        var page = 0;
        for (var guard = 0; guard < 32; guard++)
        {
            var slice = options.Skip(page * perPage).Take(perPage).ToList();
            var index = await Show(player, slice, paged);
            if (index == -2)
            {
                page = (page + 1) * perPage >= options.Count ? 0 : page + 1;
                continue;
            }
            return index >= 0 && index < slice.Count ? slice[index] : null;
        }
        return null;
    }

    /// <summary>Показать карты услуг: индекс выбранной, −1 — пропуск, −2 — «Ещё…».</summary>
    private static async Task<int> Show(Player player, List<Option> slice, bool withMore)
    {
        var state = RequiemNet.State;
        if (state == null) return -1;
        var cards = new List<CardModel>();
        try
        {
            foreach (var option in slice)
            {
                var card = option.Make(state, player);
                card.SetDisplay(option.Price, option.Amount);
                cards.Add(card);
            }
            if (withMore) cards.Add(state.CreateCard<CounterMore>(player));

            // Экран напрямую (а не CardSelectCmd): выбор чисто локальный, PlayerChoiceSynchronizer не нужен.
            var chosen = await RequiemNet.ChooseCard(cards, canSkip: true);
            if (chosen == null) return -1;
            if (chosen is CounterMore) return -2;
            return cards.IndexOf(chosen);
        }
        finally
        {
            foreach (var card in cards) state.RemoveCard(card);
        }
    }

    // ───── кража (14 §1) ─────

    public static bool CanSteal(Player player, MerchantEntry entry)
    {
        if (entry is MerchantCardRemovalEntry || !MerchantLedger.Active(player)) return false;
        if (MerchantLedger.Of(player) is not { } ledger || ledger.TheftTriedHere(player.RunState)) return false;
        return RequiemConfig.AllowTheft || player.Relics.Any(r => r is ITheftEnabler);
    }

    /// <summary>Сейчас идёт покупка-кража: золото не списывается и «потерял золото» не рассылается.</summary>
    internal static bool StealingNow { get; private set; }

    public static async Task<bool> TryTheft(MerchantEntry entry, MerchantInventory? inventory, Player player)
    {
        var ledger = MerchantLedger.Of(player);
        if (ledger == null) return false;
        var price = entry.Cost;
        var chance = ShopRules.TheftChancePercent(price);

        if (!await RequiemNet.Confirm("SLAYTHEREQUIEM-MERCHANT_LEDGER", "theft", body =>
            {
                body.Add("Chance", chance);
                body.Add("Gold", price);
            }))
            return false;

        await ShopActions.Perform(player, ShopAction.TheftAttempt);
        var hash = ShopRules.Fnv1a($"{player.RunState.Rng.StringSeed}|theft|{player.NetId}|{price}", player.RunState.TotalFloor);
        if (ShopRules.TheftSucceeds(hash, price))
        {
            StealingNow = true;
            try { return await entry.OnTryPurchaseWrapper(inventory, ignoreCost: true); }
            finally { StealingNow = false; }
        }

        await ShopActions.Perform(player, ShopAction.TheftFailed, (int)ledger.CurrentShop, ShopRules.TheftFailDamage(player.Creature.MaxHp));
        return false;
    }
}

/// <summary>Прилавок открывается, когда игрок закрывает витрину торговца.</summary>
[HarmonyPatch(typeof(NMerchantInventory), "Close")]
public static class MerchantCounterPatch
{
    [HarmonyPostfix]
    public static void OpenCounter()
    {
        var state = RequiemNet.State;
        if (state == null || state.CurrentRoom is not MerchantRoom || NRun.Instance?.MerchantRoom == null) return;
        var me = LocalContext.GetMe(state);
        if (me == null || me.Creature.IsDead || !ShopServices.HasOptions(me)) return;
        TaskHelper.RunSafely(ShopServices.OpenCounter(me));
    }
}

/// <summary>Кража: клик по товару, на который не хватает золота, при наличии источника кражи.</summary>
[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.OnTryPurchaseWrapper))]
public static class MerchantTheftPatch
{
    private static readonly AccessTools.FieldRef<MerchantEntry, Player> PlayerRef =
        AccessTools.FieldRefAccess<MerchantEntry, Player>("_player");

    [HarmonyPrefix]
    public static bool OfferTheft(MerchantEntry __instance, MerchantInventory? inventory, bool ignoreCost, ref Task<bool> __result)
    {
        if (ignoreCost || !__instance.IsStocked || __instance.EnoughGold) return true;
        var player = PlayerRef(__instance);
        if (player == null || !LocalContext.IsMe(player) || !ShopServices.CanSteal(player, __instance)) return true;
        __result = ShopServices.TryTheft(__instance, inventory, player);
        return false;
    }
}

/// <summary>
/// Ванильная запись реликвии рассылает «потерял золото» даже при покупке без оплаты — при краже это списало бы
/// золото у вора на машинах других игроков. Пока идёт кража, рассылку пропускаем.
/// </summary>
[HarmonyPatch(typeof(RewardSynchronizer), nameof(RewardSynchronizer.SyncLocalGoldLost))]
public static class TheftGoldSyncPatch
{
    [HarmonyPrefix]
    public static bool SkipWhileStealing() => !ShopServices.StealingNow;
}
