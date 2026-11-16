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
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

/// <summary>Карта-болезнь (Хворь/Болезнь, этап 4 — Повелитель Мора): снимается Панацеей Травницы.</summary>
public interface IAilment { }

/// <summary>Источник кражи в лавках: Знамение Ловкача, Кольцо Безликого Вора, предыстория Плут (14 §1).</summary>
public interface ITheftEnabler
{
    /// <summary>Включает ли кражу сейчас (кольцо — только надетое).</summary>
    bool EnablesTheft => true;
    /// <summary>Лишние попытки кражи за визит (Кольцо Безликого Вора).</summary>
    int ExtraTheftAttempts => 0;
}

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

    public override bool IsMenuToken => true;
    public override int MaxUpgradeLevel => 0;

    public void SetDisplay(int price, int amount)
    {
        if (DynamicVars.TryGetValue(PriceVar, out var p)) p.BaseValue = price;
        if (DynamicVars.TryGetValue(AmountVar, out var a)) a.BaseValue = amount;
    }
}

// CounterSell и CounterSellReagents с M6 п. 5 на прилавке не показываются: продажа — вкладка «Продать» (ShopTabs.cs).
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
// M5: Чародей, Ювелир, Храм, Скупщик, Караван (14 §2.3–2.8).
[Pool(typeof(TokenCardPool))] public sealed class CounterEnchantOwnStone : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterEnchantShopStone : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterRewriteScroll : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterEnchantCard : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterSoulStone : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterEngrave : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterFacet : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterCleanse : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterTempleHeal : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterAtonement : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterChangeBlessing : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterRemoveCurse : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterClearName : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterTrade : CounterCard { }
// M5 п. 5: вьючный зверь (10 §6).
[Pool(typeof(TokenCardPool))] public sealed class CounterBuyMule : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterShoeMule : CounterCard { }
[Pool(typeof(TokenCardPool))] public sealed class CounterMuleChest : CounterCard { }

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
/// Прилавок торговца (docs/design/14 §1–2): услуги и Заказ. Открывается вкладкой «Услуги» слева от витрины
/// (Shops/ShopTabs.cs, M6 п. 5; продажа — вкладка «Продать»): экран «выбери карту», где каждая карта — услуга
/// с ценой (до 3 на экране, «Ещё…» листает). Выбор локальный, а эффект — синхронное действие
/// <see cref="ShopActions"/>, поэтому всё работает и в коопе.
/// </summary>
public static class ShopServices
{
    private sealed record Option(Func<RunState, Player, CounterCard> Make, int Price, int Amount, Func<Task> Run);

    private static bool _open;

    /// <summary>Сброс при новом забеге (ревью F-06).</summary>
    internal static void ResetForNewRun() => _open = false;

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

    /// <summary>Огранка: чары <paramref name="from"/>, которые подходят слоту <paramref name="to"/>, поверх чар цели. null — переносить нечего.</summary>
    public static List<EnchantSlot>? FacetResult(Player player, EquipmentRelic from, EquipmentRelic to)
    {
        var moving = from.EnchantSlots.Where(slot => EnchantRules.Allowed(slot.Enchant, to.Slot)).ToList();
        if (moving.Count == 0) return null;
        var slots = Progression.Enchanting.SlotsFor(player);
        var result = to.EnchantSlots;
        foreach (var slot in moving) result = EnchantRules.Apply(result, slot.Enchant, slot.Power, slots);
        return result;
    }

    public static bool CanFacet(Player player)
    {
        var gear = Loadout.Equipped(player);
        return gear.Any(from => from.EnchantSlots.Count > 0 && gear.Any(to => to != from && FacetResult(player, from, to) != null));
    }

    /// <summary>Что можно отдать Каравану: снятое снаряжение из рюкзака.</summary>
    public static List<EquipmentRelic> Tradeable(Player player) => SellService.Sellable(player).ToList();

