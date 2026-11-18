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
using SlayTheRequiem.SlayTheRequiemCode.Rules;

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
        if (blockLost > 0) await CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), Owner, blockLost, null);
        if (hpDamage > 0)
        {
            using (DamageTyping.As(DamageType.Fire))
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner, hpDamage, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        }

        if (!Owner.IsAlive) return;
        if (next <= 0) await PowerCmd.Remove(this);
        else await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, next - Amount, null, null, silent: true);
    }
}

/// <summary>
/// Озноб: в начале хода носитель теряет N ◆, затем стаки /2. У врага ◆ = <see cref="EnemyStaminaPower"/>;
/// если она падает до нуля — Обледенение (см. EnemyResponses).
/// </summary>
public sealed class ChillPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner) || Amount <= 0) return;

        var (loss, next) = StatusMath.ChillTick(Amount);
        // Инеерождённые («Кровь стужи»): Озноб на них спадает целиком.
        if (Owner.HasPower<FrostbloodPower>() || Owner.Player?.Relics.OfType<Relics.FrostbornOrigin>().Any() == true) next = 0;

        var stamina = RequiemResource.Of<StaminaResource>(Owner.Player);
        if (stamina != null) stamina.Amount = Math.Max(0, stamina.Amount - loss);
        else if (Owner.GetPower<EnemyStaminaPower>() is { } enemyStamina) await enemyStamina.Drain(loss);

        if (!Owner.IsAlive) return;
        if (next <= 0) await PowerCmd.Remove(this);
        else await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, next - Amount, null, null, silent: true);
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

        // ✦ героя — звёзды игры; у врагов-магов — EnemyManaPower.
        var enemyMana = Owner.GetPower<EnemyManaPower>();
        var current = Owner.IsPlayer ? Mana.Amount(Owner) : enemyMana?.DisplayAmount ?? 0;
        var (manaLoss, shock, next) = StatusMath.StaticTick(Amount, current);
        if (manaLoss > 0)
        {
            if (Owner.IsPlayer) await Mana.Lose(Owner.Player, manaLoss);
            else if (enemyMana != null) await enemyMana.Drain(manaLoss);
        }
        if (shock > 0)
        {
            using (DamageTyping.As(DamageType.Shock))
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner, shock, ValueProp.Unpowered, null, null, null);
        }

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
        using var _ = DamageTyping.As(DamageType.True);
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner, loss, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        if (!Owner.IsAlive) return;
        if (next <= 0) await PowerCmd.Remove(this);
        else await PowerCmd.Decrement(this);
    }
}
