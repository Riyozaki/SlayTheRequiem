using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Rewards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// A paid event offer uses the native synchronized choice/retry loop, but rechecks its real price AFTER selection
/// and pays only after the actual obtain-modified card commits. No lease is held while the screen is open, so
/// unloading is possible. Scope is the specific reward, not every Add/ShouldAddToDeck in the run.
/// A committed/partially paid offer is never charged a second time if a relic allows several card selections.
/// </summary>
internal sealed class RewardPurchase : IDisposable
{
    private static readonly ConditionalWeakTable<CardReward, RewardPurchase> Rewards = new();
    private static readonly ConditionalWeakTable<Player, RewardPurchase> Owners = new();
    private readonly CardReward _reward;
    private readonly int _price;
    private readonly Func<bool> _available;
    private readonly string _target;
    private bool _disposed;
    internal bool HasCommitted { get; private set; }
    internal OperationResult LastResult { get; private set; } = new(OperationStatus.Cancelled);

    internal RewardPurchase(CardReward reward, int price, string target, Func<bool> available)
    {
        if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
        if (Owners.TryGetValue(reward.Player, out _)) throw new InvalidOperationException("Another paid reward offer is already active");
        _reward = reward; _price = price; _target = target; _available = available;
        Rewards.Add(reward, this);
        Owners.Add(reward.Player, this);
    }

    internal static bool IsPaid(CardReward reward) => Rewards.TryGetValue(reward, out _);
    internal static string? PreviewReason(CardModel card) => card.Owner is { } owner
        && Owners.TryGetValue(owner, out var offer) && offer._reward.Cards.Contains(card)
            ? offer.Reason() ?? UI.RequiemUi.TipText("REWARD_PAID_READY.title", ("Cost", offer._price)) : null;

    private string? Reason()
    {
        if (InventoryGate.For(_reward.Player).Blocked) return UI.RequiemUi.TipText("OPERATION_BLOCKED.title");
        if (!_available() || _reward.Player.Creature.IsDead) return UI.RequiemUi.TipText("REWARD_OFFER_STALE.title");
        return !HasCommitted && _reward.Player.Gold < _price
            ? UI.RequiemUi.TipText("REWARD_PAYMENT_BLOCKED.title", ("Cost", _price), ("Gold", _reward.Player.Gold)) : null;
    }

    // Called only by the narrow native CardReward.OnSelect adapter. Unpaid rewards keep their original policy.
    internal static Task<CardPileAddResult> Add(CardModel card, PileType pile, CardPilePosition position,
        AbstractModel? clonedBy, bool skipVisuals, CardReward reward) => Rewards.TryGetValue(reward, out var offer)
            ? offer.Add(card, pile, position, clonedBy, skipVisuals)
            : CardAcquisition.AddSafely(card, pile, position, clonedBy, skipVisuals);

    private async Task<CardPileAddResult> Add(CardModel card, PileType pile, CardPilePosition position,
        AbstractModel? clonedBy, bool skipVisuals)
    {
        var owner = _reward.Player;
        using var lease = await InventoryGate.For(owner).Enter();
        if (lease == null || _disposed || card.Owner != owner || pile != PileType.Deck || Reason() is { })
        {
            LastResult = OperationResult.Reject(_target, "Paid offer/price changed after selection; no acquisition or payment");
            return new CardPileAddResult { success = false, cardAdded = card };
        }
        using var mutation = InventoryGate.Track(owner, lease, _target);
        CardAcquisition.BeginAdd(card);
        var result = await TransferProtocol.MoveAsync(_target,
            async () => (await CardPileCmd.Add(card, pile, position, clonedBy, skipVisuals)).success,
            () => CardAcquisition.CommittedCard(card, owner) != null,
            async () =>
            {
                if (HasCommitted) return;
                HasCommitted = true; // BEFORE awaiting payment: even a late exception cannot make this a new sale
                if (!_available() || owner.Gold < _price) throw new InvalidOperationException("Paid card committed but payment precondition changed in an obtain hook");
                var before = owner.Gold;
                await PlayerCmd.LoseGold(_price, owner);
                if (OperationAccounting.Paid(before, owner.Gold) != _price) throw new InvalidOperationException("Paid card committed but actual payment did not match its price");
            });
        LastResult = result;
        MainFile.Logger.Info($"[CardReward] phase=paid-offer target={_target} status={result.Status} {result.Detail}");
        if (result.Status == OperationStatus.PartialCommit) InventoryGate.Block(owner, $"Paid reward {_target}: {result.Detail}");
        mutation.Complete();
        var actual = CardAcquisition.CommittedCard(card, owner);
        return new CardPileAddResult { success = actual != null, cardAdded = actual ?? card };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Rewards.Remove(_reward);
        if (Owners.TryGetValue(_reward.Player, out var current) && ReferenceEquals(current, this)) Owners.Remove(_reward.Player);
    }
}

/// <summary>
/// Explicit paid-event policy: this sells a card, not an alternative combat-reward effect. Only a free, side-effect
/// free skip is offered; normal/deferred card rewards retain all native alternatives. Otherwise a replacement
/// skip/heal could be repeated without ever buying a card or completing the event.
/// </summary>
[CriticalPatch]
[HarmonyPatch(typeof(CardRewardAlternative), nameof(CardRewardAlternative.Generate))]
public static class PaidRewardAlternativesPatch
{
    [HarmonyPrefix]
    public static bool Prefix(CardReward cardReward, ref IReadOnlyList<CardRewardAlternative> __result)
    {
        if (!RewardPurchase.IsPaid(cardReward)) return true;
        __result = cardReward.CanSkip
            ? [new CardRewardAlternative("Skip", PostAlternateCardRewardAction.EndSelectionAndDoNotCompleteReward)] : [];
        return false;
    }
}
