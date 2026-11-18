using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Cards.Regent;

// Карты Регента в системе Реквиема (docs/design/18 §1, пулы — 19): маг на ✦. Его колода — заклинания (08) плюс ванильные
// звёздные карты; эти карты связывают одно с другим: удешевляют и удваивают заклинания, копят и тратят ✦, защищают мага,
// пока он колдует. Регент играет на ванильной модели Regent — карты в пуле Regent. Своих ✦-атак мало специально:
// урон Регента — заклинания и звёзды.

/// <summary>База карт Регента: «мана» — ✦ (звёзды ванильного Регента, <see cref="Mana"/>).</summary>
public abstract class RegentCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : RequiemCard(cost, type, rarity, target)
{
    public const string ManaVar = "Mana";
}

// ═════════════ Обычные ═════════════

/// <summary>Удар посохом: 6 урона (◆ 1); следующее заклинание в этот ход на 1 ✦ дешевле.</summary>
[Pool(typeof(RegentCardPool))]
public sealed class StaffStrike : RegentCard
{
    public StaffStrike() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(6, 3);
        WithStamina(1);
        WithPower<SpellFocusPower>(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target, "vfx/vfx_attack_blunt");
        await Apply<SpellFocusPower>(ctx, Owner.Creature, Var(nameof(SpellFocusPower)));
    }
}

/// <summary>Руна защиты: 5 Блока и Оберег 3 (поглощает урон заклинаниями и стихиями).</summary>
[Pool(typeof(RegentCardPool))]
public sealed class RuneOfWarding : RegentCard
{
    public RuneOfWarding() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(5, 2);
        WithPower<SpellShieldPower>(3, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Block(play);
        await Apply<SpellShieldPower>(ctx, Owner.Creature, Var(nameof(SpellShieldPower)));
    }
}

/// <summary>Сосредоточение: 0⚡ — +1 ✦ и Фокус 1 (следующее заклинание хода на 1 ✦ дешевле).</summary>
[Pool(typeof(RegentCardPool))]
public sealed class ArcaneFocus : RegentCard
{
    public ArcaneFocus() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithVar(ManaVar, 1, 1);
        WithPower<SpellFocusPower>(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Mana.Gain(Owner, (int)Var(ManaVar));
        await Apply<SpellFocusPower>(ctx, Owner.Creature, Var(nameof(SpellFocusPower)));
    }
}

/// <summary>Выжигание магии: 1⚡ 1✦ — 6 урона разрядом и Статика 3 (выжигает ✦ врага, 03).</summary>
[Pool(typeof(RegentCardPool))]
public sealed class ManaBurn : RegentCard
{
    public override DamageType DamageType => DamageType.Shock;
    public override int CanonicalStarCost => 1;

    public ManaBurn() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(6, 2);
        WithPower<StaticPower>(3, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target, "vfx/vfx_attack_lightning");
        if (play.Target.IsAlive) await Apply<StaticPower>(ctx, play.Target, Var(nameof(StaticPower)));
    }
}

/// <summary>Изучение гримуара: взять 2 карты; за каждое взятое заклинание +1 ✦.</summary>
[Pool(typeof(RegentCardPool))]
public sealed class GrimoireStudy : RegentCard
{
    public GrimoireStudy() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCards(2, 1);
        WithVar(ManaVar, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var drawn = await CardPileCmd.Draw(ctx, DynamicVars.Cards.BaseValue, Owner);
        var spells = drawn.Count(c => c is SpellCard);
        if (spells > 0) await Mana.Gain(Owner, spells * (int)Var(ManaVar));
    }
}

// ═════════════ Необычные ═════════════

/// <summary>Концентрация (сила): каждое заклинание даёт 3 Блока.</summary>
[Pool(typeof(RegentCardPool))]
public sealed class Concentration : RegentCard
{
    public Concentration() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<ConcentrationPower>(3, 1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<ConcentrationPower>(ctx, Owner.Creature, Var(nameof(ConcentrationPower)));
}

/// <summary>Родник магии (сила): прирост ✦ +1 до конца боя (Прозрение).</summary>
[Pool(typeof(RegentCardPool))]
public sealed class MagickaWell : RegentCard
{
    public MagickaWell() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<InsightPower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<InsightPower>(ctx, Owner.Creature, Var(nameof(InsightPower)));
}

/// <summary>Удар по душе: 1⚡ 1✦ — 8 урона магией; по цели с Хворью — ещё раз (мост к некромантии Договора).</summary>
[Pool(typeof(RegentCardPool))]
public sealed class SoulRend : RegentCard
{
    public override DamageType DamageType => DamageType.Magic;
    public override int CanonicalStarCost => 1;

    public SoulRend() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(8, 3);
        WithTip(typeof(BlightPower));
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var blighted = play.Target.HasPower<BlightPower>();
        await Hit(ctx, play.Target, "vfx/vfx_attack_slash");
        if (blighted && play.Target.IsAlive) await Hit(ctx, play.Target, "vfx/vfx_attack_slash");
    }
}

/// <summary>Прилив магии: 0⚡ — +3 ✦. Изгнание.</summary>
[Pool(typeof(RegentCardPool))]
public sealed class ManaSurge : RegentCard
{
    public ManaSurge() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar(ManaVar, 3, 1);
        WithKeyword(CardKeyword.Exhaust);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) => Mana.Gain(Owner, (int)Var(ManaVar));
}

// ═════════════ Редкие ═════════════

/// <summary>Двойное заклинание: следующее заклинание в этот ход срабатывает дважды.</summary>
[Pool(typeof(RegentCardPool))]
public sealed class DualCasting : RegentCard
{
    public DualCasting() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithPower<DualCastPower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<DualCastPower>(ctx, Owner.Creature, Var(nameof(DualCastPower)));
}

/// <summary>Мастерство заклинателя (сила): первое заклинание каждого хода на 1 ✦ дешевле.</summary>
[Pool(typeof(RegentCardPool))]
public sealed class ArcaneMastery : RegentCard
{
    public ArcaneMastery() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<ArcaneMasteryPower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<ArcaneMasteryPower>(ctx, Owner.Creature, Var(nameof(ArcaneMasteryPower)));
}

/// <summary>Магическая буря: 2⚡ X✦ — тратит все ✦; за каждый — 4 урона магией всем врагам.</summary>
[Pool(typeof(RegentCardPool))]
public sealed class ManaStorm : RegentCard
{
    public override DamageType DamageType => DamageType.Magic;
    public override bool HasStarCostX => true;

    public ManaStorm() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
        WithDamage(4, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var hits = ResolveStarXValue();
        if (hits <= 0) return;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, null).TargetingAllOpponents(CombatState)
            .WithHitCount(hits).WithHitFx("vfx/vfx_attack_lightning").Execute(ctx);
    }
}
