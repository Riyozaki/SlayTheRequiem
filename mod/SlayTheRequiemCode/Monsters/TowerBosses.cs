using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using V = MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

// ═════════════ Акт 4б — Звёздная Башня: маги и лей-линии (11 §6.1) ═════════════

/// <summary>
/// Хранитель Астролябии: 430 ♥. Звездопад 6×4 магией, Выравнивание сфер (Блок + Сила), Затмение разума (−4 ✦, Слово
/// подчинения, удар). Фаза 2 (50 %): астролябия раскручена — Луч созвездия бьёт по кругу огнём → холодом → разрядом
/// со статусом стихии; Метеоритный дождь; Оберег орбит (Блок + лечение).
/// </summary>
public sealed class AstrolabeKeeper : ActBossMonster
{
    public const int BaseHp = 430;
    public const int StarfallDamage = 6;
    public const int StarfallHits = 4;
    public const int AlignBlock = 20;
    public const int AlignStrength = 2;
    public const int EclipseDamage = 10;
    public const int EclipseManaDrain = 4;
    public const int RayDamage = 24;
    public const int MeteorDamage = 7;
    public const int MeteorHits = 4;
    public const int OrreryBlock = 24;
    public const int OrreryHeal = 12;

    protected override int Hp => BaseHp;
    public override string ArtKey => "astrolabe_keeper";

    private static readonly DamageType[] Rotation = [DamageType.Fire, DamageType.Frost, DamageType.Shock];
    private int _ray;
    public override DamageType CurrentAttackType => NextMove?.Id.StartsWith("CONSTELLATION_RAY", StringComparison.Ordinal) == true
        ? Rotation[_ray % Rotation.Length] : base.CurrentAttackType;

    protected override IReadOnlyList<MoveState> PhaseOne() =>
    [
        new MoveState("STARFALL", async _ => { await Cast(); await Hit(StarfallDamage, StarfallHits, DamageType.Magic, null); },
            Multi(StarfallDamage, StarfallHits)),
        new MoveState("ALIGN_SPHERES", async _ => { await Block(AlignBlock); await Buff<V.StrengthPower>(AlignStrength); },
            new BuffIntent(), new DefendIntent()),
        new MoveState("MIND_ECLIPSE", async _ => { await Cast(); await Hit(EclipseDamage, type: DamageType.Magic, fx: null); await DrainMana(EclipseManaDrain); await Debuff<WordOfBindingPower>(1); },
            Atk(EclipseDamage), new DebuffIntent()),
    ];

    protected override IReadOnlyList<MoveState> PhaseTwo() =>
    [
        new MoveState("CONSTELLATION_RAY", Ray, Atk(RayDamage), new DebuffIntent()),
        new MoveState("METEOR_SHOWER", _ => Hit(MeteorDamage, MeteorHits, DamageType.Fire, null), Multi(MeteorDamage, MeteorHits)),
        new MoveState("CONSTELLATION_RAY_2", Ray, Atk(RayDamage), new DebuffIntent()),
        new MoveState("ORRERY_WARD", async _ => { await Block(OrreryBlock); await Heal(OrreryHeal); }, new DefendIntent(), new HealIntent()),
    ];

    private async Task Ray(IReadOnlyList<Creature> targets)
    {
        var element = EnemyActionBudget.TypeOf(Creature, CurrentAttackType);
        _ray++;
        await Cast();
        await Hit(RayDamage, type: element, fx: null);
        await ElementStatus(element, 4, 3, 2);
    }
}

/// <summary>
/// Змей лей-линий: 460 ♥. Пьёт ✦: в конце каждого хода врагов выжигает 1 ✦ у героев. Кольцо чар (магия + Блок),
/// Всплеск линии 5×3 со Статикой, Пожирание маны (−4 ✦, лечение 16). Фаза 2 (50 %): линия рвётся — Сила +3, выжигание 2 ✦;
/// Разрыв линии (магия + Статика 3), Удар змея, Пир чар.
/// </summary>
public sealed class LeyWyrm : ActBossMonster
{
    public const int BaseHp = 460;
    public const int PassiveManaDrain = 1;
    public const int PassiveManaDrainPhaseTwo = 2;
    public const int CoilDamage = 16;
    public const int CoilBlock = 10;
    public const int SurgeDamage = 5;
    public const int SurgeHits = 3;
    public const int SurgeStatic = 2;
    public const int DevourManaDrain = 4;
    public const int DevourHeal = 16;
    public const int RuptureStrength = 3;
    public const int RuptureDamage = 12;
    public const int RuptureStatic = 3;
    public const int StrikeDamage = 26;
    public const int FeastManaDrain = 5;
    public const int FeastHeal = 20;

    protected override int Hp => BaseHp;
    public override string ArtKey => "ley_wyrm";

    protected override IReadOnlyList<MoveState> PhaseOne() =>
    [
        new MoveState("ARCANE_COIL", async _ => { await Hit(CoilDamage, type: DamageType.Magic, fx: "vfx/vfx_attack_blunt"); await Block(CoilBlock); },
            Atk(CoilDamage), new DefendIntent()),
        new MoveState("LEY_SURGE", async _ => { await Cast(); await Hit(SurgeDamage, SurgeHits, DamageType.Shock, null); await Debuff<StaticPower>(SurgeStatic); },
            Multi(SurgeDamage, SurgeHits), new DebuffIntent()),
        new MoveState("DEVOUR_MANA", async _ => { await Cast(); await DrainMana(DevourManaDrain); await Heal(DevourHeal); },
            new DebuffIntent(), new HealIntent()),
    ];

