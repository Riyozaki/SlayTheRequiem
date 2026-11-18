using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;

namespace SlayTheRequiem.SlayTheRequiemCode.Debugging;

/// <summary>
/// B2.1 native-model fixtures for the existing isolated integrity command. They call production effect adapters
/// with an explicitly confirmed plan, NOT the real local/remote decision UI. Source-only until run in the legal
/// game environment; JSON player round trips here are not disk Continue, visual, replay or two-client acceptance.
/// </summary>
public static partial class IntegrityRegression
{
    private static Grimoire ConfirmStart(Player owner, StartingBonus bonus, string origin = HeroCreation.None,
        string background = HeroCreation.None, string backgroundSource = "")
    {
        var grimoire = Mastery.GrimoireOf(owner)!;
        var plan = HeroCreationPlan.Empty(CreationMode.Start).ChooseOrigin(origin).ChooseBackground(background).ChooseBonus(bonus)
            .Confirm(HeroCreation.Capture(owner));
        if (backgroundSource.Length > 0) plan = plan.PrepareBackgroundSource(backgroundSource);
        grimoire.CreationData = plan.Encode();
        return grimoire;
    }

    private static async Task ApplyConfirmed(Grimoire grimoire)
    {
        using var lease = await InventoryGate.For(grimoire.Owner).Enter() ?? throw new InvalidOperationException("Fixture gate blocked");
        await HeroCreation.ApplyConfirmedStart(grimoire, lease);
    }

    // Explicit fixture acknowledgment, deliberately bypassing only the screen. Effects were applied by production code.
    private static void AcknowledgeStart(Grimoire grimoire)
    {
        grimoire.CreationData = HeroCreationPlan.Read(grimoire.CreationData).Acknowledge().Encode();
        grimoire.StartPhase = StartupState.Complete;
        grimoire.OriginGranted = true;
        HeroRecord.Of(grimoire.Owner)!.RecordNativeSchools();
    }

    private static async Task CreationGold()
    {
        var owner = Fixture(); var hp = owner.Creature.MaxHp; var gold = owner.Gold;
        var grimoire = ConfirmStart(owner, StartingBonus.Gold, ModelDb.Relic<FrostbornOrigin>().Id.Entry);
        await ApplyConfirmed(grimoire);
        Require(owner.Gold == gold + HeroCreationPlan.GoldGift && owner.Creature.MaxHp == hp + 5
            && !grimoire.StartingChoicesComplete && HeroCreationPlan.Read(grimoire.CreationData).Stage == CreationStage.Receipt,
            "Start gift/identity receipt committed too early or changed the wrong resource");
        HeroProgressionSave.Validate(owner.ToSerializable());
        owner = Reload(owner); grimoire = Mastery.GrimoireOf(owner)!;
        var refused = false;
        try { await ApplyConfirmed(grimoire); }
        catch (InvalidOperationException) { refused = true; }
        Require(refused && owner.Gold == gold + HeroCreationPlan.GoldGift && owner.Creature.MaxHp == hp + 5, "Receipt replay changed resources or was not refused");
        AcknowledgeStart(grimoire);
        HeroProgressionSave.Validate(owner.ToSerializable());
        Require(grimoire.StartingChoicesComplete && Pack(owner).OperationPending.Length == 0, "Acknowledgment left a false in-flight mutation");
    }

    private static async Task CreationPotion()
    {
        var owner = Fixture(); var count = owner.PotionSlots.Count(p => p != null);
        var grimoire = ConfirmStart(owner, StartingBonus.Potion);
        await ApplyConfirmed(grimoire);
        var plan = HeroCreationPlan.Read(grimoire.CreationData);
        var source = HeroCreation.ReadPotion(plan.BonusSource);
        Require(plan.Stage == CreationStage.Receipt && owner.PotionSlots.Count(p => p != null) == count + 1
            && owner.PotionSlots.Any(p => p is { Rarity: PotionRarity.Common } && p.Id == source.Id), "Starter potion is not the exact saved Common source");
        HeroProgressionSave.Validate(owner.ToSerializable());
        owner = Reload(owner);
        Require(Mastery.GrimoireOf(owner)!.CreationData == plan.Encode() && owner.PotionSlots.Any(p => p?.Id == source.Id), "Potion source/receipt changed on JSON reload");
    }

