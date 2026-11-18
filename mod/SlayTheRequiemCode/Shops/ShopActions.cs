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
using SlayTheRequiem.SlayTheRequiemCode.Rules;

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

/// <summary>All entry points go through the revisioned executor. Execute is deliberately not a public bypass.</summary>
public static class ShopActions
{
    public static Task<OperationResult> Perform(Player player, ShopAction kind, int a = 0, int b = 0, string s = "") =>
        ShopOperations.Perform(player, kind, a, b, s ?? "");

    internal static async Task<OperationResult> Execute(Player player, ShopRequest request, ShopQuote quote, OperationGate.Lease lease)
    {
        var (kind, a, b, s) = (request.Kind, request.A, request.B, request.S);
        var ledger = MerchantLedger.Of(player);
        var paid = 0;
        var paymentShop = ledger?.CurrentShop ?? ShopType.Wanderer;
        void Check()
        {
            if (InventoryGate.For(player).Blocked) throw new InvalidOperationException("Inventory was blocked while selecting the operation");
            if (!ShopActionPolicy.TryQuote(player, kind, a, b, s, out var current, out var error) || current != quote)
                throw new InvalidOperationException($"Stale operation after selection: {error}");
        }
        async Task Charge()
        {
            if (!ShopActionPolicy.Environment(player, kind) || player.Gold < quote.Price)
                throw new InvalidOperationException("Payment precondition changed");
            if (quote.Price == 0) return;
            var before = player.Gold;
            try { await PlayerCmd.LoseGold(quote.Price, player, GoldLossType.Spent); }
            finally { paid += OperationAccounting.Paid(before, player.Gold); }
            if (before - player.Gold != quote.Price) throw new InvalidOperationException("Payment amount did not match the quote");
        }
        OperationResult Done(OperationStatus status = OperationStatus.Committed, string detail = "")
        {
            return new(status, request.Id, detail, paid);
        }
        OperationResult Cancel() => new(OperationStatus.Cancelled, request.Id);
        EquipmentRelic? Item(string key) => InventoryIdentity.FindRelic(player, key) as EquipmentRelic;
        bool Selected(CardModel? card) => card != null && ShopActionPolicy.OwnsCard(player, card) && card.IsRemovable;
        Task CleanRemoved(IEnumerable<CardModel> cards)
        {
            // Match the native RemoveFromState contract, including a late CardRemoved exception. Keep Owner
            // available to NCard previews/tweens that outlive the command; detaching RunState here breaks them.
            foreach (var card in cards.Where(c => !player.Deck.Cards.Contains(c) && c.Pile == null))
                card.HasBeenRemovedFromState = true;
            return Task.CompletedTask;
        }
        async Task<OperationResult> PaidEffect(Func<Task<bool>> command, Func<bool> committed, Func<Task>? after = null)
        {
            Check(); // repeat price, owner, environment and availability AFTER every native selector
            var result = await TransferProtocol.MoveAsync(request.Id, command, committed,
                after == null ? Charge : () => TransferProtocol.Finish(Charge, after));
            return result with { Gold = paid };
        }

        try
        {
            switch (kind)
            {
                case ShopAction.Sell:
                {
                    // Validate the WHOLE request before removing anything. Pay only for observed removals, including
                    // a removal whose later hook threw. Never use the client-supplied payout.
                    var items = ShopActionPolicy.Split(s).Select(key => Item(key)!).ToList();
                    var values = items.Select(i => SellService.Value(i, ledger!.CurrentTrust, ledger.CurrentShop == ShopType.Fence)).ToList();
                    var earned = 0;
                    Exception? error = null;
                    for (var i = 0; i < items.Count; i++)
                    {
                        if (items[i].Owner != player || !items[i].IsStowed || !player.Relics.Contains(items[i]))
                        { error = new InvalidOperationException("Sale target changed during removal hooks"); break; }
                        try { await RelicCmd.Remove(items[i]); }
                        catch (Exception e) { error = e; }
                        if (!player.Relics.Contains(items[i])) earned += values[i];
                        if (error != null) break;
                    }
                    var before = player.Gold;
                    if (earned > 0) await PlayerCmd.GainGold(earned, player);
                    var actual = player.Gold - before;
                    if (earned == 0) return error == null ? OperationResult.Reject(request.Id, "No items removed")
                        : new(OperationStatus.Error, request.Id, error.ToString());
                    return new(OperationAccounting.SaleStatus(earned, actual, error != null),
                        request.Id, error?.ToString() ?? (actual == earned ? "" : $"Sale removed items for {earned}, observed payout={actual}"), -actual);
                }
                case ShopAction.TrustSpent:
                    if (!PurchaseReceipts.Consume(player, s, quote.Amount)) return OperationResult.Reject(request.Id, "Purchase receipt already claimed");
                    ledger!.AddSpent(ledger.CurrentShop, quote.Amount);
                    return Done();

                case ShopAction.Sharpen:
                {
                    var item = Item(s)!;
                    var before = item.Upgrades;
                    return await PaidEffect(
                        () => { item.Upgrades = checked(before + 1); item.Flash(); return Task.FromResult(true); },
                        () => item.Upgrades != before);
                }
                case ShopAction.Reforge:
                {
                    var parts = s.Split('|');
                    var old = Item(parts[0])!;
                    var canonical = ShopServices.ReforgeResult(player, old)!;
                    var fresh = (EquipmentRelic)canonical.ToMutable();
                    fresh.Upgrades = old.Upgrades;
                    fresh.Enchants = old.Enchants;
                    Check();
                    var transfer = await TransferProtocol.MoveAsync(request.Id,
                        async () => { await RelicCmd.Obtain(fresh, player); return true; },
                        () => player.Relics.Contains(fresh),
                        () => TransferProtocol.Finish(
                            () =>
                            {
                                if (player.Relics.Contains(old)) player.RemoveRelicInternal(old);
                                if (player.Relics.Contains(old)) throw new InvalidOperationException("Reforge committed but its source relic remains");
                                return Task.CompletedTask;
                            }, Charge));
                    return transfer with { Gold = paid };
                }
                case ShopAction.Brew:
                {
                    var pouch = HerbalistPouch.Of(player)!;
                    var brew = ShopServices.BestBrew(player, b == 1)!.Value;
                    var potion = RequiemPotion.Create(brew.Recipe.Brew, brew.Tier);
                    if (potion == null) return OperationResult.Reject(request.Id, "Brew model unavailable");
                    Check();
                    HerbalistPouch.MarkBrewCost(potion, brew.Ingredients);
                    // No snapshot overwrite/refund: that could erase materials granted by an intervening hook.
                    // The original ingredients and payment are consumed only after an observed potion commit, even
                    // if a later procure hook threw. Veto/full slots never cost either resource.
                    var brewed = await TransferProtocol.MoveAsync(request.Id,
                        async () => (await PotionCmd.TryToProcure(potion, player)).success,
                        () => player.PotionSlots.Contains(potion),
                        () => TransferProtocol.Finish(
                            () =>
                            {
                                pouch.CompleteBrew(potion); // same durable, exactly-once cost as the actual procure hook
                                return Task.CompletedTask;
                            }, Charge),
                        () => { HerbalistPouch.CancelBrewCost(potion); return Task.CompletedTask; });
                    return brewed with { Gold = paid };
                }
                case ShopAction.BuyReagents:
                {
                    var pouch = HerbalistPouch.Of(player)!;
                    var kit = ShopServices.ReagentKit(player);
                    var expected = (int[])pouch.Reagents.Clone();
                    foreach (var reagent in kit) expected[(int)reagent]++;
                    return await PaidEffect(() => Task.FromResult(pouch.TryAddKit(kit)),
                        () => expected.SequenceEqual(pouch.Reagents));
                }
                case ShopAction.SellReagents:
                {
                    var pouch = HerbalistPouch.Of(player)!;
                    var sold = Enumerable.Range(0, pouch.Reagents.Length)
                        .SelectMany(i => Enumerable.Repeat((Reagent)i, pouch.Reagents[i])).ToArray();
                    var before = player.Gold;
                    var moved = await TransferProtocol.Move(request.Id,
                        async () => { await PlayerCmd.GainGold(quote.Value, player); return true; },
                        () => player.Gold > before,
                        () =>
                        {
                            if (!pouch.TrySpend(sold)) throw new InvalidOperationException("Reagent payout committed but the sold quantities changed");
                        });
                    var actual = player.Gold - before;
                    if (moved.Changed && actual != quote.Value)
                        moved = moved with { Status = OperationStatus.PartialCommit, Detail = moved.Detail + $" Expected payout={quote.Value}, actual={actual}" };
                    return moved with { Gold = -actual };
                }
                case ShopAction.Panacea:
                {
                    var ailments = ShopServices.Ailments(player).Where(c => c.IsRemovable).ToList();
                    return await PaidEffect(async () =>
                    {
                        foreach (var card in ailments)
                        {
                            if (!Selected(card)) return false;
                            await CardPileCmd.RemoveFromDeck(card);
                            if (player.Deck.Cards.Contains(card)) return false;
                        }
                        return true;
                    }, () => ailments.Any(c => !player.Deck.Cards.Contains(c)), () => CleanRemoved(ailments));
                }
                case ShopAction.PlaceOrder:
                    return await PaidEffect(
                        () => { ledger!.PlaceOrder(ledger.CurrentShop, (OrderCategory)b); return Task.FromResult(true); },
                        () => ledger!.OrderShop == (int)ledger.CurrentShop + 1 && ledger.OrderCategoryIndex == b);
                case ShopAction.TheftAttempt:
                    Check(); ledger!.MarkTheftAttempt(player.RunState); Pantheon.Faith.Sin(player);
                    return Done();
                case ShopAction.TheftFailed:
                    Check(); ledger!.ResetTrust(ledger.CurrentShop);
                    await CreatureCmd.SetCurrentHp(player.Creature, Math.Max(1, player.Creature.CurrentHp - quote.Amount));
                    return Done();

                case ShopAction.EnchantItem:
                {
                    var hero = HeroRecord.Of(player)!;
                    var enchanted = await EnchantRestSiteOption.EnchantFlow(player, b, null, () => { Check(); return true; }, async () =>
                    {
                        await TransferProtocol.Finish(Charge, () =>
                        {
                            if (s == "own" && !hero.SpendStone(b)) throw new InvalidOperationException("Enchantment committed but its soul stone could not be consumed");
                            return Task.CompletedTask;
                        });
                        return true;
                    }, lease);
                    return enchanted with { Target = request.Id, Gold = paid };
                }
                case ShopAction.RewriteScroll:
                {
                    var scroll = (RequiemScroll)InventoryIdentity.FindPotion(player, s)!;
                    Check();
                    var rewrite = await RequiemScroll.Rewrite(player, scroll, Charge, lease);
                    return rewrite with { Target = request.Id, Gold = paid };
                }
                case ShopAction.EnchantCard:
                {
                    var enchanted = await CardEnchantRestSiteOption.EnchantFlow(player, () => { Check(); return true; },
                        async () => { await Charge(); return true; }, lease);
                    return enchanted with { Target = request.Id, Gold = paid };
                }
                case ShopAction.BuyStone:
                {
                    var hero = HeroRecord.Of(player)!;
                    var before = hero.Stones(quote.Amount);
                    return await PaidEffect(
                        () => { hero.AddStone(quote.Amount); hero.Flash(); return Task.FromResult(true); },
                        () => hero.Stones(quote.Amount) > before);
                }
                case ShopAction.Engrave:
                {
                    var engraved = await EnchantRestSiteOption.EnchantFlow(player, 1, i => i.Slot is EquipSlot.Ring or EquipSlot.Amulet,
                        () => { Check(); return true; }, async () => { await Charge(); return true; }, lease);
                    return engraved with { Target = request.Id, Gold = paid };
                }
                case ShopAction.Facet:
                {
                    var sources = Loadout.Equipped(player).Where(i => i.EnchantSlots.Count > 0).ToList();
                    if (await PickItem(player, sources) is not { } from) return Cancel();
                    var targets = Enchanting.Enchantable(player).Where(t => t != from && ShopServices.FacetResult(player, from, t) != null).ToList();
                    if (await PickItem(player, targets) is not { } to) return Cancel();
                    Check();
                    if (!Loadout.Equipped(player).Contains(from) || !Enchanting.Enchantable(player).Contains(to)
                        || ShopServices.FacetResult(player, from, to) is not { } moved) return OperationResult.Reject(request.Id, "Stale enchant targets");
                    var encoded = EnchantRules.Serialize(moved);
                    return await PaidEffect(() =>
                    {
                        // Both saved fields change before ANY display callback. A late target notification must
                        // not leave the source with a second copy of the transferred enchantment.
                        to.Enchants = encoded; from.Enchants = "";
                        return Task.FromResult(true);
                    }, () => to.Enchants == encoded && from.Enchants.Length == 0,
                    () => TransferProtocol.Finish(
                        () => { to.SetEnchants(moved); to.Flash(); return Task.CompletedTask; },
                        () => { from.SetEnchants([]); return Task.CompletedTask; },
                        async () => { if (HeroRecord.Of(player) is { } hero) { await hero.SyncMaxHp(); hero.Refresh(); } }));
                }
                case ShopAction.Cleanse:
                {
                    var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1) { Cancelable = true, RequireManualConfirmation = true };
                    var card = (await CardSelectCmd.FromDeckForRemoval(player, prefs)).FirstOrDefault();
                    if (card == null) return Cancel();
                    Check();
                    if (!Selected(card)) return OperationResult.Reject(request.Id, "Stale removal target");
                    return await PaidEffect(
                        async () => { await CardPileCmd.RemoveFromDeck(card); return true; },
                        () => !player.Deck.Cards.Contains(card), () => CleanRemoved([card]));
                }
                case ShopAction.TempleHeal:
                {
                    var before = player.Creature.CurrentHp;
                    return await PaidEffect(
                        async () => { await CreatureCmd.Heal(player.Creature, quote.Amount); return player.Creature.CurrentHp > before; },
                        () => player.Creature.CurrentHp > before);
                }
                case ShopAction.Atonement:
                {
                    var sigil = Pantheon.Faith.SigilOf(player)!;
                    var before = sigil.Recorded;
                    var act = sigil.RedeemedAct;
                    return await PaidEffect(() => Task.FromResult(Pantheon.Faith.Redeem(player)),
                        () => sigil.Recorded != before || sigil.RedeemedAct != act,
                        () => { Pantheon.Faith.RefreshAll(player); return Task.CompletedTask; });
                }
                case ShopAction.ChangeBlessing:
                {
                    var old = Pantheon.CelestialBlessing.Of(player);
                    var fresh = Pantheon.CelestialBlessing.Create(Pantheon.CelestialBlessing.Blessings[quote.Amount]);
                    return await PaidEffect(
                        async () => { await RelicCmd.Obtain(fresh, player); return true; },
                        () => player.Relics.Contains(fresh),
                        () => TransferProtocol.Finish(
                            async () =>
                            {
                                if (old != null && player.Relics.Contains(old)) await old.RemoveSelf();
                                if (old != null && player.Relics.Contains(old)) throw new InvalidOperationException("New blessing committed but the old blessing remains");
                            },
                            () => { Pantheon.Faith.RefreshAll(player); return Task.CompletedTask; }));
                }
                case ShopAction.RemoveCurse:
                {
                    var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1) { Cancelable = true, RequireManualConfirmation = true };
                    var card = (await CardSelectCmd.FromDeckGeneric(player, prefs, c => c.Type == CardType.Curse && c.IsRemovable)).FirstOrDefault();
                    if (card == null) return Cancel();
                    Check();
                    if (!Selected(card) || card.Type != CardType.Curse) return OperationResult.Reject(request.Id, "Stale curse target");
                    var before = player.Creature.MaxHp;
                    return await TransferProtocol.MoveAsync(request.Id,
                        async () => { await CardPileCmd.RemoveFromDeck(card); return true; },
                        () => !player.Deck.Cards.Contains(card),
                        () => TransferProtocol.Finish(() => CleanRemoved([card]), async () =>
                        {
                            await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), player.Creature, quote.Amount, false);
                            if (before - player.Creature.MaxHp != quote.Amount) throw new InvalidOperationException("Curse removed but actual max-HP price differed");
                        }));
                }
                case ShopAction.ClearName:
                {
                    var before = ledger!.Spent[quote.Amount];
                    return await PaidEffect(
                        () => { ledger.ClearName((ShopType)quote.Amount); return Task.FromResult(true); },
                        () => ledger.Spent[quote.Amount] > before);
                }
                case ShopAction.Trade:
                {
                    if (await PickItem(player, ShopServices.Tradeable(player)) is not { } given) return Cancel();
                    Check();
                    if (!given.IsStowed || !player.Relics.Contains(given) || ShopServices.TradeResult(player, given) is not { } canonical)
                        return OperationResult.Reject(request.Id, "Stale trade target");
                    var fresh = canonical.ToMutable();
                    return await TransferProtocol.MoveAsync(request.Id,
                        async () => { await RelicCmd.Obtain(fresh, player); return true; },
                        () => player.Relics.Contains(fresh),
                        () => TransferProtocol.Finish(
                            () =>
                            {
                                if (player.Relics.Contains(given)) player.RemoveRelicInternal(given);
                                if (player.Relics.Contains(given)) throw new InvalidOperationException("Trade committed but its source relic was not removed");
                                return Task.CompletedTask;
                            },
                            // A RelicRemoved notification can throw AFTER detach. Consume the once-per-visit
                            // entitlement even then; the new item exists and must not authorize another trade.
                            () => { ledger!.MarkTrade(player.RunState); return Task.CompletedTask; }));
                }
                case ShopAction.BuyMule:
                {
                    Check();
                    var adopted = await TransferProtocol.MoveAsync(request.Id,
                        async () => { await PackMule.Adopt(player); return true; },
                        () => PackMule.Of(player) != null, Charge);
                    return adopted with { Gold = paid };
                }
                case ShopAction.ShoeMule:
                {
                    var mule = PackMule.Of(player)!;
                    return await PaidEffect(() => Task.FromResult(mule.Shoe()), () => mule.Shod);
                }
                case ShopAction.MuleChest:
                    return await ChestRestSiteOption.ChestFlow(player, lease) ? Done() : Cancel();
                case ShopAction.DiscardCard:
                {
                    var card = InventoryIdentity.FindCard(player, s);
                    Check();
                    if (card == null || !UI.DeckDiscard.CanExecute(player, card)) return OperationResult.Reject(request.Id, "Cannot discard this instance");
                    return await TransferProtocol.MoveAsync(request.Id,
                        async () => { await CardPileCmd.RemoveFromDeck(card, showPreview: LocalContext.IsMe(player)); return true; },
                        () => !player.Deck.Cards.Contains(card), () => CleanRemoved([card]));
                }
                default: return OperationResult.Reject(request.Id, "Unknown operation");
            }
        }
        finally
        {
            // Trust follows the observed payment even if an effect/animation later throws. Never credit a UI quote.
            if (paid > 0) ledger?.AddSpent(paymentShop, paid);
        }
    }

    private static async Task<EquipmentRelic?> PickItem(Player player, IReadOnlyList<EquipmentRelic> items)
    {
        if (items.Count == 0) return null;
        if (items.Count == 1) return items[0];
        return await RelicSelectCmd.FromChooseARelicScreen(player, items.Cast<RelicModel>().ToList()) as EquipmentRelic;
    }
}
