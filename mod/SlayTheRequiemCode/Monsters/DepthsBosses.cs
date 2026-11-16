using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using V = MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

// ═════════════ Акт 2б — Заводные Глубины: механизмы и пар (11 §6.1) ═════════════

/// <summary>
/// Заводной колосс: 320 ♥, Латы 4 (таблица врагов). Поршневой удар, Набор давления (Блок + Сила), Паровой залп (огонь + Горение).
/// Фаза 2 (50 %): пластины сорваны — Латы сняты, в конце каждого хода врагов течь пара: 3 огнём героям и Горение 2 себе;
/// бьёт дважды, «Разгон» даёт Силу.
/// </summary>
public sealed class ClockworkColossus : ActBossMonster
{
    public const int BaseHp = 320;
    public const int SlamDamage = 18;
    public const int PressureBlock = 16;
    public const int PressureStrength = 2;
    public const int VolleyDamage = 7;
    public const int VolleyBurning = 3;
    public const int LeakDamage = 3;
    public const int LeakSelfBurning = 2;
    public const int FlurryDamage = 9;
    public const int FlurryHits = 2;
    public const int BurstDamage = 14;
    public const int BurstBurning = 4;
    public const int OverdriveStrength = 3;

    protected override int Hp => BaseHp;
    public override string ArtKey => "clockwork_colossus";
    protected override string? CastAnim => null;

    protected override IReadOnlyList<MoveState> PhaseOne() =>
    [
        new MoveState("PISTON_SLAM", _ => Hit(SlamDamage, fx: "vfx/vfx_attack_blunt"), Atk(SlamDamage)),
        new MoveState("BUILD_PRESSURE", async _ => { await Block(PressureBlock); await Buff<V.StrengthPower>(PressureStrength); },
            new BuffIntent(), new DefendIntent()),
        new MoveState("STEAM_VOLLEY", async _ => { await Hit(VolleyDamage, type: DamageType.Fire, fx: null); await Debuff<BurningPower>(VolleyBurning); },
            Atk(VolleyDamage), new DebuffIntent()),
    ];

    protected override IReadOnlyList<MoveState> PhaseTwo() =>
    [
        new MoveState("PISTON_FLURRY", _ => Hit(FlurryDamage, FlurryHits, fx: "vfx/vfx_attack_blunt"), Multi(FlurryDamage, FlurryHits)),
        new MoveState("BOILER_BURST", async _ => { await Hit(BurstDamage, type: DamageType.Fire, fx: null); await Debuff<BurningPower>(BurstBurning); },
            Atk(BurstDamage), new DebuffIntent()),
        new MoveState("OVERDRIVE", _ => Buff<V.StrengthPower>(OverdriveStrength), new BuffIntent()),
    ];

    protected override async Task OnPhaseTwo()
    {
        if (Creature.GetPower<PlatingPower>() is { } plating) await PowerCmd.Remove(plating);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Enemy || Phase != 2 || !Creature.IsAlive) return;
        await Blast(LeakDamage, DamageType.Fire);
        await PowerCmd.Apply<BurningPower>(choiceContext, Creature, LeakSelfBurning, Creature, null);
    }
}

/// <summary>
/// Архитектор автоматонов: 280 ♥. Ставит латунных часовых (до 2), укрепляет их (Латы +3), бьёт штангенциркулем с Расколом.
/// Фаза 2 (50 %): садится в экзокаркас — Латы 6, Сила +2; Экзомолот, Заклёпочный залп, Аварийный ремонт.
/// </summary>
public sealed class AutomatonArchitect : ActBossMonster
{
    public const int BaseHp = 280;
    public const int MaxSentries = 2;
    public const int ReinforcePlating = 3;
    public const int ReinforceBlock = 12;
    public const int CaliperDamage = 13;
    public const int CaliperSunder = 2;
    public const int ExoPlating = 6;
    public const int ExoStrength = 2;
    public const int HammerDamage = 22;
    public const int RivetDamage = 6;
    public const int RivetHits = 3;
    public const int RepairHeal = 18;
    public const int RepairBlock = 14;

    protected override int Hp => BaseHp;
    public override string ArtKey => "automaton_architect";

    protected override IReadOnlyList<MoveState> PhaseOne() =>
    [
        new MoveState("DEPLOY_SENTRY", async _ => { await Cast(); await DeploySentries(); }, new SummonIntent()),
        new MoveState("CALIPER_STRIKE", async _ => { await Hit(CaliperDamage); await Debuff<SunderPower>(CaliperSunder); },
            Atk(CaliperDamage), new DebuffIntent()),
        new MoveState("REINFORCE", async _ => { await Cast(); await BuffAllies<PlatingPower>(ReinforcePlating); await Block(ReinforceBlock); },
            new BuffIntent(), new DefendIntent()),
    ];

    protected override IReadOnlyList<MoveState> PhaseTwo() =>
    [
        new MoveState("EXO_HAMMER", _ => Hit(HammerDamage, fx: "vfx/vfx_attack_blunt"), Atk(HammerDamage)),
        new MoveState("RIVET_BURST", _ => Hit(RivetDamage, RivetHits, fx: null), Multi(RivetDamage, RivetHits)),
        new MoveState("EMERGENCY_REPAIR", async _ => { await Heal(RepairHeal); await Block(RepairBlock); await DeploySentries(); },
            new HealIntent(), new SummonIntent()),
    ];

