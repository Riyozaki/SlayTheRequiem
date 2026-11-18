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
/// Упорядоченная HP-защита резервирует удар и фиксирует расход перед потерей ♥ (включая вложенные удары).
/// </summary>
public sealed class DodgePower : RequiemPower
{
    public const int StaminaCost = 2;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _usedThisTurn;
    private int _pendingStamina;

    public int Remaining => Math.Max(0, Amount - _usedThisTurn);

    private bool CanDodge(Creature target, ValueProp props, Creature? dealer) =>
        Config.RunSettings.AutoDodge && target == Owner && Remaining > 0 && dealer != null && dealer != Owner && props.IsPoweredAttack()
        && (RequiemResource.Of<StaminaResource>(Owner.Player)?.Amount ?? 0) - _pendingStamina >= StaminaCost;

    internal decimal MitigateHit(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (amount < 1m || !CanDodge(target, props, dealer)) return amount;
        if (!HitDefensePipeline.WillCommit) return 0m;
        _usedThisTurn++;
        _pendingStamina += StaminaCost;
        var owner = Owner;
        HitDefensePipeline.Defer(async () =>
        {
            _pendingStamina -= StaminaCost;
            RequiemResource.Of<StaminaResource>(owner.Player)?.ModifyAmount(-StaminaCost);
            Flash();
            UI.RequiemVfx.Dodge(owner);
            var ctx = new ThrowingPlayerChoiceContext();
            foreach (var listener in owner.Powers.OfType<IDodgeListener>().ToList()) await listener.AfterDodged(ctx, dealer);
            if (owner.Player != null)
                foreach (var listener in owner.Player.Relics.OfType<IDodgeListener>().ToList()) await listener.AfterDodged(ctx, dealer);
        }, () => { _pendingStamina -= StaminaCost; _usedThisTurn--; });
        return 0m;
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

    internal decimal MitigateHit(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0 || props.HasFlag(ValueProp.Unblockable)) return amount;
        var mana = Mana.Amount(Owner);
        var absorbed = SpellDefenseRules.ManaWardUsed(amount, Remaining, mana - _pendingMana);
        if (absorbed <= 0) return amount;
        if (!HitDefensePipeline.WillCommit) return amount - absorbed;
        _pendingMana += absorbed;
        _usedThisTurn += absorbed;
        var owner = Owner;
        HitDefensePipeline.Defer(async () =>
        {
            _pendingMana -= absorbed;
            Flash();
            await Mana.Lose(owner.Player, absorbed);
        }, () => { _pendingMana -= absorbed; _usedThisTurn -= absorbed; });
        return amount - absorbed;
    }

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner)) _usedThisTurn = 0;
        return Task.CompletedTask;
    }
}
