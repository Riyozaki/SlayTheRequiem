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

// ═════════════ Акт 2а — Ледяные Курганы: нежить и стужа (11 §6.1) ═════════════

/// <summary>
/// Король-под-Курганом: 300 ♥. Приходит с 2 хускарлами (свита гибнет с ним); пока жив хоть один — Клятва дружины (урон по королю −40 %),
/// гибель хускарла — Сила +2 королю. Фаза 2 (50 %): встаёт с трона — Сила +3, двойной «Королевский суд» с Расколом,
/// «Могильный холод» по всем и по хускарлу за раз.
/// </summary>
public sealed class KingUnderBarrow : ActBossMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<BarrowHuskarl>()];
    public const int BaseHp = 300;
    public const int OathReductionPercent = 40;
    public const int FallenOathStrength = 2;
    public const int BladeDamage = 16;
    public const int BladeChill = 2;
    public const int CallBlock = 12;
    public const int CrownBlock = 18;
    public const int CrownStrength = 2;
    public const int RiseStrength = 3;
    public const int JudgementDamage = 10;
    public const int JudgementHits = 2;
    public const int JudgementSunder = 2;
    public const int GraveColdDamage = 7;
    public const int GraveColdChill = 3;

    protected override int Hp => BaseHp;
    public override string ArtKey => "king_under_barrow";

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await Buff<OathGuardPower>(OathReductionPercent);
    }

    protected override IReadOnlyList<MoveState> PhaseOne() =>
    [
        new MoveState("WINTER_BLADE", async _ => { await Hit(BladeDamage, type: DamageType.Frost); await Debuff<ChillPower>(BladeChill); },
            Atk(BladeDamage), new DebuffIntent()),
        new MoveState("CALL_THE_OATHSWORN", async _ => { await Cast(); await Block(CallBlock); await RefillGuard(2); },
            new SummonIntent(), new DefendIntent()),
        new MoveState("CROWN_OF_RIME", async _ => { await Block(CrownBlock); await Buff<V.StrengthPower>(CrownStrength); await BuffAllies<V.StrengthPower>(CrownStrength); },
            new BuffIntent(), new DefendIntent()),
    ];

    protected override IReadOnlyList<MoveState> PhaseTwo() =>
    [
        new MoveState("KINGS_JUDGEMENT", async _ => { await Hit(JudgementDamage, JudgementHits, DamageType.Frost); await Debuff<SunderPower>(JudgementSunder); },
            Multi(JudgementDamage, JudgementHits), new DebuffIntent()),
        new MoveState("GRAVE_COLD", async _ => { await Cast(); await Hit(GraveColdDamage, type: DamageType.Frost, fx: null); await Debuff<ChillPower>(GraveColdChill); },
            Atk(GraveColdDamage), new DebuffIntent()),
        new MoveState("RAISE_OATHSWORN", async _ => { await Cast(); await RefillGuard(1); await Block(CallBlock); },
            new SummonIntent(), new DefendIntent()),
    ];

    protected override Task OnPhaseTwo() => Buff<V.StrengthPower>(RiseStrength);

    private async Task RefillGuard(int max)
    {
        var missing = max - Allies.Count(a => a.Monster is BarrowHuskarl);
        for (var i = 0; i < missing; i++) await Summon<BarrowHuskarl>();
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, CardModel? source, bool wasRemovalPrevented, float deathAnimLength)
    {
        await base.AfterDeath(choiceContext, creature, source, wasRemovalPrevented, deathAnimLength);
        if (!wasRemovalPrevented && !creature.IsAlive && creature.Monster is BarrowHuskarl && Creature.IsAlive)
            await PowerCmd.Apply<V.StrengthPower>(choiceContext, Creature, FallenOathStrength, Creature, null);
    }
}

/// <summary>Хускарл кургана: 32 ♥, секира 9 и щит (Блок 6) — через ход.</summary>
public sealed class BarrowHuskarl : OwnArtMinion
{
    public const int AxeDamage = 9;
    public const int ShieldBlock = 6;
    protected override int Hp => 32;
    public override string ArtKey => "barrow_huskarl";
    protected override string? CastAnim => null;

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("HUSKARL_AXE", _ => Hit(AxeDamage), Atk(AxeDamage)),
        new MoveState("HUSKARL_SHIELD", _ => Block(ShieldBlock), new DefendIntent()),
    ];
}

/// <summary>
/// Костяной змей стужи: 280 ♥, в небе — Полёт (физические атаки −50 %). «Пикирование» сажает его на ход («Укус на земле»),
/// затем взлетает снова. Фаза 2 (50 %): крылья ломаются — Полёт снят навсегда, Сила +4, «Костяная буря» и «Ледниковый рёв».
/// </summary>
public sealed class BoneFrostWyrm : ActBossMonster
{
    public const int BaseHp = 280;
    public const int GustDamage = 6;
    public const int GustHits = 2;
    public const int GustStaminaDrain = 1;
    public const int BreathDamage = 12;
    public const int BreathChill = 3;
    public const int DiveDamage = 22;
    public const int GroundBiteDamage = 14;
    public const int BrokenWingsStrength = 4;
    public const int StormDamage = 7;
    public const int StormHits = 3;
    public const int RoarDamage = 14;
    public const int RoarChill = 4;
    public const int TailDamage = 18;