    private static async Task CreationFullBelt()
    {
        var owner = Fixture();
        var potion = PotionFactory.GetPotionOptions(owner, Array.Empty<PotionModel>()).First(p => p.Rarity == PotionRarity.Common);
        for (var i = 0; owner.HasOpenPotionSlots && i < 64; i++) Require(owner.AddPotionInternal(potion.ToMutable()).success, "Could not fill potion fixture");
        Require(!owner.HasOpenPotionSlots, "Potion belt fixture did not fill");
        var hp = owner.Creature.MaxHp;
        var grimoire = ConfirmStart(owner, StartingBonus.Potion, ModelDb.Relic<FrostbornOrigin>().Id.Entry);
        await ApplyConfirmed(grimoire);
        var retry = HeroCreationPlan.Read(grimoire.CreationData);
        Require(retry.Stage == CreationStage.Bonus && retry.IdentityApplied && !retry.BonusApplied && owner.Creature.MaxHp == hp + 5
            && Pack(owner).OperationPending.Length == 0 && !InventoryGate.For(owner).Blocked, "Known full-belt refusal blocked/replayed the identity");
        grimoire.CreationData = retry.ChooseBonus(StartingBonus.Gold).Confirm(HeroCreation.Capture(owner)).Encode();
        await ApplyConfirmed(grimoire);
        Require(owner.Creature.MaxHp == hp + 5 && owner.Relics.OfType<FrostbornOrigin>().Count() == 1, "Bonus-only retry repeated a one-shot identity effect");
        HeroProgressionSave.Validate(owner.ToSerializable());
    }

    private static async Task CreationRemoval()
    {
        var owner = Fixture();
        var first = owner.RunState.CreateCard<StrikeIronclad>(owner); owner.Deck.AddInternal(first, silent: true);
        var second = owner.RunState.CreateCard<StrikeIronclad>(owner); owner.Deck.AddInternal(second, silent: true);
        InventoryIdentity.Ensure(owner);
        var keep = InventoryIdentity.CardKey(second);
        var grimoire = ConfirmStart(owner, StartingBonus.RemoveBasic);
        var plan = HeroCreationPlan.Read(grimoire.CreationData).IdentityComplete()
            .PrepareBonusSource(TravelPack.CardPayload(first.ToSerializable())); // explicit fixture selection, no fake UI result
        grimoire.OriginChoiceId = grimoire.BackgroundChoiceId = HeroCreation.None;
        grimoire.CreationData = plan.Encode();
        await ApplyConfirmed(grimoire);
        Require(!owner.Deck.Cards.Contains(first) && owner.Deck.Cards.Count(c => c.Id == first.Id) == 1
            && InventoryIdentity.FindCard(owner, keep) == second, "Starter removal substituted/deleted the other equal-looking card");
        HeroProgressionSave.Validate(owner.ToSerializable());
    }

    private static async Task CreationVagrant()
    {
        var owner = Fixture(); var first = DeckCard(owner); var second = DeckCard(owner);
        InventoryIdentity.Ensure(owner);
        var keep = InventoryIdentity.CardKey(second); var hp = owner.Creature.MaxHp;
        var source = TravelPack.CardPayload(first.ToSerializable());
        var grimoire = ConfirmStart(owner, StartingBonus.Gold, background: ModelDb.Relic<VagrantBackground>().Id.Entry, backgroundSource: source);
        await ApplyConfirmed(grimoire);
        Require(owner.Creature.MaxHp == hp + 5 && !owner.Deck.Cards.Contains(first) && InventoryIdentity.FindCard(owner, keep) == second
            && HeroCreationPlan.Read(grimoire.CreationData).BackgroundSource == source, "Vagrant's preselected optional removal was lost or substituted");
        HeroProgressionSave.Validate(owner.ToSerializable());
    }

