using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using V = MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

// ═════════════ Акт 4а — Кровавый Двор: вампиры, день и ночь (11 §6.1) ═════════════

/// <summary>
/// Алая Графиня: 420 ♥. Кровавая роза (Кровотечение), Бал масок (рабы крови, до 2), Алый поцелуй — лечится на весь снятый урон.
/// Фаза 2 (50 %): истинный облик — Туман 1 (атаки мимо, кроме Света и заклинаний), Сила +3; рой мышей с вампиризмом 50 %,
/// кровавый дождь, вечный поцелуй.
/// </summary>
public sealed class CrimsonCountess : ActBossMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<BloodThrall>()];
    public const int BaseHp = 420;
    public const int RoseDamage = 9;
    public const int RoseHits = 2;
    public const int RoseBleed = 3;
    public const int MaxThralls = 2;
    public const int BallBlock = 14;
    public const int KissDamage = 22;
    public const int KissDrainPercent = 100;
    public const int TrueFormMist = 1;
    public const int TrueFormStrength = 3;
    public const int SwarmDamage = 6;
    public const int SwarmHits = 4;
    public const int SwarmDrainPercent = 50;
    public const int RainDamage = 10;
    public const int RainBleed = 3;
    public const int EternalKissDamage = 28;

    protected override int Hp => BaseHp;
    public override string ArtKey => "crimson_countess";

    protected override IReadOnlyList<MoveState> PhaseOne() =>
    [
        new MoveState("BLOOD_ROSE", async _ => { await Hit(RoseDamage, RoseHits); await Debuff<BleedPower>(RoseBleed); },
            Multi(RoseDamage, RoseHits), new DebuffIntent()),
        new MoveState("MASKED_BALL", async _ => { await Cast(); await Block(BallBlock); await CallThralls(); },
            new SummonIntent(), new DefendIntent()),
        new MoveState("CRIMSON_KISS", _ => HitAndDrain(KissDamage, KissDrainPercent), Atk(KissDamage), new HealIntent()),
    ];

    protected override IReadOnlyList<MoveState> PhaseTwo() =>
    [
        new MoveState("BAT_SWARM", _ => HitAndDrain(SwarmDamage, SwarmDrainPercent, SwarmHits, fx: null), Multi(SwarmDamage, SwarmHits), new HealIntent()),
        new MoveState("BLOOD_RAIN", async _ => { await Cast(); await Hit(RainDamage, fx: null); await Debuff<BleedPower>(RainBleed); },
            Atk(RainDamage), new DebuffIntent()),
        new MoveState("ETERNAL_KISS", _ => HitAndDrain(EternalKissDamage, KissDrainPercent), Atk(EternalKissDamage), new HealIntent()),
    ];

    protected override async Task OnPhaseTwo()
    {
        await Buff<MistFormPower>(TrueFormMist);
        await Buff<V.StrengthPower>(TrueFormStrength);
    }

    private async Task CallThralls()
    {
        var missing = MaxThralls - Allies.Count(a => a.Monster is BloodThrall);
        for (var i = 0; i < missing; i++) await Summon<BloodThrall>();
    }
}

/// <summary>
/// Страж Затмения: 440 ♥, одна фаза — две стойки по 2 хода. День (Солнечная стойка): урон по нему −50 %, кроме Света;
/// Солнечная плита (Блок + удар), Луч рассвета (Свет + Горение). Ночь (Лунная стойка): Свет по нему ×1.5; Лунные когти,
/// Сумерки (удар + −3 ✦). Ниже 50 % ♥ — Затмение: стойка меняется каждый ход. Смена стойки меняет картинку.
/// </summary>
public sealed class EclipseWarden : ActBossMonster
{
    public const string NightArt = "night";
    public const int BaseHp = 440;
    public const int StanceTurns = 2;
    public const int EclipseStanceTurns = 1;
    public const int EclipseAtPercent = 50;
    public const int PlateBlock = 22;
    public const int PlateDamage = 10;
    public const int RayDamage = 16;
    public const int RayBurning = 2;
    public const int TalonDamage = 8;
    public const int TalonHits = 3;
    public const int NightfallDamage = 20;
    public const int NightfallManaDrain = 3;

