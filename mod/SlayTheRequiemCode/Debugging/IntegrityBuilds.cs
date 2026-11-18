using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using SlayTheRequiem.SlayTheRequiemCode.Cards.Regent;
using SlayTheRequiem.SlayTheRequiemCode.Cards.Silent;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;

namespace SlayTheRequiem.SlayTheRequiemCode.Debugging;

public static partial class IntegrityRegression
{
    private static Task BuildRegentStarter()
    {
        var owner = Player.CreateForNewRun<Regent>(UnlockState.all, ulong.MaxValue);
        RunState.CreateForTest([owner], seed: "STR-B2-BUILDS");
        Require(owner.Deck.Cards.Count == 10, "Regent starter inflated or truncated");
        Require(owner.Deck.Cards.OfType<Firebolt>().Count() == 1 && owner.Deck.Cards.OfType<ConjureSpiritWolf>().Count() == 1
            && owner.Deck.Cards.OfType<ArcaneFocus>().Count() == 1, "Native starting inventory missed its mage links");
        var before = owner.Deck.Cards.Select(c => c.Id).ToArray();
        var restored = Reload(owner);
        Require(restored.Deck.Cards.Select(c => c.Id).SequenceEqual(before), "Continue reapplied the starting-deck mapping");
        return Task.CompletedTask;
    }

    private static Task BuildElementSignature()
    {
        var owner = Fixture();
        var essence = (ElementLordEssence)ModelDb.Relic<ElementLordEssence>().ToMutable();
        essence.ChosenElement = (int)Element.Fire; owner.AddRelicInternal(essence, silent: true);
        var before = Mastery.Of(owner, SpellSchool.Destruction);
        var meteor = owner.RunState.CreateCard<Meteor>(owner);
        var ice = owner.RunState.CreateCard<IceTomb>(owner);
        Require(meteor.Affinity.Castable && meteor.Affinity.ManaCost == meteor.BaseManaCost && meteor.EffectMultiplier == 1,
            "Chosen Path signature is impossible/strained instead of taught");
        Require(!ice.Affinity.Castable && Mastery.Of(owner, SpellSchool.Destruction) == before, "Path teaching leaked to another signature/school counter");
        return Task.CompletedTask;
    }

    private static Task BuildPhysicalRingDisplacement()
    {
        var owner = Fixture();
        // Pick a real ring model from the registered equipment, not equality on synthetic type records.
        var canonical = ModelDb.AllRelicPools.SelectMany(p => p.AllRelics).OfType<EquipmentRelic>()
            .First(r => r.Slot == EquipSlot.Ring);
        var first = (EquipmentRelic)canonical.ToMutable(); var second = (EquipmentRelic)canonical.ToMutable();
        var third = (EquipmentRelic)canonical.ToMutable();
        foreach (var item in new[] { first, second, third }) { item.IsStowed = true; owner.AddRelicInternal(item, silent: true); }
        Loadout.Equip(owner, first); Loadout.Equip(owner, second); Loadout.Equip(owner, third);
        Require(first.IsStowed && second.IsEquipped && third.IsEquipped, "Identical rings displaced by type instead of physical slot");
        return Task.CompletedTask;
    }
}
