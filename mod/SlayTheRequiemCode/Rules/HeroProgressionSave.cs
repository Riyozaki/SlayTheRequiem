using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>Read-only preflight of source journals and wizard receipts BEFORE native deserialization/migration.</summary>
internal static class HeroProgressionSave
{
    internal static void Validate(SerializablePlayer player)
    {
        var grimoires = player.Relics.Where(r => r.Id == ModelDb.Relic<Grimoire>().Id).ToList();
        if (grimoires.Count == 0) return;
        if (grimoires.Count != 1) throw new FormatException("Duplicate saved hero Grimoire");
        var records = player.Relics.Where(r => r.Id == ModelDb.Relic<HeroRecord>().Id).ToList();
        var packs = player.Relics.Where(r => r.Id == ModelDb.Relic<TravelPack>().Id).ToList();
        if (records.Count != 1 || packs.Count != 1) throw new FormatException("Saved Requiem hero lacks its unique progression/inventory store");
        var origins = player.Relics.Where(r => ModelDb.GetByIdOrNull<RelicModel>(r.Id) is OriginRelic).ToList();
        var backgrounds = player.Relics.Where(r => ModelDb.GetByIdOrNull<RelicModel>(r.Id) is BackgroundRelic).ToList();
        var paths = player.Relics.Where(r => ModelDb.GetByIdOrNull<RelicModel>(r.Id) is PathEssence).ToList();
        if (origins.Count > 1 || backgrounds.Count > 1 || paths.Count > 1) throw new FormatException("Duplicate saved hero identity/Path");
        var props = grimoires[0].Props;
        var record = records[0].Props;
        var pack = packs[0].Props;
        var hero = ModelDb.GetById<CharacterModel>(player.CharacterId).GetType().Name;
        if (HeroIdentityRules.OriginTypes(hero).Count == 0) throw new FormatException("Unsupported source-accounted Requiem hero");
        var counters = new[] { Int(props, nameof(Grimoire.Destruction)), Int(props, nameof(Grimoire.Restoration)), Int(props, nameof(Grimoire.Alteration)),
            Int(props, nameof(Grimoire.Conjuration)), Int(props, nameof(Grimoire.Illusion)) };
        if (counters.Any(n => n is < 0 or > SpellAffinity.MaxMastery)) throw new FormatException("Invalid saved mastery counters; no clamping rewrite");
        var investments = new[] { Int(record, nameof(HeroRecord.StatHealth)), Int(record, nameof(HeroRecord.StatStamina)), Int(record, nameof(HeroRecord.StatMana)),
            Int(record, nameof(HeroRecord.StatBurden)), Int(record, nameof(HeroRecord.StatMastery)) };
        if (investments.Any(n => n < 0) || investments.Sum(n => (long)n) > int.MaxValue)
            throw new FormatException("Invalid saved stat investment; no inferred refund");
        var learned = ReadSkills(Text(record, nameof(HeroRecord.Skills)));
        var path = paths.Count == 0 ? HeroPath.None : ((PathEssence)ModelDb.GetById<RelicModel>(paths[0].Id)).Path;
        var offer = Text(record, nameof(HeroRecord.PathOfferData));
        if (offer.Length > 0) _ = PathOfferState.Read(offer);
        var snapshot = new HeroCreationSnapshot(Int(props, nameof(Grimoire.StartProtocol)), Int(props, nameof(Grimoire.StartPhase)),
            Bool(props, nameof(Grimoire.OriginGranted)), Bool(props, nameof(Grimoire.LegacyCreation)), Text(props, nameof(Grimoire.MasterySourceData)),
            Text(props, nameof(Grimoire.CreationData)), Text(props, nameof(Grimoire.RebirthData)), path,
            Bool(pack, nameof(TravelPack.OperationBlocked)) || Text(pack, nameof(TravelPack.OperationPending)).Length > 0);
        snapshot.Validate(Int(pack, nameof(TravelPack.DataSchema)));
        if (snapshot.Protocol < StartupState.Protocol) return; // immutable counters survive a backed-up opaque migration
        var sources = MasterySources.Read(snapshot.Mastery);
        var nativeMask = Int(record, nameof(HeroRecord.NativeMask));
        if (nativeMask < 0 || (nativeMask & ~159) != 0) throw new FormatException("Invalid saved native-school mask");
        // Interrupted native effects may have saved between journal/counter/point updates. Preserve the exact
        // blocked state for whole-run checkpoint recovery; it is never a license to replay or refund those fields.
        if (!snapshot.Interrupted)
        {
            sources.RequireCounters(counters);
            sources.RequireHero(hero, learned, investments[4]);
        }
        if (snapshot.Legacy) return;
        var start = HeroCreationPlan.Read(snapshot.Start);
        ValidateDecisions(hero, start);
        var rebirth = snapshot.Rebirth.Length == 0 ? null : HeroCreationPlan.Read(snapshot.Rebirth);
        if (rebirth != null) ValidateDecisions(hero, rebirth);
        if (snapshot.Interrupted) return; // keep the exact uncertain state blocked; never reconstruct a partial callback
        var effective = rebirth is { IdentityApplied: true } ? rebirth : start;
        var origin = origins.Count == 0 ? null : (OriginRelic)ModelDb.GetById<RelicModel>(origins[0].Id);
        var background = backgrounds.Count == 0 ? null : (BackgroundRelic)ModelDb.GetById<RelicModel>(backgrounds[0].Id);
        if (!effective.IdentityApplied)
        {
            if (origin != null || background != null) throw new FormatException("Unreceipted identity outside an interrupted effect");
            return;
        }
        if ((origin?.Id.Entry ?? HeroCreation.None) != effective.Origin || (background?.Id.Entry ?? HeroCreation.None) != effective.Background
            || Text(props, nameof(Grimoire.OriginChoiceId)) != effective.Origin || Text(props, nameof(Grimoire.BackgroundChoiceId)) != effective.Background)
            throw new FormatException("Saved creation receipt disagrees with the actual identity");
        if (sources.IdentityKnown)
        {
            var expected = sources.WithIdentity(HeroIdentityRules.OriginMastery(origin?.GetType().Name), HeroIdentityRules.BackgroundMastery(background?.GetType().Name));
            if (sources.Encode() != expected.Encode()) throw new FormatException("Saved mastery identity sources disagree with the actual relics");
            var mask = 128;
            foreach (var school in Enum.GetValues<SpellSchool>()) if (sources.IsIdentitySchool(school)) mask |= 1 << (int)school;
            if (mask != nativeMask) throw new FormatException("Saved native schools disagree with identity sources");
        }
        if (background != null && !Bool(backgrounds[0].Props, nameof(BackgroundRelic.StartupSetupDone)))
            throw new FormatException("Current identity has no completed one-shot setup receipt");
    }