    private static async Task<Grimoire> CompletedSourceHero(Player owner)
    {
        var grimoire = ConfirmStart(owner, StartingBonus.Gold, ModelDb.Relic<FrostbornOrigin>().Id.Entry, ModelDb.Relic<FrostbinderBackground>().Id.Entry);
        await ApplyConfirmed(grimoire); AcknowledgeStart(grimoire);
        var hero = HeroRecord.Of(owner)!;
        hero.Renown = ProgressionRules.RenownToReach(10);
        Require(await hero.SpendStat(StatAxis.Health) && await hero.SpendStat(StatAxis.Mastery, SpellSchool.Destruction)
            && await hero.SpendStat(StatAxis.Mastery, SpellSchool.Restoration), "Could not spend the known native/foreign stat costs");
        await RelicCmd.Obtain(ModelDb.Relic<TreatiseOfDestruction>().ToMutable(), owner);
        Require(await hero.Learn(Skill.Destruction1), "Retained-skill fixture could not learn its node");
        HeroProgressionSave.Validate(owner.ToSerializable());
        return grimoire;
    }

    private static void ConfirmReborn(Player owner)
    {
        var grimoire = Mastery.GrimoireOf(owner)!;
        grimoire.RebirthData = HeroCreationPlan.Empty(CreationMode.Rebirth).ChooseOrigin(ModelDb.Relic<ForgebloodOrigin>().Id.Entry)
            .ChooseBackground(ModelDb.Relic<BerserkerBackground>().Id.Entry).Confirm(HeroCreation.Capture(owner), HeroCreation.Fingerprint(owner)).Encode();
    }

    // The same native issuer/companion effect chain as Paths.Grant, without its final local/remote receipt selector.
    private static async Task ObtainConfirmedReborn(Player owner)
    {
        using var lease = await InventoryGate.For(owner).Enter() ?? throw new InvalidOperationException("Fixture gate blocked");
        using var mutation = InventoryGate.Track(owner, lease, "path:" + HeroPath.Reborn);
        var essence = (RebornEssence)ModelDb.Relic<RebornEssence>().ToMutable();
        PathGrants.Prepare(essence, owner);
        await RelicCmd.Obtain(essence, owner);
        essence.SignGrant = RelicGrantState.Read(essence.SignGrant)!.IssuerCompleted().Encode();
        Require((await PathGrants.CompleteSign(essence, lease)).Succeeded, "Reborn's no-companion receipt failed");
        mutation.Complete();
    }

    private static async Task RebornSources()
    {
        var owner = Fixture(); var grimoire = await CompletedSourceHero(owner); var hero = HeroRecord.Of(owner)!;
        var loot = (OakStaff)ModelDb.Relic<OakStaff>().ToMutable(); owner.AddRelicInternal(loot, silent: true);
        var lootKey = InventoryIdentity.RelicKey(loot); var lootPayload = Json(loot.ToSerializable());
        var cards = owner.Deck.Cards.Select(c => TravelPack.CardPayload(c.ToSerializable())).ToArray();
        var skills = hero.Skills; var skillSpent = hero.SkillPointsSpent; var hp = owner.Creature.MaxHp;
        var paid = grimoire.Sources.StatPoints;
        Require(paid == hero.StatMastery && paid == 3 && HeroCreation.CanRebirth(owner), "Paid provenance / respec preflight differs from actual purchases");
        ConfirmReborn(owner);
        await ObtainConfirmedReborn(owner);
        Require(hero.StatPointsSpent == 0 && grimoire.Sources.StatPoints == 0 && hero.Skills == skills && hero.SkillPointsSpent == skillSpent
            && grimoire.Level(SpellSchool.Destruction) == 2 && grimoire.Level(SpellSchool.Restoration) == 0,
            "Reborn refunded skills/treatises, retained paid mastery or did not reset stats");
        Require(owner.Creature.MaxHp == hp + 5 - ProgressionRules.HealthPerPoint && !owner.Relics.OfType<IronBattleaxe>().Any()
            && Json(InventoryIdentity.FindRelic(owner, lootKey)!.ToSerializable()) == lootPayload
            && owner.Deck.Cards.Select(c => TravelPack.CardPayload(c.ToSerializable())).SequenceEqual(cards),
            "Reborn replayed Berserker's starter gift, lost acquired loot/cards, or reconciled HP twice");
        Require(HeroCreationPlan.Read(grimoire.RebirthData).Stage == CreationStage.Receipt && !hero.IsNative(SpellSchool.Destruction),
            "Reborn receipt/native schools still use the old background or receipt was auto-acknowledged");
        HeroProgressionSave.Validate(owner.ToSerializable());
        owner = Reload(owner); var before = Json(owner.ToSerializable());
        await Paths.Grant(owner, HeroPath.Reborn);
        Require(before == Json(owner.ToSerializable()), "Existing Reborn essence reissued a one-shot effect on retry/JSON reload");
    }

