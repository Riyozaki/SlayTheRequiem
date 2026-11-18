using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

/// <summary>Хват: следующая атака владельца в этом ходу бесплатна по ◆ и наносит +N урона (см. StaminaCosts).</summary>
public sealed class GripPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        dealer == Owner && cardSource?.Type == CardType.Attack && props.IsPoweredAttack() ? Amount : 0m;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Type == CardType.Attack && cardPlay.Card.Owner?.Creature == Owner && cardPlay.IsLastInSeries)
            await PowerCmd.Remove(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

/// <summary>Закалка: +N ◆ к приросту в начале хода (читается StaminaResource).</summary>
public sealed class TemperingPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>Кровь стужи: накладываемый владельцем Озноб +N; Озноб на владельце спадает целиком (см. ChillPower).</summary>
public sealed class FrostbloodPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyPowerAmountGivenAdditive(PowerModel power, Creature giver, decimal amount, Creature? target, CardModel? cardSource) =>
        power is ChillPower && giver == Owner && amount > 0 ? Amount : 0m;
}

/// <summary>Непоколебимость героя: Шаткость на владельца не накладывается.</summary>
public sealed class UnbreakablePower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target != Owner || canonicalPower is not StaggerPower || amount <= 0) return false;
        modifiedAmount = 0;
        return true;
    }
}

// ═════════════ M4 п. 7: силы новых карт героев (16 §3, 17 §3) ═════════════

/// <summary>Строй легиона (Цитадельцы): в начале хода, если надет щит, +N Блока.</summary>
public sealed class LegionDrillPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature != Owner || Progression.HeroRecord.Of(player) is not { HasShield: true }) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}

/// <summary>Сердце горна (Горнокровные): каждая 3-я атака за ход тянет N карт.</summary>
public sealed class ForgeHeartPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _attacksThisTurn;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature == Owner) _attacksThisTurn = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Card.Owner;
        if (player?.Creature != Owner || !cardPlay.IsLastInSeries || cardPlay.Card.Type != CardType.Attack
            || cardPlay.Card is Spells.SpellCard) return;
        _attacksThisTurn++;
        if (_attacksThisTurn % 3 != 0) return;
        Flash();
        await CardPileCmd.Draw(choiceContext, Amount, player);
    }
}

/// <summary>Ритм ветра (Ветроплясун): каждая 3-я карта за ход тянет N карт.</summary>
public sealed class WindRhythmPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _cardsThisTurn;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature == Owner) _cardsThisTurn = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Card.Owner;
        if (player?.Creature != Owner || !cardPlay.IsLastInSeries) return;
        _cardsThisTurn++;
        if (_cardsThisTurn % 3 != 0) return;
        Flash();
        await CardPileCmd.Draw(choiceContext, Amount, player);
    }
}
