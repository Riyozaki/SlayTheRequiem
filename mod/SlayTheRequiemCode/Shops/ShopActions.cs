using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Potions;
using SlayTheRequiem.SlayTheRequiemCode.Progression;

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

    // ───── M5: Чародей, Ювелир, Храм, Скупщик, Караван (14 §2.3–2.8) ─────
    // Услуги с выбором (предмет, карта, чары) выполняются у всех игроков, а сам выбор идёт синхронными экранами игры
    // (RelicSelectCmd / CardSelectCmd), как ванильное удаление карты у торговца: золото списывается только после выбора.

    /// <summary>a = цена, b = сила камня, s = "own" — свой камень, иначе камень лавки.</summary>
    EnchantItem = 12,
    /// <summary>a = цена, s = Id свитка в слоте зелий.</summary>
    RewriteScroll = 13,
    /// <summary>a = цена.</summary>
    EnchantCard = 14,
    /// <summary>a = цена, b = сила камня.</summary>
    BuyStone = 15,
    /// <summary>a = цена (Гравировка: чары силы 1 на кольцо/амулет).</summary>
    Engrave = 16,
    /// <summary>a = цена (Огранка: перенос чар; предметы выбираются экраном у всех).</summary>
    Facet = 17,
    /// <summary>a = цена (Очищение: удалить карту).</summary>
    Cleanse = 18,
    /// <summary>a = цена, b = лечение.</summary>
    TempleHeal = 19,
    /// <summary>a = цена.</summary>
    Atonement = 20,
    /// <summary>a = цена, b = бог (<see cref="Celestial"/>).</summary>
    ChangeBlessing = 21,
    /// <summary>b = потеря макс. ♥ (снять проклятие у Скупщика).</summary>
    RemoveCurse = 22,
    /// <summary>a = цена, b = вид лавки.</summary>
    ClearName = 23,
    /// <summary>Обмен Каравана: отданный предмет выбирается экраном у всех, полученный — по зерну.</summary>
    Trade = 24,

    // ───── M5 п. 5: вьючный зверь (10 §6) ─────

    /// <summary>a = цена (Странник, Караван).</summary>
    BuyMule = 25,
    /// <summary>a = цена («Подковать» у Оружейника).</summary>
    ShoeMule = 26,
    /// <summary>Сундук у прилавка (со зверем): выбор карт и вещей — синхронные экраны у всех.</summary>
    MuleChest = 27,

    // ───── плейтест 2026-11: Груз (02 §3 «выбрасывать можно в любой момент вне боя») ─────

    /// <summary>a = индекс карты в колоде, s = id карты (сверка). Не только в лавке: любое место вне боя.</summary>
    DiscardCard = 28,
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
                    // Кража — грех, удачная или нет: −1 ступень праведности (docs/design/05 §2).
                    Pantheon.Faith.Sin(player);
                    break;

                case ShopAction.EnchantItem:
                {
                    var hero = Progression.HeroRecord.Of(player);
                    if (hero == null) break;
                    var own = s == "own";
                    if (own && hero.Stones(b) <= 0) break;
                    await Alchemy.EnchantRestSiteOption.EnchantFlow(player, b, null, async () =>
                    {
                        if (own && !hero.SpendStone(b)) return false;
                        await Pay(player, ledger, a);
                        return true;
                    });
                    break;
                }
                case ShopAction.RewriteScroll:
                {
                    var scroll = player.PotionSlots.OfType<RequiemScroll>().FirstOrDefault(p => p.Id.Entry == s);
                    if (scroll == null) break;
                    await Pay(player, ledger, a);
                    await RequiemScroll.Rewrite(player, scroll);
                    break;
                }
                case ShopAction.EnchantCard:
                    await Progression.CardEnchantRestSiteOption.EnchantFlow(player, async () =>
                    {
                        await Pay(player, ledger, a);
                        return true;
                    });
                    break;

                case ShopAction.BuyStone:
                {
                    var hero = Progression.HeroRecord.Of(player);
                    if (hero == null) break;
                    await Pay(player, ledger, a);
                    hero.AddStone(Math.Clamp(b, 1, 3));
                    hero.Flash();
                    break;
                }
                case ShopAction.Engrave:
                    await Alchemy.EnchantRestSiteOption.EnchantFlow(player, 1, i => i.Slot is EquipSlot.Ring or EquipSlot.Amulet, async () =>
                    {
                        await Pay(player, ledger, a);
                        return true;
                    });
                    break;

                case ShopAction.Facet:
                {
                    var sources = Loadout.Equipped(player).Where(i => i.EnchantSlots.Count > 0).ToList();
                    if (await PickItem(player, sources) is not { } from) break;
                    var targets = Enchanting.Enchantable(player).Where(t => t != from && ShopServices.FacetResult(player, from, t) != null).ToList();
                    if (await PickItem(player, targets) is not { } to || ShopServices.FacetResult(player, from, to) is not { } moved) break;
                    await Pay(player, ledger, a);
                    to.SetEnchants(moved);
                    from.SetEnchants([]);
                    to.Flash();
                    if (Progression.HeroRecord.Of(player) is { } h)
                    {
                        await h.SyncMaxHp();
                        h.Refresh();
                    }
                    break;
                }
                case ShopAction.Cleanse:
                {
                    var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1) { Cancelable = true, RequireManualConfirmation = true };
                    var card = (await CardSelectCmd.FromDeckForRemoval(player, prefs)).FirstOrDefault();
                    if (card == null) break;
                    await Pay(player, ledger, a);
                    await CardPileCmd.RemoveFromDeck(card);
                    break;
                }
                case ShopAction.TempleHeal:
                    await Pay(player, ledger, a);
                    await CreatureCmd.Heal(player.Creature, Math.Max(0, b));
                    break;

                case ShopAction.Atonement:
                    if (!Pantheon.Faith.CanRedeem(player)) break;
                    await Pay(player, ledger, a);
                    Pantheon.Faith.Redeem(player);
                    Pantheon.Faith.RefreshAll(player);
                    break;

                case ShopAction.ChangeBlessing:
                    await Pay(player, ledger, a);
                    await Pantheon.CelestialBlessing.Grant(player, (Celestial)b);
                    Pantheon.Faith.RefreshAll(player);
                    break;

                case ShopAction.RemoveCurse:
                {
                    if (!ShopRules.CanPayCurseRemoval(player.Creature.MaxHp)) break;
                    var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1) { Cancelable = true, RequireManualConfirmation = true };
                    var card = (await CardSelectCmd.FromDeckGeneric(player, prefs, c => c.Type == CardType.Curse)).FirstOrDefault();
                    if (card == null) break;
                    await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), player.Creature, Math.Max(0, b), false);
                    await CardPileCmd.RemoveFromDeck(card);
                    break;
                }
                case ShopAction.ClearName:
                    if (ledger == null) break;
                    await Pay(player, ledger, a);
                    ledger.ClearName((ShopType)b);
                    break;

                case ShopAction.Trade:
                {
                    if (ledger == null || ledger.TradedHere(player.RunState)) break;
                    if (await PickItem(player, ShopServices.Tradeable(player)) is not { } given) break;
                    if (ShopServices.TradeResult(player, given) is not { } canonical) break;
                    ledger.MarkTrade(player.RunState);
                    await RelicCmd.Remove(given);
                    await RelicCmd.Obtain(canonical.ToMutable(), player);
                    break;
                }

                case ShopAction.BuyMule:
                {
                    if (!Alchemy.PackMule.CanAdopt(player)) break;
                    await Pay(player, ledger, a);
                    await Alchemy.PackMule.Adopt(player);
                    break;
                }
                case ShopAction.ShoeMule:
                {
                    if (Alchemy.PackMule.Of(player) is not { Shod: false } mule) break;
                    await Pay(player, ledger, a);
                    mule.Shoe();
                    break;
                }
                case ShopAction.MuleChest:
                    if (Alchemy.PackMule.Of(player) != null) await ChestRestSiteOption.ChestFlow(player);
                    break;

                case ShopAction.DiscardCard:
                {
                    var deck = player.Deck.Cards;
                    var card = a >= 0 && a < deck.Count && deck[a].Id.Entry == s
                        ? deck[a]
                        : deck.FirstOrDefault(c => c.Id.Entry == s && DeckDiscard.CanDiscard(c));
                    if (card != null) await CardPileCmd.RemoveFromDeck(card, showPreview: LocalContext.IsMe(player));
                    break;
                }

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

    /// <summary>Выбор предмета синхронным экраном игры (один вариант — без экрана).</summary>
    private static async Task<EquipmentRelic?> PickItem(Player player, IReadOnlyList<EquipmentRelic> items)
    {
        if (items.Count == 0) return null;
        if (items.Count == 1) return items[0];
        return await RelicSelectCmd.FromChooseARelicScreen(player, items.Cast<RelicModel>().ToList()) as EquipmentRelic;
    }

    private static IEnumerable<string> Split(string s) => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
