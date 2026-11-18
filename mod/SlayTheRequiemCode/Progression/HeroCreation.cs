using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;
using SlayTheRequiem.SlayTheRequiemCode.UI;
using Choice = SlayTheRequiem.SlayTheRequiemCode.UI.NHeroCreationScreen.Choice;

namespace SlayTheRequiem.SlayTheRequiemCode.Progression;

/// <summary>
/// Native adapter for the saved creation plan. The shared inventory lease serializes decisions, but uncertainty
/// markers cover EFFECTS ONLY, not the time spent choosing. Every await with an irreversible callback is tracked.
/// Neow/Omen remain native and independent; no random fallback or second starter gift is hidden here.
/// </summary>
internal static class HeroCreation
{
    internal const string None = "none";
    private const string Back = "back", Random = "random", Confirm = "confirm", Cancel = "cancel";

    internal static async Task ResumeStart(Grimoire grimoire, OperationGate.Lease lease)
    {
        var player = grimoire.Owner;
        if (grimoire.LegacyCreation) return;
        var plan = HeroCreationPlan.Read(grimoire.CreationData);
        void Save(HeroCreationPlan next) { grimoire.CreationData = next.Encode(); plan = next; }
        while (plan.Stage != CreationStage.Complete)
        {
            if (plan.Stage < CreationStage.Applying)
            {
                await Draft(player, plan, Save);
                plan = HeroCreationPlan.Read(grimoire.CreationData);
            }
            if (plan.Stage == CreationStage.Applying)
            {
                await ApplyConfirmedStart(grimoire, lease);
                plan = HeroCreationPlan.Read(grimoire.CreationData);
                if (plan.Stage < CreationStage.Applying) continue;
            }
            if (plan.Stage == CreationStage.Receipt)
            {
                await NHeroCreationScreen.Pick(player, Text("receipt"), Receipt(player, plan), [new(Confirm, Text("begin"))]);
                Save(plan.Acknowledge());
                grimoire.StartPhase = StartupState.Complete;
                grimoire.OriginGranted = true; // only after the independent bonus AND the actual summary acknowledgment
                HeroRecord.Of(player)?.RecordNativeSchools();
            }
        }
    }

    /// <summary>Apply only a confirmed plan; the mandatory result screen is a separate resumable boundary.</summary>
    internal static async Task ApplyConfirmedStart(Grimoire grimoire, OperationGate.Lease lease)
    {
        var player = grimoire.Owner;
        var plan = HeroCreationPlan.Read(grimoire.CreationData);
        if (grimoire.LegacyCreation || plan.Stage != CreationStage.Applying)
            throw new InvalidOperationException("Starting effects require an unconsumed confirmed plan");
        void Save(HeroCreationPlan next) { grimoire.CreationData = next.Encode(); plan = next; }
        if (!plan.IdentityApplied)
        {
            RequireChoices(player, plan);
            if (player.Relics.Any(r => r is OriginRelic or BackgroundRelic))
                throw new InvalidOperationException("Unreceipted starting identity already exists; do not replay its one-shot gifts");
            await PrepareBackground(player, plan, Save); // Vagrant's removal is a durable decision, not an open mutation
            plan = HeroCreationPlan.Read(grimoire.CreationData);
            using var mutation = InventoryGate.Track(player, lease, "hero-start:identity");
            try
            {
                if (Origin(plan.Origin) is { } origin) await RelicCmd.Obtain(origin.ToMutable(), player);
                if (Background(plan.Background) is { } background) await RelicCmd.Obtain(background.ToMutable(), player);
                CheckGate(player);
                RequireObservedIdentity(player, plan);
                grimoire.RefreshIdentitySources();
                grimoire.OriginChoiceId = plan.Origin;
                grimoire.BackgroundChoiceId = plan.Background;
                Save(plan.IdentityComplete());
                mutation.Complete();
            }
            catch (Exception e) { Fail(player, mutation, "startup identity", e); throw; }
        }
        RequireObservedIdentity(player, plan);
        if (!await PrepareBonus(player, plan, Save)) return;
        plan = HeroCreationPlan.Read(grimoire.CreationData);
        using var bonusMutation = InventoryGate.Track(player, lease, "hero-start:bonus:" + plan.Bonus);
        try
        {
            if (!await ApplyBonus(player, plan))
            {
                Save(plan.RetryBonusChoice()); // a known refusal, no bonus consumed, identity gifts are not repeated
                bonusMutation.Complete();
                return;
            }
            CheckGate(player);
            Save(plan.EffectsComplete()); // receipt precedes clearing the write-ahead effect marker
            TravelPack.Of(player)?.Refresh();
            bonusMutation.Complete();
        }
        catch (Exception e) { Fail(player, bonusMutation, "startup bonus", e); throw; }
    }

