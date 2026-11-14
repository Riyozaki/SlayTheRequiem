using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Spells;

/// <summary>Предрасположенность в бою: цена ✦ по владению и ослабление «С трудом» (docs/design/08 §8.2).</summary>
public sealed class SpellRules() : CustomSingletonModel(CustomSingletonModel.HookType.Combat)
{
    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card is not SpellCard spell || originalCost < 0 || !RequiemMode.IsInSystem(card.Owner)) return false;
        // Хуки цены идут цепочкой, порядок слушателей не гарантирован: добавляем только «надбавку предрасположенности»
        // от базовой цены, чтобы скидки других моделей (Высокая кровь, реликвии) не масштабировались.
        var cast = SpellAffinity.Evaluate(spell.BaseManaCost, spell.Tier, Mastery.Of(card.Owner, spell.School), spell.FromScroll);
        if (!cast.Castable) return false;
        var delta = cast.ManaCost - spell.BaseManaCost;
        if (delta == 0) return false;
        modifiedCost = Math.Max(0, originalCost + delta);
        return true;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        cardSource is SpellCard spell ? spell.EffectMultiplier : 1m;

    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay) =>
        cardSource is SpellCard spell ? spell.EffectMultiplier : 1m;
}

/// <summary>
/// Заклинания в наградах за бой (08 §8.4): каждая карта награды с шансом «доли заклинаний» героя заменяется
/// заклинанием школы с владением ≥ 1 и ступенью ≤ владение + 1. Латоносец с нулевым владением заклинаний в наградах не видит.
/// </summary>
public sealed class SpellRewardRules() : CustomSingletonModel(CustomSingletonModel.HookType.Run)
{
    public override bool TryModifyCardRewardOptions(Player player, List<CardCreationResult> cardRewardOptions, CardCreationOptions creationOptions)
    {
        if (!RequiemMode.IsInSystem(player) || creationOptions.Source != CardCreationSource.Encounter) return false;

        var eligible = ModelDb.AllCardPools.OfType<SpellCardPool>().SelectMany(p => p.AllCards).OfType<SpellCard>()
            .Where(s => SpellAffinity.AllowedInRewards(s.Tier, Mastery.Of(player, s.School)))
            .ToList();
        if (eligible.Count == 0) return false;

        var share = (double)SpellAffinity.RewardShare(player.Character.GetType().Name, Mastery.Total(player));
        var rng = player.PlayerRng.Rewards;
        var changed = false;
        foreach (var option in cardRewardOptions)
        {
            if (rng.NextDouble() >= share) continue;
            var taken = cardRewardOptions.Select(o => o.Card.CanonicalInstance).ToHashSet();
            var pick = rng.NextItem(eligible.Where(e => !taken.Contains(e)));
            if (pick == null) break;
            option.ModifyCard(player.RunState.CreateCard(pick, player));
            changed = true;
        }
        return changed;
    }
}
