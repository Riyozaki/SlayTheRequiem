using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Spells;

// Разрушение (docs/design/08 §2). Урон заклинаний несиловой, тип урона = стихия → его режут/усиливают сопротивления,
// а холод/разряд по ходу снимают ◆/✦ (RequiemCombatRules.AfterDamageReceived).

/// <summary>Искры: 0⚡ 1✦, 3 разрядом и Статика 1. Удерживается; каждый ход в руке +1 урона до конца боя.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Sparks : SpellCard
{
    public override DamageType DamageType => DamageType.Shock;

    public Sparks() : base(0, 1, SpellSchool.Destruction, SpellTier.Novice, CardType.Attack, TargetType.AnyEnemy)
    {
        WithSpellDamage(3, 1);
        WithPower<StaticPower>(1);
        WithKeyword(CardKeyword.Retain);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Bolt(ctx, play.Target, "vfx/vfx_attack_lightning");
        if (play.Target.IsAlive) await Apply<StaticPower>(ctx, play.Target, Eff(nameof(StaticPower)));
    }

    /// <summary>«Удерживаемая»: остаётся в руке в конце хода и копит заряд.</summary>
    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (Owner != null && participants.Contains(Owner.Creature) && Pile?.Type == PileType.Hand)
            DynamicVars.Damage.BaseValue += 1;
        return Task.CompletedTask;
    }
}

