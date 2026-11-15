using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Progression;

namespace SlayTheRequiem.SlayTheRequiemCode.Cards.Silent;

// M4 п. 7 (card spec §3.2, 17 §3): карты-опоры предысторий Ветроплясун (каждая 3-я карта хода) и Беглец (0-энергия).

/// <summary>Третий шаг: если в этом ходу уже сыграно 2+ карты, бьёт дважды.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class ThirdStep : SilentCard, IWeaponCard
{
    public const int CardsNeeded = 2;

    public ThirdStep() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(5, 2);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var twice = (HeroRecord.Of(Owner)?.CardsThisTurn ?? 0) >= CardsNeeded;
        await Hit(ctx, play.Target, "vfx/vfx_flying_slash");
        if (twice && play.Target.IsAlive) await Hit(ctx, play.Target, "vfx/vfx_flying_slash");
    }
}

/// <summary>Ритм ветра: каждая 3-я карта за ход тянет карту.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class WindRhythm : SilentCard
{
    public WindRhythm() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<WindRhythmPower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<WindRhythmPower>(ctx, Owner.Creature, Var(nameof(WindRhythmPower)));
}

/// <summary>Ускользнуть: 0⚡ — Блок и карта.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class SlipAway : SilentCard
{
    public SlipAway() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(3, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Block(play);
        await CardPileCmd.Draw(ctx, 1, Owner);
    }
}

/// <summary>Мимолётный удар: 0⚡; +урон за каждую 0-энергетическую карту, сыгранную ранее в этом ходу (до 3).</summary>
[Pool(typeof(SilentCardPool))]
public sealed class FleetingStrike : SilentCard
{
    public const string PerZeroVar = "PerZero";
    public const int MaxCounted = 3;

    public FleetingStrike() : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(3, 1);
        WithVar(PerZeroVar, 2, 1);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var zero = Math.Min(MaxCounted, HeroRecord.Of(Owner)?.ZeroCostThisTurn ?? 0);
        await Hit(ctx, play.Target, "vfx/vfx_flying_slash", DynamicVars.Damage.BaseValue + zero * Var(PerZeroVar));
    }
}
