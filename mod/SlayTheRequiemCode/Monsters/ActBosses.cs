using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Art;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Extensions;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

// Боссы актов 2 и 4 (docs/design/11 §6.1, M5 п. 1): по 3 на каждый вариант — 2а, 2б, 4а, 4б.
// ♥ по кривой 13 §2.1 (акт 2 — 260–320, акт 4 — 400–460); настройку HP/уровня/Непоколебимость даёт EnemySetup.
// Economy revision 1 не добавляет к уже авторскому HP второй бюджет заимствованного акта; старый сейв revision 0 сохраняет надбавку.
// Облик — свой PNG (OwnArtMonster), фаза 2 — своя картинка «_p2» со вспышкой.

/// <summary>
/// Босс акта: две фазы. Фаза 1 — цикл <see cref="PhaseOne"/>; при ♥ ниже <see cref="PhaseTwoAtPercent"/> % — картинка «p2»,
/// <see cref="OnPhaseTwo"/> и сразу первый ход цикла <see cref="PhaseTwo"/>. <see cref="Turn"/> — номер хода врагов.
/// </summary>
public abstract class ActBossMonster : OwnArtMonster
{
    public override EnemyRole CombatRole => EnemyRole.Boss;

    public const string PhaseTwoArt = "p2";

    /// <summary>Порог фазы 2 в % ♥; 0 — у босса одна фаза (картинки меняет сам, напр. стойки).</summary>
    public virtual int PhaseTwoAtPercent => 50;

    public int Phase { get; private set; } = 1;

    protected int Turn { get; private set; }

    protected override IReadOnlyList<string> ArtVariants => [PhaseTwoArt];

    protected abstract IReadOnlyList<MoveState> PhaseOne();

    protected virtual IReadOnlyList<MoveState> PhaseTwo() => PhaseOne();

    /// <summary>Переход в фазу 2 (картинка уже сменилась).</summary>
    protected virtual Task OnPhaseTwo() => Task.CompletedTask;

    private MoveState? _phaseTwoStart;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var one = PhaseOne();
        Link(one);
        var states = one.Cast<MonsterState>().ToList();
        if (PhaseTwoAtPercent > 0)
        {
            var two = PhaseTwo();
            if (two.Count > 0 && !ReferenceEquals(two[0], one[0]) && two.All(m => one.All(o => o.Id != m.Id)))
            {
                Link(two);
                states.AddRange(two);
                _phaseTwoStart = two[0];
            }
        }
        return new MonsterMoveStateMachine(states, one[0]);
    }

    private static void Link(IReadOnlyList<MoveState> moves)
    {
        for (var i = 0; i < moves.Count; i++) moves[i].FollowUpState = moves[(i + 1) % moves.Count];
    }

    public override Task AfterSideTurnStart(MegaCrit.Sts2.Core.Combat.CombatSide side, IReadOnlyList<Creature> participants, MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        if (side == MegaCrit.Sts2.Core.Combat.CombatSide.Enemy) Turn++;
        return Task.CompletedTask;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Creature || !Creature.IsAlive || Phase != 1 || PhaseTwoAtPercent <= 0 || !BelowPercent(PhaseTwoAtPercent)) return;
        Phase = 2;
        ShowArt(PhaseTwoArt);
        await OnPhaseTwo();
        if (_phaseTwoStart != null && Creature.IsAlive) SetMoveImmediate(_phaseTwoStart, forceTransition: true);
    }

    /// <summary>Свита гибнет вместе с боссом (бой не тянется ради добивания призванных).</summary>
    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature != Creature || wasRemovalPrevented) return;
        foreach (var ally in Allies.ToList()) await MegaCrit.Sts2.Core.Commands.CreatureCmd.Kill(ally);
    }

    /// <summary>Лечение от нанесённого урона (вампиры): доля снятых ♥ героев за удар.</summary>
    protected async Task HitAndDrain(int damage, int percent, int hits = 1, DamageType type = DamageType.Physical, string? fx = "vfx/vfx_attack_slash")
    {
        var before = Heroes.Sum(h => h.CurrentHp);
        await Hit(damage, hits, type, fx);
        await Heal(Math.Max(0, before - Heroes.Sum(h => h.CurrentHp)) * percent / 100);
    }

    /// <summary>Облик на случай, если PNG ещё не собран Godot (откат из <see cref="OwnArtMonster"/>).</summary>
    protected override MonsterModel LookOf => ModelDb.Monster<MegaCrit.Sts2.Core.Models.Monsters.Vantom>();
}

/// <summary>Свита боссов со своей картинкой.</summary>
public abstract class OwnArtMinion : OwnArtMonster
{
    public override EnemyRole CombatRole => EnemyRole.Minion;

    protected override MonsterModel LookOf => ModelDb.Monster<MegaCrit.Sts2.Core.Models.Monsters.TheLost>();
}

/// <summary>
/// Энкаунтер босса акта: значок на карте — свой статичный (<c>images/map/bosses/{IconKey}.png</c> + <c>_outline.png</c>,
/// как ванильные <c>placeholder/*_icon</c>: игра красит значок цветом пройденного пути, контур — цветом фона карты).
/// </summary>
public abstract class ActBossEncounter() : RequiemEncounter(RoomType.Boss)
{
    public abstract string IconKey { get; }

    public string IconBase => $"map/bosses/{IconKey}".ImagePath();

    public override string BossNodePath => VanillaArt.Exists(IconBase + ".png")
        ? IconBase
        : ModelDb.Encounter<VantomBoss>().BossNodePath;

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;
}
