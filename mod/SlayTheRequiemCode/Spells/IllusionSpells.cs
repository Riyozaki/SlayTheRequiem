using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Spells;

// Иллюзия (docs/design/08 §6): контроль врагов и скрытность. Контроль ограничен потолком ♥ цели по ступени
// (SpellAffinity.ControlHpCap): Ученик ≤ 40, Адепт ≤ 70, Эксперт ≤ 120, Мастер — без потолка. Боссы не поддаются.

internal static class IllusionRules
{
    /// <summary>Поддаётся ли цель контролю этой ступени.</summary>
    public static bool CanControl(Creature target, SpellTier tier)
    {
        if (target.Monster == null || !target.IsAlive) return false;
        if (EnemySetup.RankOf(target.CombatState) == EnemyRank.Boss || target.HasPower<UnshakeablePower>()) return false;
        var cap = SpellAffinity.ControlHpCap(tier);
        return cap == null || target.CurrentHp <= cap;
    }
}

/// <summary>Приглушённые шаги: 0⚡ 1✦, следующая атака — Подлая; Блок 3.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class MuffledSteps : SpellCard
{
    public MuffledSteps() : base(0, 1, SpellSchool.Illusion, SpellTier.Novice, CardType.Skill, TargetType.Self)
    {
        WithBlock(3, 3);
        WithTip(typeof(SneakNextPower));
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Block(play, Eff(DynamicVars.Block.BaseValue));
        await Apply<SneakNextPower>(ctx, Owner.Creature, 1);
    }
}

/// <summary>Смятение: 1⚡ 2✦, враг (♥ ≤ 40) атакует не героя, а случайного другого врага — 1 ход.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Confusion : SpellCard
{
    public Confusion() : base(1, 2, SpellSchool.Illusion, SpellTier.Apprentice, CardType.Skill, TargetType.AnyEnemy)
    {
        WithPower<ConfusionPower>(1, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        if (IllusionRules.CanControl(play.Target, Tier))
            await Apply<ConfusionPower>(ctx, play.Target, Eff(nameof(ConfusionPower)));
    }
}

/// <summary>Страх: 1⚡ 3✦, враг (♥ ≤ 70) наносит вдвое меньше урона 2 хода.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Terror : SpellCard
{
    public Terror() : base(1, 3, SpellSchool.Illusion, SpellTier.Adept, CardType.Skill, TargetType.AnyEnemy)
    {
        WithPower<FearPower>(2, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        if (IllusionRules.CanControl(play.Target, Tier))
            await Apply<FearPower>(ctx, play.Target, Eff(nameof(FearPower)));
    }
}

/// <summary>Невидимость: 1⚡ 4✦, Невидимость 2 (вражеские атаки вдвое слабее, ваши атаки — Подлые до первой).</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Invisibility : SpellCard
{
    public Invisibility() : base(1, 4, SpellSchool.Illusion, SpellTier.Expert, CardType.Skill, TargetType.Self)
    {
        WithPower<InvisibilityPower>(2, 1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<InvisibilityPower>(ctx, Owner.Creature, Eff(nameof(InvisibilityPower)));
}

/// <summary>Безумие: 3⚡ 6✦, два случайных врага (не боссы) на 2 хода обращают атаки друг на друга.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Madness : SpellCard
{
    public const int Targets = 2;

    public Madness() : base(3, 6, SpellSchool.Illusion, SpellTier.Master, CardType.Skill, TargetType.AllEnemies)
    {
        WithPower<ConfusionPower>(2, 1);
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var candidates = CombatState.HittableEnemies.Where(e => IllusionRules.CanControl(e, Tier)).ToList();
        var rng = Owner.RunState.Rng.CombatTargets;
        for (var i = 0; i < Targets && candidates.Count > 0; i++)
        {
            var target = rng.NextItem(candidates)!;
            candidates.Remove(target);
            await Apply<ConfusionPower>(ctx, target, Eff(nameof(ConfusionPower)));
        }
    }
}
