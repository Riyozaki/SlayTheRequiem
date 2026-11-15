using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;

namespace SlayTheRequiem.SlayTheRequiemCode.Acts;

/// <summary>
/// Новый акт Реквиема (docs/design/13 §3) на <see cref="CustomActModel"/>. Карта шириной 9 (<see cref="WideMapPatch"/>),
/// обычные враги заимствованы у ванильных актов (с надбавкой ♥ из <see cref="ActRules.EnemyHpBonusPercent"/>),
/// элиты — свои; боссы — ванильные боссы, ещё не встреченные в этом забеге (<see cref="ActRoomsPatch"/>).
/// Свои элиты перечислены здесь явно: их <c>IsValidForAct</c> для новых актов возвращает false, чтобы BaseLib не добавил их второй раз.
/// Номер акта для BaseLib — −1: сами в ванильную раскладку (<c>ModelDb.ActsByIndex</c>) не попадают, слот акту даёт
/// <see cref="ActListPatch"/>; <see cref="Slot"/> — его место в пяти актах (1 — второй акт, 3 — четвёртый).
/// </summary>
public abstract class RequiemActModel(int slot) : CustomActModel(-1)
{
    public int Slot { get; } = slot;

    public abstract ActKind Kind { get; }

    /// <summary>Ванильный акт, чьи фоны карты, лагеря и боя используются, пока нет своих (этап 8).</summary>
    protected abstract string LookId { get; }

    protected override string CustomMapTopBgPath => ImageHelper.GetImagePath($"packed/map/map_bgs/{LookId}/map_top_{LookId}.png");
    protected override string CustomMapMidBgPath => ImageHelper.GetImagePath($"packed/map/map_bgs/{LookId}/map_middle_{LookId}.png");
    protected override string CustomMapBotBgPath => ImageHelper.GetImagePath($"packed/map/map_bgs/{LookId}/map_bottom_{LookId}.png");
    protected override string CustomRestSiteBackgroundPath => SceneHelper.GetScenePath($"rest_site/{LookId}_rest_site");
    protected override BackgroundAssets CustomGenerateBackgroundAssets(Rng rng) => new(LookId, rng);
    protected override string CustomBackgroundScenePath => SceneHelper.GetScenePath($"backgrounds/{LookId}/{LookId}_background");

    protected override ActMap? CustomCreateMap(RunState runState, bool replaceTreasureWithElites) =>
        RequiemConfig.WideMaps ? WideActMap.CreateFor(runState, replaceTreasureWithElites, ActRules.MapWidth(Kind)) : null;

    protected override int BaseNumberOfRooms => ActRules.Rooms(Kind);
    protected override int NumberOfWeakEncounters => 2;

    /// <summary>Ванильные обычные и слабые бои.</summary>
    protected abstract IEnumerable<EncounterModel> BorrowedEncounters { get; }

    /// <summary>Свои элиты (редкая — отдельно, <see cref="RareElite"/>).</summary>
    protected abstract IEnumerable<EncounterModel> OwnElites { get; }

    /// <summary>Пул боссов: свои боссы варианта акта (docs/design/11 §6.1, Monsters/*Bosses.cs).</summary>
    protected abstract IEnumerable<EncounterModel> BossPool { get; }

    /// <summary>Редкая элита акта (15 %, <see cref="ActRules.RareEliteChancePercent"/>).</summary>
    public abstract EncounterModel? RareElite { get; }

    public override IEnumerable<EncounterModel> GenerateAllEncounters() =>
        BorrowedEncounters.Concat(OwnElites).Concat(BossPool).ToList();

    /// <summary>Свои события актов — этап 8; пока общие события игры и мода (BaseLib добавит события с Acts = [этот акт]).</summary>
    public override IEnumerable<EventModel> AllEvents => [];

    public override MapPointTypeCounts GetMapPointTypes(Rng mapRng)
    {
        // Карта шире — больше «?» и лагерей (13 §4: ~65–75 узлов против ~50).
        var restCount = mapRng.NextGaussianInt(7, 1, 6, 8);
        var unknownCount = MapPointTypeCounts.StandardRandomUnknownCount(mapRng) + 2;
        return new MapPointTypeCounts(unknownCount, restCount);
    }
}

/// <summary>Акт 2а — Ледяные Курганы: Стужа, Тьма курганов, Запертые склепы. Курганные мертвецы.</summary>
public sealed class FrozenBarrows() : RequiemActModel(1)
{
    public override ActKind Kind => ActKind.FrozenBarrows;
    protected override string LookId => "underdocks";

