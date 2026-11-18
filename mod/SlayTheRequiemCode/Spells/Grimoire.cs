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
    public override bool IsAllowed(IRunState runState) => false;
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

    public void SetLevel(SpellSchool school, int value)
    {
        value = Math.Clamp(value, 0, SpellAffinity.MaxMastery);
        switch (school)
        {
            case SpellSchool.Destruction: Destruction = value; break;
            case SpellSchool.Restoration: Restoration = value; break;
            case SpellSchool.Alteration: Alteration = value; break;
            case SpellSchool.Conjuration: Conjuration = value; break;
            case SpellSchool.Illusion: Illusion = value; break;
        }
        InvokeDisplayAmountChanged();
    }

    /// <summary>+1 владения (трактат, узел ветки, Наставник). false — уже Мастер.</summary>
    public bool Raise(SpellSchool school)
    {
        var current = Level(school);
        if (current >= SpellAffinity.MaxMastery) return false;
        SetLevel(school, current + 1);
        return true;
    }

    private bool _starting;
    private int _startPhase, _startProtocol;
    private string _originChoiceId = "", _backgroundChoiceId = "";

    /// <summary>0/1: origin not selected/selected; 2/3: origin committed/background selected; 4: complete.</summary>
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
    public bool StartingChoicesComplete => StartupState.IsComplete(StartPhase, OriginGranted);

    internal void MigrateLegacyStart()
    {
        if (!StartupState.NeedsLegacyMigration(StartProtocol)) return;
        StartPhase = StartupState.LegacyPhase(OriginGranted);
        if (!OriginGranted && Owner.Relics.Any(r => r is OriginRelic or BackgroundRelic))
            InventoryGate.Block(Owner, "Legacy startup contains choice relics without its completed declaration. One-shot bonuses cannot be safely replayed; retain the migration backup and restore a known-safe checkpoint.");
        if (OriginGranted)
        {
            OriginChoiceId = Owner.Relics.OfType<OriginRelic>().FirstOrDefault()?.Id.Entry ?? "none";
            BackgroundChoiceId = Owner.Relics.OfType<BackgroundRelic>().FirstOrDefault()?.Id.Entry ?? "none";
        }
        StartProtocol = StartupState.Protocol;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _starting = false;
    }

    /// <summary>Resume only unfinished startup steps; flags follow observed grants, never precede an await.</summary>
    public override async Task AfterRoomEntered(MegaCrit.Sts2.Core.Rooms.AbstractRoom room)
    {
        if (_starting || StartPhase == 4 || !RequiemMode.IsInSystem(Owner)) return;
        _starting = true;
        try
        {
            using var lease = await InventoryGate.For(Owner).Enter();
            if (lease == null) return;
            using var mutation = InventoryGate.Track(Owner, lease, "starting-choices");
            try
            {
                var origin = Owner.Relics.OfType<OriginRelic>().FirstOrDefault();
                if (origin != null && StartPhase < 2) { OriginChoiceId = origin.Id.Entry; StartPhase = 2; }
                if (StartPhase == 0)
                {
                    var chosen = await OriginRelic.Pick(Owner);
                    OriginChoiceId = chosen?.Id.Entry ?? "none";
                    StartPhase = 1; // persist the decision BEFORE the obtain callback
                }
                if (StartPhase == 1)
                {
                    if (OriginChoiceId != "none")
                    {
                        var canonical = ModelDb.AllRelics.OfType<OriginRelic>().FirstOrDefault(r => r.Id.Entry == OriginChoiceId)
                            ?? throw new InvalidOperationException("Selected origin model unavailable; saved decision retained");
                        origin = (OriginRelic)canonical.ToMutable();
                        await RelicCmd.Obtain(origin, Owner);
                        if (!Owner.Relics.Contains(origin)) throw new InvalidOperationException("Origin grant did not commit");
                    }
                    StartPhase = 2;
                }
                origin ??= Owner.Relics.OfType<OriginRelic>().FirstOrDefault();
                var background = Owner.Relics.OfType<BackgroundRelic>().FirstOrDefault();
                if (background != null) { BackgroundChoiceId = background.Id.Entry; StartPhase = 4; }
                if (StartPhase == 2)
                {
                    var chosen = await BackgroundRelic.Pick(Owner, origin);
                    BackgroundChoiceId = chosen?.Id.Entry ?? "none";
                    StartPhase = 3;
                }
                if (StartPhase == 3)
                {
                    if (BackgroundChoiceId != "none")
                    {
                        var canonical = ModelDb.AllRelics.OfType<BackgroundRelic>().FirstOrDefault(r => r.Id.Entry == BackgroundChoiceId)
                            ?? throw new InvalidOperationException("Selected background model unavailable; saved decision retained");
                        background = (BackgroundRelic)canonical.ToMutable();
                        await RelicCmd.Obtain(background, Owner);
                        if (!Owner.Relics.Contains(background)) throw new InvalidOperationException("Background grant did not commit");
                    }
                    StartPhase = 4;
                }
                OriginGranted = StartPhase == 4; // HeroRecord may now offer the next independent startup step
                mutation.Complete();
            }
            catch (Exception e)
            {
                // AfterObtained can fail after adding a relic/HP/cards. Never replay unknown one-shot bonuses.
                InventoryGate.Block(Owner, $"Starting choices interrupted at phase={StartPhase}: {e}");
                mutation.Complete();
            }
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
        foreach (var school in Mastery.Schools)
            SetLevel(school, SpellAffinity.StartingMastery(heroTypeName, school));
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
                if (!RequiemMode.IsInSystem(player)) continue;
                if (Alchemy.HerbalistPouch.Of(player) is { BeltGranted: false } pouch) await pouch.AfterRoomEntered(room);
                if (Mastery.GrimoireOf(player) is { StartingChoicesComplete: false } grimoire) await grimoire.AfterRoomEntered(room);
            }
        Rollback.MemoryStone.AfterActHooksCompleted(runState);
    }
}
