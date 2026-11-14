using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
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
        if (enemy.HasPower<EnemyManaPower>())
        {
            await TryFocus(enemy);
            return;
        }
        var stamina = enemy.GetPower<EnemyStaminaPower>();
        if (stamina == null || stamina.DisplayAmount > 0 || !enemy.Monster.IntendsToAttack) return;

        await CreatureCmd.Stun(enemy, async _ =>
        {
            await CreatureCmd.GainBlock(enemy, EnemyRules.BreatherBlock, ValueProp.Unpowered, null);
            enemy.GetPower<EnemyStaminaPower>()?.Set(int.MaxValue);
        });
    }

    /// <summary>Враг-маг без ✦ на заклинание: «Сосредоточиться» — Оберег и полная ✦ вместо атаки.</summary>
    public static async Task TryFocus(Creature enemy)
    {
        if (enemy.Monster == null || !enemy.IsAlive || enemy.IsStunned || enemy.HasPower<UnshakeablePower>()) return;
        var mana = enemy.GetPower<EnemyManaPower>();
        if (mana == null || mana.DisplayAmount >= EnemyRules.MinSpellMana || !enemy.Monster.IntendsToAttack) return;

        await CreatureCmd.Stun(enemy, async _ =>
        {
            await PowerCmd.Apply<SpellShieldPower>(new ThrowingPlayerChoiceContext(), enemy, EnemyRules.FocusSpellShield, enemy, null);
            enemy.GetPower<EnemyManaPower>()?.Set(int.MaxValue);
        });
    }

    /// <summary>
    /// Паралич (docs/design/03, 08 §7): враг с ♥ не выше потолка ступени пропускает следующее намерение.
    /// Боссы и Непоколебимые не парализуются — вместо этого получают Шаткость 5. Возвращает true, если сработало.
    /// </summary>
    public static async Task<bool> TryParalyze(PlayerChoiceContext ctx, Creature enemy, SpellTier tier, Creature? source, CardModel? card)
    {
        if (enemy.Monster == null || !enemy.IsAlive) return false;
        var rank = EnemySetup.RankOf(enemy.CombatState);
        if (rank == EnemyRank.Boss || enemy.HasPower<UnshakeablePower>() || enemy.HasPower<ParalysisWardPower>())
        {
            await PowerCmd.Apply<StaggerPower>(ctx, enemy, BossParalysisStagger, source, card);
            return false;
        }
        var cap = SpellAffinity.ControlHpCap(tier);
        if (cap != null && enemy.CurrentHp > cap) return false;
        if (enemy.IsStunned) return true;
        await CreatureCmd.Stun(enemy);
        return true;
    }

    /// <summary>Шаткость вместо Паралича для боссов.</summary>
    public const int BossParalysisStagger = 5;

    public static async Task CheckStagger(Creature target, StaggerPower stagger)
    {
        if (!target.IsAlive) return;

        if (target.IsPlayer)
        {
            var poise = HeroStatRules.Poise(target.Player);
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
