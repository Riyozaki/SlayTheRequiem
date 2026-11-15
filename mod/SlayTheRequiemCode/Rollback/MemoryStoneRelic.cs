using BaseLib.Utils;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
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
    private int _rollbacks;
    private int _fleshScars;
    private int _soulScars;
    private int _purseScars;
    private string _revengeTargets = string.Empty;
    private int _revengeAct = -1;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CandlesUsed { get => _candlesUsed; set { AssertMutable(); _candlesUsed = value; SyncVars(); } }

    /// <summary>Дополнительные Свечи (супербосс, «Свеча в склепе» — этапы 4–6).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BonusCandles { get => _bonusCandles; set { AssertMutable(); _bonusCandles = value; SyncVars(); } }

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

    /// <summary>Акт повторной попытки, в котором действует Месть; −1 — Мести нет.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int RevengeAct { get => _revengeAct; set { AssertMutable(); _revengeAct = value; } }

    /// <summary>Уровень «Реквием». Пока своих уровней нет (этап 6) — берётся уровень Возвышения забега.</summary>
    public int RequiemLevel => IsMutable && Owner != null ? Owner.RunState.AscensionLevel : 0;

    public int CandlesLeft =>
        MemoryStoneRules.CandlesLeft(RequiemLevel, _candlesUsed, _bonusCandles, RequiemConfig.MemoryStoneCandles);

    // ───── Отпечаток ─────

    /// <summary>
    /// Отпечаток снимается после входа в каждый акт (стартовый узел — Нео/Древний — уже пройден, docs/design/15 §1).
    /// Хук на реликвии игрока, а не на синглтоне: реликвии гарантированно слушают хуки забега.
    /// </summary>
    public override Task AfterActEntered()
    {
        if (LocalContext.IsMe(Owner)) MemoryStone.TakeImprint();
        return Task.CompletedTask;
    }

    // ───── Месть ─────

    public bool IsRevengeTarget(Creature? target)
    {
        if (!IsMutable || target?.Monster == null || _revengeAct < 0 || string.IsNullOrEmpty(_revengeTargets)) return false;
        if (Owner.RunState.CurrentActIndex != _revengeAct) return false;
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