    protected override int Hp => BaseHp;
    public override string ArtKey => "bone_frost_wyrm";

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await Buff<FlightPower>(1);
    }

    protected override IReadOnlyList<MoveState> PhaseOne() =>
    [
        new MoveState("WING_GUST", async _ => { await Hit(GustDamage, GustHits, fx: null); await DrainStamina(GustStaminaDrain); },
            Multi(GustDamage, GustHits), new DebuffIntent()),
        new MoveState("FROST_BREATH", async _ => { await Cast(); await Hit(BreathDamage, type: DamageType.Frost, fx: null); await Debuff<ChillPower>(BreathChill); },
            Atk(BreathDamage), new DebuffIntent()),
        new MoveState("DIVE", async _ => { await Hit(DiveDamage, fx: "vfx/vfx_attack_blunt"); await Land(); }, Atk(DiveDamage)),
        new MoveState("GROUND_BITE", async _ => { await Hit(GroundBiteDamage); if (Phase == 1) await Buff<FlightPower>(1); }, Atk(GroundBiteDamage), new BuffIntent()),
    ];

    protected override IReadOnlyList<MoveState> PhaseTwo() =>
    [
        new MoveState("BONE_STORM", _ => Hit(StormDamage, StormHits), Multi(StormDamage, StormHits)),
        new MoveState("GLACIAL_ROAR", async _ => { await Cast(); await Hit(RoarDamage, type: DamageType.Frost, fx: null); await Debuff<ChillPower>(RoarChill); },
            Atk(RoarDamage), new DebuffIntent()),
        new MoveState("TAIL_SWEEP", async _ => { await Hit(TailDamage, fx: "vfx/vfx_attack_blunt"); await Debuff<ConcussedPower>(1); },
            Atk(TailDamage), new DebuffIntent()),
    ];

    protected override async Task OnPhaseTwo()
    {
        await Land();
        await Buff<V.StrengthPower>(BrokenWingsStrength);
    }

    private async Task Land()
    {
        if (Creature.GetPower<FlightPower>() is { } flight) await PowerCmd.Remove(flight);
    }
}

/// <summary>
/// Плакальщица курганов: 260 ♥, Призрачность (физический урон −75 %, кроме серебра и заклинаний). Плач — −2 ◆ и Страх 1;
/// Погребальная песнь — лечение 14 и призрак-раб. Фаза 2 (50 %): обретает плоть — Призрачность снята, Сила +3,
/// «Предсмертный крик» 5×4 холодом, «Последний плач» выжигает ✦.
/// </summary>
public sealed class BarrowWailer : ActBossMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<WraithThrall>()];
    public const int BaseHp = 260;
    public const int TouchDamage = 12;
    public const int TouchChill = 2;
    public const int WailStaminaDrain = 2;
    public const int WailFear = 1;
    public const int DirgeHeal = 14;
    public const int FleshStrength = 3;
    public const int ShriekDamage = 5;
    public const int ShriekHits = 4;
    public const int LamentDamage = 8;
    public const int LamentManaDrain = 3;
    public const int LamentChill = 3;

    protected override int Hp => BaseHp;
    public override string ArtKey => "barrow_wailer";

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await Buff<GhostlyPower>(1);
    }

    protected override IReadOnlyList<MoveState> PhaseOne() =>
    [
        new MoveState("GRAVE_TOUCH", async _ => { await Hit(TouchDamage, type: DamageType.Frost, fx: null); await Debuff<ChillPower>(TouchChill); },
            Atk(TouchDamage), new DebuffIntent()),
        new MoveState("MOURNING_WAIL", async _ => { await Cast(); await DrainStamina(WailStaminaDrain); await Debuff<FearPower>(WailFear); },
            new DebuffIntent()),
        new MoveState("GRAVE_TOUCH_2", async _ => { await Hit(TouchDamage, type: DamageType.Frost, fx: null); await Debuff<ChillPower>(TouchChill); },
            Atk(TouchDamage), new DebuffIntent()),
        new MoveState("FUNERAL_DIRGE", async _ => { await Cast(); await Heal(DirgeHeal); await Summon<WraithThrall>(); },
            new HealIntent(), new SummonIntent()),
    ];

    protected override IReadOnlyList<MoveState> PhaseTwo() =>
    [
        new MoveState("DEATH_SHRIEK", _ => Hit(ShriekDamage, ShriekHits, DamageType.Frost, null), Multi(ShriekDamage, ShriekHits)),
        new MoveState("LAST_LAMENT", async _ => { await Cast(); await Hit(LamentDamage, type: DamageType.Frost, fx: null); await DrainMana(LamentManaDrain); await Debuff<ChillPower>(LamentChill); },
            Atk(LamentDamage), new DebuffIntent()),
    ];

    protected override async Task OnPhaseTwo()
    {
        if (Creature.GetPower<GhostlyPower>() is { } ghost) await PowerCmd.Remove(ghost);
        await Buff<V.StrengthPower>(FleshStrength);
    }
}

public sealed class KingUnderBarrowBoss() : ActBossEncounter
{
    public override string IconKey => "king_under_barrow";
    protected override IReadOnlyList<MonsterModel> Lineup =>
        [ModelDb.Monster<BarrowHuskarl>(), ModelDb.Monster<KingUnderBarrow>(), ModelDb.Monster<BarrowHuskarl>()];
}

public sealed class BoneFrostWyrmBoss() : ActBossEncounter
{
    public override string IconKey => "bone_frost_wyrm";
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<BoneFrostWyrm>()];
}

public sealed class BarrowWailerBoss() : ActBossEncounter
{
    public override string IconKey => "barrow_wailer";
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<BarrowWailer>()];
}
