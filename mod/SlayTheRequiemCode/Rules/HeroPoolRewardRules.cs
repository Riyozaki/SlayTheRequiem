using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Награды за бой из пула героя (docs/design/19 §3–4). Две замены, по очереди, только для ванильных карт пула героя
/// (заклинания и карты Путей не трогаем — у них свои правила, 08 §8.4 и 06):
/// <list type="number">
/// <item><b>Тёмный договор</b> (любая ступень <see cref="PactStage"/>): с шансом <see cref="PoolRules.PactCardSharePercent"/> %
/// карта становится картой обречённости Некробиндера той же редкости (<c>gated Pact</c> в requiem_pools.csv).</item>
/// <item><b>Свои карты героя</b>: своих карт в пуле меньше, чем ванильных, — без помощи они почти не выпадают. Ванильная
/// карта становится своей картой той же редкости с шансом <see cref="PoolRules.OwnReplaceChance"/> — так доля своих
/// доходит до <see cref="PoolRules.OwnCardSharePercent"/> %.</item>
/// </list>
/// </summary>
public sealed class HeroPoolRewardRules() : CustomSingletonModel(CustomSingletonModel.HookType.Run)
{
    public override bool TryModifyCardRewardOptions(Player player, List<CardCreationResult> cardRewardOptions, CardCreationOptions creationOptions) =>
        RewardSwap.Guard(nameof(HeroPoolRewardRules), () => Modify(player, cardRewardOptions, creationOptions));

    private static bool Modify(Player player, List<CardCreationResult> cardRewardOptions, CardCreationOptions creationOptions)
    {
        if (!RequiemMode.IsInSystem(player) || creationOptions.Source != CardCreationSource.Encounter) return false;

        var pool = player.Character.CardPool;
        var unlocked = pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint).ToList();
        var own = unlocked.Where(VanillaPoolGuard.IsModCard).ToList();
        var vanilla = unlocked.Where(c => !VanillaPoolGuard.IsModCard(c)).Select(c => c.CanonicalInstance).ToHashSet();
        var pact = player.Relics.OfType<PactStage>().Any() ? PactCards() : new List<CardModel>();

        var rng = player.PlayerRng.Rewards;
        var changed = false;
        foreach (var option in cardRewardOptions)
        {
            var card = option.Card.CanonicalInstance;
            if (card is SpellCard || !vanilla.Contains(card)) continue;
            var taken = cardRewardOptions.Select(o => o.Card.CanonicalInstance).ToHashSet();

            if (pact.Count > 0 && rng.NextDouble() < PoolRules.PactCardSharePercent / 100.0
                && rng.NextItem(pact.Where(p => p.Rarity == card.Rarity && !taken.Contains(p))) is { } doom)
            {
                RewardSwap.Replace(option, doom, player);
                changed = true;
                continue;
            }

            var ownOfRarity = own.Where(o => o.Rarity == card.Rarity).ToList();
            var chance = PoolRules.OwnReplaceChance(ownOfRarity.Count, vanilla.Count(v => v.Rarity == card.Rarity));
            if (chance <= 0 || rng.NextDouble() >= chance) continue;
            if (rng.NextItem(ownOfRarity.Where(o => !taken.Contains(o))) is not { } mine) continue;
            RewardSwap.Replace(option, mine, player);
            changed = true;
        }
        return changed;
    }

    private static List<CardModel> PactCards()
    {
        var gated = RequiemData.Pools?.Gated("Pact");
        if (gated == null || gated.Count == 0) return new List<CardModel>();
        var keys = gated.Select(g => (g.Pool, g.Key)).ToHashSet();
        return ModelDb.AllCardPools
            .SelectMany(p => p.AllCards.Select(c => (Pool: PoolTable.PoolKey(p.GetType().Name), Card: c)))
            .Where(x => keys.Contains((x.Pool, x.Card.GetType().Name)))
            .Select(x => x.Card)
            .Distinct()
            .ToList();
    }
}