    /// <summary>
    /// Обмен Каравана (14 §2.8): предмет той же ступени или на одну ниже, которого у героя нет, — по зерну, этажу и отданному
    /// предмету (одинаково у всех игроков). null — подходящего нет.
    /// </summary>
    public static EquipmentRelic? TradeResult(Player player, EquipmentRelic given)
    {
        var (min, max) = ShopRules.TradeTiers(given.Tier);
        var owned = player.Relics.Select(r => r.GetType()).ToHashSet();
        var options = ModelDb.AllRelics.OfType<EquipmentRelic>()
            .Where(e => e.IsAllowedInShops && e is not MageStaff && e.Tier >= min && e.Tier <= max && !owned.Contains(e.GetType()))
            .OrderBy(e => e.Id.Entry, StringComparer.Ordinal)
            .ToList();
        if (options.Count == 0) return null;
        var hash = ShopRules.Fnv1a($"{player.RunState.Rng.StringSeed}|trade|{given.Id.Entry}", player.RunState.TotalFloor);
        return options[(int)(hash % (uint)options.Count)];
    }

    private static List<Option> Options(Player player)
    {
        var list = new List<Option>();
        var ledger = MerchantLedger.Of(player);
        if (ledger == null || !MerchantLedger.Active(player)) return list;
        var trust = ledger.CurrentTrust;
        var gold = player.Gold;
        var act = Acts.ActKinds.Tier(player.RunState);
        int Price(int basePrice) => ShopRules.ApplyTrust(basePrice, trust);

        void Paid<T>(int price, int amount, Func<Task> run) where T : CounterCard
        {
            if (price <= gold) list.Add(new Option((s, p) => s.CreateCard<T>(p), price, amount, run));
        }

        // Камень душ (Чародей, Ювелир): сила по акту.
        void AddStone(int actIndex)
        {
            if (Progression.HeroRecord.Of(player) == null) return;
            var power = ShopRules.SoulStonePower(actIndex);
            var price = Price(ShopRules.SoulStonePrice(power));
            Paid<CounterSoulStone>(price, power, () => ShopActions.Perform(player, ShopAction.BuyStone, price, power));
        }

        // Продажа снятого снаряжения и реагентов — вкладка «Продать» (Shops/ShopTabs.cs, M6 п. 5), не прилавок.

        // Вьючный зверь (10 §6): Сундук у прилавка любой лавки; купить — у Странника и в Караване; подковать — у Оружейника.
        if (Alchemy.PackMule.Of(player) is { } mule)
        {
            list.Add(new Option((s, p) => s.CreateCard<CounterMuleChest>(p), 0, Alchemy.TravelPack.Of(player)?.StoredCount ?? 0,
                () => ShopActions.Perform(player, ShopAction.MuleChest)));
            if (!mule.Shod && ledger.CurrentShop == ShopType.Armorer)
            {
                var price = Price(CampRules.MuleShoePrice);
                Paid<CounterShoeMule>(price, CampRules.ShodBurden, () => ShopActions.Perform(player, ShopAction.ShoeMule, price));
            }
        }
        else if (Alchemy.PackMule.CanAdopt(player) && ledger.CurrentShop is ShopType.Wanderer or ShopType.Caravan)
        {
            var price = Price(CampRules.MulePrice);
            Paid<CounterBuyMule>(price, CampRules.MuleBurden, () => ShopActions.Perform(player, ShopAction.BuyMule, price));
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
                }

                var ailments = Ailments(player).Count();
                if (ailments > 0)
                {
                    var price = Price(ShopRules.PanaceaPrice);
                    Paid<CounterPanacea>(price, ailments, () => ShopActions.Perform(player, ShopAction.Panacea, price, 0));
                }
                break;
            }
            case ShopType.Arcanist:
            {
                var hero = Progression.HeroRecord.Of(player);
                if (hero != null && Progression.Enchanting.Enchantable(player).Count > 0)
                {
                    var own = new[] { 3, 2, 1 }.FirstOrDefault(p => hero.Stones(p) > 0);
                    if (own > 0)
                    {
                        var price = Price(ShopRules.EnchantServicePrice(own, ownStone: true));
                        Paid<CounterEnchantOwnStone>(price, own, () => ShopActions.Perform(player, ShopAction.EnchantItem, price, own, "own"));
                    }
                    var shopStone = ShopRules.SoulStonePower(act);
                    var shopPrice = Price(ShopRules.EnchantServicePrice(shopStone, ownStone: false));
                    Paid<CounterEnchantShopStone>(shopPrice, shopStone, () => ShopActions.Perform(player, ShopAction.EnchantItem, shopPrice, shopStone, "shop"));
                }
                if (player.PotionSlots.OfType<RequiemScroll>().FirstOrDefault() is { } scroll)
                {
                    var price = Price(ShopRules.RewriteScrollPrice);
                    Paid<CounterRewriteScroll>(price, scroll.Tier, () => ShopActions.Perform(player, ShopAction.RewriteScroll, price, 0, scroll.Id.Entry));
                }
                {
                    var price = Price(ShopRules.CardEnchantPrice);
                    Paid<CounterEnchantCard>(price, 1, () => ShopActions.Perform(player, ShopAction.EnchantCard, price, 0));
                }
                AddStone(act);
                break;
            }
            case ShopType.Jeweler:
            {
                if (Progression.Enchanting.Enchantable(player).Any(i => i.Slot is EquipSlot.Ring or EquipSlot.Amulet))
                {
                    var price = Price(ShopRules.EngravePrice);
                    Paid<CounterEngrave>(price, 1, () => ShopActions.Perform(player, ShopAction.Engrave, price, 0));
                }
                if (CanFacet(player))
                {
                    var price = Price(ShopRules.FacetPrice);
                    Paid<CounterFacet>(price, 0, () => ShopActions.Perform(player, ShopAction.Facet, price, 0));
                }
                AddStone(act);
                break;
            }
            case ShopType.Temple:
            {
                var god = MerchantLedger.TempleGod(player.RunState);
                if (RunSettings.EnablePantheon && Pantheon.CelestialBlessing.Of(player)?.God != god)
                {
                    var price = Price(ShopRules.BlessingChangePrice);
                    Paid<CounterChangeBlessing>(price, (int)god, () => ShopActions.Perform(player, ShopAction.ChangeBlessing, price, (int)god));
                }
                if (player.Deck.Cards.Any(c => c.IsRemovable))
                {
                    var price = Price(ShopRules.CleansePrice(Pantheon.Faith.Effective(player) == Piety.Righteous));
                    Paid<CounterCleanse>(price, 1, () => ShopActions.Perform(player, ShopAction.Cleanse, price, 0));
                }
                var creature = player.Creature;
                if (creature.CurrentHp < creature.MaxHp)
                {
                    var price = Price(ShopRules.TempleHealPrice);
                    var heal = (int)CampRules.HealAmount(creature.MaxHp, ShopRules.TempleHealPercent);
                    Paid<CounterTempleHeal>(price, heal, () => ShopActions.Perform(player, ShopAction.TempleHeal, price, heal));
                }
                if (Pantheon.Faith.CanRedeem(player))
                {
                    var price = Price(ShopRules.AtonementPrice);
                    Paid<CounterAtonement>(price, 1, () => ShopActions.Perform(player, ShopAction.Atonement, price, 0));
                }
                break;
            }
            case ShopType.Fence:
            {
                var curses = player.Deck.Cards.Count(c => c.Type == CardType.Curse && c.IsRemovable);
                if (curses > 0 && ShopRules.CanPayCurseRemoval(player.Creature.MaxHp))
                    list.Add(new Option((s, p) => s.CreateCard<CounterRemoveCurse>(p), 0, ShopRules.CurseRemovalMaxHp,
                        () => ShopActions.Perform(player, ShopAction.RemoveCurse, 0, ShopRules.CurseRemovalMaxHp)));
                var target = ShopRules.ClearNameTarget(ledger.Spent);
                if (target >= 0)
                {
                    var price = Price(ShopRules.ClearNamePrice);
                    Paid<CounterClearName>(price, target, () => ShopActions.Perform(player, ShopAction.ClearName, price, target));
                }
                break;
            }
            case ShopType.Caravan:
            {
                if (!ledger.TradedHere(player.RunState) && Tradeable(player).Any(i => TradeResult(player, i) != null))
                    list.Add(new Option((s, p) => s.CreateCard<CounterTrade>(p), 0, 1, () => ShopActions.Perform(player, ShopAction.Trade)));
                // Реагенты в лавках (09, M5 п. 5): Караван везёт и травы — тот же набор, что у Травницы.
                if (HerbalistPouch.Of(player) != null)
                {
                    var kitPrice = Price(ShopRules.ReagentKitPrice(act));
                    var kit = string.Join(",", ReagentKit(player).Select(r => ((int)r).ToString()));
                    Paid<CounterReagentKit>(kitPrice, ShopRules.ReagentKitSize,
                        () => ShopActions.Perform(player, ShopAction.BuyReagents, kitPrice, 0, kit));
                }
                break;
            }
        }

        if (ShopRules.CanOrder(trust) && !ledger.HasOrder && ShopRules.OrderOptions(ledger.CurrentShop).Count > 0)
            list.Add(new Option((s, p) => s.CreateCard<CounterOrder>(p), 0, 0, () => OrderMenu(player, ledger)));
        return list;
    }

    public static bool HasOptions(Player player) => Options(player).Count > 0;

    /// <summary>Сколько услуг у прилавка сейчас (число на вкладке «Услуги»).</summary>
    public static int ServiceCount(Player player) => Options(player).Count;

    /// <summary>Прилавок открыт (вкладка «Услуги» подсвечена, пока идёт выбор).</summary>
    public static bool CounterOpen => _open;

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
        var act = Acts.ActKinds.Tier(player.RunState);
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
        var enablers = player.Relics.OfType<ITheftEnabler>().ToList();
        var attempts = 1 + enablers.Sum(e => e.ExtraTheftAttempts);
        if (MerchantLedger.Of(player) is not { } ledger || ledger.TheftTriedHere(player.RunState, attempts)) return false;
        return RunSettings.AllowTheft || enablers.Any(e => e.EnablesTheft);
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

        // Пойманного вора выставляет Стража: в одиночной игре — бой с облегчённой элитой (лавка закрывается),
        // в коопе (бой общий, а лавка у каждого своя) — по-прежнему потеря 20 % макс. ♥.
        var fight = !RequiemNet.IsMultiplayer;
        await ShopActions.Perform(player, ShopAction.TheftFailed, (int)ledger.CurrentShop,
            fight ? 0 : ShopRules.TheftFailDamage(player.Creature.MaxHp));
        if (fight) StartGuardFight(player);
        return false;
    }

    /// <summary>Бой со Стражей лавки (<see cref="Monsters.ShopGuardElite"/>) поверх лавки; после боя — карта.</summary>
    private static void StartGuardFight(Player player)
    {
        try
        {
            var encounter = ModelDb.Encounter<Monsters.ShopGuardElite>().ToMutable();
            var room = new CombatRoom(encounter, player.RunState) { ShouldResumeParentEventAfterCombat = false };
            TaskHelper.RunSafely(RunManager.Instance.EnterRoomWithoutExitingCurrentRoom(room, true));
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Стража лавки: бой не начался ({e.Message}) — только потеря доверия.");
        }
    }

    // ───── предупреждение о владении школой (08 §8, 14 §2.3) ─────

    /// <summary>Подтверждение уже получено — повторный вызов покупки идёт без окна.</summary>
    internal static bool MasteryConfirmed { get; set; }

    /// <summary>Разрыв ступени и владения у заклинания с витрины (0 — колдуется свободно).</summary>
    public static int MasteryGap(Player player, MerchantEntry entry) =>
        entry is MerchantCardEntry { CreationResult.Card: Spells.SpellCard spell } && RequiemMode.IsInSystem(player)
            ? Math.Max(0, SpellAffinity.Gap(spell.Tier, Spells.Mastery.Of(player, spell.School)))
            : 0;

    public static async Task<bool> ConfirmMasteryAndBuy(MerchantEntry entry, MerchantInventory? inventory, Player player)
    {
        if (entry is not MerchantCardEntry { CreationResult.Card: Spells.SpellCard spell }) return false;
        var mastery = Spells.Mastery.Of(player, spell.School);
        var gap = MasteryGap(player, entry);
        if (!await RequiemNet.Confirm("SLAYTHEREQUIEM-MERCHANT_LEDGER", "mastery", body =>
            {
                body.Add("School", (int)spell.School);
                body.Add("Mastery", mastery);
                body.Add("Tier", (int)spell.Tier);
                body.Add("Gap", gap);
            }))
            return false;
        MasteryConfirmed = true;
        try { return await entry.OnTryPurchaseWrapper(inventory); }
        finally { MasteryConfirmed = false; }
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
/// Предупреждение о владении школой: заклинание выше владения (колдуется «С трудом» — ✦ ×1.5, или «С напряжением» — ✦ ×2
/// и эффект ×0.75) покупается только после подтверждения.
/// </summary>
[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.OnTryPurchaseWrapper))]
public static class SpellMasteryWarningPatch
{
    private static readonly AccessTools.FieldRef<MerchantEntry, Player> PlayerRef =
        AccessTools.FieldRefAccess<MerchantEntry, Player>("_player");

