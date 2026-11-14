using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

/// <summary>
/// Уворот N (лёгкая броня, docs/design/02 §4.2): N раз за ход удар врага, который должен снять ♥, гасится целиком
/// ценой 2 ◆. Заряды восстанавливаются в начале хода героя. Без ◆ — не срабатывает.
/// Расчёт: ModifyHpLostBeforeOsty обнуляет потерю, списание и реакции (<see cref="IDodgeListener"/>) — после удара.
/// </summary>
public sealed class DodgePower : RequiemPower
{
    public const int StaminaCost = 2;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _usedThisTurn;
    private bool _pending;

    public int Remaining => Math.Max(0, Amount - _usedThisTurn);

    private bool CanDodge(Creature target, ValueProp props, Creature? dealer) =>
        Config.RequiemConfig.AutoDodge && target == Owner && Remaining > 0 && dealer != null && dealer != Owner && props.IsPoweredAttack()
        && (RequiemResource.Of<StaminaResource>(Owner.Player)?.Amount ?? 0) >= StaminaCost;

    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (amount <= 0 || _pending || !CanDodge(target, props, dealer)) return amount;
        _pending = true;
        return 0m;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!_pending || target != Owner) return;
        _pending = false;
        _usedThisTurn++;
        RequiemResource.Of<StaminaResource>(Owner.Player)?.ModifyAmount(-StaminaCost);
        Flash();
        foreach (var listener in Owner.Powers.OfType<IDodgeListener>().ToList())
            await listener.AfterDodged(choiceContext, dealer);
        if (Owner.Player != null)
            foreach (var listener in Owner.Player.Relics.OfType<IDodgeListener>().ToList())
                await listener.AfterDodged(choiceContext, dealer);
    }

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner)) _usedThisTurn = 0;
        return Task.CompletedTask;
    }
}

/// <summary>Реакция на успешный Уворот (Контрудар, Кольчуга контрабандиста).</summary>
public interface IDodgeListener
{
    Task AfterDodged(PlayerChoiceContext ctx, Creature? attacker);
}

/// <summary>
/// Покров N (одеяния): первые N урона за ход по ♥ оплачиваются ✦ вместо здоровья (1 ✦ = 1 урон).
/// </summary>
public sealed class WardPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _usedThisTurn;
    private int _pendingMana;

    public int Remaining => Math.Max(0, Amount - _usedThisTurn);

    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0 || props.HasFlag(ValueProp.Unblockable)) return amount;
        var mana = Mana.Amount(Owner);
        var absorbed = Math.Min((int)amount, Math.Min(Remaining, mana - _pendingMana));
        if (absorbed <= 0) return amount;
        _pendingMana += absorbed;
        _usedThisTurn += absorbed;
        return amount - absorbed;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || _pendingMana <= 0) return;
        var pay = _pendingMana;
        _pendingMana = 0;
        Flash();
        await Mana.Lose(Owner.Player, pay);
    }

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner)) _usedThisTurn = 0;
        return Task.CompletedTask;
    }
}
