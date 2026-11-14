using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Resources;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

// Стихийные статусы (docs/design/03). Вся арифметика — в Core/StatusMath (покрыта тестами),
// здесь только привязка к хукам игры. Шаблон — ванильные PoisonPower/StrengthPower.

/// <summary>Горение: в конце хода носителя N урона огнём, Блок плавится вдвое быстрее; затем стаки /2.</summary>
public sealed class BurningPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Amount <= 0) return;

        var (damage, next) = StatusMath.BurningTick(Amount);
        var (blockLost, hpDamage) = StatusMath.BurningVsBlock(damage, Owner.Block);
        if (blockLost > 0) await CreatureCmd.LoseBlock(Owner, blockLost);
        if (hpDamage > 0)
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner, hpDamage, ValueProp.Unblockable | ValueProp.Unpowered, null, null);

        if (!Owner.IsAlive) return;
        if (next <= 0) await PowerCmd.Remove(this);
        else await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, next - Amount, null, null, silent: true);
    }
}

/// <summary>
/// Озноб: в начале хода носитель теряет N ◆, затем стаки /2.
/// Этап 0: у врагов ещё нет ◆, поэтому на враге Озноб просто режет урон атак (Устал −25 %, при 6+ — Изнурён −50 %).
/// </summary>
public sealed class ChillPower : RequiemPower
{
    public const int ExhaustedThreshold = 6;

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner) || Amount <= 0) return;

        var (loss, next) = StatusMath.ChillTick(Amount);
        var stamina = RequiemResource.Of<StaminaResource>(Owner.Player);
        if (stamina != null) stamina.Amount = Math.Max(0, stamina.Amount - loss);

        if (next <= 0) await PowerCmd.Remove(this);
        else await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, next - Amount, null, null, silent: true);
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (dealer != Owner || Owner.IsPlayer || !props.IsPoweredAttack()) return 1m;
        return StaminaRules.DamageMultiplier(Amount >= ExhaustedThreshold ? StaminaState.Exhausted : StaminaState.Tired);
    }
}

/// <summary>Статика: в начале хода выжигает N ✦; на недостающее — урон разрядом. Затем −1.</summary>
public sealed class StaticPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner) || Amount <= 0) return;

        var mana = RequiemResource.Of<ManaResource>(Owner.Player);
        var (manaLoss, shock, next) = StatusMath.StaticTick(Amount, mana?.Amount ?? 0);
        if (mana != null && manaLoss > 0) mana.Amount -= manaLoss;
        if (shock > 0)
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner, shock, ValueProp.Unpowered, null, null);

        if (!Owner.IsAlive) return;
        if (next <= 0) await PowerCmd.Remove(this);
        else await PowerCmd.Decrement(this);
    }
}

/// <summary>Кровотечение: каждый раз, когда носитель играет Атаку / бьёт атакой, он теряет N ♥; затем −1.</summary>
public sealed class BleedPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Type != CardType.Attack || cardPlay.Card.Owner?.Creature != Owner || !cardPlay.IsFirstInSeries) return;
        await Bleed();
    }

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        // Враги (и любые не-карточные атаки): одна потеря за атакующее действие, мульти-удар = одна атака.
        if (command.Attacker != Owner || Owner.IsPlayer) return;
        await Bleed();
    }

    private async Task Bleed()
    {
        var (loss, next) = StatusMath.BleedOnAttack(Amount);
        if (loss <= 0) return;
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner, loss, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
        if (!Owner.IsAlive) return;
        if (next <= 0) await PowerCmd.Remove(this);
        else await PowerCmd.Decrement(this);
    }
}
