using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

/// <summary>Реакции врагов на исчерпание ◆ и шаткость: Обледенение, Отдышаться, Ошеломление, Слом.</summary>
public static class EnemyResponses
{
    /// <summary>
    /// ◆ врага упала до нуля. Если на нём Озноб — Обледенение (намерение отменено, затем иммунитет 2 хода).
    /// Иначе при атакующем намерении — «Отдышаться» (Блок 5 и полная ◆ вместо атаки). Непоколебимые просто Изнурены.
    /// </summary>
    public static async Task OnStaminaDepleted(Creature enemy)
    {
        if (enemy.Monster == null || !enemy.IsAlive || enemy.IsStunned) return;
        if (enemy.HasPower<UnshakeablePower>()) return;

        if (enemy.HasPower<ChillPower>() && !enemy.HasPower<FrozenImmunityPower>())
        {
            await CreatureCmd.Stun(enemy);
            await PowerCmd.Apply<FrozenImmunityPower>(new ThrowingPlayerChoiceContext(), enemy, EnemyRules.FrozenImmunityTurns, null, null, silent: true);
            return;
        }

        await TryBreather(enemy);
    }

    /// <summary>Заменить атакующее намерение без ◆ на «Отдышаться».</summary>
    public static async Task TryBreather(Creature enemy)
    {
        if (enemy.Monster == null || !enemy.IsAlive || enemy.IsStunned || enemy.HasPower<UnshakeablePower>()) return;
        var stamina = enemy.GetPower<EnemyStaminaPower>();
        if (stamina == null || stamina.DisplayAmount > 0 || !enemy.Monster.IntendsToAttack) return;

        await CreatureCmd.Stun(enemy, async _ =>
        {
            await CreatureCmd.GainBlock(enemy, EnemyRules.BreatherBlock, ValueProp.Unpowered, null);
            enemy.GetPower<EnemyStaminaPower>()?.Set(int.MaxValue);
        });
    }

    public static async Task CheckStagger(Creature target, StaggerPower stagger)
    {
        if (!target.IsAlive) return;

        if (target.IsPlayer)
        {
            var poise = RequiemMode.StatsFor(target.Player).Poise;
            if (stagger.Amount < poise) return;
            await PowerCmd.Remove(stagger);
            await PowerCmd.Apply<ConcussedPower>(new ThrowingPlayerChoiceContext(), target, 1, null, null);
            return;
        }

        if (target.GetPower<UnshakeablePower>() is { } unshakeable)
        {
            var result = StaggerRules.Accumulate(stagger.Amount, unshakeable.Amount);
            if (!result.Triggered) return;
            await PowerCmd.Remove(stagger);
            await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), unshakeable, result.NewThreshold - unshakeable.Amount, null, null, silent: true);
            if (!target.IsStunned) await CreatureCmd.Stun(target);
            await PowerCmd.Apply<BrokenPower>(new ThrowingPlayerChoiceContext(), target, 1, null, null);
            return;
        }

        if (target.GetPower<PoisePower>() is { } poisePower)
        {
            var result = StaggerRules.Accumulate(stagger.Amount, poisePower.Amount);
            if (!result.Triggered) return;
            await PowerCmd.Remove(stagger);
            await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), poisePower, result.NewThreshold - poisePower.Amount, null, null, silent: true);
            if (!target.IsStunned) await CreatureCmd.Stun(target);
        }
    }
}
