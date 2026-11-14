using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Spells;

// Изменение (docs/design/08 §4): временные Латы, телекинез, контроль (Паралич) и защита от магии (Поглощение).

/// <summary>Дубовая плоть: 1⚡ 1✦, Латы +1 на 3 хода.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Oakflesh : SpellCard
{
    public Oakflesh() : base(1, 1, SpellSchool.Alteration, SpellTier.Novice, CardType.Skill, TargetType.Self)
    {
        WithPower<TemporaryPlatingPower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<TemporaryPlatingPower>(ctx, Owner.Creature, Var(nameof(TemporaryPlatingPower)));
}

/// <summary>Импульс (телекинез): 1⚡ 1✦, 5 магией и Раскол 1.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Telekinesis : SpellCard
{
    public override DamageType DamageType => DamageType.Magic;

    public Telekinesis() : base(1, 1, SpellSchool.Alteration, SpellTier.Novice, CardType.Attack, TargetType.AnyEnemy)
    {
        WithSpellDamage(5, 3);
        WithPower<SunderPower>(1, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Bolt(ctx, play.Target, "vfx/vfx_attack_blunt");
        if (play.Target.IsAlive) await Apply<SunderPower>(ctx, play.Target, Eff(nameof(SunderPower)));
    }
}

/// <summary>Каменная плоть: 1⚡ 3✦, Латы +2 на 3 хода.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Stoneflesh : SpellCard
{
    public Stoneflesh() : base(1, 3, SpellSchool.Alteration, SpellTier.Adept, CardType.Skill, TargetType.Self)
    {
        WithPower<TemporaryPlatingPower>(2, 1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<TemporaryPlatingPower>(ctx, Owner.Creature, Var(nameof(TemporaryPlatingPower)));
}

/// <summary>Паралич: 2⚡ 5✦, цель (♥ ≤ 120) пропускает намерение; боссы и Непоколебимые — Шаткость 5.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Paralyze : SpellCard
{
    public Paralyze() : base(2, 5, SpellSchool.Alteration, SpellTier.Expert, CardType.Skill, TargetType.AnyEnemy)
    {
        WithCostUpgradeBy(-1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        return EnemyResponses.TryParalyze(ctx, play.Target, Tier, Owner.Creature, this);
    }
}

/// <summary>Поглощение: 1⚡ 3✦, Поглощение заклинаний 2 (до начала вашего следующего хода).</summary>
[Pool(typeof(SpellCardPool))]
public sealed class SpellAbsorption : SpellCard
{
    public SpellAbsorption() : base(1, 3, SpellSchool.Alteration, SpellTier.Expert, CardType.Skill, TargetType.Self)
    {
        WithPower<SpellAbsorptionPower>(2, 1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<SpellAbsorptionPower>(ctx, Owner.Creature, Var(nameof(SpellAbsorptionPower)));
}

/// <summary>Драконья кожа: 2⚡ 6✦, Латы +3 на 3 хода и Поглощение заклинаний 1.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Dragonhide : SpellCard
{
    public Dragonhide() : base(2, 6, SpellSchool.Alteration, SpellTier.Master, CardType.Skill, TargetType.Self)
    {
        WithPower<TemporaryPlatingPower>(3, 1);
        WithPower<SpellAbsorptionPower>(1, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Apply<TemporaryPlatingPower>(ctx, Owner.Creature, Var(nameof(TemporaryPlatingPower)));
        await Apply<SpellAbsorptionPower>(ctx, Owner.Creature, Var(nameof(SpellAbsorptionPower)));
    }
}

/// <summary>Массовый паралич: 3⚡ 8✦, все не-боссы пропускают ход (без потолка ♥). Изгнание.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class MassParalysis : SpellCard
{
    public MassParalysis() : base(3, 8, SpellSchool.Alteration, SpellTier.Master, CardType.Skill, TargetType.AllEnemies)
    {
        WithKeyword(CardKeyword.Exhaust);
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        foreach (var enemy in CombatState.HittableEnemies.Where(e => e.IsAlive).ToList())
            await EnemyResponses.TryParalyze(ctx, enemy, Tier, Owner.Creature, this);
    }
}
