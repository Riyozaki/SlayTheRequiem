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

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

// Акт 2б — Заводные Глубины (docs/design/11 §5): Центурион, Паучья Матка, Механик-Отступник, редкое Сердце-Реактор.

/// <summary>Центурион Глубин: 140 ♥, Латы 5; «Паровой выдох» — Горение 4 всем; после 3 атак перегревается — ход без Лат.</summary>
public sealed class DeepCenturion : RequiemMonster
{
    public const int Plating = 5;
    public const int HammerDamage = 16;
    public const int SteamBurning = 4;
    public const int SteamDamage = 6;
    public const int AttacksBeforeOverheat = 3;

    protected override int Hp => 140;
    protected override MonsterModel LookOf => ModelDb.Monster<PunchConstruct>();

    private int _attacks;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var hammer = new MoveState("PISTON_HAMMER", Hammer, Atk(HammerDamage));
        var steam = new MoveState("STEAM_EXHALE", Steam, Atk(SteamDamage), new DebuffIntent());
        var vent = new MoveState("OVERHEAT", Vent, new StunIntent());
        var next = new ConditionalBranchState("CENTURION_NEXT");
        next.AddState(vent, () => _attacks >= AttacksBeforeOverheat);
        next.AddState(hammer, () => _attacks < AttacksBeforeOverheat && _attacks % 2 == 0);
        next.AddState(steam, () => _attacks < AttacksBeforeOverheat && _attacks % 2 == 1);
        hammer.FollowUpState = next;
        steam.FollowUpState = next;
        vent.FollowUpState = next;
        return new MonsterMoveStateMachine([hammer, steam, vent, next], hammer);
    }

    private async Task Hammer(IReadOnlyList<Creature> targets)
    {
        _attacks++;
        await Hit(HammerDamage, fx: "vfx/vfx_attack_blunt");
    }

    private async Task Steam(IReadOnlyList<Creature> targets)
    {
        _attacks++;
        await Hit(SteamDamage, type: DamageType.Fire, fx: null);
        await Debuff<BurningPower>(SteamBurning);
    }

    /// <summary>Перегрев: Латы сброшены на ход (вернутся в начале следующего хода врагов).</summary>
    private async Task Vent(IReadOnlyList<Creature> targets)
    {
        _attacks = 0;
        if (Creature.GetPower<PlatingPower>() is { } plating) await PowerCmd.Remove(plating);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == MegaCrit.Sts2.Core.Combat.CombatSide.Player && Creature.IsAlive && !Creature.HasPower<PlatingPower>() && _attacks > 0)
            await PowerCmd.Apply<PlatingPower>(Ctx, Creature, Plating, Creature, null);
    }
}

/// <summary>Паучья Матка: 90 ♥ + рой; каждый ход — паучок-ремонтник (6 ♥), чинящий союзников на 6 ♥ и +1 Латы.</summary>
public sealed class SpiderMatriarch : RequiemMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<RepairSpider>()];
    public const int FangDamage = 10;
    public const int WebDamage = 5;
    public const int WebHits = 2;

    protected override int Hp => 90;
    protected override MonsterModel LookOf => ModelDb.Monster<FossilStalker>();

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("BROOD", Brood, Atk(FangDamage), new SummonIntent()),
        new MoveState("WEB_SPIT", Web, Multi(WebDamage, WebHits), new SummonIntent()),
    ];

    private async Task Brood(IReadOnlyList<Creature> targets)
    {
        await Hit(FangDamage);
        await Summon<RepairSpider>();
    }

    private async Task Web(IReadOnlyList<Creature> targets)
    {
        await Hit(WebDamage, WebHits, DamageType.Poison, null);
        await Summon<RepairSpider>();
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, CardModel? source, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature == Creature && !wasRemovalPrevented) await KillMinions<RepairSpider>();
    }
}

/// <summary>Паучок-ремонтник (6 ♥): чинит союзника с наименьшим ♥ на 6 и +1 Латы. Разряд убивает паучков цепью (6 ♥).</summary>
public sealed class RepairSpider : RequiemMinion
{
    public const int Repair = 6;
    public const int RepairPlating = 1;
    protected override int Hp => 6;
    protected override MonsterModel LookOf => ModelDb.Monster<Stabbot>();
    protected override string? CastAnim => null;

    protected override IReadOnlyList<MoveState> Cycle() => [new MoveState("REPAIR", Fix, new HealIntent(), new BuffIntent())];

