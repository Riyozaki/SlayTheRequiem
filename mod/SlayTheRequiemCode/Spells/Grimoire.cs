using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Relics;

namespace SlayTheRequiem.SlayTheRequiemCode.Spells;

/// <summary>
/// Гримуар — служебная стартовая реликвия героя в системе Реквиема: хранит владение пятью школами (сохраняется
/// вместе с забегом и синхронизируется в коопе как обычная реликвия). Счётчик = суммарное владение.
/// Выдаётся патчем <see cref="Patches.StartingRelicsPatch"/>; в награды/лавки не попадает.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class Grimoire : RequiemRelic
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool IsAllowedInShops => false;
    protected override bool IsAllowedInternal(IRunState runState) => false;
    public override bool ShowCounter => true;
    public override int DisplayAmount => Mastery.Schools.Sum(Level);

    private int _destruction, _restoration, _alteration, _conjuration, _illusion;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Destruction { get => _destruction; set { AssertMutable(); _destruction = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Restoration { get => _restoration; set { AssertMutable(); _restoration = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Alteration { get => _alteration; set { AssertMutable(); _alteration = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Conjuration { get => _conjuration; set { AssertMutable(); _conjuration = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Illusion { get => _illusion; set { AssertMutable(); _illusion = value; } }

    public int Level(SpellSchool school) => school switch
    {
        SpellSchool.Destruction => Destruction,
        SpellSchool.Restoration => Restoration,
        SpellSchool.Alteration => Alteration,
        SpellSchool.Conjuration => Conjuration,
        SpellSchool.Illusion => Illusion,
        _ => 0,
    };

    private string _masterySourceData = "", _creationData = "", _rebirthData = "";
    private bool _legacyCreation;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string MasterySourceData
    {
        get => _masterySourceData;
        set { AssertMutable(); if (!string.IsNullOrEmpty(value)) _ = MasterySources.Read(value); _masterySourceData = value ?? ""; }
    }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string CreationData
    {
        get => _creationData;
        set { AssertMutable(); if (!string.IsNullOrEmpty(value) && HeroCreationPlan.Read(value).Mode != CreationMode.Start) throw new FormatException("Wrong startup plan mode"); _creationData = value ?? ""; }
    }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string RebirthData
    {
        get => _rebirthData;
        set { AssertMutable(); if (!string.IsNullOrEmpty(value) && HeroCreationPlan.Read(value).Mode != CreationMode.Rebirth) throw new FormatException("Wrong rebirth plan mode"); _rebirthData = value ?? ""; }
    }
    /// <summary>Explicit compatibility receipt: already-completed old startup, no new bonus or replay.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool LegacyCreation { get => _legacyCreation; set { AssertMutable(); _legacyCreation = value; } }

    internal int[] MasteryCounters => Mastery.Schools.Select(Level).ToArray();
    internal MasterySources Sources
    {
        get
        {
            var sources = MasterySources.Read(MasterySourceData);
            sources.RequireCounters(MasteryCounters);
            return sources;
        }
    }
    internal void ApplySources(MasterySources sources)
    {
        var encoded = sources.Encode(); // validate/encode before changing any field
        AssertMutable();
        _destruction = sources.Level(SpellSchool.Destruction);
        _restoration = sources.Level(SpellSchool.Restoration);
        _alteration = sources.Level(SpellSchool.Alteration);
        _conjuration = sources.Level(SpellSchool.Conjuration);
        _illusion = sources.Level(SpellSchool.Illusion);
        _masterySourceData = encoded;
        InvokeDisplayAmountChanged();
    }
    internal void MigrateLegacyMastery()
    {
        if (MasterySourceData.Length == 0) MasterySourceData = MasterySources.PreserveLegacy(MasteryCounters).Encode();
    }
    /// <summary>Debug fixture override, deliberately opaque/non-refundable; gameplay must use source-qualified APIs.</summary>
    public void SetLevel(SpellSchool school, int value)
    {
        if (!Enum.IsDefined(school)) throw new ArgumentOutOfRangeException(nameof(school));
        var levels = MasteryCounters;
        levels[(int)school] = Math.Clamp(value, 0, SpellAffinity.MaxMastery);
        ApplySources(MasterySources.PreserveLegacy(levels));
    }
    /// <summary>Treatise/event acquisition. Paid stats and learned skills have distinct entry points.</summary>
    public bool Raise(SpellSchool school)
    {
        if (!Sources.TryRaiseAcquired(school, out var next)) return false;
        ApplySources(next); return true;
    }
    internal bool BuyMastery(SpellSchool school, int points)
    {
        if (!Sources.TryBuyStat(school, points, out var next)) return false;
        ApplySources(next); return true;
    }
    internal void ApplySkill(Skill skill) => ApplySources(Sources.WithSkill(skill));
    internal void RefreshIdentitySources()
    {
        if (!Sources.IdentityKnown) return; // preserve opaque legacy counters, do not invent new old grants
        ApplySources(Sources.WithIdentity(
            HeroIdentityRules.OriginMastery(Owner.Relics.OfType<OriginRelic>().SingleOrDefault()?.GetType().Name),
            HeroIdentityRules.BackgroundMastery(Owner.Relics.OfType<BackgroundRelic>().SingleOrDefault()?.GetType().Name)));
        Progression.HeroRecord.Of(Owner)?.RecordNativeSchools();
    }

    private bool _starting;
    private MegaCrit.Sts2.Core.Rooms.AbstractRoom? _processedRoom;
    private int _startPhase, _startProtocol;
    private string _originChoiceId = "", _backgroundChoiceId = "";

    // Keep v1 field names for backed-up migration; the v2 wizard's actual progress is CreationData.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int StartPhase
    {
        get => _startPhase;
        set { AssertMutable(); if (value is < 0 or > StartupState.Complete) throw new FormatException("Invalid saved startup phase"); _startPhase = value; }
    }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int StartProtocol
    {
        get => _startProtocol;
        set { AssertMutable(); if (value is < 0 or > StartupState.Protocol) throw new FormatException("Unsupported saved startup protocol"); _startProtocol = value; }
    }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string OriginChoiceId { get => _originChoiceId; set { AssertMutable(); _originChoiceId = value ?? ""; } }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string BackgroundChoiceId { get => _backgroundChoiceId; set { AssertMutable(); _backgroundChoiceId = value ?? ""; } }
    public bool StartingChoicesComplete => StartupState.IsComplete(StartPhase, OriginGranted)
        && (LegacyCreation || CreationData.Length > 0 && HeroCreationPlan.Read(CreationData).Stage == CreationStage.Complete);

    internal void MigrateLegacyStart()
    {
        if (!StartupState.NeedsLegacyMigration(StartProtocol)) return;
        if (!OriginGranted || StartProtocol == 1 && StartPhase != StartupState.Complete)
            throw new FormatException("Unfinished legacy identity has no safe one-shot receipt; retain the pre-load backup and use its matching build/checkpoint");
        StartPhase = StartupState.Complete;
        OriginChoiceId = Owner.Relics.OfType<OriginRelic>().SingleOrDefault()?.Id.Entry ?? "none";
        BackgroundChoiceId = Owner.Relics.OfType<BackgroundRelic>().SingleOrDefault()?.Id.Entry ?? "none";
        LegacyCreation = true;
        StartProtocol = StartupState.Protocol;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _starting = false;
        _processedRoom = null; // journals are immutable strings, no mutable plan/cache shared across players
    }

    public override async Task AfterRoomEntered(MegaCrit.Sts2.Core.Rooms.AbstractRoom room)
    {
        if (_starting || ReferenceEquals(_processedRoom, room) || !RequiemMode.IsInSystem(Owner)) return;
        _starting = true;
        var run = Owner.RunState;
        var wasLive = ReferenceEquals(run, Net.RequiemNet.State);
        try
        {
            using var lease = await InventoryGate.For(Owner).Enter();
            if (lease == null || wasLive && (!ReferenceEquals(run, Net.RequiemNet.State) || !ReferenceEquals(run.CurrentRoom, room))) return;
            var resumingRebirth = RebirthData.Length > 0 && HeroCreationPlan.Read(RebirthData).Stage != CreationStage.Complete;
            if (!StartingChoicesComplete) await Progression.HeroCreation.ResumeStart(this, lease);
            if (StartingChoicesComplete) await Progression.HeroCreation.ResumeRebirth(this, lease);
            if (StartingChoicesComplete && !resumingRebirth && Progression.HeroRecord.Of(Owner) is { } hero)
                await hero.ResumePendingPaths(lease);
            _processedRoom = room; // a refused lease is NOT a processed room; a same-room retry must still enter
        }
        finally { _starting = false; }
    }

    private bool _originGranted;
    private string _runSettingsCode = string.Empty;

    /// <summary>
    /// Снимок игровых настроек забега (<see cref="Config.RunSettings"/>, ревью F-03): одинаковая строка у всех героев;
    /// при загрузке забега правила берутся отсюда, а не из меню машины.
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string RunSettingsCode { get => _runSettingsCode; set { AssertMutable(); _runSettingsCode = value ?? string.Empty; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool OriginGranted { get => _originGranted; set { AssertMutable(); _originGranted = value; } }

    public void InitFor(string? heroTypeName)
    {
        ApplySources(MasterySources.ForNewHero(heroTypeName ?? ""));
        StartProtocol = StartupState.Protocol;
        CreationData = HeroCreationPlan.Empty(CreationMode.Start).Encode();
    }

}

/// <summary>Трактат владения: при получении +1 к школе и исчезает (docs/design/08 §8.3).</summary>
public abstract class TreatiseRelic : RequiemRelic
{
    public abstract SpellSchool School { get; }
    public override RelicRarity Rarity => RelicRarity.Shop;
    /// <summary>Базовая цена (14 §2.3); в лавке Реквиема — 120 +40 за каждый купленный ранее (Книга торговца).</summary>
    public override int MerchantCost => 120;

    public override async Task AfterObtained()
    {
        Mastery.GrimoireOf(Owner)?.Raise(School);
        if (Owner.RunState.CurrentRoom is MegaCrit.Sts2.Core.Rooms.MerchantRoom)
            Shops.MerchantLedger.Of(Owner)?.NoteTreatiseBought();
        await MegaCrit.Sts2.Core.Commands.RelicCmd.Remove(this);
    }
}

[Pool(typeof(SharedRelicPool))] public sealed class TreatiseOfDestruction : TreatiseRelic { public override SpellSchool School => SpellSchool.Destruction; }
[Pool(typeof(SharedRelicPool))] public sealed class TreatiseOfRestoration : TreatiseRelic { public override SpellSchool School => SpellSchool.Restoration; }
[Pool(typeof(SharedRelicPool))] public sealed class TreatiseOfAlteration : TreatiseRelic { public override SpellSchool School => SpellSchool.Alteration; }
[Pool(typeof(SharedRelicPool))] public sealed class TreatiseOfConjuration : TreatiseRelic { public override SpellSchool School => SpellSchool.Conjuration; }
[Pool(typeof(SharedRelicPool))] public sealed class TreatiseOfIllusion : TreatiseRelic { public override SpellSchool School => SpellSchool.Illusion; }


/// <summary>
/// MapRoom intentionally skips AfterRoomEntered in the game. Finish startup even for a no-Ancient start, then
/// expose the act boundary only AFTER every native act listener and our startup tasks completed.
/// </summary>
[CriticalPatch]
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterActEntered))]
public static class StartupActBoundaryPatch
{
    [HarmonyPostfix]
    public static void Postfix(IRunState runState, ref Task __result) => __result = Finish(__result, runState);

    private static async Task Finish(Task native, IRunState runState)
    {
        await native;
        if (!ReferenceEquals(runState, Net.RequiemNet.State)) return;
        if (runState.CurrentRoom is MegaCrit.Sts2.Core.Rooms.MapRoom room)
            foreach (var player in runState.Players)
            {
                if (!ReferenceEquals(runState, Net.RequiemNet.State) || !ReferenceEquals(runState.CurrentRoom, room)) return;
                if (!RequiemMode.IsInSystem(player)) continue;
                if (Alchemy.HerbalistPouch.Of(player) is { BeltGranted: false } pouch) await pouch.AfterRoomEntered(room);
                if (Mastery.GrimoireOf(player) is { } grimoire) await grimoire.AfterRoomEntered(room);
            }
        Rollback.MemoryStone.AfterActHooksCompleted(runState);
    }
}

/// <summary>Resume mandatory choices before combat resources are prepared, not after a stale pool was installed.</summary>
[CriticalPatch]
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.BeforeRoomEntered))]
public static class HeroCreationCombatBoundaryPatch
{
    [HarmonyPostfix]
    public static void Postfix(IRunState runState, MegaCrit.Sts2.Core.Rooms.AbstractRoom room, ref Task __result) =>
        __result = Finish(__result, runState, room);

    private static async Task Finish(Task native, IRunState runState, MegaCrit.Sts2.Core.Rooms.AbstractRoom room)
    {
        await native;
        if (!ReferenceEquals(runState, Net.RequiemNet.State) || room is not MegaCrit.Sts2.Core.Rooms.CombatRoom) return;
        foreach (var player in runState.Players)
        {
            if (!ReferenceEquals(runState, Net.RequiemNet.State) || !ReferenceEquals(runState.CurrentRoom, room)) return;
            if (!RequiemMode.IsInSystem(player)) continue;
            Progression.HeroRecord.Of(player)?.NoteRoomVisit(room); // preview the actual next-combat stamina pool
            if (Alchemy.HerbalistPouch.Of(player) is { BeltGranted: false } pouch) await pouch.AfterRoomEntered(room);
            if (Mastery.GrimoireOf(player) is { } grimoire)
            {
                await grimoire.AfterRoomEntered(room);
                if (!grimoire.StartingChoicesComplete || grimoire.RebirthData.Length > 0
                    && HeroCreationPlan.Read(grimoire.RebirthData).Stage != CreationStage.Complete)
                    throw new InvalidOperationException("Mandatory hero decisions are incomplete/blocked; restore the safe checkpoint before combat");
            }
        }
    }
}

/// <summary>Continue skips ordinary room hooks for MapRoom and prefinished rooms. Resume only decisions/receipts, never victory gifts.</summary>
[CriticalPatch]
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Rooms.AbstractRoom), nameof(MegaCrit.Sts2.Core.Rooms.AbstractRoom.Enter))]
public static class HeroCreationRestoreBoundaryPatch
{
    [HarmonyPostfix]
    public static void Postfix(MegaCrit.Sts2.Core.Rooms.AbstractRoom __instance, IRunState? runState,
        bool isRestoringRoomStackBase, ref Task __result) => __result = Finish(__result, __instance, runState, isRestoringRoomStackBase);

    private static async Task Finish(Task native, MegaCrit.Sts2.Core.Rooms.AbstractRoom room, IRunState? runState, bool restoringBase)
    {
        await native;
        if (restoringBase || runState == null || !ReferenceEquals(runState, Net.RequiemNet.State)
            || room is not MegaCrit.Sts2.Core.Rooms.MapRoom && !room.IsPreFinished) return;
        foreach (var player in runState.Players)
        {
            if (!ReferenceEquals(runState, Net.RequiemNet.State) || !ReferenceEquals(runState.CurrentRoom, room)) return;
            if (!RequiemMode.IsInSystem(player)) continue;
            if (Alchemy.HerbalistPouch.Of(player) is { BeltGranted: false } pouch) await pouch.AfterRoomEntered(room);
            if (Mastery.GrimoireOf(player) is { } grimoire) await grimoire.AfterRoomEntered(room);
        }
    }
}