    protected override async Task OnPhaseTwo()
    {
        await Buff<PlatingPower>(ExoPlating);
        await Buff<V.StrengthPower>(ExoStrength);
    }

    private async Task DeploySentries()
    {
        var missing = MaxSentries - Allies.Count(a => a.Monster is BrassSentry);
        for (var i = 0; i < missing; i++) await Summon<BrassSentry>();
    }
}

/// <summary>Латунный часовой: 24 ♥, Латы 2 (таблица врагов), болт 7 разрядом.</summary>
public sealed class BrassSentry : OwnArtMinion
{
    public const int BoltDamage = 7;
    protected override int Hp => 24;
    public override string ArtKey => "brass_sentry";
    protected override string? CastAnim => null;
    protected override IReadOnlyList<MoveState> Cycle() =>
        [new MoveState("SENTRY_BOLT", _ => Hit(BoltDamage, type: DamageType.Shock, fx: null), Atk(BoltDamage))];
}

/// <summary>
/// Котловой змей: 300 ♥. Давление растёт на 1 каждый ход врагов; удар холодом сбивает 1. На пороге (3, в фазе 2 — 2)
/// — «Сброс давления»: 16 огнём и Горение 3, давление в ноль. Между сбросами — ошпаривающий
/// укус и кольца (Блок + Оглушение).
/// </summary>
public sealed class BoilerWyrm : ActBossMonster
{
    public const int BaseHp = 300;
    public const int PressureThreshold = 3;
    public const int PressureThresholdPhaseTwo = 2;
    public const int ReleaseDamage = 16;
    public const int ReleaseBurning = 3;
    public const int BiteDamage = 14;
    public const int BiteBurning = 2;
    public const int CoilDamage = 10;
    public const int CoilBlock = 12;
    public const int ScaldStrength = 2;

    protected override int Hp => BaseHp;
    public override string ArtKey => "boiler_wyrm";

    private int _alternate;

    private int Pressure => Creature.GetPower<PressurePower>()?.Amount ?? 0;
    private int Threshold => Phase == 2 ? PressureThresholdPhaseTwo : PressureThreshold;

    /// <summary>Ходы выбираются по давлению (ветвление), а не циклом.</summary>
    protected override IReadOnlyList<MoveState> PhaseOne() =>
    [
        new MoveState("SCALDING_BITE", async _ => { _alternate++; await Hit(BiteDamage, type: DamageType.Fire); await Debuff<BurningPower>(BiteBurning); },
            Atk(BiteDamage), new DebuffIntent()),
        new MoveState("IRON_COIL", async _ => { _alternate++; await Hit(CoilDamage, fx: "vfx/vfx_attack_blunt"); await Block(CoilBlock); await Debuff<ConcussedPower>(1); },
            Atk(CoilDamage), new DefendIntent(), new DebuffIntent()),
        new MoveState("PRESSURE_RELEASE", Release, Atk(ReleaseDamage), new DebuffIntent()),
    ];

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var moves = PhaseOne();
        var next = new ConditionalBranchState("BOILER_NEXT");
        next.AddState(moves[2], () => Pressure >= Threshold);
        next.AddState(moves[0], () => _alternate % 2 == 0);
        next.AddState(moves[1], () => true);
        foreach (var move in moves) move.FollowUpState = next;
        return new MonsterMoveStateMachine(moves.Cast<MonsterState>().Append(next).ToList(), moves[0]);
    }

    private async Task Release(IReadOnlyList<Creature> targets)
    {
        await Cast();
        await Hit(ReleaseDamage, type: DamageType.Fire, fx: null);
        await Debuff<BurningPower>(ReleaseBurning);
        if (Creature.GetPower<PressurePower>() is { } power) await PowerCmd.Remove(power);
    }

    protected override Task OnPhaseTwo() => Buff<V.StrengthPower>(ScaldStrength);

    public override async Task AfterSideTurnStart(MegaCrit.Sts2.Core.Combat.CombatSide side, IReadOnlyList<Creature> participants, MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        await base.AfterSideTurnStart(side, participants, combatState);
        if (side == MegaCrit.Sts2.Core.Combat.CombatSide.Player && Creature.IsAlive)
            await PowerCmd.Apply<PressurePower>(Ctx, Creature, 1, Creature, null);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        await base.AfterDamageReceived(choiceContext, target, result, props, dealer, cardSource);
        if (target != Creature || !Creature.IsAlive || result.UnblockedDamage <= 0) return;
        if (DamageTyping.Resolve(props, dealer, cardSource) == DamageType.Frost && Creature.GetPower<PressurePower>() is { } power)
            await PowerCmd.Decrement(power);
    }
}

public sealed class ClockworkColossusBoss() : ActBossEncounter
{
    public override string IconKey => "clockwork_colossus";
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<ClockworkColossus>()];
}

public sealed class AutomatonArchitectBoss() : ActBossEncounter
{
    public override string IconKey => "automaton_architect";
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<BrassSentry>(), ModelDb.Monster<AutomatonArchitect>()];
}

public sealed class BoilerWyrmBoss() : ActBossEncounter
{
    public override string IconKey => "boiler_wyrm";
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<BoilerWyrm>()];
}