    protected override int Hp => BaseHp;
    public override string ArtKey => "eclipse_warden";
    public override int PhaseTwoAtPercent => 0;
    protected override IReadOnlyList<string> ArtVariants => [NightArt];

    private bool _night;
    private int _stanceMoves;
    private int _moves;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await Buff<SunStancePower>(1);
    }

    protected override IReadOnlyList<MoveState> PhaseOne() =>
    [
        new MoveState("SOLAR_PLATE", async _ => { await Block(PlateBlock); await Hit(PlateDamage, fx: "vfx/vfx_attack_blunt"); await EndMove(); },
            Atk(PlateDamage), new DefendIntent()),
        new MoveState("DAWN_RAY", async _ => { await Cast(); await Hit(RayDamage, type: DamageType.Light, fx: null); await Debuff<BurningPower>(RayBurning); await EndMove(); },
            Atk(RayDamage), new DebuffIntent()),
        new MoveState("MOON_TALONS", async _ => { await Hit(TalonDamage, TalonHits); await EndMove(); }, Multi(TalonDamage, TalonHits)),
        new MoveState("NIGHTFALL", async _ => { await Hit(NightfallDamage); await DrainMana(NightfallManaDrain); await EndMove(); },
            Atk(NightfallDamage), new DebuffIntent()),
    ];

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var moves = PhaseOne();
        var next = new ConditionalBranchState("ECLIPSE_NEXT");
        next.AddState(moves[0], () => !_night && _moves % 2 == 0);
        next.AddState(moves[1], () => !_night);
        next.AddState(moves[2], () => _moves % 2 == 0);
        next.AddState(moves[3], () => true);
        foreach (var move in moves) move.FollowUpState = next;
        return new MonsterMoveStateMachine(moves.Cast<MonsterState>().Append(next).ToList(), moves[0]);
    }

    /// <summary>Конец хода стойки: после 2 ходов (в Затмении — после каждого) — смена дня и ночи.</summary>
    private async Task EndMove()
    {
        _moves++;
        _stanceMoves++;
        var length = BelowPercent(EclipseAtPercent) ? EclipseStanceTurns : StanceTurns;
        if (_stanceMoves < length || !Creature.IsAlive) return;
        _stanceMoves = 0;
        _night = !_night;
        if (_night)
        {
            if (Creature.GetPower<SunStancePower>() is { } sun) await PowerCmd.Remove(sun);
            await Buff<MoonStancePower>(1);
            ShowArt(NightArt);
        }
        else
        {
            if (Creature.GetPower<MoonStancePower>() is { } moon) await PowerCmd.Remove(moon);
            await Buff<SunStancePower>(1);
            ShowArt(null);
        }
    }
}

