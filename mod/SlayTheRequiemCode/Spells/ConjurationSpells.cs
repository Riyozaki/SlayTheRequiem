using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Combat;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using VanillaMinion = MegaCrit.Sts2.Core.Models.Powers.MinionPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Spells;

// Колдовство (docs/design/08 §5): призванное оружие и призывы. Призыв — сила на герое (см. SummonPower),
// слот один: новый призыв прогоняет прежний.

/// <summary>Призрачный клинок (жетон): 0⚡, Оружейная атака 6, Изгнание. Бьёт и бесплотных.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class SpectralBlade : RequiemCard, IWeaponCard
{
    public SpectralBlade() : base(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
    {
        WithDamage(6, 3);
        WithKeyword(CardKeyword.Exhaust);
        WithStamina(1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        return Hit(ctx, play.Target);
    }
}

/// <summary>Призванный клинок: 0⚡ 1✦, в руку «Призрачный клинок».</summary>
[Pool(typeof(SpellCardPool))]
public sealed class BoundBlade : SpellCard
{
    public BoundBlade() : base(0, 1, SpellSchool.Conjuration, SpellTier.Novice, CardType.Skill, TargetType.Self)
    {
        WithUpgradingCardTip<SpectralBlade>();
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (CombatManager.Instance.IsOverOrEnding) return;
        var blade = CombatState.CreateCard<SpectralBlade>(Owner);
        if (IsUpgraded) CardCmd.Upgrade(blade);
        await CardPileCmd.AddGeneratedCardToCombat(blade, PileType.Hand, Owner);
    }
}

/// <summary>Базовый класс заклинания призыва: ♥ призыва — переменная «SummonHp».</summary>
public abstract class SummonSpell<TSummon>(int energy, int mana, SpellTier tier, int hp, int hpUpgrade)
    : SpellCard(energy, mana, SpellSchool.Conjuration, tier, CardType.Skill, TargetType.Self) where TSummon : SummonPower
{
    public const string HpVar = "SummonHp";

    protected void Init() => WithVar(HpVar, hp, hpUpgrade);

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        SummonPower.Summon<TSummon>(ctx, Owner.Creature, Eff(HpVar), this);
}

/// <summary>Призыв волка-духа: 1⚡ 2✦, Призыв 8 ♥, бьёт на 4.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class ConjureSpiritWolf : SummonSpell<SpiritWolfPower>
{
    public ConjureSpiritWolf() : base(1, 2, SpellTier.Novice, 8, 4) => Init();
}

/// <summary>Огненный атронах: 2⚡ 3✦, Призыв 14 ♥; бьёт 5 огнём + Горение 2; вам Плащ огня 2.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class ConjureFlameAtronach : SummonSpell<FlameAtronachPower>
{
    public ConjureFlameAtronach() : base(2, 3, SpellTier.Adept, 14, 4) => Init();

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await base.OnPlay(ctx, play);
        await Apply<FireCloakPower>(ctx, Owner.Creature, Eff(2));
    }
}

/// <summary>Ледяной атронах: 2⚡ 3✦, Призыв 22 ♥; принимает атаки первым, атакующему — Озноб 2.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class ConjureFrostAtronach : SummonSpell<FrostAtronachPower>
{
    public ConjureFrostAtronach() : base(2, 3, SpellTier.Adept, 22, 6) => Init();
}

/// <summary>Грозовой атронах: 2⚡ 4✦, Призыв 16 ♥; раз в ход 6 разрядом всем.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class ConjureStormAtronach : SummonSpell<StormAtronachPower>
{
    public ConjureStormAtronach() : base(2, 4, SpellTier.Expert, 16, 4) => Init();
}

/// <summary>Изгнание: 1⚡ 3✦, уничтожить вражеского прислужника (призыв, атронах); прочим — 10 магией.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Banish : SpellCard
{
    public override DamageType DamageType => DamageType.Magic;

    public Banish() : base(1, 3, SpellSchool.Conjuration, SpellTier.Expert, CardType.Attack, TargetType.AnyEnemy)
    {
        WithSpellDamage(10, 4);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        return play.Target.HasPower<VanillaMinion>()
            ? CreatureCmd.Kill(play.Target)
            : Bolt(ctx, play.Target, "vfx/vfx_attack_slash");
    }
}
