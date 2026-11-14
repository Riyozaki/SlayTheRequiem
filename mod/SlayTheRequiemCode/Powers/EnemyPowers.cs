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
/// ◆ врага (docs/design/11 §1). Amount = пул (чтобы сила не исчезала на нуле), текущее значение — <see cref="Current"/>,
/// оно и показывается на иконке. Атака тратит ◆, конец хода врага даёт +1. Мало ◆ — враг бьёт слабее
/// (Устал/Изнурён); ноль ◆ при атакующем намерении — «Отдышаться» вместо атаки.
/// </summary>
public sealed class EnemyStaminaPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public int Pool => Math.Max(1, Amount);
    public int Current { get; private set; } = -1;
    public override int DisplayAmount => Current < 0 ? Pool : Current;

    private void EnsureInit()
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

    /// <summary>Отнять ◆ (Озноб, холод, «Выпивающий» эффекты). Возвращает true, если ◆ упала до нуля этим вызовом.</summary>
    public async Task<bool> Drain(int amount)
    {
        EnsureInit();
        if (amount <= 0 || Current == 0) return false;
        Set(Current - amount);
        if (Current > 0) return false;
        await EnemyResponses.OnStaminaDepleted(Owner);
        return true;
    }

    public StaminaState State => DisplayAmount == 0 ? StaminaState.Exhausted
        : DisplayAmount * 2 < Pool ? StaminaState.Tired
        : StaminaState.Rested;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (dealer != Owner || !props.IsPoweredAttack()) return 1m;
        return StaminaRules.DamageMultiplier(State);
    }

    public override Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (command.Attacker != Owner) return Task.CompletedTask;
        var results = command.Results.ToList();
        var total = results.Sum(hit => hit.Sum(r => r.TotalDamage));
        EnsureInit();
        Set(Current - EnemyRules.AttackCost(results.Count, total));
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            EnsureInit();
            Set(Current + EnemyRules.StaminaRegen);
        }
        return Task.CompletedTask;
    }
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

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner ? EnemyRules.BrokenDamageTakenMultiplier : 1m;

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
