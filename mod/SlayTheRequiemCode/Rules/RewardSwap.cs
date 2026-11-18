using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Debugging;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>Stage a whole replacement rule before publishing any of its results. Upgrade rolls are preserved.</summary>
public static class RewardSwap
{
    public static void Replace(CardCreationResult option, CardModel canonical, Player player)
    {
        var wasUpgraded = option.Card.IsUpgraded;
        var card = player.RunState.CreateCard(canonical, player);
        try
        {
            if (wasUpgraded && card.IsUpgradable) CardCmd.Upgrade(card);
            option.ModifyCard(card);
        }
        catch
        {
            player.RunState.RemoveCard(card);
            throw;
        }
    }

    /// <summary>
    /// On error retain the input of THIS rule, not a partially replaced list. RNG draws stay consumed (no rewind
    /// of a shared stream); the exact counters/fallback are logged. Earlier successful rules remain committed.
    /// </summary>
    public static bool Guard(string rule, Player player, List<CardCreationResult> options,
        Func<List<CardCreationResult>, bool> body)
    {
        // A saved one-time claim is not a fresh pool roll; keep its exact model/upgrade/debt on every retry.
        if (options.Any(o => CardAcquisition.IsDeferredClaim(o.Card))) return false;
        var staged = options.Select(o =>
        {
            var copy = new CardCreationResult(o.originalCard);
            if (o.HasBeenModified) copy.ModifyCard(o.Card);
            foreach (var relic in o.ModifyingRelics) copy.ModifyCard(o.Card, relic);
            return copy;
        }).ToList();
        var originals = options.Select(o => o.Card).ToHashSet();
        var counter = player.PlayerRng.Rewards.Counter;
        CardRewardTrace.Options($"replace:{rule}:before", player, options);
        try
        {
            var changed = body(staged);
            if (staged.Count != options.Count) throw new InvalidOperationException("Replacement rule changed option count");
            for (var i = 0; i < staged.Count; i++)
                if (!ReferenceEquals(options[i].Card, staged[i].Card)) options[i].ModifyCard(staged[i].Card);
            CardRewardTrace.Options($"replace:{rule}:after", player, options);
            return changed;
        }
        catch (Exception e)
        {
            foreach (var card in staged.Select(s => s.Card).Where(c => !originals.Contains(c)).Distinct())
                if (!player.Deck.Cards.Contains(card) && player.RunState.ContainsCard(card)) player.RunState.RemoveCard(card);
            MainFile.Logger.Error($"[CardReward] rule={rule} fallback=entire-rule-input rng={counter}->{player.PlayerRng.Rewards.Counter}: {e}");
            CardRewardTrace.Options($"replace:{rule}:fallback", player, options, force: true);
            return false;
        }
    }
}