    public override Color MapTraveledColor => new("1A2230");
    public override Color MapUntraveledColor => new("5C6E86");
    public override Color MapBgColor => new("9DB0C4");
    public override string[] BgMusicOptions => ["event:/music/act2_a1_v2", "event:/music/act2_a2_v2"];
    public override string[] MusicBankPaths => ["res://banks/desktop/act2_a1.bank", "res://banks/desktop/act2_a2.bank"];
    public override string AmbientSfx => "event:/sfx/ambience/act2_ambience";
    public override string ChestSpineResourcePath => "res://animations/backgrounds/treasure_room/chest_room_act_2_skel_data.tres";
    public override string ChestSpineSkinNameNormal => "act2";
    public override string ChestSpineSkinNameStroke => "act2_stroke";
    public override string ChestOpenSfx => "event:/sfx/ui/treasure/treasure_act2";

    public override IEnumerable<AncientEventModel> AllAncients =>
        [ModelDb.AncientEvent<Orobas>(), ModelDb.AncientEvent<Pael>(), ModelDb.AncientEvent<Tezcatara>()];

    protected override IEnumerable<EncounterModel> BorrowedEncounters =>
    [
        ModelDb.Encounter<CorpseSlugsWeak>(), ModelDb.Encounter<ToadpolesWeak>(), ModelDb.Encounter<NibbitsWeak>(),
        ModelDb.Encounter<LivingFogNormal>(), ModelDb.Encounter<HauntedShipNormal>(), ModelDb.Encounter<FossilStalkerNormal>(),
        ModelDb.Encounter<CultistsNormal>(), ModelDb.Encounter<CorpseSlugsNormal>(), ModelDb.Encounter<InkletsNormal>(),
        ModelDb.Encounter<FogmogNormal>(), ModelDb.Encounter<SlitheringStranglerNormal>(), ModelDb.Encounter<MawlerNormal>(),
    ];

    protected override IEnumerable<EncounterModel> OwnElites =>
        [ModelDb.Encounter<TombGuardianElite>(), ModelDb.Encounter<GlacierGiantElite>(), ModelDb.Encounter<PhantomBladeElite>()];

    protected override IEnumerable<EncounterModel> BossPool =>
        [ModelDb.Encounter<KingUnderBarrowBoss>(), ModelDb.Encounter<BoneFrostWyrmBoss>(), ModelDb.Encounter<BarrowWailerBoss>()];

    public override EncounterModel RareElite => ModelDb.Encounter<FirstSnowPrinceElite>();
}

/// <summary>Акт 2б — Заводные Глубины: Пар и вентили, Перегрев. Заводные стражи и береговые грабители.</summary>
public sealed class ClockworkDepths() : RequiemActModel(1)
{
    public override ActKind Kind => ActKind.ClockworkDepths;
    protected override string LookId => "hive";

    public override Color MapTraveledColor => new("2B1D12");
    public override Color MapUntraveledColor => new("8A6A3E");
    public override Color MapBgColor => new("B89A63");
    public override string[] BgMusicOptions => ["event:/music/act2_a1_v2", "event:/music/act2_a2_v2"];
    public override string[] MusicBankPaths => ["res://banks/desktop/act2_a1.bank", "res://banks/desktop/act2_a2.bank"];
    public override string AmbientSfx => "event:/sfx/ambience/act2_ambience";
    public override string ChestSpineResourcePath => "res://animations/backgrounds/treasure_room/chest_room_act_2_skel_data.tres";
    public override string ChestSpineSkinNameNormal => "act2";
    public override string ChestSpineSkinNameStroke => "act2_stroke";
    public override string ChestOpenSfx => "event:/sfx/ui/treasure/treasure_act2";

    public override IEnumerable<AncientEventModel> AllAncients =>
        [ModelDb.AncientEvent<Orobas>(), ModelDb.AncientEvent<Pael>(), ModelDb.AncientEvent<Tezcatara>()];

    protected override IEnumerable<EncounterModel> BorrowedEncounters =>
    [
        ModelDb.Encounter<SludgeSpinnerWeak>(), ModelDb.Encounter<SeapunkWeak>(), ModelDb.Encounter<ShrinkerBeetleWeak>(),
        ModelDb.Encounter<FuzzyWurmCrawlerWeak>(),
        ModelDb.Encounter<CubexConstructNormal>(), ModelDb.Encounter<PunchConstructNormal>(), ModelDb.Encounter<RubyRaidersNormal>(),
        ModelDb.Encounter<GremlinMercNormal>(), ModelDb.Encounter<TwoTailedRatsNormal>(), ModelDb.Encounter<SewerClamNormal>(),
        ModelDb.Encounter<SeapunkNormal>(), ModelDb.Encounter<SnappingJaxfruitNormal>(), ModelDb.Encounter<FlyconidNormal>(),
    ];

    protected override IEnumerable<EncounterModel> OwnElites =>
        [ModelDb.Encounter<DeepCenturionElite>(), ModelDb.Encounter<SpiderMatriarchElite>(), ModelDb.Encounter<RenegadeMechanicElite>()];

