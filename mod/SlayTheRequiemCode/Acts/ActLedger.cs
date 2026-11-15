using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Commands;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rollback;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

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

    public bool Has(int[] list, int code) => Array.IndexOf(list, code) >= 0;

    public void AddGate(int code) { if (!Has(_gates, code)) Gates = [.. _gates, code]; }
    public void OpenVault(int code) { if (!Has(_openedVaults, code)) OpenedVaults = [.. _openedVaults, code]; }
    public void TurnValve(int code) { if (!Has(_turnedValves, code)) TurnedValves = [.. _turnedValves, code]; }

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
        if (Owner?.RunState is { } run && RequiemConfig.EnableRequiemSystems && actIndex < run.Acts.Count)
            foreach (var code in ActMarks.For(run, map, actIndex).Keys) ActMarks.Show(map, code);
        return Task.CompletedTask;
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (Owner?.RunState is not { } run || !RequiemConfig.EnableRequiemSystems || run.CurrentMapCoord is not { } coord) return;
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
        }
        if (ActMarks.NewGate(run, coord) is { } gate)
        {
            AddGate(gate);
            if (run.Map != null) ActMarks.Show(run.Map, gate);
        }
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (Owner?.RunState is not { } run) return;
        if (RequiemConfig.EnableRequiemSystems)
        {
            var rule = ActKinds.CurrentRule(run);
            if (rule == ActRule.Frost && _warmthFights > 0) WarmthFights--;
            if (rule == ActRule.Steam && _valveRelief > 0) ValveRelief--;
            if (rule == ActRule.Frost && room.RoomType == RoomType.Elite) TryFindClawKey(run.CurrentActIndex);
            if (rule == ActRule.DayNight && room.RoomType != RoomType.Boss && run.CurrentMapCoord is { } c && ActRules.IsNight(c.row))
                room.AddExtraReward(Owner, new GoldReward(ActRules.NightGoldBonus, Owner));
            if (run.Map != null && run.CurrentMapCoord is { } at && ActMarks.At(run, at) == MapMark.AbyssGate)
                ActMarks.Hide(run.Map, MapMarks.Encode(run.CurrentActIndex, at.col, at.row));
        }
        await SuperbossVictory(room);
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
