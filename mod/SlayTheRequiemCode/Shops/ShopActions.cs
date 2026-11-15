using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Potions;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

/// <summary>Действия в лавке Реквиема, которые меняют состояние и поэтому синхронизируются в коопе.</summary>
public enum ShopAction
{
    /// <summary>s = Id предметов рюкзака через запятую, a = золото.</summary>
    Sell = 1,
    /// <summary>a = вид лавки, b = потрачено золота (покупка у ванильного прилавка — хук только у покупателя).</summary>
    TrustSpent = 2,
    /// <summary>s = Id предмета, a = цена.</summary>
    Sharpen = 3,
    /// <summary>s = "старый|новый" Id, a = цена.</summary>
    Reforge = 4,
    /// <summary>a = цена, b = 1 — яд, 0 — зелье.</summary>
    Brew = 5,
    /// <summary>a = цена, s = реагенты через запятую (индексы <see cref="Reagent"/>).</summary>
    BuyReagents = 6,
    /// <summary>a = выручка.</summary>
    SellReagents = 7,
    /// <summary>a = цена.</summary>
    Panacea = 8,
    /// <summary>a = вид лавки, b = категория заказа, s = цена.</summary>
    PlaceOrder = 9,
    /// <summary>a = вид лавки, b = потеря ♥.</summary>
    TheftFailed = 10,
    /// <summary>Попытка кражи на этом этаже (одна за визит), операндов нет.</summary>
    TheftAttempt = 11,
}

/// <summary>
/// Выполнение действий лавки: <see cref="Perform"/> — у инициатора (выполнить и разослать),
/// <see cref="Apply"/> — у всех (сам эффект; детерминирован и не спрашивает игрока).
/// </summary>
public static class ShopActions
{
    public static async Task Perform(Player player, ShopAction kind, int a = 0, int b = 0, string s = "")
    {
        if (!LocalContext.IsMe(player)) return;
        RequiemNet.SendTargeted(new ShopActionMessage
        {
            location = RequiemNet.CurrentLocation,
            kind = (int)kind,
            a = a,
            b = b,
            s = s ?? string.Empty,
        });
        await Apply(player, kind, a, b, s ?? string.Empty);
    }

    public static async Task Apply(Player player, ShopAction kind, int a, int b, string s)
    {
        var ledger = MerchantLedger.Of(player);
        try
        {
            switch (kind)
            {
                case ShopAction.Sell:
                {
                    foreach (var id in Split(s))
                    {
                        var item = Loadout.All(player).FirstOrDefault(r => r.IsStowed && r.Id.Entry == id);
                        if (item != null) await RelicCmd.Remove(item);
                    }
                    if (a > 0) await PlayerCmd.GainGold(a, player);
                    break;
                }
                case ShopAction.TrustSpent:
                    ledger?.AddSpent((ShopType)a, b);
                    break;

                case ShopAction.Sharpen:
                {
                    var item = Loadout.All(player).FirstOrDefault(r => r.Id.Entry == s && r.CanUpgrade);
                    if (item == null) break;
                    await Pay(player, ledger, a);
                    item.Upgrades += 1;
                    item.Flash();
                    break;
                }
                case ShopAction.Reforge:
                {
                    var parts = s.Split('|');
                    if (parts.Length != 2) break;
                    var old = Loadout.All(player).FirstOrDefault(r => r.Id.Entry == parts[0]);
                    var canonical = ModelDb.AllRelics.OfType<EquipmentRelic>().FirstOrDefault(r => r.Id.Entry == parts[1]);
                    if (old == null || canonical == null) break;
                    await Pay(player, ledger, a);
                    var wasStowed = old.IsStowed;
                    var upgrades = old.Upgrades;
                    var fresh = (EquipmentRelic)canonical.ToMutable();
                    await RelicCmd.Replace(old, fresh);
                    fresh.Upgrades = upgrades;
                    if (wasStowed) Loadout.Stow(fresh);
                    break;
                }
                case ShopAction.Brew:
                {
                    var pouch = HerbalistPouch.Of(player);
                    var option = ShopServices.BestBrew(player, poison: b == 1);
                    if (pouch == null || option == null) break;
                    var potion = RequiemPotion.Create(option.Value.Recipe.Brew, option.Value.Tier);
                    if (potion == null) break;
                    var result = await PotionCmd.TryToProcure(potion, player);
                    if (!result.success) break;
                    pouch.TrySpend(option.Value.Ingredients);
                    await Pay(player, ledger, a);
                    break;
                }
                case ShopAction.BuyReagents:
                {
                    var pouch = HerbalistPouch.Of(player);
                    if (pouch == null) break;
                    await Pay(player, ledger, a);
                    foreach (var id in Split(s))
                        if (int.TryParse(id, out var r) && Enum.IsDefined(typeof(Reagent), r)) pouch.Add((Reagent)r, 1);
                    break;
                }
                case ShopAction.SellReagents:
                {
                    var pouch = HerbalistPouch.Of(player);
                    if (pouch == null) break;
                    pouch.Reagents = new int[pouch.Reagents.Length];
                    if (a > 0) await PlayerCmd.GainGold(a, player);
                    break;
                }
                case ShopAction.Panacea:
                {
                    var ailments = ShopServices.Ailments(player).ToList();
                    if (ailments.Count == 0) break;
                    await Pay(player, ledger, a);
                    foreach (var card in ailments) await CardPileCmd.RemoveFromDeck(card);
                    break;
                }
                case ShopAction.PlaceOrder:
                {
                    if (ledger == null || !int.TryParse(s, out var price)) break;
                    await Pay(player, ledger, price);
                    ledger.PlaceOrder((ShopType)a, (OrderCategory)b);
                    break;
                }
                case ShopAction.TheftAttempt:
                    ledger?.MarkTheftAttempt(player.RunState);
                    break;

                case ShopAction.TheftFailed:
                {
                    ledger?.ResetTrust((ShopType)a);
                    var creature = player.Creature;
                    await CreatureCmd.SetCurrentHp(creature, Math.Max(1, creature.CurrentHp - Math.Max(0, b)));
                    break;
                }
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Лавка: действие {kind} не выполнено ({e.Message}).");
        }
    }

    /// <summary>Оплата услуги: золото «потрачено» (для истории забега) и засчитывается в доверие текущей лавки.</summary>
    private static async Task Pay(Player player, MerchantLedger? ledger, int price)
    {
        if (price <= 0) return;
        await PlayerCmd.LoseGold(price, player, GoldLossType.Spent);
        ledger?.AddSpent(ledger.CurrentShop, price);
    }

    private static IEnumerable<string> Split(string s) => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
