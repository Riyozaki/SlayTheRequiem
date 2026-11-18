using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

public sealed record ShopQuote(int Price = 0, int Value = 0, int Amount = 0, string Target = "");

/// <summary>Executor authority. UI quotes and network operands are requests, never prices, quantities or permissions.</summary>
public static class ShopActionPolicy
{
    public static bool Environment(Player player, ShopAction kind) =>
        RequiemMode.IsInSystem(player) && player.Creature.IsAlive && !CombatManager.Instance.IsInProgress
        && (kind == ShopAction.DiscardCard || player.RunState.CurrentRoom is MerchantRoom && MerchantLedger.Active(player)
            && MerchantLedger.Of(player)?.CurrentFloor == player.RunState.TotalFloor);

    public static bool OwnsCard(Player player, CardModel card) =>
        card.IsMutable && card.Owner == player && card.Pile?.Type == PileType.Deck && player.Deck.Cards.Contains(card);

    public static bool TryQuote(Player player, ShopAction kind, int a, int b, string s, out ShopQuote quote, out string error)
    {
        quote = new();
        error = "Operation no longer available";
        if (!Enum.IsDefined(kind) || !Environment(player, kind)) return false;
        var ledger = MerchantLedger.Of(player);
        var shop = ledger?.CurrentShop ?? ShopType.Wanderer;
        var trust = ledger?.CurrentTrust ?? 0;
        var act = Acts.ActKinds.Tier(player.RunState);
        var hero = HeroRecord.Of(player);
        var pouch = HerbalistPouch.Of(player);
        var price = 0;
        var value = 0;
        var amount = b;
        var target = s;
        bool At(params ShopType[] shops) => shops.Contains(shop);
        EquipmentRelic? Item(string key) => InventoryIdentity.FindRelic(player, key) as EquipmentRelic;
        bool Paid(int basePrice) { price = ShopRules.ApplyTrust(basePrice, trust); return a == price; }
        bool valid;
        switch (kind)
        {
            case ShopAction.DiscardCard:
                valid = InventoryIdentity.FindCard(player, s) is { } card && UI.DeckDiscard.CanExecute(player, card);
                break;
            case ShopAction.Sell:
            {
                var keys = Split(s);
                var items = keys.Select(Item).ToList();
                valid = keys.Length > 0 && keys.Distinct(StringComparer.Ordinal).Count() == keys.Length
                    && items.All(i => i != null && i.Owner == player && i.IsStowed && player.Relics.Contains(i));
                if (valid) value = items.Sum(i => SellService.Value(i!, trust, shop == ShopType.Fence));
                valid &= a == value;
                break;
            }
            case ShopAction.TrustSpent:
                valid = ledger != null && a == (int)shop && PurchaseReceipts.Has(player, s, b);
                amount = b; // Verified against an observed gold delta, not the incoming integer.
                break;
            case ShopAction.Sharpen:
                valid = At(ShopType.Armorer) && Item(s) is { CanUpgrade: true, IsStowed: false } item
                    && (item == Loadout.InSlot(player, EquipSlot.MainHand) || item == ShopServices.ArmorToSharpen(player))
                    && Paid(ShopRules.SharpenPrice(item.Tier));
                break;
            case ShopAction.Reforge:
            {
                var parts = s.Split('|');
                valid = At(ShopType.Armorer) && parts.Length == 2 && Item(parts[0]) is { IsStowed: false } old
                    && old == Loadout.InSlot(player, EquipSlot.MainHand)
                    && ShopServices.ReforgeResult(player, old) is { } next && parts[1] == next.Id.Entry
                    && Paid(ShopRules.ReforgePrice);
                break;
            }
            case ShopAction.Brew:
                valid = At(ShopType.Herbalist) && b is 0 or 1 && pouch != null
                    && ShopServices.BestBrew(player, b == 1) is { } brew && AlchemyRules.CanAfford(pouch.Reagents, brew.Ingredients)
                    && Paid(ShopRules.BrewServicePrice);
                break;
            case ShopAction.BuyReagents:
                valid = At(ShopType.Herbalist, ShopType.Caravan) && pouch != null
                    && AlchemyRules.Fit(pouch.Total, ShopRules.ReagentKitSize) == ShopRules.ReagentKitSize
                    && s == string.Join(",", ShopServices.ReagentKit(player).Select(r => ((int)r).ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    && Paid(ShopRules.ReagentKitPrice(act));
                break;
            case ShopAction.SellReagents:
                value = ShopRules.ReagentSellValue(pouch?.Total ?? 0);
                valid = value > 0 && a == value;
                break;
            case ShopAction.Panacea:
                valid = At(ShopType.Herbalist) && ShopServices.Ailments(player).Any(c => c.IsRemovable) && Paid(ShopRules.PanaceaPrice);
                break;
            case ShopAction.PlaceOrder:
                if (!Enum.IsDefined(typeof(OrderCategory), b)) return false;
                price = ShopRules.ApplyTrust(ShopRules.OrderPrice(ShopRules.OrderItemPrice((OrderCategory)b, act)), trust);
                valid = ledger != null && a == (int)shop && Enum.IsDefined(typeof(OrderCategory), b)
                    && ShopRules.CanOrder(trust) && !ledger.HasOrder && ShopRules.OrderOptions(shop).Contains((OrderCategory)b)
                    && int.TryParse(s, out var quoted) && quoted == price;
                break;
            case ShopAction.EnchantItem:
                valid = At(ShopType.Arcanist) && hero != null && Enchanting.Enchantable(player).Count > 0
                    && b is >= 1 and <= 3 && s is "own" or "shop"
                    && (s == "own" ? hero.Stones(b) > 0 : b == ShopRules.SoulStonePower(act))
                    && Paid(ShopRules.EnchantServicePrice(b, s == "own"));
                break;
            case ShopAction.RewriteScroll:
                valid = At(ShopType.Arcanist) && InventoryIdentity.FindPotion(player, s) is RequiemScroll { RewriteCommitted: false }
                    && Paid(ShopRules.RewriteScrollPrice);
                break;
            case ShopAction.EnchantCard:
                valid = At(ShopType.Arcanist) && player.Deck.Cards.Count > 0 && Paid(ShopRules.CardEnchantPrice);
                break;
            case ShopAction.BuyStone:
                valid = At(ShopType.Arcanist, ShopType.Jeweler) && hero != null && b == ShopRules.SoulStonePower(act)
                    && Paid(ShopRules.SoulStonePrice(b));
                break;
            case ShopAction.Engrave:
                valid = At(ShopType.Jeweler) && Enchanting.Enchantable(player).Any(i => i.Slot is EquipSlot.Ring or EquipSlot.Amulet)
                    && Paid(ShopRules.EngravePrice);
                break;
            case ShopAction.Facet:
                valid = At(ShopType.Jeweler) && ShopServices.CanFacet(player) && Paid(ShopRules.FacetPrice);
                break;
            case ShopAction.Cleanse:
                valid = At(ShopType.Temple) && player.Deck.Cards.Any(c => c.IsRemovable)
                    && Paid(ShopRules.CleansePrice(Pantheon.Faith.Effective(player) == Piety.Righteous));
                break;
            case ShopAction.TempleHeal:
                amount = (int)CampRules.HealAmount(player.Creature.MaxHp, ShopRules.TempleHealPercent);
                valid = At(ShopType.Temple) && player.Creature.CurrentHp < player.Creature.MaxHp && b == amount && Paid(ShopRules.TempleHealPrice);
                break;
            case ShopAction.Atonement:
                valid = At(ShopType.Temple) && Pantheon.Faith.CanRedeem(player) && Paid(ShopRules.AtonementPrice);
                break;
            case ShopAction.ChangeBlessing:
                amount = (int)MerchantLedger.TempleGod(player.RunState);
                valid = At(ShopType.Temple) && RunSettings.EnablePantheon && b == amount
                    && Pantheon.CelestialBlessing.Of(player)?.God != (Celestial)amount && Paid(ShopRules.BlessingChangePrice);
                break;
            case ShopAction.RemoveCurse:
                amount = ShopRules.CurseRemovalMaxHp;
                valid = At(ShopType.Fence) && b == amount && ShopRules.CanPayCurseRemoval(player.Creature.MaxHp)
                    && player.Deck.Cards.Any(c => c.Type == CardType.Curse && c.IsRemovable);
                break;
            case ShopAction.ClearName:
                amount = ledger == null ? -1 : ShopRules.ClearNameTarget(ledger.Spent);
                valid = At(ShopType.Fence) && amount >= 0 && b == amount && Paid(ShopRules.ClearNamePrice);
                break;
            case ShopAction.Trade:
                valid = At(ShopType.Caravan) && ledger != null && !ledger.TradedHere(player.RunState)
                    && ShopServices.Tradeable(player).Any(i => ShopServices.TradeResult(player, i) != null);
                break;
            case ShopAction.BuyMule:
                valid = At(ShopType.Wanderer, ShopType.Caravan) && PackMule.CanAdopt(player) && Paid(CampRules.MulePrice);
                break;
            case ShopAction.ShoeMule:
                valid = At(ShopType.Armorer) && PackMule.Of(player) is { Shod: false } && Paid(CampRules.MuleShoePrice);
                break;
            case ShopAction.MuleChest:
                valid = PackMule.Of(player) != null;
                break;
            case ShopAction.TheftAttempt:
            {
                var enablers = player.Relics.OfType<ITheftEnabler>().ToList();
                valid = ledger != null && !ledger.TheftTriedHere(player.RunState, 1 + enablers.Sum(e => e.ExtraTheftAttempts))
                    && (RunSettings.AllowTheft || enablers.Any(e => e.EnablesTheft));
                break;
            }
            case ShopAction.TheftFailed:
                amount = Net.RequiemNet.IsMultiplayer ? ShopRules.TheftFailDamage(player.Creature.MaxHp) : 0;
                valid = ledger != null && a == (int)shop && b == amount && ledger.TheftFloor == player.RunState.TotalFloor + 1
                    && ledger.TheftTries > 0;
                break;
            default: valid = false; break;
        }
        if (!valid) return false;
        if (price < 0 || player.Gold < price) { error = "Insufficient gold"; return false; }
        quote = new(price, value, amount, target);
        error = "";
        return true;
    }

    internal static string[] Split(string s) => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
