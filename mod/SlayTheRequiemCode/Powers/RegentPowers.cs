using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

// Силы карт Регента (docs/design/18 §1, 19 §5): всё крутится вокруг заклинаний за ✦.

/// <summary>Фокус N: следующие N заклинаний в этот ход стоят на 1 ✦ меньше. Сгорает в конце хода.</summary>
public sealed class SpellFocusPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card is not SpellCard || card.Owner?.Creature != Owner || originalCost <= 0 || Amount <= 0) return false;
        modifiedCost = originalCost - 1;
        return true;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card is SpellCard && cardPlay.Card.Owner?.Creature == Owner && cardPlay.IsLastInSeries)
            await PowerCmd.Decrement(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

/// <summary>Двойное заклинание N: следующие N заклинаний в этот ход срабатывают дважды (как ванильный Burst для навыков).</summary>
public sealed class DualCastPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount) =>
        card is SpellCard && card.Owner?.Creature == Owner && Amount > 0 ? playCount + 1 : playCount;

    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        await PowerCmd.Decrement(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

/// <summary>Концентрация N: каждое ваше заклинание даёт N Блока.</summary>
public sealed class ConcentrationPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card is not SpellCard || cardPlay.Card.Owner?.Creature != Owner || !cardPlay.IsLastInSeries || Amount <= 0) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}

/// <summary>Мастерство заклинателя N: первые N заклинаний каждого хода стоят на 1 ✦ меньше (до конца боя).</summary>
public sealed class ArcaneMasteryPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _usedThisTurn;

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card is not SpellCard || card.Owner?.Creature != Owner || originalCost <= 0 || _usedThisTurn >= Amount) return false;
        modifiedCost = originalCost - 1;
        return true;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card is SpellCard && cardPlay.Card.Owner?.Creature == Owner && cardPlay.IsLastInSeries) _usedThisTurn++;
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner) _usedThisTurn = 0;
        return Task.CompletedTask;
    }
}