    private async Task Fix(IReadOnlyList<Creature> targets)
    {
        var target = Allies.Where(a => a.Monster is not RepairSpider).OrderBy(a => a.CurrentHp * 100 / Math.Max(1, a.MaxHp)).FirstOrDefault();
        if (target == null) return;
        await CreatureCmd.Heal(target, Repair);
        await PowerCmd.Apply<PlatingPower>(Ctx, target, RepairPlating, Creature, null);
    }
}

/// <summary>Механик-Отступник: 85 ♥, ставит Турели (12 ♥, 4 урона каждый ход); со смертью Механика турели глохнут.</summary>
public sealed class RenegadeMechanic : RequiemMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<MechanicTurret>()];
    public const int WrenchDamage = 9;
    public const int MaxTurrets = 2;

    protected override int Hp => 85;
    protected override MonsterModel LookOf => ModelDb.Monster<TurretOperator>();
    protected override string? CastAnim => "Crank";

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("DEPLOY_TURRET", Deploy, new SummonIntent()),
        new MoveState("WRENCH", _ => Hit(WrenchDamage, fx: "vfx/vfx_attack_blunt"), Atk(WrenchDamage)),
        new MoveState("WRENCH_2", _ => Hit(WrenchDamage, fx: "vfx/vfx_attack_blunt"), Atk(WrenchDamage)),
    ];

    private async Task Deploy(IReadOnlyList<Creature> targets)
    {
        await Cast();
        if (Allies.Count(a => a.Monster is MechanicTurret) < MaxTurrets) await Summon<MechanicTurret>();
        else await Block(12);
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, CardModel? source, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature == Creature && !wasRemovalPrevented) await KillMinions<MechanicTurret>();
    }
}

public sealed class MechanicTurret : RequiemMinion
{
    public const int BoltDamage = 4;
    protected override int Hp => 12;
    protected override MonsterModel LookOf => ModelDb.Monster<Zapbot>();
    protected override string? CastAnim => null;

    protected override IReadOnlyList<MoveState> Cycle() => [new MoveState("BOLT", _ => Hit(BoltDamage), Atk(BoltDamage))];
}

/// <summary>Сердце-Реактор (редкое): 160 ♥, не атакует — каждые 2 хода выброс (Шок/разряд всем); вокруг 2 стража.</summary>
public sealed class ReactorHeart : RequiemMonster
{
    public const int DischargeDamage = 14;
    public const int DischargeStatic = 3;

    protected override int Hp => 160;
    protected override MonsterModel LookOf => ModelDb.Monster<GlobeHead>();

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("CHARGE", async _ => { await Cast(); await Block(10); }, new BuffIntent(), new DefendIntent()),
        new MoveState("DISCHARGE", Discharge, Atk(DischargeDamage), new DebuffIntent()),
    ];

    private async Task Discharge(IReadOnlyList<Creature> targets)
    {
        await Blast(DischargeDamage, DamageType.Shock);
        await Debuff<StaticPower>(DischargeStatic);
    }
}

public sealed class ReactorGuard : RequiemMinion
{
    public const int ClampDamage = 7;
    protected override int Hp => 40;
    protected override MonsterModel LookOf => ModelDb.Monster<Guardbot>();
    protected override string AttackAnim => "Cast";

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("CLAMP", _ => Hit(ClampDamage, fx: "vfx/vfx_attack_blunt"), Atk(ClampDamage)),
        new MoveState("SHIELD_HEART", async _ =>
        {
            var heart = Allies.FirstOrDefault(a => a.Monster is ReactorHeart);
            if (heart != null) await CreatureCmd.GainBlock(heart, 10, ValueProp.Move, null);
        }, new DefendIntent()),
    ];
}

public sealed class DeepCenturionElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<DeepCenturion>()];
}

public sealed class SpiderMatriarchElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup =>
        [ModelDb.Monster<RepairSpider>(), ModelDb.Monster<SpiderMatriarch>(), ModelDb.Monster<RepairSpider>()];
}

public sealed class RenegadeMechanicElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<MechanicTurret>(), ModelDb.Monster<RenegadeMechanic>()];
}

public sealed class ReactorHeartElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup =>
        [ModelDb.Monster<ReactorGuard>(), ModelDb.Monster<ReactorHeart>(), ModelDb.Monster<ReactorGuard>()];
}