    protected override IEnumerable<EncounterModel> BossPool =>
        [ModelDb.Encounter<ClockworkColossusBoss>(), ModelDb.Encounter<AutomatonArchitectBoss>(), ModelDb.Encounter<BoilerWyrmBoss>()];

    public override EncounterModel RareElite => ModelDb.Encounter<ReactorHeartElite>();
}

/// <summary>Акт 4а — Кровавый Двор: День и ночь. Вампиры.</summary>
public sealed class BloodCourt() : RequiemActModel(3)
{
    public override ActKind Kind => ActKind.BloodCourt;
    protected override string LookId => "overgrowth";

    public override Color MapTraveledColor => new("2A0E12");
    public override Color MapUntraveledColor => new("7A3A40");
    public override Color MapBgColor => new("A86A6A");

    public override IEnumerable<AncientEventModel> AllAncients =>
        [ModelDb.AncientEvent<Nonupeipe>(), ModelDb.AncientEvent<Tanx>(), ModelDb.AncientEvent<Vakuu>()];

    protected override IEnumerable<EncounterModel> BorrowedEncounters =>
    [
        ModelDb.Encounter<BowlbugsWeak>(), ModelDb.Encounter<ThievingHopperWeak>(), ModelDb.Encounter<TunnelerWeak>(),
        ModelDb.Encounter<HunterKillerNormal>(), ModelDb.Encounter<LouseProgenitorNormal>(), ModelDb.Encounter<MytesNormal>(),
        ModelDb.Encounter<OvicopterNormal>(), ModelDb.Encounter<TheObscuraNormal>(), ModelDb.Encounter<ChompersNormal>(),
        ModelDb.Encounter<TheLostAndForgottenNormal>(), ModelDb.Encounter<SlimedBerserkerNormal>(),
    ];

    protected override IEnumerable<EncounterModel> OwnElites =>
        [ModelDb.Encounter<BloodNobleElite>(), ModelDb.Encounter<GargoyleWardenElite>(), ModelDb.Encounter<VampireHunterElite>()];

    protected override IEnumerable<EncounterModel> BossPool =>
        [ModelDb.Encounter<CrimsonCountessBoss>(), ModelDb.Encounter<EclipseWardenBoss>(), ModelDb.Encounter<ChaliceHierophantBoss>()];

    public override EncounterModel RareElite => ModelDb.Encounter<LordsBrideElite>();
}

/// <summary>Акт 4б — Звёздная Башня: Лей-линии (порталы — этап 8). Отступники-маги.</summary>
public sealed class StarTower() : RequiemActModel(3)
{
    public override ActKind Kind => ActKind.StarTower;
    protected override string LookId => "glory";

    public override Color MapTraveledColor => new("10142A");
    public override Color MapUntraveledColor => new("4A5A8C");
    public override Color MapBgColor => new("8C9AC8");

    public override IEnumerable<AncientEventModel> AllAncients =>
        [ModelDb.AncientEvent<Nonupeipe>(), ModelDb.AncientEvent<Tanx>(), ModelDb.AncientEvent<Vakuu>()];

    protected override IEnumerable<EncounterModel> BorrowedEncounters =>
    [
        ModelDb.Encounter<ExoskeletonsWeak>(), ModelDb.Encounter<ScrollsOfBitingWeak>(), ModelDb.Encounter<TunnelerWeak>(),
        ModelDb.Encounter<ExoskeletonsNormal>(), ModelDb.Encounter<BowlbugsNormal>(), ModelDb.Encounter<SlumberingBeetleNormal>(),
        ModelDb.Encounter<SpinyToadNormal>(), ModelDb.Encounter<TheObscuraNormal>(), ModelDb.Encounter<ScrollsOfBitingNormal>(),
        ModelDb.Encounter<OwlMagistrateNormal>(), ModelDb.Encounter<GlobeHeadNormal>(),
    ];

    protected override IEnumerable<EncounterModel> OwnElites =>
        [ModelDb.Encounter<SorceressIlvarreElite>(), ModelDb.Encounter<RenegadeSummonerElite>(), ModelDb.Encounter<MirrorWardenElite>()];

    protected override IEnumerable<EncounterModel> BossPool =>
        [ModelDb.Encounter<AstrolabeKeeperBoss>(), ModelDb.Encounter<LeyWyrmBoss>(), ModelDb.Encounter<ArchmageSimulacrumBoss>()];

    public override EncounterModel RareElite => ModelDb.Encounter<StarExaminerElite>();
}

