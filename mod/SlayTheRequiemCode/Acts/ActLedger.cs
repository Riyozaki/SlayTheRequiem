using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rollback;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Shops;
using SlayTheRequiem.SlayTheRequiemCode.UI;

namespace SlayTheRequiem.SlayTheRequiemCode.Acts;

/// <summary>
/// Дорожная карта (docs/design/13) — служебная стартовая реликвия: состояние актов забега, которое должно пережить
/// сохранение: побеждённые супербоссы, ответы Багрового портала, Врата Бездны на карте, Когтистые ключи и открытые склепы,
/// Тепло (Стужа), разгрузка вентилей (Перегрев), встреченные редкие элиты. Счётчик — побеждённые супербоссы.
/// Отряд в коопе читает объединение по всем героям (<see cref="ActRunState"/>).
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ActLedger : RequiemRelic
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => false;
    public override bool ShowCounter => true;
    public override int DisplayAmount => ActRules.CountSuperbosses(_superbossMask);

    public const string LocKey = "SLAYTHEREQUIEM-ACT_LEDGER";

    public static ActLedger? Of(Player? player) => player?.Relics.OfType<ActLedger>().FirstOrDefault();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Superbosses", 0),
        new DynamicVar("Keys", 0),
        new DynamicVar("Warmth", 0),
        new DynamicVar("Relief", 0),
    ];

    private int _superbossMask;
    private int _portalMask;
    private int _portalAskedMask;
    private int _rareEliteMask;
    private int _brideMask;
    private int _superbossCandles;
    private int[] _gates = [];
    private int[] _openedVaults = [];
    private int[] _turnedValves = [];
    private int _clawKeys;
    private int _clawKeysAct;      // акт (индекс + 1), за который считаются ключи; 0 — ещё не было
    private int _clawKeysFound;    // ключей найдено в этом акте (не больше ActRules.MaxClawKeysPerAct)
    private int _warmthFights;
    private int _valveRelief;
    private bool _namelessAdded;
    private int _mapPlanAct;
    private string _mapPlanText = string.Empty;
    private int _nightSightAct;
    private int _lastRestAct;
    private int _bannerAct;
    private int[] _nodeVisits = [];

    /// <summary>Побеждённые супербоссы — биты <see cref="Superboss"/>.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int SuperbossMask { get => _superbossMask; set { AssertMutable(); _superbossMask = value; SyncVars(); } }

    /// <summary>Порталы, в которые герой согласился войти (бит — индекс акта).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PortalMask { get => _portalMask; set { AssertMutable(); _portalMask = value; } }

    /// <summary>Порталы, на которые ответ уже дан (бит — индекс акта).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PortalAskedMask { get => _portalAskedMask; set { AssertMutable(); _portalAskedMask = value; } }

    /// <summary>Акты, в которых уже встречена редкая элита.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int RareEliteMask { get => _rareEliteMask; set { AssertMutable(); _rareEliteMask = value; } }

    /// <summary>Акты, где побеждена Невеста Владыки (Владыка Кровавого Двора −10 % ♥).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BrideMask { get => _brideMask; set { AssertMutable(); _brideMask = value; } }

    /// <summary>Свечей Реквиема получено за супербоссов (не больше <see cref="ActRules.MaxSuperbossCandles"/>).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int SuperbossCandles { get => _superbossCandles; set { AssertMutable(); _superbossCandles = Math.Max(0, value); } }

    /// <summary>Врата Бездны: <see cref="MapMarks.Encode"/>(акт, колонка, ряд).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] Gates { get => _gates; set { AssertMutable(); _gates = value ?? []; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] OpenedVaults { get => _openedVaults; set { AssertMutable(); _openedVaults = value ?? []; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] TurnedValves { get => _turnedValves; set { AssertMutable(); _turnedValves = value ?? []; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ClawKeys { get => _clawKeys; set { AssertMutable(); _clawKeys = Math.Max(0, value); SyncVars(); } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ClawKeysAct { get => _clawKeysAct; set { AssertMutable(); _clawKeysAct = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ClawKeysFound { get => _clawKeysFound; set { AssertMutable(); _clawKeysFound = Math.Max(0, value); } }

    /// <summary>Боёв без Стужи после «Отогреться».</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int WarmthFights { get => _warmthFights; set { AssertMutable(); _warmthFights = Math.Max(0, value); SyncVars(); } }

    /// <summary>Боёв без Перегрева после вентиля.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ValveRelief { get => _valveRelief; set { AssertMutable(); _valveRelief = Math.Max(0, value); SyncVars(); } }

    /// <summary>Безымянный предел уже дописан к забегу.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool NamelessAdded { get => _namelessAdded; set { AssertMutable(); _namelessAdded = value; } }

    /// <summary>Акт (индекс + 1), для которого записан <see cref="MapPlanText"/>.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MapPlanAct { get => _mapPlanAct; set { AssertMutable(); _mapPlanAct = value; } }

    /// <summary>План вариаций путей карты акта (<see cref="MapPlan.Encode"/>, M6 п. 1).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string MapPlanText { get => _mapPlanText; set { AssertMutable(); _mapPlanText = value ?? string.Empty; } }

    /// <summary>Зелье Ночного зрения действует до конца этого акта (индекс + 1).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int NightSightAct { get => _nightSightAct; set { AssertMutable(); _nightSightAct = value; } }

    /// <summary>«Последний привал» взят в этом акте (индекс + 1): босс акта +10 % ♥.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastRestAct { get => _lastRestAct; set { AssertMutable(); _lastRestAct = value; } }

    /// <summary>«Небесный щит» Знамени Небесных действует до конца этого акта (индекс + 1).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BannerAct { get => _bannerAct; set { AssertMutable(); _bannerAct = value; } }

    /// <summary>Подтипы пройденных узлов, решённые при входе (лавки, Таверна, «?»): код клетки × 32 + подтип — для значков.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] NodeVisits { get => _nodeVisits; set { AssertMutable(); _nodeVisits = value ?? []; } }

    /// <summary>Запомнить подтип узла, в который вошёл герой (значок пройденного узла на карте).</summary>
    public void RecordVisit(IRunState run, NodeSubtype subtype)
    {
        if (run.CurrentMapCoord is not { } at) return;
        var code = MapMarks.Encode(run.CurrentActIndex, at.col, at.row);
        var entry = code * 32 + (int)subtype;
        if (_nodeVisits.Any(v => v / 32 == code)) NodeVisits = [.. _nodeVisits.Where(v => v / 32 != code), entry];
        else NodeVisits = [.. _nodeVisits, entry];
        UI.MapVisuals.RefreshSoon();
    }

    /// <summary>Подтип пройденного узла, если он записан.</summary>
    public NodeSubtype? VisitedSubtype(int code)
    {
        foreach (var v in _nodeVisits)
            if (v / 32 == code && Enum.IsDefined(typeof(NodeSubtype), v % 32)) return (NodeSubtype)(v % 32);
        return null;
    }

    public bool Has(int[] list, int code) => Array.IndexOf(list, code) >= 0;

    public void AddGate(int code) { if (!Has(_gates, code)) Gates = [.. _gates, code]; }
    public void OpenVault(int code) { if (!Has(_openedVaults, code)) OpenedVaults = [.. _openedVaults, code]; }
    public void TurnValve(int code)
    {
        if (Has(_turnedValves, code)) return;
        TurnedValves = [.. _turnedValves, code];
        UI.MapVisuals.RefreshSoon(); // трубы этого вентиля переключились (M6)
    }

    /// <summary>Когтистый ключ с элиты/Тайника: не больше <see cref="ActRules.MaxClawKeysPerAct"/> за акт.</summary>
    public bool TryFindClawKey(int actIndex)
    {
        if (_clawKeysAct != actIndex + 1)
        {
            ClawKeysAct = actIndex + 1;
            ClawKeysFound = 0;
        }
        if (_clawKeysFound >= ActRules.MaxClawKeysPerAct) return false;
        ClawKeysFound++;
        ClawKeys++;
        Flash();
        UI.MapVisuals.RefreshSoon(); // Запертые врата теперь открываемы
        return true;
    }

    public void Defeat(Superboss boss)
    {
        SuperbossMask |= 1 << (int)boss;
        Flash();
    }

    /// <summary>
    /// Награда супербосса (11 §6): редкий артефакт, Чёрный Камень душ и Свеча Реквиема (не больше одной за забег,
    /// на «Проклятом» — нет). Каждый герой получает свою — реликвия есть у всех.
    /// </summary>
    // ───── правила актов: отметки на карте, вход в узел, после боя ─────

    public override Task AfterMapGenerated(ActMap map, int actIndex)
    {
        if (Owner?.RunState is not { } run || !RunSettings.EnableRequiemSystems || actIndex >= run.Acts.Count) return Task.CompletedTask;
        // Вариации путей (M6 п. 1): план строится один раз для карты, каждый герой хранит его копию.
        var plan = MapGraph.Prepare(run, map, actIndex);
        MapPlanAct = actIndex + 1;
        MapPlanText = plan.Encode();
        foreach (var code in ActMarks.For(run, map, actIndex).Keys) ActMarks.Show(map, code);
        UI.MapVisuals.RefreshSoon();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 📖 Библиотека (M5, 13 §4.2): часть «?» в актах 2–5 — Библиотека. Бросок от зерна и этажа (одинаков у всех),
    /// подменяет событие только Дорожная карта первого игрока; Святилища, Камни отлива и события форм не трогает.
    /// </summary>
    public override EventModel ModifyNextEvent(EventModel currentEvent)
    {
        if (!RunSettings.EnableRequiemSystems || !IsMutable || Owner?.RunState is not { } run) return currentEvent;
        if (run.Players.FirstOrDefault() != Owner) return currentEvent;
        if (currentEvent is Pantheon.PantheonEvent or Transformations.FormEvent or Events.NodeEvent) return currentEvent;
        // Знамя Небесных (Слава) — всегда Святилище.
        if (ActMarks.At(run, run.CurrentMapCoord) == MapMark.CelestialBanner && RunSettings.EnablePantheon)
            return ModelDb.Event<Pantheon.ShrineEvent>();
        var roll = (int)(ShopRules.Fnv1a((run.Rng.StringSeed ?? string.Empty) + "|library", run.TotalFloor * 13 + run.CurrentActIndex) % 100u);
        return NodeRules.RollLibrary(ActKinds.Slot(run), roll) ? ModelDb.Event<Events.LibraryEvent>() : currentEvent;
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (Owner?.RunState is not { } run || !RunSettings.EnableRequiemSystems || run.CurrentMapCoord is not { } coord) return;
        var code = MapMarks.Encode(run.CurrentActIndex, coord.col, coord.row);
        switch (ActMarks.At(run, coord))
        {
            case MapMark.Valve when !Has(_turnedValves, code):
                TurnValve(code);
                ValveRelief = ActRules.ValveReliefFights;
                Flash();
                break;
            case MapMark.LockedVault when !Has(_openedVaults, code) && _clawKeys > 0:
                ClawKeys--;
                OpenVault(code);
                Flash();
                await PlayerCmd.GainGold(ActRules.VaultGold, Owner);
                break;
            case MapMark.Cache when !Has(_openedVaults, code):
                await OpenCache(run, code);
                break;
            case MapMark.CelestialBanner when _bannerAct != run.CurrentActIndex + 1 && Owner != null
                                              && Pantheon.Faith.Effective(Owner) == Piety.Righteous:
                BannerAct = run.CurrentActIndex + 1;
                Flash();
                if (Pantheon.CelestialBlessing.Of(Owner) is { } blessing) await blessing.SyncMaxHp();
                break;
        }
        // «Последний привал» (13 §4.1): Камень души, но босс акта получит +10 % ♥.
        if (MapGraph.IsLastRest(run, coord) && _lastRestAct != run.CurrentActIndex + 1 && Owner != null)
        {
            LastRestAct = run.CurrentActIndex + 1;
            Flash();
            if (RequiemMode.IsInSystem(Owner)) HeroRecord.Of(Owner)?.AddStone(1);
        }
        if (room is EventRoom events && NodeRules.FromUnknownRoll(events.CanonicalEvent switch
            {
                Pantheon.ShrineEvent => 1,
                Pantheon.DarkShrineEvent => 2,
                Pantheon.EbbStonesEvent => 3,
                _ => 0,
            }, events.CanonicalEvent is Events.LibraryEvent) is { } unknownSubtype)
            RecordVisit(run, unknownSubtype);
        UI.MapVisuals.RefreshSoon();
        if (ActMarks.NewGate(run, coord) is { } gate)
        {
            AddGate(gate);
            if (run.Map != null) ActMarks.Show(run.Map, gate);
        }
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (Owner?.RunState is not { } run) return;
        if (RunSettings.EnableRequiemSystems)
        {
            var rule = ActKinds.CurrentRule(run);
            if (rule == ActRule.Frost && _warmthFights > 0) WarmthFights--;
            if (rule == ActRule.Steam && _valveRelief > 0) ValveRelief--;
            if (rule == ActRule.Frost && room.RoomType == RoomType.Elite) TryFindClawKey(run.CurrentActIndex);
            if (rule == ActRule.DayNight && room.RoomType != RoomType.Boss && run.CurrentMapCoord is { } c && ActRules.IsNight(c.row))
                room.AddExtraReward(Owner, new GoldReward(ActRules.NightGoldBonus, Owner));
            if (run.Map != null && run.CurrentMapCoord is { } at && ActMarks.At(run, at) == MapMark.AbyssGate)
                ActMarks.Hide(run.Map, MapMarks.Encode(run.CurrentActIndex, at.col, at.row));
            await NodeVictory(run, room);
        }
        await SuperbossVictory(room);
    }

    // ───── подтипы узлов (M5, 13 §4.2) ─────

    /// <summary>
    /// ⚿ Тайник: без выбора (одинаково у всех в коопе) — запертый сундук открывается Когтистым ключом (золото + зелье),
    /// без ключа остаётся только обычный сундук; сундук с ловушкой бьёт и отдаёт золото. Вид — от зерна узла.
    /// </summary>
    private async Task OpenCache(IRunState run, int code)
    {
        if (Owner == null) return;
        var slot = ActKinds.Slot(run);
        var trapped = NodeRules.CacheTrapped((int)(ShopRules.Fnv1a(run.Rng.StringSeed ?? string.Empty, code) % 100u));
        if (trapped)
        {
            OpenVault(code);
            Flash();
            if (LocalContext.IsMe(Owner)) RequiemSfx.Play(SfxEvents.CacheTrap);
            await CreatureCmd.Damage(new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(), Owner.Creature,
                NodeRules.CacheTrap(slot), MegaCrit.Sts2.Core.ValueProps.ValueProp.Unblockable | MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered,
                null, null);
            await PlayerCmd.GainGold(NodeRules.CacheGold(slot), Owner);
            return;
        }
        if (_clawKeys <= 0) return;
        ClawKeys--;
        OpenVault(code);
        Flash();
        if (LocalContext.IsMe(Owner)) RequiemSfx.Play(SfxEvents.CacheOpen);
        await PlayerCmd.GainGold(NodeRules.CacheGold(slot), Owner);
        await RewardsCmd.OfferCustom(Owner, [new PotionReward(Owner)]);
    }

    /// <summary>Награды Логова (реагенты, броня из шкуры), Испытания (реликвия) и Засады (золото).</summary>
    private async Task NodeVictory(IRunState run, CombatRoom room)
    {
        if (Owner == null || run.CurrentMapCoord is not { } at) return;
        var slot = ActKinds.Slot(run);
        // Конец Опасной тропы (M6, 13 §4.1): после боя — Тайник (золото и зелье).
        if (MapGraph.IsDangerEnd(run, at) && room.RoomType is RoomType.Monster or RoomType.Elite)
        {
            room.AddExtraReward(Owner, new GoldReward(NodeRules.CacheGold(slot), Owner));
            room.AddExtraReward(Owner, new PotionReward(Owner));
        }
        switch (ActMarks.SubtypeAt(run, at))
        {
            case NodeSubtype.Lair when room.RoomType == RoomType.Elite:
                if (HerbalistPouch.Of(Owner) is { } pouch)
                {
                    pouch.Add(Reagent.Bloodmoss, NodeRules.LairReagents(slot));
                    pouch.Add(Reagent.DragonScale, 1);
                }
                var (min, max) = NodeRules.LairHideTiers(slot);
                var hide = ShopStock.PickEquipment(Owner, min, max, e => e.Slot is EquipSlot.Body or EquipSlot.Head or EquipSlot.Hands
                    or EquipSlot.Feet, new HashSet<Type>());
                if (hide != null) await RelicCmd.Obtain(hide.ToMutable(), Owner);
                break;
            case NodeSubtype.Trial when room.RoomType == RoomType.Elite:
                room.AddExtraReward(Owner, new RelicReward(slot >= 3 ? RelicRarity.Rare : RelicRarity.Uncommon, Owner));
                break;
            case NodeSubtype.Ambush when room.RoomType == RoomType.Monster:
                room.AddExtraReward(Owner, new GoldReward(NodeRules.AmbushGold(slot), Owner));
                break;
        }
    }

    private async Task SuperbossVictory(CombatRoom room)
    {
        if (room.Encounter is not SuperbossEncounter boss || Owner == null) return;
        Defeat(boss.Kind);
        room.AddExtraReward(Owner, new RelicReward(RelicRarity.Rare, Owner));
        if (RequiemMode.IsInSystem(Owner)) HeroRecord.Of(Owner)?.AddStone(3);
        if (_superbossCandles < ActRules.MaxSuperbossCandles
            && RequiemLevelRules.CandlesAllowed(RequiemLevel.Of(Owner))
            && MemoryStoneRelic.Of(Owner) is { } stone)
        {
            stone.BonusCandles++;
            SuperbossCandles++;
        }
        await Task.CompletedTask;
    }

    private void SyncVars()
    {
        if (DynamicVars.TryGetValue("Superbosses", out var s)) s.BaseValue = ActRules.CountSuperbosses(_superbossMask);
        if (DynamicVars.TryGetValue("Keys", out var k)) k.BaseValue = _clawKeys;
        if (DynamicVars.TryGetValue("Warmth", out var w)) w.BaseValue = _warmthFights;
        if (DynamicVars.TryGetValue("Relief", out var r)) r.BaseValue = _valveRelief;
        InvokeDisplayAmountChanged();
    }
}
