using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// A pending claim does not hold the inventory gate while its synchronized screen is open: unloading must remain
/// possible. Only the Add commits under a lease, with the exact durable entitlement/room rechecked AFTER selection.
/// The saved q: key reconciles an interrupted obtain clone; repeating that choice cannot mint another card.
/// Native/foreign card rewards are not globally locked (a foreign camp action may already own its lease).
/// </summary>
internal sealed class DeferredRewardScope : IDisposable
{
    private static readonly ConditionalWeakTable<CardReward, DeferredRewardScope> Offers = new();
    private readonly CardReward _reward;
    private readonly TravelPack _pack;
    private readonly SerializableCard _snapshot;
    private readonly string _payload;
    private readonly AbstractRoom? _room;
    private bool _disposed;

    internal DeferredRewardScope(CardReward reward, TravelPack pack, SerializableCard snapshot)
    {
        _reward = reward; _pack = pack; _snapshot = snapshot; _room = reward.Player.RunState.CurrentRoom;
        _payload = TravelPack.CardPayload(snapshot);
        if (reward.Player != pack.Owner || !pack.PendingMatches(snapshot, _payload))
            throw new InvalidOperationException("Deferred reward needs an owned, unambiguous durable claim");
        Offers.Add(reward, this);
    }

    internal static bool TryGet(CardReward reward, out DeferredRewardScope offer) => Offers.TryGetValue(reward, out offer!);

    internal async Task<CardPileAddResult> Add(CardModel card, PileType pile, CardPilePosition position,
        AbstractModel? clonedBy, bool skipVisuals)
    {
        var owner = _reward.Player;
        using var lease = await InventoryGate.For(owner).Enter();
        var key = CardAcquisition.ClaimKey(_snapshot);
        if (lease == null || _disposed || TravelPack.Of(owner) != _pack || card.Owner != owner || pile != PileType.Deck
            || !ReferenceEquals(_room, owner.RunState.CurrentRoom) || CombatManager.Instance.IsInProgress
            || owner.Creature.IsDead || !_pack.PendingMatches(_snapshot, _payload)
            || key.Length == 0 || CardAcquisition.ClaimKey(card) != key)
        {
            MainFile.Logger.Warn($"[CardReward] phase=deferred-stale claim={key} card={card.Id}; no acquisition");
            return new CardPileAddResult { cardAdded = card, success = false };
        }
        if (CardAcquisition.CommittedCard(card, owner) is { } committed)
            return new CardPileAddResult { cardAdded = committed, success = true };
        return await CardAcquisition.AddSafely(card, pile, position, clonedBy, skipVisuals);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Offers.Remove(_reward);
    }
}
