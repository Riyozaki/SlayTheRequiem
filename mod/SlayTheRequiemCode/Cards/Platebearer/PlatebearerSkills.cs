using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using PlatingPower = SlayTheRequiem.SlayTheRequiemCode.Powers.PlatingPower;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Resources;

namespace SlayTheRequiem.SlayTheRequiemCode.Cards.Platebearer;

/// <summary>Перевести дух: +◆.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class CatchBreath : RequiemCard
{
    public const string GainVar = "StaminaGain";

    public CatchBreath() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithVar(GainVar, 3, 2);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        RequiemResource.Of<StaminaResource>(Owner)?.ModifyAmount((int)Var(GainVar));
        return Task.CompletedTask;
    }
}

/// <summary>Поднять щит: Блок, больше при Латах.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class RaiseShield : RequiemCard
{
    public const string BonusVar = "PlatingBonus";

    public RaiseShield() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(7, 3);
        WithVar(BonusVar, 3, 1);
        WithTip(typeof(PlatingPower));
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Block(play);
        if (Owner.Creature.HasPower<PlatingPower>()) await Block(play, Var(BonusVar));
    }
}

/// <summary>Твёрдая стойка: Блок и ◆.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class SteadyStance : RequiemCard
{
    public SteadyStance() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(5, 3);
        WithVar(CatchBreath.GainVar, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Block(play);
        RequiemResource.Of<StaminaResource>(Owner)?.ModifyAmount((int)Var(CatchBreath.GainVar));
    }
}

/// <summary>Сбить пламя: снять Горение и Озноб с себя + Блок.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class SmotherFlames : RequiemCard
{
    public SmotherFlames() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(3, 3);
        WithTip(typeof(BurningPower));
        WithTip(typeof(ChillPower));
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PowerCmd.Remove<BurningPower>(Owner.Creature);
        await PowerCmd.Remove<ChillPower>(Owner.Creature);
        await Block(play);
    }
}

/// <summary>Боевой клич: Слабость и Шаткость всем врагам. Клич: перезарядка 4 (вне системы Реквиема — сжигается).</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class BattleCry : RequiemCard, IShoutCard
{
    public int ShoutCooldown => 4;

    public BattleCry() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithPower<WeakPower>(1, 1);
        WithPower<StaggerPower>(3);
    }

    protected override CardLocation GetResultLocationForCardPlay()
    {
        var location = base.GetResultLocationForCardPlay();
        return location.pileType == PileType.Discard && !Shouts.Active(Owner)
            ? new CardLocation(location.player, PileType.Exhaust, location.position)
            : location;
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await ApplyAll<WeakPower>(ctx, Var(nameof(WeakPower)));
        await ApplyAll<StaggerPower>(ctx, Var(nameof(StaggerPower)));
    }
}

/// <summary>Сменить хват: следующая атака в этом ходу не тратит ◆ и бьёт сильнее.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class ChangeGrip : RequiemCard
{
    public ChangeGrip() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<GripPower>(3, 2);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<GripPower>(ctx, Owner.Creature, Var(nameof(GripPower)));
}

/// <summary>
/// Дух клинка (старт «Клинка-ансея» Дюнных клинков, 04): переключатель — первый розыгрыш призывает дух
/// (<see cref="BladeSpiritPower"/>: атаки +урон магией ⌊✦/2⌋, но стоят +1 ✦), следующий — отпускает.
/// </summary>
[Pool(typeof(IroncladCardPool))]
public sealed class BladeSpirit : RequiemCard
{
    public BladeSpirit() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Owner.Creature.GetPower<BladeSpiritPower>() is { } spirit) await PowerCmd.Remove(spirit);
        else await PowerCmd.Apply<BladeSpiritPower>(ctx, Owner.Creature, 1, Owner.Creature, this);
    }
}