/// <summary>
/// ★ Безымянный предел (docs/design/13): короткая карта (магазин, лагерь, элита-эхо, лагерь) и Тень Без Имени.
/// Не выпадает сам (номер акта −1): дописывается в конец забега после пятого супербосса (<see cref="NamelessLimitPatch"/>).
/// </summary>
public sealed class NamelessLimit() : CustomActModel(-1)
{
    protected override string CustomMapTopBgPath => ImageHelper.GetImagePath("packed/map/map_bgs/glory/map_top_glory.png");
    protected override string CustomMapMidBgPath => ImageHelper.GetImagePath("packed/map/map_bgs/glory/map_middle_glory.png");
    protected override string CustomMapBotBgPath => ImageHelper.GetImagePath("packed/map/map_bgs/glory/map_bottom_glory.png");
    protected override string CustomRestSiteBackgroundPath => SceneHelper.GetScenePath("rest_site/glory_rest_site");

    public override Color MapTraveledColor => new("0A0A0F");
    public override Color MapUntraveledColor => new("3A3346");
    public override Color MapBgColor => new("6A6278");

    protected override int BaseNumberOfRooms => ActRules.Rooms(ActKind.NamelessLimit);
    protected override int NumberOfWeakEncounters => 1;

    public override IEnumerable<AncientEventModel> AllAncients =>
        [ModelDb.AncientEvent<Nonupeipe>(), ModelDb.AncientEvent<Tanx>(), ModelDb.AncientEvent<Vakuu>()];

    public override IEnumerable<EventModel> AllEvents => [];

    public override IEnumerable<EncounterModel> GenerateAllEncounters() =>
    [
        // Обычные бои нужны ванильной раскладке комнат, хотя на карте предела их нет.
        ModelDb.Encounter<ScrollsOfBitingWeak>(), ModelDb.Encounter<FabricatorNormal>(),
        ModelDb.Encounter<EchoKnightElite>(),
        ModelDb.Encounter<NamelessShadowBoss>(),
    ];

    protected override ActMap? CustomCreateMap(RunState runState, bool replaceTreasureWithElites) => new NamelessLimitMap(runState);
}

/// <summary>Карта Безымянного предела: одна тропа — магазин, лагерь, элита-эхо, лагерь — и Тень.</summary>
public sealed class NamelessLimitMap : ActMap
{
    private static readonly MapPointType[] Path = [MapPointType.Shop, MapPointType.RestSite, MapPointType.Elite, MapPointType.RestSite];
    private const int Middle = 3;

    public override MapPoint BossMapPoint { get; }
    public override MapPoint StartingMapPoint { get; }
    protected override MapPoint?[,] Grid { get; }

    public NamelessLimitMap(IRunState runState)
    {
        Grid = new MapPoint[ActRules.VanillaMapWidth, Path.Length + 1];
        BossMapPoint = new MapPoint(Middle, GetRowCount()) { PointType = MapPointType.Boss };
        StartingMapPoint = new MapPoint(Middle, 0) { PointType = MapPointType.Ancient };
        for (var i = 0; i < Path.Length; i++)
        {
            var point = new MapPoint(Middle, i + 1) { PointType = Path[i] };
            Grid[Middle, i + 1] = point;
            if (i > 0) Grid[Middle, i]!.AddChildPoint(point);
        }
        startMapPoints.Add(Grid[Middle, 1]!);
        Grid[Middle, GetRowCount() - 1]!.AddChildPoint(BossMapPoint);
        StartingMapPoint.AddChildPoint(Grid[Middle, 1]!);
    }
}

/// <summary>Вид акта забега по модели (ванильные акты — по типу, новые — по <see cref="RequiemActModel.Kind"/>).</summary>
public static class ActKinds
{
    public static ActKind Of(ActModel? act) => act switch
    {
        RequiemActModel r => r.Kind,
        NamelessLimit => ActKind.NamelessLimit,
        Overgrowth or Underdocks => ActKind.Foothills,
        Hive => ActKind.Hive,
        Glory => ActKind.Glory,
        _ => ActKind.Other,
    };

    public static ActKind Current(IRunState? run) => run == null || run.Acts.Count == 0 ? ActKind.Other : Of(run.Act);

    /// <summary>Полный забег из пяти актов (+ Безымянный предел).</summary>
    public static bool IsFullRun(IRunState? run) => run != null && run.Acts.Count >= ActRules.FullRunActs;

    public static ActRule CurrentRule(IRunState? run) => ActRules.RuleOf(Current(run));

    /// <summary>Ступень 0..2 текущего акта для таблиц, рассчитанных на три акта (лавки, Пантеон).</summary>
    public static int Tier(IRunState run) => ActRules.VanillaTier(run.CurrentActIndex, run.Acts.Count);

    /// <summary>Слот 0..4 текущего акта (в «Коротком забеге» 0/2/4).</summary>
    public static int Slot(IRunState run) => ActRules.SlotOf(run.CurrentActIndex, run.Acts.Count);
}
