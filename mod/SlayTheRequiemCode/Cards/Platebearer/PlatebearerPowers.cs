using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Cards.Platebearer;

/// <summary>Сомкнуть латы: +Латы.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class CloseRanks : RequiemCard
{
    public CloseRanks() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<PlatingPower>(2, 1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<PlatingPower>(ctx, Owner.Creature, Var(nameof(PlatingPower)));
}

/// <summary>Закалка: +1 ◆ к приросту каждый ход.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class Tempering : RequiemCard
{
    public Tempering() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<TemperingPower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<TemperingPower>(ctx, Owner.Creature, Var(nameof(TemperingPower)));
}

/// <summary>Кровь стужи: сопротивление холоду I, ваш Озноб сильнее на 1, Озноб на вас спадает целиком.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class Frostblood : RequiemCard
{
    public Frostblood() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<FrostbloodPower>(1);
        WithPower<FrostResistancePower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Apply<FrostbloodPower>(ctx, Owner.Creature, Var(nameof(FrostbloodPower)));
        await Apply<FrostResistancePower>(ctx, Owner.Creature, Var(nameof(FrostResistancePower)));
    }
}

/// <summary>Непоколебимость: +Латы, вас нельзя ошеломить.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class Unbreakable : RequiemCard
{
    public Unbreakable() : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<PlatingPower>(3, 1);
        WithPower<UnbreakablePower>(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Apply<PlatingPower>(ctx, Owner.Creature, Var(nameof(PlatingPower)));
        await Apply<UnbreakablePower>(ctx, Owner.Creature, 1);
    }
}
