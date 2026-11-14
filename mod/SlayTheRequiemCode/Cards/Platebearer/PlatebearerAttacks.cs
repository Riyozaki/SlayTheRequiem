using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Cards.Platebearer;

// Атаки Латоносца (docs/design/16-platebearer-cards.md). Латоносец играет на модели Ironclad,
// поэтому карты добавляются в пул Ironclad.

/// <summary>Удар щитом: урон + Шаткость.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class ShieldBash : RequiemCard
{
    public ShieldBash() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(5, 2);
        WithPower<StaggerPower>(3, 1);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target, "vfx/vfx_attack_blunt");
        await Apply<StaggerPower>(ctx, play.Target, Var(nameof(StaggerPower)));
    }
}

/// <summary>Сокрушающий удар: тяжёлый удар + Раскол.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class CrushingBlow : RequiemCard
{
    public CrushingBlow() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(14, 4);
        WithPower<SunderPower>(2, 1);
        WithStamina(3);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target, "vfx/vfx_attack_blunt");
        await Apply<SunderPower>(ctx, play.Target, Var(nameof(SunderPower)));
    }
}

/// <summary>Рассечь латы: дешёвый Раскол.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class ArmorSplitter : RequiemCard
{
    public ArmorSplitter() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(6, 2);
        WithPower<SunderPower>(2, 1);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target);
        await Apply<SunderPower>(ctx, play.Target, Var(nameof(SunderPower)));
    }
}

/// <summary>Ледяной ветер: холод по всем + Озноб (стартовая карта Хладовяза).</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class IceWind : RequiemCard
{
    public override DamageType DamageType => DamageType.Frost;

    public IceWind() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
        WithDamage(4, 2);
        WithPower<ChillPower>(2, 1);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await HitAll(ctx, "vfx/vfx_attack_slash");
        await ApplyAll<ChillPower>(ctx, Var(nameof(ChillPower)));
    }
}

/// <summary>Пробивающий выпад: Кровотечение.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class PiercingThrust : RequiemCard
{
    public PiercingThrust() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(7, 2);
        WithPower<BleedPower>(2, 1);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target, "vfx/vfx_flying_slash");
        await Apply<BleedPower>(ctx, play.Target, Var(nameof(BleedPower)));
    }
}

/// <summary>Удар плашмя: почти без урона, но много Шаткости.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class FlatStrike : RequiemCard
{
    public FlatStrike() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(3, 1);
        WithPower<StaggerPower>(4, 2);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target, "vfx/vfx_attack_blunt");
        await Apply<StaggerPower>(ctx, play.Target, Var(nameof(StaggerPower)));
    }
}

/// <summary>Размашистый удар: по всем + Шаткость всем.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class SweepingBlow : RequiemCard
{
    public SweepingBlow() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithDamage(8, 3);
        WithPower<StaggerPower>(2, 1);
        WithStamina(2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await HitAll(ctx);
        await ApplyAll<StaggerPower>(ctx, Var(nameof(StaggerPower)));
    }
}

/// <summary>Морозный удар: холод + Озноб.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class FrostStrike : RequiemCard
{
    public override DamageType DamageType => DamageType.Frost;

    public FrostStrike() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(8, 3);
        WithPower<ChillPower>(3, 1);
        WithStamina(2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target);
        await Apply<ChillPower>(ctx, play.Target, Var(nameof(ChillPower)));
    }
}

/// <summary>Вопреки всему: ◆ не тратит; если вы Устали или Изнурены — урон ×2.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class AgainstAllOdds : RequiemCard
{
    public AgainstAllOdds() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(7, 3);
        WithStamina(0);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var tired = RequiemCombatRules.IsOwnerWeary(Owner);
        await Hit(ctx, play.Target, "vfx/vfx_attack_blunt", DynamicVars.Damage.BaseValue * (tired ? 2 : 1));
    }
}

/// <summary>Молот гнева: огромный удар, Шаткость и Раскол.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class WrathHammer : RequiemCard
{
    public WrathHammer() : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(22, 6);
        WithPower<StaggerPower>(8, 2);
        WithPower<SunderPower>(3);
        WithStamina(4);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target, "vfx/vfx_attack_blunt");
        await Apply<StaggerPower>(ctx, play.Target, Var(nameof(StaggerPower)));
        await Apply<SunderPower>(ctx, play.Target, Var(nameof(SunderPower)));
    }
}