    // Save the draft after every synchronized decision. Back never removes a relic or spends a point.
    private static async Task<bool> Draft(Player player, HeroCreationPlan initial, Action<HeroCreationPlan> save)
    {
        var plan = initial;
        void Save(HeroCreationPlan next) { next.Validate(); save(next); plan = next; }
        while (plan.Stage < CreationStage.Applying)
        {
            var choices = new List<Choice>();
            var rebirth = plan.Mode == CreationMode.Rebirth;
            var heading = Text(rebirth ? "rebirth" : "heading") + " · " + Text("stage." + plan.Stage);
            var body = Text(rebirth ? "rebirthRules" : "rules");
            switch (plan.Stage)
            {
                case CreationStage.Origin:
                {
                    var options = OriginRelic.Choices(player)?.OfType<OriginRelic>().ToList()
                        ?? throw new InvalidOperationException("Unsupported hero identity");
                    choices.AddRange(options.Select(r => RelicChoice(r, HeroIdentityRules.OriginMastery(r.GetType().Name))));
                    choices.Add(new(None, Text("noOrigin"), Text("noneHint")));
                    choices.Add(new(Random, Text("random"), Text("randomHint")));
                    if (rebirth) choices.Add(new(Cancel, Text("cancelRebirth")));
                    var id = await NHeroCreationScreen.Pick(player, heading, body, choices);
                    if (id == Cancel) return false;
                    if (id == Random) id = options[OriginRelic.OriginRng(player, "wizard_origin").NextInt(options.Count)].Id.Entry;
                    Save(plan.ChooseOrigin(id));
                    break;
                }
                case CreationStage.Background:
                {
                    var options = BackgroundOptions(Origin(plan.Origin)).ToList();
                    choices.AddRange(options.Select(r => RelicChoice(r, HeroIdentityRules.BackgroundMastery(r.GetType().Name), BackgroundAllowed(player, r))));
                    choices.Add(new(None, Text("noBackground"), Text("noneHint")));
                    choices.Add(new(Random, Text("random"), Text("randomHint"), options.Any(r => BackgroundAllowed(player, r))));
                    choices.Add(new(Back, Text("back")));
                    if (rebirth) choices.Add(new(Cancel, Text("cancelRebirth")));
                    body += "\n" + Text("selectedOrigin", ("Name", Title(Origin(plan.Origin))));
                    var id = await NHeroCreationScreen.Pick(player, heading, body, choices);
                    if (id == Cancel) return false;
                    if (id == Back) { Save(plan.Back()); break; }
                    if (id == Random)
                    {
                        var ready = options.Where(r => BackgroundAllowed(player, r)).ToList();
                        id = ready[OriginRelic.OriginRng(player, "wizard_background_" + plan.Origin).NextInt(ready.Count)].Id.Entry;
                    }
                    Save(plan.ChooseBackground(id));
                    break;
                }
                case CreationStage.Bonus:
                {
                    choices.Add(new(nameof(StartingBonus.Gold), Text("gold"), Text("goldHint", ("Amount", HeroCreationPlan.GoldGift))));
                    choices.Add(new(nameof(StartingBonus.Potion), Text("potion"), Text("potionHint"),
                        !plan.IdentityApplied || player.HasOpenPotionSlots && CommonPotions(player).Count > 0));
                    choices.Add(new(nameof(StartingBonus.RemoveBasic), Text("remove"), Text("removeHint"), player.Deck.Cards.Any(Basic)));
                    if (!plan.IdentityApplied) choices.Add(new(Back, Text("back")));
                    else body += "\n" + Text("bonusRetry");
                    var id = await NHeroCreationScreen.Pick(player, heading, body, choices);
                    if (id == Back) { Save(plan.Back()); break; }
                    Save(plan.ChooseBonus(Enum.Parse<StartingBonus>(id)));
                    break;
                }
                case CreationStage.Review:
                {
                    RequireChoices(player, plan);
                    choices.Add(new(Confirm, Text(rebirth ? "confirmRebirth" : "apply")));
                    choices.Add(new(Back, Text("back")));
                    if (rebirth) choices.Add(new(Cancel, Text("cancelRebirth")));
                    var id = await NHeroCreationScreen.Pick(player, heading, Review(player, plan), choices);
                    if (id == Cancel) return false;
                    if (id == Back) { Save(plan.Back()); break; }
                    if (rebirth) RequireRebirth(player);
                    Save(plan.Confirm(Capture(player), rebirth ? Fingerprint(player) : ""));
                    break;
                }
                default: throw new InvalidOperationException("Unknown hero decision stage");
            }
        }
        return true;
    }

