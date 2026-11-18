using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Resources;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

// Акт 2а — Ледяные Курганы (docs/design/11 §5): Хранитель Гробницы, Призрачный Великан, Клинок-Призрак, редкий Принц Первого Снега.

/// <summary>
/// Призрачный Великан Ледника: 150 ♥, Призрачность (физический урон −75 %, кроме серебра и заклинаний); топот — Оглушение
/// героя, если его Стойкость (◆) &lt; 6; каждые 4 хода «Ледяной выдох» — Озноб 5.
/// </summary>
public sealed class GlacierGiant : RequiemMonster
{
    public const int StompDamage = 12;
    public const int StompStaminaThreshold = 6;
    public const int FistDamage = 18;
    public const int BreathChill = 5;
    public const int BreathDamage = 8;

    protected override int Hp => 150;
    protected override MonsterModel LookOf => ModelDb.Monster<WaterfallGiant>();
    protected override string? CastAnim => "Erupt";

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<GhostlyPower>(Ctx, Creature, 1, Creature, null);
    }

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("STOMP", Stomp, Atk(StompDamage), new DebuffIntent()),
        new MoveState("FIST", _ => Hit(FistDamage, fx: "vfx/vfx_attack_blunt"), Atk(FistDamage)),
        new MoveState("STOMP_2", Stomp, Atk(StompDamage), new DebuffIntent()),
        new MoveState("FROST_BREATH", Breath, Atk(BreathDamage), new DebuffIntent()),
    ];

    private async Task Stomp(IReadOnlyList<Creature> targets)
    {
        await Hit(StompDamage, fx: "vfx/vfx_attack_blunt");
        foreach (var hero in Heroes.ToList())
            if (RequiemResource.Of<StaminaResource>(hero.Player) is { } stamina && stamina.Amount < StompStaminaThreshold)
                await PowerCmd.Apply<ConcussedPower>(Ctx, hero, 1, Creature, null);
    }

    private async Task Breath(IReadOnlyList<Creature> targets)
    {
        await Cast();
        await Hit(BreathDamage, type: DamageType.Frost, fx: null);
        await Debuff<ChillPower>(BreathChill);
    }
}

/// <summary>
/// Клинок-Призрак Хаэр: 105 ♥, два клинка — каждый удар вампиризм 6, Раскол 2, выжигание 3 ✦. Получив ≥ 15 урона за ход —
/// Адреналин (−50 % физического урона следующий ход): бой на ритм, в ход Адреналина бьют заклинаниями.
/// </summary>
public sealed class PhantomBlade : RequiemMonster
{
    public const int BladeDamage = 6;
    public const int BladeHits = 2;
    public const int BladeLifesteal = 6;
    public const int BladeSunder = 2;
    public const int BladeManaBurn = 3;
    public const int AdrenalineThreshold = 15;
    public const int LungeDamage = 15;

    protected override int Hp => 105;
    protected override MonsterModel LookOf => ModelDb.Monster<SpectralKnight>();
    protected override string AttackAnim => "AttackSword";

    private int _damageThisTurn;

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("TWIN_BLADES", TwinBlades, Multi(BladeDamage, BladeHits), new DebuffIntent()),
        new MoveState("LUNGE", _ => Hit(LungeDamage), Atk(LungeDamage)),
        new MoveState("TWIN_BLADES_2", TwinBlades, Multi(BladeDamage, BladeHits), new DebuffIntent()),
        new MoveState("GUARD", _ => Block(14), new DefendIntent()),
    ];

    private async Task TwinBlades(IReadOnlyList<Creature> targets)
    {
        for (var i = 0; i < BladeHits; i++)
        {
            await Hit(BladeDamage);
            await Heal(BladeLifesteal);
            await Debuff<SunderPower>(BladeSunder);
            await DrainMana(BladeManaBurn);
        }
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Creature || !Creature.IsAlive) return;
        var before = _damageThisTurn;
        _damageThisTurn += result.UnblockedDamage;
        if (before < AdrenalineThreshold && _damageThisTurn >= AdrenalineThreshold && !Creature.HasPower<AdrenalinePower>())
            await PowerCmd.Apply<AdrenalinePower>(choiceContext, Creature, 2, Creature, null);
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == MegaCrit.Sts2.Core.Combat.CombatSide.Player) _damageThisTurn = 0;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Принц Первого Снега (редкая): 140 ♥, удары холодом с Кровотечением; Регенерация растёт по мере падения ♥ (2/4/6);
/// на 50 % — 2 ледяных атронаха.
/// </summary>
public sealed class FirstSnowPrince : RequiemMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<IceAtronach>()];
    public const int SnowBladeDamage = 11;
    public const int SnowBladeBleed = 2;
    public const int BlizzardDamage = 5;
    public const int BlizzardHits = 3;

    protected override int Hp => 140;
    protected override MonsterModel LookOf => ModelDb.Monster<MysteriousKnight>();
    protected override string? CastAnim => null;

    private bool _atronachs;

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("SNOW_BLADE", SnowBlade, Atk(SnowBladeDamage), new DebuffIntent()),
        new MoveState("BLIZZARD", _ => Hit(BlizzardDamage, BlizzardHits, DamageType.Frost), Multi(BlizzardDamage, BlizzardHits)),
        new MoveState("ROYAL_GUARD", _ => Block(16), new DefendIntent()),
    ];

    private async Task SnowBlade(IReadOnlyList<Creature> targets)
    {
        await Hit(SnowBladeDamage, type: DamageType.Frost);
        await Debuff<BleedPower>(SnowBladeBleed);
    }

    /// <summary>Регенерация по доле ♥: ≥ 66 % — 2, ≥ 33 % — 4, ниже — 6.</summary>
    public static int RegenFor(int hp, int maxHp) => hp * 3 >= maxHp * 2 ? 2 : hp * 3 >= maxHp ? 4 : 6;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Enemy || !Creature.IsAlive) return;
        await Heal(RegenFor(Creature.CurrentHp, Creature.MaxHp));
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Creature || !Creature.IsAlive || _atronachs || !BelowPercent(50)) return;
        _atronachs = true;
        await Summon<IceAtronach>();
        await Summon<IceAtronach>();
    }
}

public sealed class IceAtronach : RequiemMinion
{
    public const int ShardDamage = 6;
    public const int ShardChill = 1;
    protected override int Hp => 24;
    protected override MonsterModel LookOf => ModelDb.Monster<LivingFog>();

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("ICE_SHARD", async _ =>
        {
            await Hit(ShardDamage, type: DamageType.Frost);
            await Debuff<ChillPower>(ShardChill);
        }, Atk(ShardDamage), new DebuffIntent()),
    ];
}

public sealed class GlacierGiantElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<GlacierGiant>()];
}

public sealed class PhantomBladeElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<PhantomBlade>()];
}

public sealed class FirstSnowPrinceElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<FirstSnowPrince>()];
}