    [HarmonyPrefix]
    public static bool WarnAboutMastery(MerchantEntry __instance, MerchantInventory? inventory, bool ignoreCost, ref Task<bool> __result)
    {
        if (ignoreCost || ShopServices.MasteryConfirmed || !__instance.IsStocked || !__instance.EnoughGold) return true;
        var player = PlayerRef(__instance);
        if (player == null || !LocalContext.IsMe(player) || !MerchantLedger.Active(player) || ShopServices.MasteryGap(player, __instance) <= 0)
            return true;
        __result = ShopServices.ConfirmMasteryAndBuy(__instance, inventory, player);
        return false;
    }
}

/// <summary>
/// Ценник товара, который можно украсть (<see cref="ShopServices.CanSteal"/>): вместо красной цены — «цена ✋шанс %»
/// фиолетовым, чтобы было видно, что клик не «не хватает золота», а попытка кражи. Патчим UpdateVisual каждого слота
/// (наследники NMerchantSlot пишут ценник сами после вызова базового метода).
/// </summary>
public static class TheftPriceTag
{
    private static readonly AccessTools.FieldRef<NMerchantSlot, MegaCrit.Sts2.addons.mega_text.MegaLabel> CostLabel =
        AccessTools.FieldRefAccess<NMerchantSlot, MegaCrit.Sts2.addons.mega_text.MegaLabel>("_costLabel");

