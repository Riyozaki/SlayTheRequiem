using BaseLib.Utils;
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

    /// <summary>Выдать происхождение (если его ещё нет) при входе в первую комнату забега.</summary>
    public override async Task AfterRoomEntered(MegaCrit.Sts2.Core.Rooms.AbstractRoom room)
    {
        if (Owner.Relics.OfType<OriginRelic>().Any() || OriginGranted) return;
        OriginGranted = true;
        var origin = Owner.Character.GetType().Name switch
        {
            "Regent" => OriginRelic.ForRegent(Owner),
            _ => null,
        };
        if (origin != null) await MegaCrit.Sts2.Core.Commands.RelicCmd.Obtain(origin, Owner);
    }

    private bool _originGranted;

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
    public override int MerchantCost => 90;

    public override async Task AfterObtained()
    {
        Mastery.GrimoireOf(Owner)?.Raise(School);
        await MegaCrit.Sts2.Core.Commands.RelicCmd.Remove(this);
    }
}

[Pool(typeof(SharedRelicPool))] public sealed class TreatiseOfDestruction : TreatiseRelic { public override SpellSchool School => SpellSchool.Destruction; }
[Pool(typeof(SharedRelicPool))] public sealed class TreatiseOfRestoration : TreatiseRelic { public override SpellSchool School => SpellSchool.Restoration; }
[Pool(typeof(SharedRelicPool))] public sealed class TreatiseOfAlteration : TreatiseRelic { public override SpellSchool School => SpellSchool.Alteration; }
[Pool(typeof(SharedRelicPool))] public sealed class TreatiseOfConjuration : TreatiseRelic { public override SpellSchool School => SpellSchool.Conjuration; }
[Pool(typeof(SharedRelicPool))] public sealed class TreatiseOfIllusion : TreatiseRelic { public override SpellSchool School => SpellSchool.Illusion; }
