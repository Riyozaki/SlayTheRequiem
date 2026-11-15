using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using V = MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

// Акт 1 — Подножие Шпиля (docs/design/11 §5): к Атаманше добавляются Вожак стаи, Мшистый Старейшина и редкая Хозяйка Котла.

/// <summary>Вожак стаи: 60 ♥ + 2 волка. Волки встают через 2 хода, пока жив вожак; когда волков нет — вожак Неистов (+Сила).</summary>
public sealed class PackLeader : RequiemMonster
{
    public const int MaulDamage = 9;
    public const int MaulBleed = 2;
    public const int HowlStrength = 2;
    public const int WolfReviveTurns = 2;
    public const int FrenzyStrength = 3;

    protected override int Hp => 60;
    protected override MonsterModel LookOf => ModelDb.Monster<Mawler>();

    private int _wolvesDownFor;
    private bool _frenzied;

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("MAUL", Maul, Atk(MaulDamage), new DebuffIntent()),
        new MoveState("HOWL", Howl, new BuffIntent()),
        new MoveState("MAUL_2", Maul, Atk(MaulDamage), new DebuffIntent()),
    ];

    private async Task Maul(IReadOnlyList<Creature> targets)
    {
        await Hit(MaulDamage);
        await Debuff<BleedPower>(MaulBleed);
    }

    private async Task Howl(IReadOnlyList<Creature> targets)
    {
        await Cast();
        await BuffAllies<V.StrengthPower>(HowlStrength);
    }

    public override async Task AfterSideTurnStart(MegaCrit.Sts2.Core.Combat.CombatSide side, IReadOnlyList<Creature> participants, MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Enemy || !Creature.IsAlive) return;
        var wolves = Allies.Count(a => a.Monster is PackWolf);
        if (wolves >= 2) { _wolvesDownFor = 0; return; }
        if (wolves == 0 && !_frenzied)
        {
            _frenzied = true;
            await Buff<V.StrengthPower>(FrenzyStrength);
        }
        if (++_wolvesDownFor >= WolfReviveTurns)
        {
            _wolvesDownFor = 0;
            await Summon<PackWolf>();
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature == Creature && !wasRemovalPrevented) await KillMinions<PackWolf>();
    }
}

public sealed class PackWolf : RequiemMinion
{
    public const int BiteDamage = 5;
    protected override int Hp => 18;
    protected override MonsterModel LookOf => ModelDb.Monster<Mawler>();
    protected override string? CastAnim => null;

    protected override IReadOnlyList<MoveState> Cycle() => [new MoveState("BITE", _ => Hit(BiteDamage), Atk(BiteDamage))];
}

/// <summary>Мшистый Старейшина: 95 ♥, Регенерация 8 (Горение отключает её полностью); каждые 3 хода «Корни» — Паралич (−◆ хода), если ◆ &lt; 3.</summary>
public sealed class MossElder : RequiemMonster
{
    public const int Regeneration = 8;
    public const int SlamDamage = 14;
    public const int SweepDamage = 6;
    public const int RootsStaminaThreshold = 3;

    protected override int Hp => 95;
    protected override MonsterModel LookOf => ModelDb.Monster<VineShambler>();
    protected override string AttackAnim => "Chomp";
    protected override string? CastAnim => "Vines";

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("SLAM", _ => Hit(SlamDamage), Atk(SlamDamage)),
        new MoveState("SWEEP", _ => Hit(SweepDamage, 2), Multi(SweepDamage, 2)),
        new MoveState("ROOTS", Roots, new DebuffIntent(), new DefendIntent()),
    ];

    private async Task Roots(IReadOnlyList<Creature> targets)
    {
        await Cast();
        await Block(10);
        foreach (var hero in Heroes.ToList())
            if (Resources.RequiemResource.Of<Resources.StaminaResource>(hero.Player) is { } stamina && stamina.Amount < RootsStaminaThreshold)
                await PowerCmd.Apply<ConcussedPower>(Ctx, hero, 1, Creature, null);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Enemy || !Creature.IsAlive || Creature.HasPower<BurningPower>()) return;
        await Heal(Regeneration);
    }
}

/// <summary>Хозяйка Котла (редкая): каждый 2-й ход пьёт зелье (видно на намерении); атака ≥ 10 до её хода разбивает котёл.</summary>
public sealed class CauldronMistress : RequiemMonster
{
    public const int LadleDamage = 8;
    public const int BrewHeal = 15;
    public const int BrewStrength = 3;
    public const int BrewBlock = 15;
    public const int SmashThreshold = 10;

    protected override int Hp => 58;
    protected override MonsterModel LookOf => ModelDb.Monster<DampCultist>();

    private bool _brewing;
    private bool _smashed;

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("LADLE", _ => Hit(LadleDamage), Atk(LadleDamage)),
        new MoveState("STIR", Stir, new BuffIntent()),
        new MoveState("DRINK", Drink, new HealIntent(), new BuffIntent()),
    ];

    private async Task Stir(IReadOnlyList<Creature> targets)
    {
        await Cast();
        _brewing = true;
        _smashed = false;
    }

    private async Task Drink(IReadOnlyList<Creature> targets)
    {
        _brewing = false;
        if (_smashed) return;
        await Heal(BrewHeal);
        await Buff<V.StrengthPower>(BrewStrength);
        await Block(BrewBlock);
    }

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Creature && _brewing && dealer is { IsPlayer: true } && result.TotalDamage >= SmashThreshold) _smashed = true;
        return Task.CompletedTask;
    }
}

public sealed class PackLeaderElite() : RequiemEncounter(RoomType.Elite)
{
    protected override Func<ActModel, bool> ValidFor => act => act is Overgrowth or Underdocks;
    protected override IReadOnlyList<MonsterModel> Lineup =>
        [ModelDb.Monster<PackWolf>(), ModelDb.Monster<PackLeader>(), ModelDb.Monster<PackWolf>()];
}

public sealed class MossElderElite() : RequiemEncounter(RoomType.Elite)
{
    protected override Func<ActModel, bool> ValidFor => act => act is Overgrowth or Underdocks;
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<MossElder>()];
}

/// <summary>Редкая элита акта 1: не в общем пуле, подставляется с шансом 15 % (<see cref="Acts.RareElitePatch"/>).</summary>
public sealed class CauldronMistressElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<CauldronMistress>()];
}
