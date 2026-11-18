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

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

/// <summary>
/// Ресурс врага (◆ или ✦). Amount = пул (чтобы сила не исчезала на нуле), текущее значение — <see cref="Current"/>,
/// оно и показывается на иконке.
/// </summary>
public abstract class EnemyResourcePower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>В ряду статусов не показываем: ◆/✦ врага видны точками под полоской ♥ (UI/NEnemyResourcePips, art spec 4.4).</summary>
    protected override bool IsVisibleInternal => false;

    public int Pool => Math.Max(1, Amount);
    public int Current { get; private set; } = -1;
    public override int DisplayAmount => Current < 0 ? Pool : Current;

    protected abstract int Regen { get; }

    protected void EnsureInit()
    {
        if (Current < 0) Current = Pool;
    }

    public void Set(int value)
    {
        EnsureInit();
        var clamped = Math.Clamp(value, 0, Pool);
        if (clamped == Current) return;
        Current = clamped;
        InvokeDisplayAmountChanged();
    }

    /// <summary>Отнять ресурс извне (Озноб/холод для ◆, Статика/разряд для ✦). true — если упал до нуля этим вызовом.</summary>
    public async Task<bool> Drain(int amount)
    {
        EnsureInit();
        if (amount <= 0 || Current == 0) return false;
        Set(Current - amount);
        if (Current > 0) return false;
        await OnDepleted();
        return true;
    }

    protected abstract Task OnDepleted();

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            EnsureInit();
            Set(Current + Regen);
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// ◆ врага (docs/design/11 §1). Атака тратит ◆, конец хода врага даёт +1. Мало ◆ — враг бьёт слабее
/// (Устал/Изнурён); ноль ◆ при атакующем намерении — «Отдышаться» вместо атаки. У врагов-магов атаки
/// оплачиваются ✦ (<see cref="EnemyManaPower"/>), а ◆ тратится только на физические удары — то есть почти никогда.
/// </summary>
public sealed class EnemyStaminaPower : EnemyResourcePower
{
    protected override int Regen => EnemyRules.StaminaRegen;

    protected override Task OnDepleted() => EnemyResponses.OnStaminaDepleted(Owner);

    public StaminaState State => DisplayAmount == 0 ? StaminaState.Exhausted
        : DisplayAmount * 2 < Pool ? StaminaState.Tired
        : StaminaState.Rested;

    private bool OwnerIsCaster => Owner.HasPower<EnemyManaPower>();

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner || !props.IsPoweredAttack() || OwnerIsCaster) return 1m;
        return StaminaRules.DamageMultiplier(Rules.EnemyActionBudget.StaminaBeforePayment(Owner, DisplayAmount, Pool));
    }
}

/// <summary>
/// ✦ врага-мага (docs/design/11 §1, 02 §2). Заклинание (атака стихией/магией) стоит 2–3 ✦, прирост +2 за ход.
/// Без ✦ — «Сосредоточиться» вместо атаки. Разряд и Статика выжигают ✦ — так срывается вражеская магия.
/// </summary>
public sealed class EnemyManaPower : EnemyResourcePower
{
    protected override int Regen => EnemyRules.CasterManaRegen;

    protected override Task OnDepleted() => EnemyResponses.TryFocus(Owner);

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        dealer == Owner && props.IsCardOrMonsterMove() ? Rules.EnemyActionBudget.ManaDamageMultiplier(Owner, DisplayAmount) : 1m;

}

/// <summary>Стойкость N: порог ошеломления. Растёт на 50 % после каждого ошеломления.</summary>
public sealed class PoisePower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>Непоколебим (боссы): не ошеломляется; шаткость копит Слом, N = порог Слома.</summary>
public sealed class UnshakeablePower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>
/// Шаткость: копится от карт с «Ошеломлением». Враг: при шаткости ≥ Стойкости — Ошеломлён (намерение отменено),
/// Непоколебимый — Сломлен. Герой: при шаткости ≥ его Стойкости — Оглушение (−1 энергия на следующий ход).
/// </summary>
public sealed class StaggerPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power != this || Amount <= 0) return;
        await EnemyResponses.CheckStagger(Owner, this);
    }
}

/// <summary>Сломлен: получает +25 % урона, снимается в конце следующего хода носителя.</summary>
public sealed class BrokenPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        target == Owner ? EnemyRules.BrokenDamageTakenMultiplier : 1m;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        UI.RequiemVfx.Broken(Owner);
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

/// <summary>Иммунитет к Обледенению N ходов (после того как враг был скован льдом).</summary>
public sealed class FrozenImmunityPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Decrement(this);
    }
}

/// <summary>Оглушение героя: −1 энергия в следующем ходу (затем снимается).</summary>
public sealed class ConcussedPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyMaxEnergy(MegaCrit.Sts2.Core.Entities.Players.Player player, decimal amount) =>
        player.Creature == Owner ? Math.Max(0, amount - Amount) : amount;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}