    private static async Task PrepareBackground(Player player, HeroCreationPlan plan, Action<HeroCreationPlan> save)
    {
        if (Background(plan.Background) is not VagrantBackground)
        {
            if (plan.BackgroundSource.Length > 0) throw new FormatException("Only Vagrant has a preselected background removal");
            return;
        }
        if (plan.BackgroundSource.Length > 0) return;
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1) { Cancelable = true, RequireManualConfirmation = true };
        var selected = (await CardSelectCmd.FromDeckForRemoval(player, prefs)).ToList();
        if (selected.Count > 1 || selected.Any(c => !player.Deck.Cards.Contains(c) || !c.IsRemovable))
            throw new InvalidOperationException("Invalid synchronized Vagrant removal choice");
        InventoryIdentity.Ensure(player);
        save(plan.PrepareBackgroundSource(selected.Count == 0 ? None : TravelPack.CardPayload(selected[0].ToSerializable())));
    }

    internal static async Task ApplyVagrantRemoval(Player player)
    {
        var grimoire = Mastery.GrimoireOf(player) ?? throw new InvalidOperationException("Missing Vagrant creation plan");
        var plan = HeroCreationPlan.Read(grimoire.CreationData);
        if (!InventoryGate.IsMutating(player) || plan.Stage != CreationStage.Applying || plan.IdentityApplied
            || Background(plan.Background) is not VagrantBackground || plan.BackgroundSource.Length == 0)
            throw new InvalidOperationException("Vagrant's one-shot removal was not selected before its effects");
        if (plan.BackgroundSource != None) await RemoveExactCard(player, plan.BackgroundSource, false);
    }

    private static async Task<bool> PrepareBonus(Player player, HeroCreationPlan plan, Action<HeroCreationPlan> save)
    {
        if (plan.BonusSource.Length > 0 || plan.Bonus == StartingBonus.Gold) return true;
        if (plan.Bonus == StartingBonus.Potion)
        {
            var options = CommonPotions(player);
            if (!player.HasOpenPotionSlots || options.Count == 0) { save(plan.RetryBonusChoice()); return false; }
            var potion = options[OriginRelic.OriginRng(player, "wizard_starter_potion").NextInt(options.Count)].ToMutable();
            save(plan.PrepareBonusSource(JsonSerializer.Serialize(potion.ToSerializable(-1), JsonSerializationUtility.GetTypeInfo<SerializablePotion>())));
            return true;
        }
        if (plan.Bonus != StartingBonus.RemoveBasic) throw new InvalidOperationException("Starter bonus missing");
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1) { Cancelable = true, RequireManualConfirmation = true };
        var selected = (await CardSelectCmd.FromDeckForRemoval(player, prefs, Basic)).ToList();
        if (selected.Count == 0) { save(plan.RetryBonusChoice()); return false; } // explicit return to bonus, not random/removal
        if (selected.Count != 1 || !player.Deck.Cards.Contains(selected[0]) || !Basic(selected[0]))
            throw new InvalidOperationException("Invalid synchronized basic-card removal choice");
        InventoryIdentity.Ensure(player);
        save(plan.PrepareBonusSource(TravelPack.CardPayload(selected[0].ToSerializable())));
        return true;
    }

    private static async Task<bool> ApplyBonus(Player player, HeroCreationPlan plan)
    {
        switch (plan.Bonus)
        {
            case StartingBonus.Gold:
                await PlayerCmd.GainGold(HeroCreationPlan.GoldGift, player);
                return true;
            case StartingBonus.Potion:
            {
                var source = ReadPotion(plan.BonusSource);
                var potion = PotionModel.FromSerializable(source);
                if (potion.Rarity != PotionRarity.Common) throw new FormatException("Starter potion is not common");
                var result = await PotionCmd.TryToProcure(potion, player);
                if (!result.success && player.PotionSlots.Contains(potion)) throw new InvalidOperationException("Potion inserted without a successful receipt");
                if (result.success && !player.PotionSlots.Contains(potion)) throw new InvalidOperationException("Starter potion disappeared during its callback");
                return result.success;
            }
            case StartingBonus.RemoveBasic:
            {
                await RemoveExactCard(player, plan.BonusSource, true);
                return true;
            }
            default: throw new InvalidOperationException("Starter bonus unavailable");
        }
    }

    private static async Task RemoveExactCard(Player player, string payload, bool basicOnly)
    {
        var source = ReadCard(payload);
        var key = InventoryIdentity.CardKey(source);
        var card = key.Length > 0 ? InventoryIdentity.FindCard(player, key) : null;
        if (card == null || !card.IsRemovable || basicOnly && !Basic(card)
            || !SavedPayload.Equivalent(payload, TravelPack.CardPayload(card.ToSerializable())))
            throw new InvalidOperationException("Exact starter removal source changed; no equal-looking card substituted");
        await CardPileCmd.RemoveFromDeck([card]);
        if (player.Deck.Cards.Contains(card)) throw new InvalidOperationException("Starter removal did not commit");
    }

    /// <summary>Runs before obtaining the Path essence. An ineligible/cancelled respec never consumes a Path.</summary>
    internal static async Task<bool> PrepareRebirth(Player player)
    {
        var grimoire = Mastery.GrimoireOf(player) ?? throw new InvalidOperationException("Missing Grimoire");
        if (!CanRebirth(player))
        {
            await NHeroCreationScreen.Pick(player, Text("rebirth"), Text("legacyRebirth"), [new(Back, Text("back"))]);
            return false;
        }
        var plan = grimoire.RebirthData.Length == 0 ? HeroCreationPlan.Empty(CreationMode.Rebirth) : HeroCreationPlan.Read(grimoire.RebirthData);
        if (plan.Stage >= CreationStage.Receipt) throw new InvalidOperationException("Rebirth already receipted; do not issue another Path");
        grimoire.RebirthData = plan.Encode();
        if (!await Draft(player, plan, next => grimoire.RebirthData = next.Encode()))
        {
            grimoire.RebirthData = ""; // explicit cancel; quit/disconnect instead retains the in-progress draft
            return false;
        }
        RequireRebirth(player);
        plan = HeroCreationPlan.Read(grimoire.RebirthData);
        RequireChoices(player, plan);
        if (plan.SourceFingerprint != Fingerprint(player))
        {
            // No effect yet. Reconfirm a fresh preview; never silently apply a respec against a stale source.
            grimoire.RebirthData = (plan with { Stage = CreationStage.Review, Baseline = null, SourceFingerprint = "" }).Encode();
            return await PrepareRebirth(player);
        }
        return true;
    }

    internal static bool CanRebirth(Player player)
    {
        try { RequireRebirth(player); return true; }
        catch (Exception e) when (e is FormatException or InvalidOperationException) { return false; }
    }
    private static void RequireRebirth(Player player)
    {
        if (InventoryGate.For(player).Blocked || Mastery.GrimoireOf(player) is not { StartingChoicesComplete: true } grimoire
            || HeroRecord.Of(player) is not { } hero || !grimoire.Sources.IdentityKnown)
            throw new InvalidOperationException("Rebirth requires a completed, source-accounted hero");
        var sources = grimoire.Sources;
        sources.RequireHero(player.Character.GetType().Name, hero.Learned, hero.StatMastery);
        var expected = sources.WithIdentity(HeroIdentityRules.OriginMastery(player.Relics.OfType<OriginRelic>().SingleOrDefault()?.GetType().Name),
            HeroIdentityRules.BackgroundMastery(player.Relics.OfType<BackgroundRelic>().SingleOrDefault()?.GetType().Name));
        if (sources.Encode() != expected.Encode() || player.Relics.OfType<Pantheon.BoonRelic>().Any(b => b.GrantedMaxHp != b.ActiveBoon.MaxHp))
            throw new InvalidOperationException("Identity/HP reconciliation is not a known source snapshot");
    }

    /// <summary>Called only inside the tracked Path obtain; no selector or nested inventory acquisition here.</summary>
    internal static async Task ApplyRebirth(Player player)
    {
        if (!InventoryGate.IsMutating(player)) throw new InvalidOperationException("Rebirth effects require the Path mutation scope");
        RequireRebirth(player);
        var grimoire = Mastery.GrimoireOf(player)!;
        var hero = HeroRecord.Of(player)!;
        var plan = HeroCreationPlan.Read(grimoire.RebirthData);
        if (plan.Stage != CreationStage.Applying || plan.IdentityApplied) throw new InvalidOperationException("Rebirth was not confirmed before Path obtain");
        RequireChoices(player, plan);
        var oldOrigin = player.Relics.OfType<OriginRelic>().SingleOrDefault();
        var oldBackground = player.Relics.OfType<BackgroundRelic>().SingleOrDefault();
        var origin = Origin(plan.Origin);
        var background = Background(plan.Background);
        var hpDelta = (origin?.IdentityMaxHp ?? 0) + (background?.IdentityMaxHp ?? 0)
            - (oldOrigin?.IdentityMaxHp ?? 0) - (oldBackground?.IdentityMaxHp ?? 0);
        var sources = grimoire.Sources.WithoutStatPurchases();
        // Same identity retains its own progression/counters. A changed identity gets passives, not starter setup.
        if (oldBackground?.Id.Entry != background?.Id.Entry)
        {
            if (oldBackground != null) await RelicCmd.Remove(oldBackground);
            if (background != null)
            {
                var replacement = (BackgroundRelic)background.ToMutable();
                replacement.IdentityReplacement = true; replacement.StartupSetupDone = true;
                await RelicCmd.Obtain(replacement, player);
            }
        }
        if (oldOrigin?.Id.Entry != origin?.Id.Entry)
        {
            if (oldOrigin != null) await RelicCmd.Remove(oldOrigin);
            if (origin != null)
            {
                var replacement = (OriginRelic)origin.ToMutable(); replacement.IdentityReplacement = true;
                await RelicCmd.Obtain(replacement, player);
            }
        }
        RequireObservedIdentity(player, plan);
        hero.StatHealth = hero.StatStamina = hero.StatMana = hero.StatBurden = hero.StatMastery = 0;
        grimoire.ApplySources(sources.WithIdentity(HeroIdentityRules.OriginMastery(origin?.GetType().Name),
            HeroIdentityRules.BackgroundMastery(background?.GetType().Name)));
        grimoire.OriginChoiceId = plan.Origin; grimoire.BackgroundChoiceId = plan.Background;
        hero.RecordNativeSchools();
        // Reconcile the combined HP delta once; two separately clamped losses/gains could create free max HP.
        await Pantheon.BoonRelic.SyncIdentityMaxHp(player, hpDelta);
        CheckGate(player);
        grimoire.RebirthData = plan.IdentityComplete().EffectsComplete().Encode();
        TravelPack.Of(player)?.Refresh(); hero.Refresh();
        Pantheon.Faith.RefreshAll(player);
    }

    internal static async Task ResumeRebirth(Grimoire grimoire, OperationGate.Lease lease)
    {
        if (grimoire.RebirthData.Length == 0) return;
        var plan = HeroCreationPlan.Read(grimoire.RebirthData);
        if (plan.Stage == CreationStage.Complete) return;
        if (plan.Stage < CreationStage.Receipt)
        {
            if (Paths.Of(grimoire.Owner) != null) throw new InvalidOperationException("Unreceipted rebirth Path; retain the transition checkpoint");
            await Paths.Grant(grimoire.Owner, HeroPath.Reborn, lease); // resume a saved pre-consumption choice
            return;
        }
        if (Paths.PathOf(grimoire.Owner) != HeroPath.Reborn) throw new InvalidOperationException("Rebirth receipt without its Path");
        await NHeroCreationScreen.Pick(grimoire.Owner, Text("receipt"), Receipt(grimoire.Owner, plan), [new(Confirm, Text("continue"))]);
        grimoire.RebirthData = plan.Acknowledge().Encode();
    }

    private static void RequireObservedIdentity(Player player, HeroCreationPlan plan)
    {
        if ((player.Relics.OfType<OriginRelic>().SingleOrDefault()?.Id.Entry ?? None) != plan.Origin
            || (player.Relics.OfType<BackgroundRelic>().SingleOrDefault()?.Id.Entry ?? None) != plan.Background)
            throw new InvalidOperationException("Identity callbacks did not commit the exact confirmed choices");
    }

    internal static void RequireChoices(Player player, HeroCreationPlan plan)
    {
        var origin = Origin(plan.Origin);
        var background = Background(plan.Background);
        if (origin != null && OriginRelic.Choices(player)?.All(r => r.Id != origin.Id) != false
            || background != null && BackgroundOptions(origin).All(r => r.Id != background.Id)
            || background != null && !BackgroundAllowed(player, background))
            throw new FormatException("Saved identity is incompatible with this hero/origin/form; no choice substituted");
    }
    internal static OriginRelic? Origin(string id) => id == None ? null : ModelDb.AllRelics.OfType<OriginRelic>()
        .SingleOrDefault(r => r.Id.Entry == id) ?? throw new FormatException("Saved origin model unavailable");
    internal static BackgroundRelic? Background(string id) => id == None ? null : ModelDb.AllRelics.OfType<BackgroundRelic>()
        .SingleOrDefault(r => r.Id.Entry == id) ?? throw new FormatException("Saved background model unavailable");
    private static IEnumerable<BackgroundRelic> BackgroundOptions(OriginRelic? origin) => BackgroundRelic.Options(origin)
        .Where(t => t != null).Append(typeof(VagrantBackground)).Distinct()
        .Select(t => (BackgroundRelic)ModelDb.GetById<RelicModel>(ModelDb.GetId(t!)));
    private static bool BackgroundAllowed(Player player, BackgroundRelic background) => background is not OrdinatorBackground
        || !TransformationRules.OrdinatorForbids(Forms.Worldview(player));
    private static bool Basic(CardModel card) => card.Rarity == CardRarity.Basic && card.IsRemovable;
    private static List<PotionModel> CommonPotions(Player player) => PotionFactory.GetPotionOptions(player, Array.Empty<PotionModel>())
        .Where(p => p.Rarity == PotionRarity.Common).OrderBy(p => p.Id.Entry, StringComparer.Ordinal).ToList();
    internal static SerializablePotion ReadPotion(string payload) => RawRunDocument.DeserializeStrict<SerializablePotion>(Encoding.UTF8.GetBytes(payload), JsonSerializationUtility.Options);
    internal static SerializableCard ReadCard(string payload) => RawRunDocument.DeserializeStrict<SerializableCard>(Encoding.UTF8.GetBytes(payload), JsonSerializationUtility.Options);

    internal static CreationBaseline Capture(Player player) => new(player.Creature.MaxHp, StaminaPool(player), Mana.Pool(player),
        HeroStatRules.BurdenCapacity(player), player.Gold, Mastery.GrimoireOf(player)!.MasteryCounters, player.Deck.Cards.Select(c => c.Id.Entry).ToArray(),
        HeroRecord.Of(player)?.StatPointsSpent ?? 0, HeroRecord.Of(player)?.UnspentStatPoints ?? 0);
    private static int StaminaPool(Player player) => Math.Max(1, RequiemMode.StatsFor(player).StaminaPool + Loadout.Bonuses(player).StaminaPoolBonus
        + Mana.ManaSources(player).Sum(s => s.StaminaPoolBonus));
    internal static string Fingerprint(Player player)
    {
        var save = player.ToSerializable();
        save.Relics.RemoveAll(r => r.Id == ModelDb.Relic<Grimoire>().Id || r.Id == ModelDb.Relic<RebornEssence>().Id);
        var payload = JsonSerializer.Serialize(save, JsonSerializationUtility.GetTypeInfo<SerializablePlayer>())
            + "|" + Mastery.GrimoireOf(player)!.MasterySourceData;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
    private static Choice RelicChoice(RelicModel relic, int[] schools, bool allowed = true) => new(relic.Id.Entry,
        relic.Title.GetFormattedText(), Plain(relic.DynamicDescription.GetFormattedText()) + MasteryHint(schools)
            + (allowed ? "" : "\n" + Text("incompatible")), allowed);
    private static string MasteryHint(IReadOnlyList<int> levels) => string.Concat(Mastery.Schools.Where(s => levels[(int)s] > 0)
        .Select(s => "\n" + Text("identitySchool", ("School", School(s)))));
    private static string Review(Player player, HeroCreationPlan plan)
    {
        var origin = Origin(plan.Origin); var background = Background(plan.Background);
        var text = Text(plan.Mode == CreationMode.Start ? "reviewRules" : "rebirthRules") + "\n\n"
            + Text("selectedOrigin", ("Name", Title(origin))) + "\n" + (origin == null ? "" : Plain(origin.DynamicDescription.GetFormattedText()))
            + "\n\n" + Text("selectedBackground", ("Name", Title(background))) + "\n" + (background == null ? "" : Plain(background.DynamicDescription.GetFormattedText()));
        var sources = Mastery.GrimoireOf(player)!.Sources;
        if (plan.Mode == CreationMode.Rebirth)
        {
            text += "\n\n" + Text("refund", ("Points", HeroRecord.Of(player)!.StatPointsSpent));
            sources = sources.WithoutStatPurchases();
        }
        else text += "\n\n" + Text("selectedBonus", ("Name", BonusName(plan.Bonus)));
        sources = sources.WithIdentity(HeroIdentityRules.OriginMastery(origin?.GetType().Name), HeroIdentityRules.BackgroundMastery(background?.GetType().Name));
        text += "\n\n" + string.Join("\n", Mastery.Schools.Select(s => Text("schoolPreview", ("School", School(s)), ("Level", sources.Level(s)),
            ("Native", Text(sources.IsIdentitySchool(s) ? "native" : "foreign")))));
        return text;
    }
    private static string Receipt(Player player, HeroCreationPlan plan)
    {
        var before = plan.Baseline ?? throw new FormatException("Creation receipt baseline missing");
        var after = Capture(player);
        var text = Text("selectedOrigin", ("Name", Title(Origin(plan.Origin)))) + "\n"
            + Text("selectedBackground", ("Name", Title(Background(plan.Background)))) + "\n"
            + Text(plan.Mode == CreationMode.Start ? "selectedBonus" : "rebirthPreserved", ("Name", BonusName(plan.Bonus))) + "\n\n";
        text += Text("resources", ("OldHp", before.MaxHp), ("Hp", after.MaxHp), ("OldStamina", before.Stamina), ("Stamina", after.Stamina),
            ("OldMana", before.Mana), ("Mana", after.Mana), ("OldCapacity", before.Capacity), ("Capacity", after.Capacity),
            ("Weight", Loadout.TotalWeight(player)), ("OldGold", before.Gold), ("Gold", after.Gold));
        if (plan.Mode == CreationMode.Rebirth)
            text += "\n" + Text("refunded", ("Spent", before.StatPointsSpent), ("OldFree", before.UnspentStatPoints), ("Free", after.UnspentStatPoints));
        text += "\n" + string.Join("\n", Mastery.Schools.Select(s => Text("schoolChange", ("School", School(s)),
            ("Before", before.Mastery[(int)s]), ("After", after.Mastery[(int)s]), ("Native", Text(HeroRecord.Of(player)?.IsNative(s) == true ? "native" : "foreign")))));
        text += "\n\n" + Text("deckChanges") + "\n" + DeckChanges(before.Deck, after.Deck);
        text += "\n\n" + Text("deck", ("Count", after.Deck.Length)) + "\n" + DeckList(after.Deck);
        if (TravelPack.Of(player) is { PendingCards.Count: > 0 } pack) text += "\n\n" + Text("pending", ("Count", pack.PendingCards.Count));
        if (plan.Mode == CreationMode.Start && plan.Bonus == StartingBonus.Potion)
            text += "\n" + Text("potionReceived", ("Name", ModelDb.GetById<PotionModel>(ReadPotion(plan.BonusSource).Id).Title.GetFormattedText()));
        return text + "\n\n" + Text("separate");
    }
    private static string DeckChanges(string[] before, string[] after)
    {
        var rows = before.Concat(after).Distinct().OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => (id, delta: after.Count(n => n == id) - before.Count(n => n == id))).Where(p => p.delta != 0)
            .Select(p => (p.delta > 0 ? "+" : "") + p.delta + " × " + CardTitle(p.id)).ToList();
        return rows.Count == 0 ? Text("unchanged") : string.Join("\n", rows);
    }
    private static string DeckList(string[] deck) => string.Join("\n", deck.GroupBy(id => id).Select(g => g.Count() + " × " + CardTitle(g.Key)));
    private static string CardTitle(string id) => ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == id)?.Title.GetFormattedText() ?? id;
    private static string Title(RelicModel? relic) => relic?.Title.GetFormattedText() ?? Text("none");
    private static string BonusName(StartingBonus bonus) => Text(bonus switch
    {
        StartingBonus.Gold => "gold", StartingBonus.Potion => "potion", StartingBonus.RemoveBasic => "remove", _ => "none",
    });
    private static string School(SpellSchool school) => Text("school." + school);
    private static string Plain(string text) => Regex.Replace(text, @"\[[^\]]+\]", "");
    private static string Text(string key, params (string Name, object Value)[] values)
    {
        var loc = RequiemUi.Tip("CREATION." + key + ".title");
        foreach (var (name, value) in values)
            if (value is int n) loc.Add(name, n); else loc.Add(name, value.ToString() ?? "");
        return loc.GetFormattedText();
    }
    private static void CheckGate(Player player)
    {
        if (InventoryGate.For(player).Blocked) throw new InvalidOperationException("An identity callback has an unresolved inventory outcome");
    }
    private static void Fail(Player player, InventoryGate.Mutation mutation, string step, Exception e)
    {
        InventoryGate.Block(player, $"Hero {step} callback interrupted; restore its pre-effect checkpoint instead of replaying: {e}");
        mutation.Complete();
    }
}