    protected override IReadOnlyList<MoveState> PhaseTwo() =>
    [
        new MoveState("LEY_RUPTURE", async _ => { await Cast(); await Hit(RuptureDamage, type: DamageType.Magic, fx: null); await Debuff<StaticPower>(RuptureStatic); },
            Atk(RuptureDamage), new DebuffIntent()),
        new MoveState("SERPENT_STRIKE", _ => Hit(StrikeDamage, fx: "vfx/vfx_attack_blunt"), Atk(StrikeDamage)),
        new MoveState("ARCANE_FEAST", async _ => { await Cast(); await DrainMana(FeastManaDrain); await Heal(FeastHeal); },
            new DebuffIntent(), new HealIntent()),
    ];

    protected override Task OnPhaseTwo() => Buff<V.StrengthPower>(RuptureStrength);

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Enemy || !Creature.IsAlive) return;
        await DrainMana(Phase == 2 ? PassiveManaDrainPhaseTwo : PassiveManaDrain);
    }
}

/// <summary>
/// Симулякр Архимага: 400 ♥, стеклянный двойник мага. Чародейский шквал 7×3, Многослойный оберег (Блок 25), Ледяное копьё
/// (холод + Озноб). Фаза 2 (50 %): расщепление — 2 зеркальных осколка и Клятва стражи 50 (урон по нему −50 %, пока жив осколок);
/// Призменный луч, Сборка (осколок + Блок), Стеклянная буря.
/// </summary>
public sealed class ArchmageSimulacrum : ActBossMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<MirrorShard>()];
    public const int BaseHp = 400;
    public const int BarrageDamage = 7;
    public const int BarrageHits = 3;
    public const int WardBlock = 25;
    public const int LanceDamage = 20;
    public const int LanceChill = 3;
    public const int SplitShards = 2;
    public const int SplitGuardPercent = 50;
    public const int PrismDamage = 11;
    public const int PrismHits = 2;
    public const int PrismStatic = 2;
    public const int ReassembleBlock = 15;
    public const int StormDamage = 6;
    public const int StormHits = 4;

    protected override int Hp => BaseHp;
    public override string ArtKey => "archmage_simulacrum";

    protected override IReadOnlyList<MoveState> PhaseOne() =>
    [
        new MoveState("ARCANE_BARRAGE", async _ => { await Cast(); await Hit(BarrageDamage, BarrageHits, DamageType.Magic, null); },
            Multi(BarrageDamage, BarrageHits)),
        new MoveState("LAYERED_WARD", _ => Block(WardBlock), new DefendIntent()),
        new MoveState("FROST_LANCE", async _ => { await Cast(); await Hit(LanceDamage, type: DamageType.Frost, fx: null); await Debuff<ChillPower>(LanceChill); },
            Atk(LanceDamage), new DebuffIntent()),
    ];

    protected override IReadOnlyList<MoveState> PhaseTwo() =>
    [
        new MoveState("PRISM_RAY", async _ => { await Cast(); await Hit(PrismDamage, PrismHits, DamageType.Magic, null); await Debuff<StaticPower>(PrismStatic); },
            Multi(PrismDamage, PrismHits), new DebuffIntent()),
        new MoveState("REASSEMBLE", async _ => { await Cast(); await Block(ReassembleBlock); await Shards(1); },
            new SummonIntent(), new DefendIntent()),
        new MoveState("GLASS_STORM", _ => Hit(StormDamage, StormHits, fx: null), Multi(StormDamage, StormHits)),
    ];

    protected override async Task OnPhaseTwo()
    {
        await Shards(SplitShards);
        await Buff<OathGuardPower>(SplitGuardPercent);
    }

    private async Task Shards(int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (Allies.Count(a => a.Monster is MirrorShard) >= SplitShards) return;
            await Summon<MirrorShard>();
        }
    }
}

/// <summary>Зеркальный осколок: 20 ♥, «Осколочный разряд» 8 магией.</summary>
public sealed class MirrorShard : OwnArtMinion
{
    public const int BoltDamage = 8;
    protected override int Hp => 20;
    public override string ArtKey => "mirror_shard";
    protected override string? CastAnim => null;
    protected override IReadOnlyList<MoveState> Cycle() =>
        [new MoveState("SHARD_BOLT", _ => Hit(BoltDamage, type: DamageType.Magic, fx: null), Atk(BoltDamage))];
}

public sealed class AstrolabeKeeperBoss() : ActBossEncounter
{
    public override string IconKey => "astrolabe_keeper";
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<AstrolabeKeeper>()];
}

public sealed class LeyWyrmBoss() : ActBossEncounter
{
    public override string IconKey => "ley_wyrm";
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<LeyWyrm>()];
}

public sealed class ArchmageSimulacrumBoss() : ActBossEncounter
{
    public override string IconKey => "archmage_simulacrum";
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<ArchmageSimulacrum>()];
}