/// <summary>Огненная стрела: 1⚡ 1✦, 6 огнём и Горение 3.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Firebolt : SpellCard
{
    public override DamageType DamageType => DamageType.Fire;

    public Firebolt() : base(1, 1, SpellSchool.Destruction, SpellTier.Novice, CardType.Attack, TargetType.AnyEnemy)
    {
        WithSpellDamage(6, 3);
        WithPower<BurningPower>(3, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Bolt(ctx, play.Target, "vfx/vfx_attack_fire");
        if (play.Target.IsAlive) await Apply<BurningPower>(ctx, play.Target, Eff(nameof(BurningPower)));
    }
}

/// <summary>Огненная руна: 1⚡ 2✦, следующий ударивший вас получает 10 огнём и Горение 5.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class FireRune : SpellCard
{
    public FireRune() : base(1, 2, SpellSchool.Destruction, SpellTier.Apprentice, CardType.Skill, TargetType.Self)
    {
        WithPower<FireRunePower>(10, 4);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<FireRunePower>(ctx, Owner.Creature, Eff(nameof(FireRunePower)));
}

/// <summary>Плащ пламени: 1⚡ 2✦, Плащ огня 3 на 3 хода.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class FlameCloak : SpellCard
{
    public FlameCloak() : base(1, 2, SpellSchool.Destruction, SpellTier.Apprentice, CardType.Skill, TargetType.Self)
    {
        WithPower<FireCloakPower>(3, 2);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<FireCloakPower>(ctx, Owner.Creature, Eff(nameof(FireCloakPower)));
}

/// <summary>Огненный шар: 2⚡ 3✦, 8 огнём всем и Горение 4.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Fireball : SpellCard
{
    public override DamageType DamageType => DamageType.Fire;

    public Fireball() : base(2, 3, SpellSchool.Destruction, SpellTier.Adept, CardType.Attack, TargetType.AllEnemies)
    {
        WithSpellDamage(8, 3);
        WithPower<BurningPower>(4, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Nova(ctx, "vfx/vfx_attack_fire");
        await ApplyAll<BurningPower>(ctx, Eff(nameof(BurningPower)));
    }
}

/// <summary>Ледяное копьё: 1⚡ 3✦, 12 холодом; по Обледеневшей цели ×2.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class IceSpike : SpellCard
{
    public override DamageType DamageType => DamageType.Frost;

    public IceSpike() : base(1, 3, SpellSchool.Destruction, SpellTier.Adept, CardType.Attack, TargetType.AnyEnemy)
    {
        WithSpellDamage(12, 4);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var frozen = play.Target.IsStunned && play.Target.HasPower<FrozenImmunityPower>();
        var damage = DynamicVars.Damage.BaseValue * (frozen ? 2 : 1);
        return Bolt(ctx, play.Target, "vfx/vfx_attack_ice", damage);
    }
}

/// <summary>Шаровая молния: 2⚡ 3✦, 10 разрядом цели, затем перескок ещё на 2 врагов (−3 урона за каждый перескок).</summary>
[Pool(typeof(SpellCardPool))]
public sealed class BallLightning : SpellCard
{
    public const int JumpPenalty = 3;
    public override DamageType DamageType => DamageType.Shock;

    public BallLightning() : base(2, 3, SpellSchool.Destruction, SpellTier.Adept, CardType.Attack, TargetType.AnyEnemy)
    {
        WithSpellDamage(10, 3);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var damage = DynamicVars.Damage.BaseValue;
        await Bolt(ctx, play.Target, "vfx/vfx_attack_lightning", damage);
        foreach (var next in CombatState.HittableEnemies.Where(e => e != play.Target && e.IsAlive).Take(2).ToList())
        {
            damage -= JumpPenalty;
            if (damage <= 0) break;
            await Bolt(ctx, next, "vfx/vfx_attack_lightning", damage);
        }
    }
}

/// <summary>Стена пламени: 2⚡ 4✦, все враги получают Горение 6 в начале каждого из 2 следующих ваших ходов.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class WallOfFlames : SpellCard
{
    public WallOfFlames() : base(2, 4, SpellSchool.Destruction, SpellTier.Expert, CardType.Skill, TargetType.AllEnemies)
    {
        WithPower<FlameWallPower>(2, 1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<FlameWallPower>(ctx, Owner.Creature, Var(nameof(FlameWallPower)));
}

/// <summary>Буран: 2⚡ 4✦, 7 холодом всем и Озноб 3 всем.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Blizzard : SpellCard
{
    public override DamageType DamageType => DamageType.Frost;

    public Blizzard() : base(2, 4, SpellSchool.Destruction, SpellTier.Expert, CardType.Attack, TargetType.AllEnemies)
    {
        WithSpellDamage(7, 3);
        WithPower<ChillPower>(3, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Nova(ctx, "vfx/vfx_attack_ice");
        await ApplyAll<ChillPower>(ctx, Eff(nameof(ChillPower)));
    }
}

/// <summary>Цепь молний (подпись мастера грозы): 3⚡ 6✦, 12 разрядом трижды по случайным врагам; у первой цели выжигается вся ✦.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class ChainLightning : SpellCard
{
    public override DamageType DamageType => DamageType.Shock;

    public ChainLightning() : base(3, 6, SpellSchool.Destruction, SpellTier.Master, CardType.Attack, TargetType.RandomEnemy)
    {
        WithSpellDamage(12, 4);
        WithVar("Hits", 3);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var first = CombatState.HittableEnemies.FirstOrDefault(e => e.HasPower<EnemyManaPower>()) ?? CombatState.HittableEnemies.FirstOrDefault();
        if (first?.GetPower<EnemyManaPower>() is { } mana
            && !await Rules.SpellInterception.InterceptEffect(ctx, first, Owner.Creature, this)) await mana.Drain(mana.DisplayAmount);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, null).TargetingRandomOpponents(CombatState)
            .WithHitCount((int)Var("Hits")).Unpowered().WithHitFx("vfx/vfx_attack_lightning").Execute(ctx);
    }
}

/// <summary>Метеор (подпись мастера огня): 3⚡ 7✦, 30 огнём цели, 10 огнём остальным, Горение 10 всем.</summary>
[Pool(typeof(SpellCardPool))]
public sealed class Meteor : SpellCard
{
    public const string SplashVar = "Splash";
    public override DamageType DamageType => DamageType.Fire;

    public Meteor() : base(3, 7, SpellSchool.Destruction, SpellTier.Master, CardType.Attack, TargetType.AnyEnemy)
    {
        WithSpellDamage(30, 8);
        WithVar(SplashVar, 10, 3);
        WithPower<BurningPower>(10, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Bolt(ctx, play.Target, "vfx/vfx_attack_fire");
        foreach (var other in CombatState.HittableEnemies.Where(e => e != play.Target && e.IsAlive).ToList())
            await Bolt(ctx, other, "vfx/vfx_attack_fire", Var(SplashVar));
        await ApplyAll<BurningPower>(ctx, Eff(nameof(BurningPower)));
    }
}

/// <summary>Ледяная гробница (подпись мастера льда): 3⚡ 6✦, 20 холодом и Обледенение (пропуск хода; боссы — Шаткость).</summary>
[Pool(typeof(SpellCardPool))]
public sealed class IceTomb : SpellCard
{
    public override DamageType DamageType => DamageType.Frost;

    public IceTomb() : base(3, 6, SpellSchool.Destruction, SpellTier.Master, CardType.Attack, TargetType.AnyEnemy)
    {
        WithSpellDamage(20, 6);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Bolt(ctx, play.Target, "vfx/vfx_attack_ice");
        if (!play.Target.IsAlive) return;
        if (await EnemyResponses.TryParalyze(ctx, play.Target, SpellTier.Master, Owner.Creature, this))
            await PowerCmd.Apply<FrozenImmunityPower>(ctx, play.Target, Core.EnemyRules.FrozenImmunityTurns, null, null, silent: true);
    }
}