    private static async Task RebornLegacy()
    {
        var owner = Fixture(); var grimoire = await CompletedSourceHero(owner);
        grimoire.SetLevel(SpellSchool.Destruction, 4); // deliberate opaque legacy provenance
        grimoire.CreationData = ""; grimoire.LegacyCreation = true;
        var before = Json(owner.ToSerializable());
        Require(!HeroCreation.CanRebirth(owner) && Paths.Of(owner) == null && before == Json(owner.ToSerializable()),
            "Legacy respec guessed a paid allocation, consumed a Path or mutated its read-only preflight");
    }

    private static async Task RebornInterrupted()
    {
        var owner = Fixture(); await CompletedSourceHero(owner); ConfirmReborn(owner);
        SerializablePlayer? during = null;
        void SaveInside(RelicModel relic) { if (relic is RebornEssence) during = RoundTrip(owner.ToSerializable()); }
        owner.RelicObtained += SaveInside;
        try { await ObtainConfirmedReborn(owner); }
        finally { owner.RelicObtained -= SaveInside; }
        Require(during != null, "No native insertion-save boundary observed for Reborn");
        HeroProgressionSave.Validate(during!); // interrupted journal/counters are preserved, not repaired or rejected after native mutation
        var restored = Player.FromSerializable(during!);
        MegaCrit.Sts2.Core.Runs.RunState.CreateForTest([restored], seed: "STR-G1-INTEGRITY");
        Require(InventoryGate.For(restored).Blocked, "Interrupted Reborn insertion was not blocked on JSON reload");
        var before = Json(restored.ToSerializable());
        await Paths.Grant(restored, HeroPath.Reborn);
        Require(before == Json(restored.ToSerializable()) && HeroCreationPlan.Read(Mastery.GrimoireOf(restored)!.RebirthData).Stage == CreationStage.Applying,
            "Interrupted Reborn was replayed instead of retaining its safe checkpoint requirement");
    }

    private static async Task CreationPreflight()
    {
        var owner = Fixture(); var grimoire = ConfirmStart(owner, StartingBonus.Gold); await ApplyConfirmed(grimoire);
        var save = RoundTrip(owner.ToSerializable()); var props = save.Relics.Single(r => r.Id == grimoire.Id).Props!;
        SavedPropertyWrite.String(props, nameof(Grimoire.MasterySourceData), MasterySources.PreserveLegacy([0, 0, 0, 0, 0]).Encode());
        SavedPropertyWrite.Int(props, nameof(Grimoire.Destruction), 5);
        var before = Json(save);
        try { HeroProgressionSave.Validate(save); throw new InvalidOperationException("Mismatched mastery source/counters accepted"); }
        catch (FormatException) { Require(before == Json(save), "Progression preflight rewrote an invalid source"); }
    }
}