    private static readonly Godot.Color TheftColor = new(0.78f, 0.55f, 1f);

    public static void Apply(NMerchantSlot slot)
    {
        try
        {
            var entry = slot.Entry;
            if (entry == null || !entry.IsStocked || entry.EnoughGold) return;
            var state = RequiemNet.State;
            var me = state == null ? null : LocalContext.GetMe(state);
            if (me == null || !ShopServices.CanSteal(me, entry)) return;
            var label = CostLabel(slot);
            if (label == null) return;
            label.SetTextAutoSize($"{entry.Cost} ✋{ShopRules.TheftChancePercent(entry.Cost)}%");
            label.Modulate = TheftColor;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Ценник кражи: {e.Message}");
        }
    }
}

[HarmonyPatch(typeof(NMerchantCard), "UpdateVisual")]
public static class TheftPriceTagCardPatch
{
    [HarmonyPostfix]
    public static void Tag(NMerchantCard __instance) => TheftPriceTag.Apply(__instance);
}

[HarmonyPatch(typeof(NMerchantRelic), "UpdateVisual")]
public static class TheftPriceTagRelicPatch
{
    [HarmonyPostfix]
    public static void Tag(NMerchantRelic __instance) => TheftPriceTag.Apply(__instance);
}

[HarmonyPatch(typeof(NMerchantPotion), "UpdateVisual")]
public static class TheftPriceTagPotionPatch
{
    [HarmonyPostfix]
    public static void Tag(NMerchantPotion __instance) => TheftPriceTag.Apply(__instance);
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
