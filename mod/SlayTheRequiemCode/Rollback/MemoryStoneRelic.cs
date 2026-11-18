using BaseLib.Utils;
using HarmonyLib;
using SlayTheRequiem.SlayTheRequiemCode.Acts;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Rollback;

/// <summary>
/// Свечи Реквиема (docs/design/15) — служебная стартовая реликвия: сколько Свечей потрачено, сколько было откатов,
/// Шрамы и «Месть» (+10 % урона по тем, кто убил героя, до конца повторной попытки акта).
/// Счётчик — оставшиеся Свечи (∞ показывается как 0 и пометкой в описании).
/// Состояние реликвии само попадает в Отпечаток; после возврата <see cref="MemoryStone"/> переписывает счётчики
/// значениями из погибшей попытки, чтобы откат не возвращал потраченные Свечи.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class MemoryStoneRelic : RequiemRelic
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => false;
    public override bool ShowCounter => true;
    public override int DisplayAmount => CandlesLeft == MemoryStoneRules.Infinite ? 0 : CandlesLeft;

    public static MemoryStoneRelic? Of(Player? player) => player?.Relics.OfType<MemoryStoneRelic>().FirstOrDefault();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Candles", 0),
        new DynamicVar("Rollbacks", 0),
        new DynamicVar("FleshScars", 0),
    ];

    private int _candlesUsed;
    private int _bonusCandles;
    private int _cryptCandles;
    private int _rollbacks;
    private int _fleshScars;
    private int _soulScars;
    private int _purseScars;
    private string _revengeTargets = string.Empty;
    private int _revengeAct1;
    private int _mapRerollAct1;
    private int _pendingFlesh;
    private int _pendingSoul;
    private int _pendingPurse;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CandlesUsed { get => _candlesUsed; set { AssertMutable(); _candlesUsed = value; SyncVars(); } }

    /// <summary>Дополнительные Свечи (супербосс, «Свеча в склепе» — этапы 4–6).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BonusCandles { get => _bonusCandles; set { AssertMutable(); _bonusCandles = value; SyncVars(); } }

    /// <summary>Свечей из «Свечи в склепе» (M5 п. 6): не больше <see cref="MemoryStoneRules.MaxCryptCandles"/> за забег.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CryptCandles { get => _cryptCandles; set { AssertMutable(); _cryptCandles = Math.Max(0, value); } }

    /// <summary>Можно ли зажечь Свечу в склепе (Камень включён, Свечи конечны, не «Проклятый», ещё не брали).</summary>
    public bool CanTakeCryptCandle =>
        MemoryStoneRules.CanTakeCryptCandle(MemoryStone.Enabled, true, RequiemLevelRules.CandlesAllowed(RequiemLevel), CandlesLeft,
            _cryptCandles);

    /// <summary>+1 Свеча из склепа.</summary>
    public void TakeCryptCandle()
    {
        if (!CanTakeCryptCandle) return;
        CryptCandles++;
        BonusCandles++;
        Flash();
        InvokeDisplayAmountChanged();
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Rollbacks { get => _rollbacks; set { AssertMutable(); _rollbacks = value; SyncVars(); } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FleshScars { get => _fleshScars; set { AssertMutable(); _fleshScars = value; SyncVars(); } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int SoulScars { get => _soulScars; set { AssertMutable(); _soulScars = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PurseScars { get => _purseScars; set { AssertMutable(); _purseScars = value; } }

    /// <summary>Кому мстим: имена типов монстров и фракции через запятую («Monster:TombGuardian,Faction:undead»).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string RevengeTargets { get => _revengeTargets; set { AssertMutable(); _revengeTargets = value ?? string.Empty; } }

    /// <summary>
    /// Акт повторной попытки, в котором действует Месть, + 1 (0 — Мести нет). Хранится со сдвигом: значение 0 по
    /// умолчанию не записывается в сохранение, и «акт 1» иначе терялся бы после «Сохранить и выйти».
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int RevengeAct1 { get => _revengeAct1; set { AssertMutable(); _revengeAct1 = value; } }

    /// <summary>Акт + 1, карту которого нужно построить заново после возврата (0 — не нужно), docs/design/15 §1.3.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MapRerollAct1 { get => _mapRerollAct1; set { AssertMutable(); _mapRerollAct1 = value; } }

    public int RevengeAct => _revengeAct1 - 1;

    /// <summary>
    /// Шрамы, которые нужно наложить после загрузки Отпечатка (вписываются в Отпечаток перед возвратом и снимаются
    /// при первой генерации карты — она происходит при любой загрузке забега, одинаково у всех игроков коопа).
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingFlesh { get => _pendingFlesh; set { AssertMutable(); _pendingFlesh = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingSoul { get => _pendingSoul; set { AssertMutable(); _pendingSoul = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingPurse { get => _pendingPurse; set { AssertMutable(); _pendingPurse = value; } }

    /// <summary>Уровень «Реквием». Пока своих уровней нет (этап 6) — берётся уровень Возвышения забега.</summary>
    public int RequiemLevel => IsMutable && Owner != null ? Acts.RequiemLevel.Of(Owner.RunState) : 0;

    public int CandlesLeft =>
        MemoryStoneRules.CandlesLeft(RequiemLevel, _candlesUsed, _bonusCandles, RunSettings.MemoryStoneCandles);

    // ───── Отпечаток ─────

    /// <summary>
    /// Отпечаток снимается после входа в каждый акт (стартовый узел — Нео/Древний — уже пройден, docs/design/15 §1).
    /// Хук на реликвии игрока, а не на синглтоне: реликвии гарантированно слушают хуки забега.
    /// </summary>
    public override Task AfterActEntered()
    {
        // Host only, once for the party. AfterActEntered can precede the Ancient decision; it merely arms capture.
        if (!Net.RequiemNet.IsClient && Owner.RunState.Players.Select(Of).FirstOrDefault(s => s != null) == this)
            MemoryStone.RequestImprint();
        return Task.CompletedTask;
    }

    // ───── новая карта акта ─────

    /// <summary>
    /// После возврата к Камню карта акта строится заново (новое зерно — своё для каждого отката), босс остаётся прежним:
    /// он хранится в наборе комнат акта, а не в карте. Карту строит Камень первого игрока системы — один раз,
    /// одинаково у всех игроков коопа (всё зависит только от зерна забега и счётчика откатов).
    /// </summary>
    internal static ActMap RerollBeforeModifiers(IRunState runState, ActMap map, int actIndex)
    {
        var leader = runState.Players.Select(Of).FirstOrDefault(s => s != null);
        if (leader == null || leader._mapRerollAct1 != actIndex + 1) return map;
        try
        {
            var rng = new Rng(runState.Rng.Seed, MemoryStoneRules.MapRngName(actIndex, leader._rollbacks));
            // Run BEFORE native early and late modifiers, not halfway through their listener enumeration.
            // Golden Compass can still replace the map; Fur Coat and other late quest hooks see the final map.
            var fresh = RerollBaseMap(runState, map, rng);
            foreach (var player in runState.Players) if (Of(player) is { } stone) stone._mapRerollAct1 = 0;
            MainFile.Logger.Info($"[MemoryStone] map act={actIndex + 1} rollback={leader._rollbacks} policy={(ReferenceEquals(fresh, map) ? "special-map-retained" : fresh.GetType().Name)}");
            return fresh;
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"[MemoryStone] Map reroll failed; original map/flags retained: {e}");
            return map;
        }
    }

    internal static ActMap RerollBaseMap(IRunState state, ActMap map, Rng rng) => map switch
    {
        WideActMap wide => wide.Reroll(rng, state.Act, state.Players.Count > 1),
        StandardActMap => new StandardActMap(rng, state.Act, state.Players.Count > 1,
            !map.GetAllMapPoints().Any(p => p.PointType == MapPointType.Treasure), map.SecondBossMapPoint != null),
        _ => map, // special CustomCreateMap / other mods keep their own generation policy
    };

    // ───── отложенные Шрамы ─────

    public override async Task AfterMapGenerated(ActMap map, int actIndex)
    {
        if (!IsMutable || Owner == null || (_pendingFlesh | _pendingSoul | _pendingPurse) == 0) return;
        using var lease = await InventoryGate.For(Owner).Enter();
        if (lease == null) return; // an interrupted saved scar application must use its pre-application checkpoint
        using var mutation = InventoryGate.Track(Owner, lease, "memory-stone:scars");
        try
        {
            await MemoryStone.ApplyScars(Owner, _pendingFlesh, _pendingSoul, _pendingPurse);
            _pendingFlesh = _pendingSoul = _pendingPurse = 0; // only after the whole observed application
            mutation.Complete();
            Flash();
        }
        catch (Exception e)
        {
            InventoryGate.Block(Owner, $"Scar application interrupted; restore the prepared rollback checkpoint: {e}");
            mutation.Complete(); // persisted Block represents the uncertainty, never retry partial scars in place
        }
    }

    // ───── Месть ─────

    public bool IsRevengeTarget(Creature? target)
    {
        if (!IsMutable || target?.Monster == null || _revengeAct1 <= 0 || string.IsNullOrEmpty(_revengeTargets)) return false;
        if (Owner.RunState.CurrentActIndex != RevengeAct) return false;
        var name = target.Monster.GetType().Name;
        var faction = RequiemData.MonsterEntry(name).Faction;
        foreach (var token in _revengeTargets.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token == "Monster:" + name) return true;
            if (!string.IsNullOrEmpty(faction) && token == "Faction:" + faction) return true;
        }
        return false;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (dealer == null || dealer != Owner.Creature || !IsRevengeTarget(target)) return 1m;
        return MemoryStoneRules.RevengeMultiplier;
    }

    private void SyncVars()
    {
        if (DynamicVars.TryGetValue("Candles", out var c)) c.BaseValue = CandlesLeft == MemoryStoneRules.Infinite ? -1 : CandlesLeft;
        if (DynamicVars.TryGetValue("Rollbacks", out var r)) r.BaseValue = _rollbacks;
        if (DynamicVars.TryGetValue("FleshScars", out var f)) f.BaseValue = _fleshScars;
        InvokeDisplayAmountChanged();
    }
}


/// <summary>Preserve the native early → late map modifier pipeline around the rerolled base map.</summary>
[CriticalPatch]
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.ModifyGeneratedMap))]
public static class MemoryStoneMapBoundaryPatch
{
    [HarmonyPrefix]
    public static void Prefix(IRunState runState, ref ActMap map, int actIndex) =>
        map = MemoryStoneRelic.RerollBeforeModifiers(runState, map, actIndex);
}
