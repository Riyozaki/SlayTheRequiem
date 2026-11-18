using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>A boss-local combat entitlement, not CardAcquisition/TravelPack (these must never enter a save's deck).
/// Full hands retain the exact unpiled card. Retry after a hand slot opens and after native start-of-turn draw;
/// a late exception is resolved by observing the actual pile. Never steal a foreign card or bypass native Add.</summary>
public sealed class EncounterCardGrants
{
    private readonly EncounterGrantQueue<ulong, CardModel, ICombatState> _queue = new();
    private readonly EncounterShoutCooldowns<ulong> _shoutCooldowns = new();
    internal IReadOnlyList<CardModel> ReservedCards => _queue.Entries.Select(e => e.Token).ToArray();
    private bool _flushing;
    private bool _flushRequested;

    public void Request<T>(SuperbossMonster boss, int window, Player? onlyPlayer = null) where T : EncounterCounterCard
    {
        if (!boss.Creature.IsAlive || boss.Creature.CombatState is not { } combat) return;
        foreach (var player in combat.Players.Where(p => p.Creature.IsAlive && (onlyPlayer == null || p == onlyPlayer)))
        {
            if (!_shoutCooldowns.Ready(player.NetId, typeof(T).Name, player.PlayerCombatState?.TurnNumber ?? 1)) continue;
            _queue.Request(player.NetId, typeof(T).Name, window, combat, () =>
            {
                // Keep an earlier full-hand entitlement before creating anything. A new phase may reuse an
                // unused retained card or a card still cooling on the shout shelf, never an exhausted copy.
                var pending = _queue.Entries.FirstOrDefault(e => e.Recipient == player.NetId && e.Kind == typeof(T).Name
                    && ReferenceEquals(e.Combat, combat) && e.Token.Owner == player && e.Token.Pile == null
                    && !e.Token.HasBeenRemovedFromState && combat.ContainsCard(e.Token));
                return pending?.Token ?? player.PlayerCombatState?.AllCards.OfType<T>().FirstOrDefault(c =>
                    !c.HasBeenRemovedFromState && c.Owner == player && combat.ContainsCard(c)
                    && c.Pile?.Type is PileType.Hand or PileType.Draw or PileType.Discard or PileType.Play)
                    ?? player.Creature.GetPower<ShoutShelfPower>()?.Entries.Select(e => e.Card).OfType<T>().FirstOrDefault();
            }, () => combat.CreateCard<T>(player));
        }
    }

    public void RecordPlay(SuperbossMonster boss, CardPlay play)
    {
        var card = play.Card;
        if (!boss.Creature.IsAlive || card is not EncounterCounterCard || card is not IShoutCard shout || card.Owner is not { } player
            || boss.Creature.CombatState?.Players.Contains(player) != true || player.PlayerCombatState == null) return;
        _shoutCooldowns.Played(player.NetId, card.GetType().Name, player.PlayerCombatState.TurnNumber, Shouts.CooldownOf(player, shout));
    }

    private static bool CanDeliver(Player player, ICombatState combat) => player.Creature.IsAlive
        && ReferenceEquals(player.Creature.CombatState, combat) && combat.CurrentSide == CombatSide.Player
        && player.PlayerCombatState?.Phase is PlayerTurnPhase.Start or PlayerTurnPhase.AutoPrePlay or PlayerTurnPhase.Play
        && CombatManager.Instance.IsInProgress && !CombatManager.Instance.IsEnding;

    private bool Cooling(Player player, CardModel card) => card is IShoutCard
        && !_shoutCooldowns.Ready(player.NetId, card.GetType().Name, player.PlayerCombatState?.TurnNumber ?? 1);

    public async Task Flush(SuperbossMonster boss)
    {
        if (!boss.Creature.IsAlive || boss.Creature.CombatState is not { } combat) return;
        _flushRequested = true;
        if (_flushing) return; // native Add re-enters this through pile hooks: never await our own insertion
        _flushing = true;
        try
        {
            // Include a co-op request arriving during an awaited insertion, but do not spin forever when a
            // third-party hook keeps redirecting a token out of Hand. Remaining debts retry on the next hook.
            for (var pass = 0; pass < 2 && _flushRequested && boss.Creature.IsAlive; pass++)
            {
                _flushRequested = false;
                foreach (var entry in _queue.Entries)
                {
                    var player = combat.Players.FirstOrDefault(p => p.NetId == entry.Recipient);
                    var card = entry.Token;
                    if (!boss.Creature.IsAlive || player == null || !ReferenceEquals(entry.Combat, combat)
                        || !boss.NeedsCounter(card.GetType()) || !CanDeliver(player, combat) || Cooling(player, card)) continue;
                    if (card.Owner != player || card.HasBeenRemovedFromState || !combat.ContainsCard(card)
                        || player.Creature.GetPower<ShoutShelfPower>()?.ContainsCaptured(card, combat) == true) continue;
                    // A hook may have transferred the card to Deck/Exhaust/another scope. Only the owned,
                    // registered combat instance in None/Draw/Discard is ours to deliver; never pull from Deck.
                    if (card.Pile != null && card.Pile.Type is not (PileType.Draw or PileType.Discard)) continue;
                    if (player.PlayerCombatState!.Hand.Cards.Count >= CardPile.MaxCardsInHand) continue;
                    try
                    {
                        if (!entry.GenerationAttempted)
                        {
                            // Do not repeat CardGenerated history/bonuses after a hook throws following insertion.
                            entry.GenerationAttempted = true;
                            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
                        }
                        else await CardPileCmd.Add(card, PileType.Hand);
                    }
                    catch (Exception e)
                    {
                        MainFile.Logger.Error($"[EncounterCounter] boss={boss.Id} card={card.Id} pile={card.Pile?.Type}: {e}");
                        // Same instance stays in this combat ledger. No synthetic success and no second create.
                    }
                }
                foreach (var player in combat.Players.Where(p => p.Creature.IsAlive))
                {
                    var waiting = _queue.Entries.Count(e => e.Recipient == player.NetId && ReferenceEquals(e.Combat, combat)
                        && e.Token.Owner == player && !e.Token.HasBeenRemovedFromState && combat.ContainsCard(e.Token)
                        && boss.NeedsCounter(e.Token.GetType()) && !Cooling(player, e.Token)
                        && (e.Token.Pile == null && player.Creature.GetPower<ShoutShelfPower>()?.ContainsCaptured(e.Token, combat) != true
                            || e.Token.Pile?.Type is PileType.Draw or PileType.Discard));
                    var shown = player.Creature.GetPower<EncounterCounterPendingPower>();
                    if (waiting == 0 && shown != null) await PowerCmd.Remove(shown);
                    else if (waiting > 0 && (shown?.Amount ?? 0) != waiting)
                        await PowerCmd.Apply<EncounterCounterPendingPower>(new ThrowingPlayerChoiceContext(), player.Creature,
                            waiting - (shown?.Amount ?? 0), boss.Creature, null);
                }
            }
        }
        finally { _flushing = false; }
    }

    public async Task Clear(SuperbossMonster boss)
    {
        var entries = _queue.Entries;
        _queue.Clear(); // clear references before awaited cleanup hooks, even if a later native removal throws
        _shoutCooldowns.Clear();
        _flushRequested = false;
        var combats = entries.Select(e => e.Combat).ToList();
        if (boss.Creature.CombatState is { } activeCombat) combats.Add(activeCombat);
        foreach (var entry in entries)
        {
            var card = entry.Token;
            var combat = entry.Combat; // the boss may already have been detached by a death/removal hook
            // Only our never-delivered, still-unpiled token. A transferred owner/combat and a captured shelf
            // card are never cleanup permission, even if an external hook left a stale old registration.
            if (card.Owner is { } player && player.NetId == entry.Recipient && ReferenceEquals(player.Creature.CombatState, combat)
                && card.Pile == null && !card.HasBeenRemovedFromState
                && player.Creature.GetPower<ShoutShelfPower>()?.ContainsCaptured(card, combat) != true && combat.ContainsCard(card))
                combat.RemoveCard(card);
        }
        foreach (var combat in combats.Distinct())
            foreach (var player in combat.Players)
                if (ReferenceEquals(player.Creature.CombatState, combat)
                    && player.Creature.GetPower<EncounterCounterPendingPower>() is { } pending && pending.Applier == boss.Creature)
                    try { await PowerCmd.Remove(pending); }
                    catch (Exception e) { MainFile.Logger.Error($"[EncounterCounter] pending cleanup boss={boss.Id} recipient={player.NetId}: {e}"); }
    }
}


/// <summary>Even an external obtain/copy effect cannot turn these encounter Tokens into permanent loot.
/// This is a narrow run hook; it neither bypasses other acquisition vetoes nor stores a durable grant debt.</summary>
public sealed class EncounterCardDeckPolicy() : CustomSingletonModel(CustomSingletonModel.HookType.Run)
{
    public override bool ShouldAddToDeck(CardModel card) => card is not EncounterCounterCard;
}