    private static void ValidateDecisions(string hero, HeroCreationPlan plan)
    {
        OriginRelic? origin = null;
        if (plan.Stage >= CreationStage.Background)
        {
            origin = HeroCreation.Origin(plan.Origin);
            if (origin != null && !HeroIdentityRules.OriginTypes(hero).Contains(origin.GetType().Name))
                throw new FormatException("Saved origin belongs to another hero");
        }
        if (plan.Stage >= CreationStage.Bonus)
        {
            var background = HeroCreation.Background(plan.Background);
            if (background != null && background is not VagrantBackground && !BackgroundRelic.Options(origin).Contains(background.GetType()))
                throw new FormatException("Saved background belongs to another origin");
        }
        if (plan.Mode == CreationMode.Start && plan.IdentityApplied && HeroCreation.Background(plan.Background) is VagrantBackground
            && plan.BackgroundSource.Length == 0) throw new FormatException("Vagrant identity has no removal/skip decision receipt");
        if (plan.BackgroundSource.Length > 0)
        {
            if (HeroCreation.Background(plan.Background) is not VagrantBackground)
                throw new FormatException("Unexpected saved background gift source");
            if (plan.BackgroundSource != HeroCreation.None)
            {
                var removal = HeroCreation.ReadCard(plan.BackgroundSource);
                if (removal.Id == null || ModelDb.GetByIdOrNull<CardModel>(removal.Id) == null || InventoryIdentity.CardKey(removal).Length == 0)
                    throw new FormatException("Saved Vagrant removal lacks its exact card source");
            }
        }
        if (plan.BonusSource.Length == 0) return;
        if (plan.Bonus == StartingBonus.Potion)
        {
            var source = HeroCreation.ReadPotion(plan.BonusSource);
            if (source.Id == null || ModelDb.GetByIdOrNull<PotionModel>(source.Id) is not { Rarity: PotionRarity.Common })
                throw new FormatException("Saved starter potion source is not an available common potion");
        }
        else if (plan.Bonus == StartingBonus.RemoveBasic)
        {
            var source = HeroCreation.ReadCard(plan.BonusSource);
            if (source.Id == null || ModelDb.GetByIdOrNull<CardModel>(source.Id) is not { Rarity: CardRarity.Basic }
                || InventoryIdentity.CardKey(source).Length == 0) throw new FormatException("Saved starter removal lacks its exact basic-card source");
        }
    }

    private static HashSet<Skill> ReadSkills(string encoded)
    {
        var learned = new HashSet<Skill>();
        if (encoded.Length == 0) return learned;
        foreach (var name in encoded.Split(','))
            if (!Enum.TryParse<Skill>(name, out var skill) || !Enum.IsDefined(skill) || skill.ToString() != name || !learned.Add(skill))
                throw new FormatException("Unknown/duplicate saved skill; no silent parse/drop migration");
        return learned;
    }
    private static int Int(SavedProperties? props, string name) => RunSettings.SavedInt(props, name);
    private static bool Bool(SavedProperties? props, string name) => RunSettings.SavedBool(props, name);
    private static string Text(SavedProperties? props, string name) => RunSettings.SavedString(props, name) ?? "";
}
