using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Cards.Silent;

/// <summary>Common entry to the hunter: still a normal weapon hit without a bow, with Sunder as its armor answer.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class HuntingShot : SilentCard, IWeaponCard
{
    public HuntingShot() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(6, 2);
        WithStamina(1);
        WithPower<SunderPower>(1, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var ranged = HeroRecord.IsRanged(HeroRecord.Of(Owner)?.MainHand ?? Core.WeaponKind.None);
        await Hit(ctx, play.Target);
        if (ranged && play.Target.IsAlive) await Apply<SunderPower>(ctx, play.Target, Var(nameof(SunderPower)));
    }
}

/// <summary>Uncommon tempo/defense link. Neither protection nor damage requires a rare engine; bows add draw.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class CoveringShot : SilentCard, IWeaponCard
{
    public CoveringShot() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(5, 2);
        WithBlock(5, 2);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var ranged = HeroRecord.IsRanged(HeroRecord.Of(Owner)?.MainHand ?? Core.WeaponKind.None);
        await Hit(ctx, play.Target);
        await Block(play);
        if (ranged) await CardPileCmd.Draw(ctx, 1, Owner);
    }
}
