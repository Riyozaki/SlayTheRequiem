using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using VanillaPoison = MegaCrit.Sts2.Core.Models.Powers.PoisonPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Spells;

internal static class RestorationVars
{
    /// <summary>Имя PowerVar ванильного Яда (= имя типа).</summary>
    public const string Poison = "PoisonPower";
}

// Восстановление (docs/design/08 §3): лечение, Обереги, свет против нежити, яд и Хворь.

/// <summary>Исцеляющий свет: 1⚡ 2✦, +5 ♥ (не действует под Хворью). Третье применение за бой — Изгнание.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class HealingLight : SpellCard
{
    public const int UsesBeforeExhaust = 3;
    // Боевые копии карт создаются заново на каждый бой — счётчик обнуляется сам.
    private int _usesThisCombat;

    public HealingLight() : base(1, 2, SpellSchool.Restoration, SpellTier.Novice, CardType.Skill, TargetType.Self)
    {
        WithHeal(5, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await CreatureCmd.Heal(Owner.Creature, Eff(Var("Heal")));
        if (++_usesThisCombat >= UsesBeforeExhaust) ExhaustOnNextPlay = true;
    }
}

/// <summary>Малый оберег: 0⚡ 1✦, Оберег 6.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class LesserWard : SpellCard
{
    public LesserWard() : base(0, 1, SpellSchool.Restoration, SpellTier.Novice, CardType.Skill, TargetType.Self)
    {
        WithPower<SpellShieldPower>(6, 3);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<SpellShieldPower>(ctx, Owner.Creature, Eff(nameof(SpellShieldPower)));
}

/// <summary>Солнечный луч: 1⚡ 2✦, 7 светом (свет по нежити, духам и Бездне ×2).</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Sunbeam : SpellCard
{
    public override DamageType DamageType => DamageType.Light;

    public Sunbeam() : base(1, 2, SpellSchool.Restoration, SpellTier.Apprentice, CardType.Attack, TargetType.AnyEnemy)
    {
        WithSpellDamage(7, 3);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        return Bolt(ctx, play.Target, "vfx/vfx_attack_light");
    }
}

/// <summary>Ядовитый спрей: 1⚡ 1✦, Яд 4.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class PoisonSpray : SpellCard
{
    public PoisonSpray() : base(1, 1, SpellSchool.Restoration, SpellTier.Apprentice, CardType.Skill, TargetType.AnyEnemy)
    {
        WithPower<VanillaPoison>(4, 2);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        return Apply<VanillaPoison>(ctx, play.Target, Eff(RestorationVars.Poison));
    }
}

/// <summary>Изгнание нежити: 2⚡ 3✦. Нежить/духи с ♥ ≤ 15 уничтожаются, остальные нежить — 8 светом и пропуск хода. Прочие — 8 светом.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class TurnUndead : SpellCard
{
    public const string ThresholdVar = "Threshold";
    public override DamageType DamageType => DamageType.Light;

    public TurnUndead() : base(2, 3, SpellSchool.Restoration, SpellTier.Adept, CardType.Attack, TargetType.AllEnemies)
    {
        WithSpellDamage(8, 3);
        WithVar(ThresholdVar, 15, 5);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        foreach (var enemy in CombatState.HittableEnemies.Where(e => e.IsAlive).ToList())
        {
            var undead = EnemySetup.IsUndead(enemy);
            if (undead && EnemyRoleRules.CanExecute(EnemySetup.RoleOf(enemy)) && enemy.CurrentHp <= Var(ThresholdVar))
            {
                if (!await SpellInterception.InterceptEffect(ctx, enemy, Owner.Creature, this)) await CreatureCmd.Kill(enemy);
                continue;
            }
            await Bolt(ctx, enemy, "vfx/vfx_attack_light");
            if (undead && enemy.IsAlive) await EnemyResponses.TryParalyze(ctx, enemy, SpellTier.Adept, Owner.Creature, this);
        }
    }
}

/// <summary>Мор: 2⚡ 4✦, Яд 5 и Хворь 2 всем.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Pestilence : SpellCard
{
    public Pestilence() : base(2, 4, SpellSchool.Restoration, SpellTier.Expert, CardType.Skill, TargetType.AllEnemies)
    {
        WithPower<VanillaPoison>(5, 2);
        WithPower<BlightPower>(2, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await ApplyAll<VanillaPoison>(ctx, Eff(RestorationVars.Poison));
        await ApplyAll<BlightPower>(ctx, Eff(nameof(BlightPower)));
    }
}

/// <summary>Солнечная вспышка: 2⚡ 4✦, 12 светом всем (×2 по нежити — правилом урона светом).</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Sunburst : SpellCard
{
    public override DamageType DamageType => DamageType.Light;

    public Sunburst() : base(2, 4, SpellSchool.Restoration, SpellTier.Expert, CardType.Attack, TargetType.AllEnemies)
    {
        WithSpellDamage(12, 4);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) => Nova(ctx, "vfx/vfx_attack_light");
}

/// <summary>Великий оберег: 2⚡ 6✦, Оберег 30; до конца боя вас нельзя Парализовать.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class GreaterWard : SpellCard
{
    public GreaterWard() : base(2, 6, SpellSchool.Restoration, SpellTier.Master, CardType.Skill, TargetType.Self)
    {
        WithPower<SpellShieldPower>(30, 10);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Apply<SpellShieldPower>(ctx, Owner.Creature, Eff(nameof(SpellShieldPower)));
        if (!Owner.Creature.HasPower<ParalysisWardPower>()) await Apply<ParalysisWardPower>(ctx, Owner.Creature, 1);
    }
}