/// <summary>
/// Иерофант Багряной Чаши: 400 ♥. Кровавый луч (магия + Кровотечение), Созыв паствы (послушники, до 2), Кровавое причастие —
/// приносит послушника в жертву: лечение 30 и Сила +2 (без послушников — Блок 20). Фаза 2 (50 %): чаша переполнена —
/// Сила +2; в конце каждого хода врагов выжигает 2 ✦ у героев и лечится на 6; Багряный суд, Разлив чаши, Последние обряды.
/// </summary>
public sealed class ChaliceHierophant : ActBossMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<ChaliceAcolyte>()];
    public const int BaseHp = 400;
    public const int BeamDamage = 18;
    public const int BeamBleed = 2;
    public const int MaxAcolytes = 2;
    public const int SacramentHeal = 30;
    public const int SacramentStrength = 2;
    public const int SacramentBlock = 20;
    public const int OverflowStrength = 2;
    public const int OverflowManaDrain = 2;
    public const int OverflowHeal = 6;
    public const int JudgementDamage = 26;
    public const int FloodDamage = 9;
    public const int FloodHits = 2;
    public const int FloodBleed = 2;
    public const int RitesBlock = 16;

    protected override int Hp => BaseHp;
    public override string ArtKey => "chalice_hierophant";

    protected override IReadOnlyList<MoveState> PhaseOne() =>
    [
        new MoveState("BLOOD_BEAM", async _ => { await Cast(); await Hit(BeamDamage, type: DamageType.Magic, fx: null); await Debuff<BleedPower>(BeamBleed); },
            Atk(BeamDamage), new DebuffIntent()),
        new MoveState("CALL_THE_FAITHFUL", async _ => { await Cast(); await CallAcolytes(MaxAcolytes); }, new SummonIntent()),
        new MoveState("BLOOD_SACRAMENT", Sacrament, new HealIntent(), new BuffIntent()),
    ];

    protected override IReadOnlyList<MoveState> PhaseTwo() =>
    [
        new MoveState("SANGUINE_JUDGEMENT", async _ => { await Cast(); await Hit(JudgementDamage, type: DamageType.Magic, fx: null); },
            Atk(JudgementDamage)),
        new MoveState("CHALICE_FLOOD", async _ => { await Hit(FloodDamage, FloodHits, DamageType.Magic, null); await Debuff<BleedPower>(FloodBleed); },
            Multi(FloodDamage, FloodHits), new DebuffIntent()),
        new MoveState("LAST_RITES", async _ => { await Cast(); await Block(RitesBlock); await CallAcolytes(1); },
            new SummonIntent(), new DefendIntent()),
    ];

    protected override Task OnPhaseTwo() => Buff<V.StrengthPower>(OverflowStrength);

    private async Task CallAcolytes(int max)
    {
        var missing = max - Allies.Count(a => a.Monster is ChaliceAcolyte);
        for (var i = 0; i < missing; i++) await Summon<ChaliceAcolyte>();
    }

    private async Task Sacrament(IReadOnlyList<Creature> targets)
    {
        await Cast();
        var victim = Allies.FirstOrDefault(a => a.Monster is ChaliceAcolyte);
        if (victim == null)
        {
            await Block(SacramentBlock);
            return;
        }
        await CreatureCmd.Kill(victim);
        await Heal(SacramentHeal);
        await Buff<V.StrengthPower>(SacramentStrength);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Enemy || Phase != 2 || !Creature.IsAlive) return;
        await DrainMana(OverflowManaDrain);
        await Heal(OverflowHeal);
    }
}

/// <summary>Послушник Чаши: 22 ♥; «Подношение» лечит Иерофанта на 8, «Покаянный удар» 6.</summary>
public sealed class ChaliceAcolyte : OwnArtMinion
{
    public const int OfferingHeal = 8;
    public const int StrikeDamage = 6;
    protected override int Hp => 22;
    public override string ArtKey => "chalice_acolyte";

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("OFFERING", async _ =>
        {
            await Cast();
            if (Allies.FirstOrDefault(a => a.Monster is ChaliceHierophant) is { } master) await CreatureCmd.Heal(master, OfferingHeal);
        }, new HealIntent()),
        new MoveState("PENITENT_STRIKE", _ => Hit(StrikeDamage), Atk(StrikeDamage)),
    ];
}

public sealed class CrimsonCountessBoss() : ActBossEncounter
{
    public override string IconKey => "crimson_countess";
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<CrimsonCountess>()];
}

public sealed class EclipseWardenBoss() : ActBossEncounter
{
    public override string IconKey => "eclipse_warden";
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<EclipseWarden>()];
}

public sealed class ChaliceHierophantBoss() : ActBossEncounter
{
    public override string IconKey => "chalice_hierophant";
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<ChaliceAcolyte>(), ModelDb.Monster<ChaliceHierophant>()];
}
