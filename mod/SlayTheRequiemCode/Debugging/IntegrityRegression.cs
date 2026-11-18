using System.Text.Json;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using SlayTheRequiem.SlayTheRequiemCode.Events;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Potions;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Shops;

namespace SlayTheRequiem.SlayTheRequiemCode.Debugging;

/// <summary>
/// REAL game models, commands, BaseLib SavedProperties and production adapters; no mirror rules or test-only
/// Harmony overrides. Isolated non-local players (no local discovery/save/sync), but still run only in a disposable
/// SINGLEPLAYER run, outside combat, BaseLib + this mod only. Never swap RunManager.State/TestMode or write saves.
/// This is an adapter regression, NOT reward UI, disk Continue/rollback, two-client or visual acceptance.
/// </summary>
public static partial class IntegrityRegression
{
    private static bool _running;
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonSerializationUtility.GetTypeInfo<T>());
    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize(Json(value), JsonSerializationUtility.GetTypeInfo<T>())!;
    private static void Require(bool condition, string detail) { if (!condition) throw new InvalidOperationException(detail); }
    private static TravelPack Pack(Player owner) => TravelPack.Of(owner) ?? throw new InvalidOperationException("Fixture TravelPack missing");

    private static Player Fixture()
    {
        var owner = Player.CreateForNewRun<Ironclad>(UnlockState.all, ulong.MaxValue);
        Require(!LocalContext.IsMe(owner), "Fixture must not be the local player");
        var run = RunState.CreateForTest([owner], seed: "STR-G1-INTEGRITY");
        run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, null);
        foreach (var card in owner.Deck.Cards.ToList()) { card.RemoveFromCurrentPile(silent: true); owner.RunState.RemoveCard(card); }
        Require(TravelPack.Of(owner) != null && HerbalistPouch.Of(owner) != null, "Enabled Requiem systems required");
        return owner;
    }

    private static CardModel DeckCard(Player owner)
    {
        var card = owner.RunState.CreateCard<Bash>(owner);
        owner.Deck.AddInternal(card, silent: true);
        return card;
    }

    private static Player Reload(Player owner)
    {
        var restored = Player.FromSerializable(RoundTrip(owner.ToSerializable()));
        RunState.CreateForTest([restored], seed: "STR-G1-INTEGRITY");
        return restored;
    }

    private static void FillToExcess(Player owner, int excess)
    {
        var target = Loadout.Capacity(owner) + excess;
        for (var i = 0; Loadout.TotalWeight(owner) < target && i < 256; i++) DeckCard(owner);
        Require(Loadout.Overload(owner).Excess == excess, "Could not construct exact burden boundary");
    }

    private static string LiveFingerprint(RunState live) => OperationIdentity.Hash(string.Join("\n",
        live.Players.Select(p => Json(p.ToSerializable()))) + $"|{Json(live.Rng.ToSerializable())}|{live.CurrentActIndex}|{live.TotalFloor}|{Config.RunSettings.Active?.Encode()}");

    public static async Task Run()
    {
        if (_running) throw new InvalidOperationException("Integrity suite already running");
        var live = RunManager.Instance.DebugOnlyGetState() ?? throw new InvalidOperationException("Diagnostic run required");
        if (!RequiemNet.IsSingleplayer || CombatManager.Instance.IsInProgress || !RequiemMode.IsInSystem(live.Players.FirstOrDefault()))
            throw new InvalidOperationException("Use a disposable singleplayer run outside combat with Requiem systems enabled");
        if (ModManager.GetLoadedMods().Any(m => m.manifest?.id is { } id && id != MainFile.ModId && id != "BaseLib"))
            throw new InvalidOperationException("Run the adapter suite with only BaseLib + SlayTheRequiem; other hooks may have global side effects");
        if (!Config.RunSettings.ShopVariety || !Config.RunSettings.EnablePantheon)
            throw new InvalidOperationException("Integrity fixtures require ShopVariety and Pantheon enabled in this run's settings snapshot");
        _running = true;
        var fingerprint = LiveFingerprint(live);
        var cases = new (string Name, Func<Task> Test)[]
        {
            ("empty-chest", Empty), ("card-properties-json-reload", CardProperties),
            ("batch-second-veto-boundary", BatchVeto), ("throw-after-store-detach", ThrowAfterStore),
            ("throw-after-chest-insert", ThrowAfterTake), ("duplicate-staves-no-equip", DuplicateStaves),
            ("legacy-and-opaque-relocation", LegacyAndOpaque), ("obtain-clone-and-reward-option", ObtainClone),
            ("saved-claim-reconciliation", ClaimReconciliation), ("scroll-clone-late-throw", ScrollClone),
            ("scroll-veto-no-payment", ScrollVeto), ("paid-offer-stale-gold-retry", PaidOffer),
            ("brew-fallback-exact-materials", BrewFinalizer), ("wire-request-receipt-roundtrip", WireRoundTrip),
            ("native-reward-veto-unload-retry", NativeRewardRetry), ("native-reward-clone-removal", NativeRewardClone),
            ("chest-restore-no-second-obtain", RelocationNoObtain), ("chest-interrupted-save-reconcile", ChestInterruptedSave),
            ("unknown-saved-property-keeps-source", UnknownProperty), ("pending-scope-no-duplicate", PendingScope),
            ("pending-replacement-debt-in-chest", ReplacementInChest), ("library-veto-affordability-retry", LibraryRetry),
            ("scroll-insertion-save-reconcile", ScrollInterruptedSave), ("brew-insertion-json-recovery", BrewRecovery),
            ("brew-late-notification-exact-once", BrewLateThrow), ("enchant-recheck-and-late-throw", EnchantLateThrow),
            ("camp-late-commit-consumes-action", CampLateThrow), ("saved-partial-block-survives-reload", SavedBlock),
            ("card-store-save-inside-detach", CardStoreInterruptedSave), ("relic-store-save-inside-detach", RelicStoreInterruptedSave),
            ("chest-live-identity-conflict", ChestIdentityConflict), ("interrupted-mutation-fails-closed", InterruptedMutation),
            ("shop-stone-late-commit-and-save", ShopStoneLateThrow), ("shop-kit-one-raw-commit", ShopKitLateThrow),
            ("shop-stale-price-no-effect", ShopStalePrice), ("shop-payment-late-throw", ShopPaymentLateThrow),
            ("legacy-card-take-insertion-save", LegacyCardInterruptedTake), ("legacy-relic-take-insertion-save", LegacyRelicInterruptedTake),
            ("abandoned-mutation-stays-blocked", AbandonedMutation), ("discard-rechecks-owner-and-overload", DiscardStaleTarget),
            ("blessing-obtain-before-revoke", BlessingLateThrow),
            ("chest-card-callback-unrelated-records", CardChestCallbackUnrelated),
            ("chest-card-callback-source-change", CardChestCallbackSource),
            ("chest-item-callback-unrelated-records", ItemChestCallbackUnrelated),
            ("chest-item-callback-source-change", ItemChestCallbackSource),
            ("chest-card-invalid-or-duplicate-identities", InvalidChestIdentities),
            ("chest-invalid-item-envelope-kept", InvalidItemEnvelope),
            ("shop-trade-late-source-removal", ShopTradeLateThrow),
            ("grant-veto-write-ahead-save", GrantBeforeInsertSave),
            ("grant-insertion-save-reconcile", GrantInsertionSave),
            ("pending-card-full-payload-restore", PendingCardPayload),
            ("pending-unknown-properties-kept", PendingUnknownProperty),
            ("pending-source-stale-choice", PendingStaleChoice),
            ("grant-source-change-keeps-claim", GrantSourceChanged),
            ("pending-claim-payload-collision", PendingClaimCollision),
            ("shout-native-removal-registration", ShoutRegistration),
            ("mandatory-source-veto-reload-once", MandatorySourcePending),
            ("mandatory-source-completed-never-reissued", MandatorySourceCompleted),
            ("legacy-start-none-not-replayed", LegacyStartNone),
            ("saved-future-schema-readonly-reject", FutureSchemaReject),
            ("timeline-rotation-keeps-unknown-properties", TimelineOpaqueProperties),
            ("receipt-epoch-and-mutation-disposal", ReceiptDisposal),
            ("mutable-service-clones-isolated", ServiceCloneIsolation),
            ("path-pending-sign-reload-no-issuer-replay", PathPendingSign),
            ("path-sign-late-insert-no-second-obtain", PathSignLateInsert),
            ("raw-native-read-preflight-no-repair", RawSavePreflight),
            ("boon-hp-writeahead-save-and-revoke", BoonHpWriteAhead),
            ("combat-status-veto-nested-reservation-cancel", CombatStatusCancellation),
            ("journal-equipment-instance-authority", JournalEquipmentOperation),
            ("combat-native-armor-cap-true", CombatNativeArmor),
            ("combat-pure-hp-preview-no-reservations", CombatHpPreview),
            ("combat-absorption-preview-cast-replay-light", CombatAbsorption),
            ("combat-individual-summon-role", CombatSummonRole),
            ("combat-enemy-action-price-snapshot", CombatActionPrice),
            ("creation-refused-lease-same-room-retry", CreationLeaseRetry),
            ("creation-gold-receipt-not-replayed", CreationGold),
            ("creation-common-potion-exact-source", CreationPotion),
            ("creation-full-belt-bonus-only-retry", CreationFullBelt),
            ("creation-basic-exact-removal", CreationRemoval),
            ("creation-vagrant-preselected-removal", CreationVagrant),
            ("reborn-refund-retains-skills-treatises-loot", RebornSources),
            ("reborn-opaque-legacy-preflight", RebornLegacy),
            ("reborn-insertion-save-fails-closed", RebornInterrupted),
            ("creation-invalid-saved-source-readonly", CreationPreflight),
        };
        var failures = 0;
        try
        {
            RunDiagnostics.Log(live, "integrity-suite");
            foreach (var (name, test) in cases)
            {
                if (!ReferenceEquals(live, RunManager.Instance.DebugOnlyGetState())) throw new InvalidOperationException("Live run changed; abort suite");
                try { await test(); MainFile.Logger.Info($"[IntegritySuite] PASS {name}"); }
                catch (Exception e) { failures++; MainFile.Logger.Error($"[IntegritySuite] FAIL {name}: {e}"); }
            }
            if (fingerprint != LiveFingerprint(live)) { failures++; MainFile.Logger.Error("[IntegritySuite] Live player state changed during isolated tests"); }
            MainFile.Logger.Info($"[IntegritySuite] cases={cases.Length} failures={failures} isolated_game_adapters=true json_player_reload=true " +
                "reward_ui=false disk_continue=false rollback=false two_clients=false visual_acceptance=false");
            if (failures > 0) throw new InvalidOperationException($"Integrity adapter suite failed: {failures}; see [IntegritySuite]");
        }
        finally { _running = false; }
    }

    private static async Task Empty()
    {
        var owner = Fixture();
        Require(Pack(owner).StoredCount == 0 && (await Pack(owner).StoreCards([])).Count == 0, "Empty chest changed");
        Require((await Pack(owner).TakeCards(new[] { 0 }))[0].Status == OperationStatus.Rejected, "Stale empty selection accepted");
    }

    private static async Task CardProperties()
    {
        var owner = Fixture(); var card = DeckCard(owner);
        CardCmd.Upgrade(card, CardPreviewStyle.None);
        CardCmd.Enchant(ModelDb.Enchantment<DoubleEnchantment>().ToMutable(), card,
            TransformationRules.EncodeDouble(CardEnchant.Flaming, CardEnchant.Breathing));
        var key = InventoryIdentity.CardKey(card); var before = Json(card.ToSerializable());
        Require((await Pack(owner).StoreCards([card]))[0].Succeeded, "Card store failed");
        var clone = (TravelPack)Pack(owner).MutableClone();
        clone.StoredCards[0].Props!.strings!.Clear();
        Require(Json(Pack(owner).StoredCards[0]) == before, "Chest clone shares SavedProperties");
        owner = Reload(owner);
        Require((await Pack(owner).TakeCards(new[] { 0 }))[0].Succeeded, "Card take failed after JSON reload");
        var restored = InventoryIdentity.FindCard(owner, key);
        Require(restored != null && Json(restored.ToSerializable()) == before && Pack(owner).StoredCount == 0, "Card properties/identity changed");
    }

    private static async Task BatchVeto()
    {
        var owner = Fixture(); var pack = Pack(owner);
        var cards = Enumerable.Range(0, 3).Select(_ => DeckCard(owner)).ToList();
        Require((await pack.StoreCards(cards)).All(r => r.Succeeded), "Batch store failed");
        FillToExcess(owner, 10); // current +10 permits first card; that card makes +11, so later cards must veto
        var result = await pack.TakeCards(new[] { 0, 1, 2 });
        Require(result.Select(r => r.Status).SequenceEqual(new[] { OperationStatus.Committed, OperationStatus.Rejected, OperationStatus.Rejected })
            && pack.StoredCards.Count == 2 && Loadout.Overload(owner).Excess == 11, "Burden boundary/batch source conservation failed");
    }

    private static async Task ThrowAfterStore()
    {
        var owner = Fixture(); var card = DeckCard(owner);
        void Fail(CardModel _) => throw new InvalidOperationException("injected CardRemoved after detach");
        owner.Deck.CardRemoved += Fail;
        try
        {
            var result = (await Pack(owner).StoreCards([card]))[0];
            Require(result.Status == OperationStatus.PartialCommit && Pack(owner).StoredCards.Count == 1
                && !owner.Deck.Cards.Contains(card) && !owner.RunState.ContainsCard(card), "Detached card lost/duplicated");
        }
        finally { owner.Deck.CardRemoved -= Fail; }
    }

    private static async Task ThrowAfterTake()
    {
        var owner = Fixture(); await Pack(owner).StoreCards([DeckCard(owner)]);
        void Fail(CardModel _) => throw new InvalidOperationException("injected CardAdded after insertion");
        owner.Deck.CardAdded += Fail;
        try
        {
            var result = (await Pack(owner).TakeCards(new[] { 0 }))[0];
            Require(result.Status == OperationStatus.PartialCommit && Pack(owner).StoredCards.Count == 0 && owner.Deck.Cards.Count == 1,
                "Insertion exception retained a second chest source or lost the card");
        }
        finally { owner.Deck.CardAdded -= Fail; }
    }

    private static async Task DuplicateStaves()
    {
        var owner = Fixture(); var pack = Pack(owner);
        var equipped = Loadout.Equipped(owner).Select(InventoryIdentity.RelicKey).ToArray();
        var first = (OakStaff)ModelDb.Relic<OakStaff>().ToMutable();
        var second = (OakStaff)ModelDb.Relic<OakStaff>().ToMutable();
        first.IsStowed = second.IsStowed = true; first.Upgrades = 2; second.Upgrades = 1;
        first.ChargesUsed = first.MaxCharges; second.ChargesUsed = 1;
        first.SetEnchants([new(Enchant.FireBrand, 3, 0)]); second.SetEnchants([new(Enchant.FrostBrand, 1, 7)]);
        owner.AddRelicInternal(first, silent: true); owner.AddRelicInternal(second, silent: true);
        var key1 = InventoryIdentity.RelicKey(first); var key2 = InventoryIdentity.RelicKey(second);
        var payload1 = Json(first.ToSerializable()); var payload2 = Json(second.ToSerializable());
        Require((await pack.StoreItem(first)).Succeeded && (await pack.StoreItem(second)).Succeeded, "Staff store failed");
        owner = Reload(owner); pack = Pack(owner);
        Require((await pack.TakeItem(key2)).Succeeded, "Exact second staff could not be restored");
        Require(Json(InventoryIdentity.FindRelic(owner, key2)!.ToSerializable()) == payload2 && pack.StoredCount == 1, "Wrong duplicate/properties restored");
        Require((await pack.TakeItem(key1)).Succeeded && Json(InventoryIdentity.FindRelic(owner, key1)!.ToSerializable()) == payload1, "Discharged staff changed");
        Require(Loadout.Equipped(owner).Select(InventoryIdentity.RelicKey).SequenceEqual(equipped), "Relocation re-equipped the hero");
    }

    private static async Task LegacyAndOpaque()
    {
        var owner = Fixture(); var pack = Pack(owner);
        var opaque = Json(new SerializableRelic
        {
            Id = new ModelId(ModelId.SlugifyCategory<RelicModel>(), "SLAYTHEREQUIEM-REMOVED_FIXTURE"),
            Props = new SavedProperties { ints = [new("ChargesUsed", 7)], strings = [new("UnknownProperty", "opaque")] },
        });
        pack.StoredItems = "OakStaff,OakStaff,RemovedEquipment";
        pack.StoredItemData = StoredItemCodec.Write([new("opaque:1", opaque)]);
        Require(pack.StoredItemRelics().Count == 2 && pack.StoredCount == 4, "Unavailable/duplicate legacy entries collapsed");
        Require((await pack.TakeItem("legacy:1:OakStaff")).Succeeded, "Exact legacy instance migration failed");
        owner = Reload(owner); pack = Pack(owner);
        Require(StoredItemCodec.TryRead(pack.StoredItemData, pack.StoredItems, out var entries, out _)
            && entries.Count == 3 && entries.Single(e => e.Key == "opaque:1").Payload == opaque
            && entries.Any(e => e.LegacyType == "RemovedEquipment"), "Opaque data lost during migration/reload");
    }

    private static async Task ObtainClone()
    {
        var owner = Fixture(); owner.AddRelicInternal(ModelDb.Relic<MoltenEgg>().ToMutable(), silent: true);
        var original = owner.RunState.CreateCard<Bash>(owner);
        CardAcquisition.MarkClaim(original, "q:fixture");
        var result = await CardAcquisition.AddSafely(original, PileType.Deck, skipVisuals: true);
        Require(result.success && !ReferenceEquals(result.cardAdded, original) && result.cardAdded.IsUpgraded
            && CardAcquisition.ClaimKey(result.cardAdded) == "q:fixture", "Obtain clone/claim did not follow the actual deck card");
        var options = new List<CardCreationResult> { new(original) };
        Require(CardAcquisition.RemoveSelectedOptions(options, r => r.Card == result.cardAdded) == 1 && options.Count == 0
            && original.Owner == owner, "Selected source remained repeatable or was detached before its holder animation completed");
        CardAcquisition.CleanUncommitted(original, owner); // after the native holder animation/reward task, not inside RemoveAll
        Require(!owner.RunState.ContainsCard(original) && owner.Deck.Cards.Contains(result.cardAdded), "Obtain cleanup removed the destination");
    }

    private static async Task ClaimReconciliation()
    {
        var owner = Fixture(); owner.AddRelicInternal(ModelDb.Relic<MoltenEgg>().ToMutable(), silent: true);
        var original = owner.RunState.CreateCard<Bash>(owner);
        Pack(owner).DeferCard(original); Pack(owner).DeferCard(original);
        Require(Pack(owner).PendingCards.Count == 1, "Same deferred entitlement was duplicated");
        Require((await CardAcquisition.AddSafely(original, PileType.Deck, skipVisuals: true)).success, "Claim obtain failed");
        owner = Reload(owner);
        var before = owner.Deck.Cards.Count;
        await Pack(owner).OfferPendingCards(); // already committed saved clone: MUST NOT open a selector
        Require(owner.Deck.Cards.Count == before && Pack(owner).PendingCards.Count == 0
            && owner.Deck.Cards.All(c => CardAcquisition.ClaimKey(c).Length == 0), "Saved claim minted a second acquisition");
    }

    private static async Task ScrollClone()
    {
        var owner = Fixture(); owner.AddRelicInternal(ModelDb.Relic<ToxicEgg>().ToMutable(), silent: true);
        var scroll = (RequiemScroll)ModelDb.Potion<ScrollOfOakflesh>().ToMutable();
        Require(owner.AddPotionInternal(scroll).success, "Fixture scroll missing");
        var before = owner.Gold;
        void Fail(CardModel _) => throw new InvalidOperationException("injected scroll AddInternal after insertion");
        owner.Deck.CardAdded += Fail;
        try
        {
            var result = await RequiemScroll.Rewrite(owner, scroll, () => PlayerCmd.LoseGold(30, owner));
            Require(result.Status == OperationStatus.PartialCommit && owner.Deck.Cards.Count == 1 && owner.Deck.Cards[0].IsUpgraded
                && owner.Gold == before - 30 && !owner.PotionSlots.Contains(scroll) && scroll.RewriteCommitted, "Scroll clone commit/payment misclassified");
        }
        finally { owner.Deck.CardAdded -= Fail; }
    }

    private static async Task ScrollVeto()
    {
        var owner = Fixture(); FillToExcess(owner, 11);
        var scroll = (RequiemScroll)ModelDb.Potion<ScrollOfOakflesh>().ToMutable(); owner.AddPotionInternal(scroll);
        var before = owner.Gold; var count = owner.Deck.Cards.Count;
        var result = await RequiemScroll.Rewrite(owner, scroll, () => PlayerCmd.LoseGold(30, owner));
        Require(result.Status == OperationStatus.Rejected && owner.Gold == before && owner.Deck.Cards.Count == count
            && owner.PotionSlots.Contains(scroll) && !scroll.RewriteCommitted, "Veto consumed the scroll/price");
    }

    private static async Task PaidOffer()
    {
        var owner = Fixture(); var card = owner.RunState.CreateCard<Bash>(owner);
        var reward = new CardReward([card], CardCreationSource.Other, owner,
            CardCreationOptions.ForNonCombatWithDefaultOdds([owner.Character.CardPool]));
        using var offer = new RewardPurchase(reward, 40, "fixture:paid", () => true);
        owner.Gold = 0; // changed AFTER the choices/quote were created
        var rejected = await CardAcquisition.AddRewardSafely(card, PileType.Deck, CardPilePosition.Bottom, null, true, reward);
        Require(!rejected.success && owner.Deck.Cards.Count == 0 && !offer.HasCommitted && owner.Gold == 0, "Stale affordability granted a free card");
        owner.Gold = 50;
        var accepted = await CardAcquisition.AddRewardSafely(card, PileType.Deck, CardPilePosition.Bottom, null, true, reward);
        Require(accepted.success && owner.Gold == 10 && offer.HasCommitted && owner.Deck.Cards.Count == 1, "Exact offer could not retry with post-selection payment");
    }

    private static Task BrewFinalizer()
    {
        var owner = Fixture(); var pouch = HerbalistPouch.Of(owner)!;
        pouch.Reagents = new int[AlchemyRules.ReagentCount]; pouch.Add(Reagent.Bloodmoss, 2);
        var potion = RequiemPotion.Create(AlchemyRules.Recipes[0].Brew, 1)!;
        pouch.BeginBrew([(potion, new[] { Reagent.Bloodmoss, Reagent.Bloodmoss })]);
        try
        {
            Require(owner.AddPotionInternal(potion).success, "Fixture potion missing");
            pouch.Add(Reagent.Bloodmoss, 1); // intervening grant must survive; no whole-array snapshot refund/clear
            pouch.CompleteBrew(); pouch.CompleteBrew();
            Require(pouch.BrewTaken && pouch.Count(Reagent.Bloodmoss) == 1, "Brew materials were not charged exactly once");
        }
        finally { pouch.EndBrew(); }
        return Task.CompletedTask;
    }

    // Native OnSelect + our REAL Harmony MoveNext adapter, replaying choices through the injectable game
    // synchronizer. This exercises completion/retry/option removal, but never creates a reward UI or a second client.
    private static async Task NativeRewardRetry()
    {
        var owner = Fixture(); FillToExcess(owner, 11);
        var card = owner.RunState.CreateCard<Bash>(owner); CardCmd.Upgrade(card, CardPreviewStyle.None);
        var upgrade = card.CurrentUpgradeLevel; var rng = owner.PlayerRng.Rewards.Counter;
        using var sync = new PlayerChoiceSynchronizer(new NetReplayGameService(0), owner.RunState);
        var reward = new CardReward([card], CardCreationSource.Other, owner,
            CardCreationOptions.ForNonCombatWithDefaultOdds([owner.Character.CardPool]), sync);
        sync.ReceiveReplayChoice(owner, 0, PlayerChoiceResult.FromIndex(0).ToNetData());
        var selection = reward.SelectUnsynchronized();
        var wait = System.Diagnostics.Stopwatch.StartNew();
        while (!selection.IsCompleted && (sync.ChoiceIds.Count == 0 || sync.ChoiceIds[0] < 2) && wait.Elapsed < TimeSpan.FromSeconds(3))
            await Task.Delay(1);
        if (selection.IsFaulted) await selection; // retain the real adapter exception, not just a failed assertion
        Require(!selection.IsCompleted && sync.ChoiceIds.Count == 1 && sync.ChoiceIds[0] == 2
            && !reward.SuccessfullySelected && reward.Cards.Contains(card) && !owner.Deck.Cards.Contains(card),
            "Native reward completed/removed its option on a veto instead of reserving the retry choice");
        var unload = owner.Deck.Cards[0]; unload.RemoveFromCurrentPile(silent: true); owner.RunState.RemoveCard(unload);
        sync.ReceiveReplayChoice(owner, 1, PlayerChoiceResult.FromIndex(0).ToNetData());
        sync.ReceiveReplayChoice(owner, 2, PlayerChoiceResult.FromIndex(null).ToNetData()); // finite escape if a broken loop asks again
        Require(await selection.WaitAsync(TimeSpan.FromSeconds(3)) && reward.SuccessfullySelected
            && owner.Deck.Cards.Contains(card) && card.CurrentUpgradeLevel == upgrade && !reward.Cards.Any()
            && owner.PlayerRng.Rewards.Counter == rng, "Native retry changed the upgrade roll/choice or failed to acquire once");
    }

    private static async Task NativeRewardClone()
    {
        var owner = Fixture(); owner.AddRelicInternal(ModelDb.Relic<MoltenEgg>().ToMutable(), silent: true);
        var card = owner.RunState.CreateCard<Bash>(owner);
        using var sync = new PlayerChoiceSynchronizer(new NetReplayGameService(0), owner.RunState);
        var reward = new CardReward([card], CardCreationSource.Other, owner,
            CardCreationOptions.ForNonCombatWithDefaultOdds([owner.Character.CardPool]), sync);
        sync.ReceiveReplayChoice(owner, 0, PlayerChoiceResult.FromIndex(0).ToNetData());
        sync.ReceiveReplayChoice(owner, 1, PlayerChoiceResult.FromIndex(null).ToNetData());
        Require(await reward.SelectUnsynchronized().WaitAsync(TimeSpan.FromSeconds(3)) && !reward.Cards.Any()
            && owner.Deck.Cards.Count == 1 && !ReferenceEquals(owner.Deck.Cards[0], card) && owner.Deck.Cards[0].IsUpgraded,
            "Native reward did not remove its selected source option after an obtain clone");
    }

    private static async Task RelocationNoObtain()
    {
        var owner = Fixture(); var card = DeckCard(owner);
        _ = InventoryIdentity.CardKey(card); // snapshot AFTER the saved inventory metadata has been assigned
        var before = Json(card.ToSerializable());
        await Pack(owner).StoreCards([card]);
        owner.AddRelicInternal(ModelDb.Relic<MoltenEgg>().ToMutable(), silent: true);
        var history = owner.RunState.CurrentMapPointHistoryEntry!.GetEntry(owner.NetId).CardsGained.Count;
        Require((await Pack(owner).TakeCards(new[] { 0 }))[0].Succeeded && !owner.Deck.Cards[0].IsUpgraded
            && Json(owner.Deck.Cards[0].ToSerializable()) == before
            && owner.RunState.CurrentMapPointHistoryEntry.GetEntry(owner.NetId).CardsGained.Count == history,
            "Relocation re-applied an obtain modifier or recorded a second gain");
    }

    private static async Task ChestInterruptedSave()
    {
        var owner = Fixture(); await Pack(owner).StoreCards([DeckCard(owner)]);
        SerializablePlayer? saved = null;
        void SaveAndFail(CardModel _) { saved = RoundTrip(owner.ToSerializable()); throw new InvalidOperationException("save inside insertion"); }
        owner.Deck.CardAdded += SaveAndFail;
        try { Require((await Pack(owner).TakeCards(new[] { 0 }))[0].Status == OperationStatus.PartialCommit, "Expected injected partial take"); }
        finally { owner.Deck.CardAdded -= SaveAndFail; }
        Require(saved != null, "Insertion save was not captured");
        owner = Player.FromSerializable(saved!); RunState.CreateForTest([owner], seed: "STR-G1-INTEGRITY");
        Require(Pack(owner).StoredCards.Count == 1 && owner.Deck.Cards.Count == 1, "Interrupted fixture did not contain both sides");
        Require((await Pack(owner).TakeCards(new[] { 0 }))[0].Succeeded && Pack(owner).StoredCards.Count == 0 && owner.Deck.Cards.Count == 1,
            "Interrupted chest source produced a second saved instance");
    }

    private static async Task UnknownProperty()
    {
        var owner = Fixture(); await Pack(owner).StoreCards([DeckCard(owner)]);
        var snapshot = Pack(owner).StoredCards[0]; snapshot.Props ??= new SavedProperties();
        (snapshot.Props.strings ??= []).Add(new("UnavailablePropertyFromOldVersion", "preserve-me"));
        var original = Json(snapshot);
        Require((await Pack(owner).TakeCards(new[] { 0 }))[0].Status == OperationStatus.Error && Pack(owner).StoredCards.Count == 1
            && owner.Deck.Cards.Count == 0 && Json(Pack(owner).StoredCards[0]) == original,
            "A known card type silently erased an unknown SavedProperty");
    }

    private static async Task PendingScope()
    {
        var owner = Fixture(); var card = owner.RunState.CreateCard<Bash>(owner);
        CardAcquisition.MarkClaim(card, "q:900000"); Pack(owner).DeferCard(card);
        Pack(owner).InstanceSequence = 0;
        Require(int.Parse(InventoryIdentity.NewClaimKey(owner)[2..], System.Globalization.CultureInfo.InvariantCulture) > 900000,
            "Saved claims were not reserved in the instance high-water mark");
        var snapshot = Pack(owner).PendingCards[0];
        Pack(owner).PendingCards.Add(RoundTrip(snapshot)); // interrupted old finalizer accidentally saved the same entitlement twice
        var reward = new CardReward([card], CardCreationSource.Other, owner,
            CardCreationOptions.ForNonCombatWithDefaultOdds([owner.Character.CardPool]));
        using (var scope = new DeferredRewardScope(reward, Pack(owner), snapshot))
        {
            using (var unload = InventoryGate.For(owner).TryEnter()) Require(unload != null, "Pending screen held the unload gate");
            var first = await CardAcquisition.AddRewardSafely(card, PileType.Deck, CardPilePosition.Bottom, null, true, reward);
            var repeat = await CardAcquisition.AddRewardSafely(card, PileType.Deck, CardPilePosition.Bottom, null, true, reward);
            Require(first.success && repeat.success && ReferenceEquals(first.cardAdded, repeat.cardAdded) && owner.Deck.Cards.Count == 1,
                "Repeating an in-flight deferred choice minted another card");
        }
        owner = Reload(owner); await Pack(owner).OfferPendingCards();
        Require(Pack(owner).PendingCards.Count == 0 && owner.Deck.Cards.Count == 1, "Duplicate saved q: was re-offered after reconciliation");
    }

    private static async Task GrantBeforeInsertSave()
    {
        var owner = Fixture(); FillToExcess(owner, 11);
        var card = owner.RunState.CreateCard<Bash>(owner); CardCmd.Upgrade(card, CardPreviewStyle.None);
        var pack = Pack(owner); SerializablePlayer? saved = null;
        void SaveAndFail(RelicModel _, IEnumerable<Creature> __)
        {
            saved = RoundTrip(owner.ToSerializable()); // real veto notification BEFORE any deck insertion
            throw new InvalidOperationException("injected veto callback save");
        }
        pack.Flashed += SaveAndFail;
        try { Require(!await CardAcquisition.AddOrDefer(card), "Throwing burden veto reported a grant"); }
        finally { pack.Flashed -= SaveAndFail; }
        Require(saved != null && pack.PendingCards.Count == 1 && !owner.Deck.Cards.Contains(card), "Veto did not retain its durable grant");
        var restored = Player.FromSerializable(saved!); RunState.CreateForTest([restored], seed: "STR-G1-INTEGRITY");
        Require(Pack(restored).PendingCards.Count == 1 && Pack(restored).PendingCards[0].CurrentUpgradeLevel == card.CurrentUpgradeLevel
            && CardAcquisition.ClaimKey(Pack(restored).PendingCards[0]).Length > 0,
            "A save before insertion lost the entitlement or its original upgrade roll");
    }

    private static async Task GrantInsertionSave()
    {
        var owner = Fixture(); var old = DeckCard(owner); var oldKey = InventoryIdentity.CardKey(old);
        owner.AddRelicInternal(ModelDb.Relic<MoltenEgg>().ToMutable(), silent: true);
        SerializablePlayer? saved = null;
        void Save(CardModel _) => saved = RoundTrip(owner.ToSerializable());
        owner.Deck.CardAdded += Save;
        try { Require(await CardAcquisition.AddOrDefer(owner.RunState.CreateCard<Bash>(owner), old), "One-time replacement did not commit"); }
        finally { owner.Deck.CardAdded -= Save; }
        Require(saved != null && Pack(owner).PendingCards.Count == 0 && owner.Deck.Cards.Count == 1 && owner.Deck.Cards[0].IsUpgraded,
            "Normal grant retained a repeatable claim or missed its obtain clone/replacement");
        owner = Player.FromSerializable(saved!); RunState.CreateForTest([owner], seed: "STR-G1-INTEGRITY");
        Require(Pack(owner).PendingCards.Count == 1 && owner.Deck.Cards.Count == 2, "Insertion save did not capture both durable views");
        await Pack(owner).OfferPendingCards(); // already committed q: — never opens a selector
        Require(Pack(owner).PendingCards.Count == 0 && owner.Deck.Cards.Count == 1 && owner.Deck.Cards[0].IsUpgraded
            && InventoryIdentity.FindCard(owner, oldKey) == null && !CardAcquisition.IsDeferredClaim(owner.Deck.Cards[0]),
            "Insertion-save reconciliation duplicated the gift or lost the replacement debt");
    }

    private static Task PendingCardPayload()
    {
        var owner = Fixture(); var card = owner.RunState.CreateCard<GeneticAlgorithm>(owner);
        card.CurrentBlock = 37; CardCmd.Upgrade(card, CardPreviewStyle.None);
        var pack = Pack(owner); pack.DeferCard(card);
        var snapshot = pack.PendingCards.Single(); var payload = Json(snapshot);
        var restored = (GeneticAlgorithm)pack.RestorePendingCard(snapshot);
        try
        {
            Require(restored.CurrentBlock == card.CurrentBlock && restored.CurrentUpgradeLevel == card.CurrentUpgradeLevel,
                "Deferred restore lost SavedProperties/upgrade state");
            restored.CurrentBlock++;
            Require(Json(snapshot) == payload, "Restored pending model mutated its durable source");
        }
        finally { CardAcquisition.CleanUncommitted(restored, owner); CardAcquisition.CleanUncommitted(card, owner); }
        return Task.CompletedTask;
    }

    private static Task PendingUnknownProperty()
    {
        var owner = Fixture(); var card = owner.RunState.CreateCard<Bash>(owner); var pack = Pack(owner);
        pack.DeferCard(card);
        var snapshot = pack.PendingCards.Single(); snapshot.Props ??= new SavedProperties();
        (snapshot.Props.strings ??= []).Add(new("UnavailablePendingProperty", "keep-this-grant"));
        var payload = Json(snapshot); var rejected = false;
        CardModel? preview = null;
        try { preview = pack.RestorePendingCard(snapshot); }
        catch (InvalidOperationException e) when (e.Message.Contains("round-trip ALL saved properties", StringComparison.Ordinal)) { rejected = true; }
        finally
        {
            if (preview != null) CardAcquisition.CleanUncommitted(preview, owner);
            CardAcquisition.CleanUncommitted(card, owner);
        }
        Require(rejected && pack.PendingCards.Count == 1 && Json(snapshot) == payload && owner.Deck.Cards.Count == 0,
            "A pending grant silently dropped an unknown SavedProperty or consumed its source");
        return Task.CompletedTask;
    }

    private static async Task PendingStaleChoice()
    {
        var owner = Fixture(); var card = owner.RunState.CreateCard<Bash>(owner); var pack = Pack(owner);
        pack.DeferCard(card);
        var snapshot = pack.PendingCards.Single(); var payload = TravelPack.CardPayload(snapshot);
        var reward = new CardReward([card], CardCreationSource.Other, owner,
            CardCreationOptions.ForNonCombatWithDefaultOdds([owner.Character.CardPool]));
        using var scope = new DeferredRewardScope(reward, pack, snapshot);
        snapshot.CurrentUpgradeLevel++;
        var rejected = await CardAcquisition.AddRewardSafely(card, PileType.Deck, CardPilePosition.Bottom, null, true, reward);
        Require(!rejected.success && owner.Deck.Cards.Count == 0 && pack.PendingCards.Count == 1, "Changed saved claim authorized its stale option");
        snapshot.CurrentUpgradeLevel--;
        var unrelated = owner.RunState.CreateCard<StrikeIronclad>(owner); pack.DeferCard(unrelated);
        var unrelatedSnapshot = pack.PendingCards.Single(s => !ReferenceEquals(s, snapshot));
        var accepted = await CardAcquisition.AddRewardSafely(card, PileType.Deck, CardPilePosition.Bottom, null, true, reward);
        Require(accepted.success, "Unrelated pending grant invalidated the original choice");
        pack.ConsumePending(snapshot, payload);
        CardAcquisition.MarkClaim(accepted.cardAdded, "");
        CardAcquisition.CleanUncommitted(unrelated, owner);
        Require(pack.PendingCards.Count == 1 && ReferenceEquals(pack.PendingCards[0], unrelatedSnapshot) && owner.Deck.Cards.Count == 1,
            "Finalization erased an unrelated deferred grant");
    }

    private static async Task GrantSourceChanged()
    {
        var owner = Fixture(); var pack = Pack(owner);
        void Change(CardModel _) => pack.PendingCards.Single().CurrentUpgradeLevel++;
        owner.Deck.CardAdded += Change;
        try { Require(await CardAcquisition.AddOrDefer(owner.RunState.CreateCard<Bash>(owner)), "Observed grant was presented as uncommitted"); }
        finally { owner.Deck.CardAdded -= Change; }
        Require(owner.Deck.Cards.Count == 1 && !owner.Deck.Cards[0].IsUpgraded && pack.PendingCards.Count == 1
            && pack.PendingCards[0].CurrentUpgradeLevel == 1 && CardAcquisition.IsDeferredClaim(owner.Deck.Cards[0])
            && InventoryGate.For(owner).Blocked, "Changed source was erased or a partial grant was left repeatable");
        owner = Reload(owner);
        Require(Pack(owner).PendingCards.Count == 1 && InventoryGate.For(owner).Blocked, "Partial grant became safe merely by reloading JSON");
    }

    private static Task PendingClaimCollision()
    {
        var owner = Fixture(); var pack = Pack(owner); var card = owner.RunState.CreateCard<Bash>(owner);
        pack.DeferCard(card); var payload = Json(pack.PendingCards.Single());
        var other = owner.RunState.CreateCard<StrikeIronclad>(owner);
        CardAcquisition.MarkClaim(other, CardAcquisition.ClaimKey(card));
        var rejected = false;
        try { pack.DeferCard(other); }
        catch (InvalidOperationException e) when (e.Message == "Conflicting deferred card entitlement") { rejected = true; }
        finally { CardAcquisition.CleanUncommitted(card, owner); CardAcquisition.CleanUncommitted(other, owner); }
        Require(rejected && pack.PendingCards.Count == 1 && Json(pack.PendingCards[0]) == payload && InventoryGate.For(owner).Blocked,
            "A reused claim key silently replaced a different saved grant");
        return Task.CompletedTask;
    }

    private static async Task ReplacementInChest()
    {
        var owner = Fixture(); var old = DeckCard(owner); var oldKey = InventoryIdentity.CardKey(old);
        FillToExcess(owner, 11);
        Require(!await CardAcquisition.AddOrDefer(owner.RunState.CreateCard<StrikeIronclad>(owner), old), "Grant should have deferred at +11");
        Require((await Pack(owner).StoreCards([old]))[0].Succeeded, "Could not unload the pending replacement source");
        var source = CardModel.FromSerializable(Pack(owner).PendingCards[0]); owner.RunState.AddCard(source, owner);
        Require((await CardAcquisition.AddSafely(source, PileType.Deck, skipVisuals: true)).success, "Pending source could not commit at +10");
        await Pack(owner).OfferPendingCards();
        Require(Pack(owner).PendingCards.Count == 1 && CardAcquisition.IsDeferredClaim(source), "Unremoved stored starter debt was cleared");
        var unload = owner.Deck.Cards.First(c => c != source); unload.RemoveFromCurrentPile(silent: true); owner.RunState.RemoveCard(unload);
        Require((await Pack(owner).TakeCards(new[] { 0 }))[0].Succeeded, "Could not return the exact replacement source");
        await Pack(owner).OfferPendingCards();
        Require(Pack(owner).PendingCards.Count == 0 && InventoryIdentity.FindCard(owner, oldKey) == null
            && !CardAcquisition.IsDeferredClaim(source) && owner.Deck.Cards.Count(c => c.Id == source.Id) == 1,
            "Replacement did not finalize exactly once when its source returned");
    }

    private static async Task LibraryRetry()
    {
        var owner = Fixture(); FillToExcess(owner, 11);
        var scroll = (RequiemScroll)ModelDb.Potion<ScrollOfOakflesh>().ToMutable(); owner.AddPotionInternal(scroll);
        var gold = owner.Gold; var count = owner.Deck.Cards.Count;
        Require((await LibraryEvent.CopyScroll(owner, scroll, 30, () => true)).Status == OperationStatus.Rejected
            && owner.Gold == gold && owner.PotionSlots.Contains(scroll), "Library burden veto consumed its price/source");
        var unload = owner.Deck.Cards[0]; unload.RemoveFromCurrentPile(silent: true); owner.RunState.RemoveCard(unload);
        owner.Gold = 0;
        Require((await LibraryEvent.CopyScroll(owner, scroll, 30, () => true)).Status == OperationStatus.Rejected
            && owner.Deck.Cards.Count == count - 1, "Library stale gold created a card");
        owner.Gold = 50;
        Require((await LibraryEvent.CopyScroll(owner, scroll, 30, () => true)).Succeeded && owner.Gold == 20
            && owner.Deck.Cards.Count == count && !owner.PotionSlots.Contains(scroll), "Library offer could not retry the same source");
    }

    private static async Task ScrollInterruptedSave()
    {
        var owner = Fixture(); owner.AddRelicInternal(ModelDb.Relic<ToxicEgg>().ToMutable(), silent: true);
        var scroll = (RequiemScroll)ModelDb.Potion<ScrollOfOakflesh>().ToMutable(); owner.AddPotionInternal(scroll);
        SerializablePlayer? saved = null;
        void SaveAndFail(CardModel _) { saved = RoundTrip(owner.ToSerializable()); throw new InvalidOperationException("scroll insertion save"); }
        owner.Deck.CardAdded += SaveAndFail;
        try { await RequiemScroll.Rewrite(owner, scroll, () => PlayerCmd.LoseGold(30, owner)); }
        finally { owner.Deck.CardAdded -= SaveAndFail; }
        Require(saved != null, "Scroll insertion save was not captured");
        owner = Player.FromSerializable(saved!); RunState.CreateForTest([owner], seed: "STR-G1-INTEGRITY");
        scroll = owner.PotionSlots.OfType<RequiemScroll>().Single();
        var before = owner.Gold;
        Require(!scroll.RewriteCommitted && scroll.RewriteClaimKey.Length > 0, "Pre-payment claim was not saved on the scroll");
        Require((await RequiemScroll.Rewrite(owner, scroll, () => PlayerCmd.LoseGold(30, owner))).Succeeded
            && owner.Deck.Cards.Count == 1 && owner.Gold == before - 30 && !owner.PotionSlots.Contains(scroll),
            "Interrupted scroll created a second card or charged the wrong payment");
    }

    private static async Task BrewRecovery()
    {
        var owner = Fixture(); var pouch = HerbalistPouch.Of(owner)!;
        pouch.Reagents = new int[AlchemyRules.ReagentCount];
        var potion = RequiemPotion.Create(Brew.Health, 1)!;
        pouch.BeginBrew([(potion, new[] { Reagent.Bloodmoss, Reagent.Bloodmoss })]);
        owner.AddPotionInternal(potion); // simulate materials changing AFTER ShouldProcurePotion, before the hook
        try { pouch.CompleteBrew(); throw new InvalidOperationException("Unpaid brew was accepted"); }
        catch (InvalidOperationException e) when (e.Message.Contains("saved debt retained", StringComparison.Ordinal)) { }
        Require(pouch.BrewTaken && HerbalistPouch.HasBrewCost(potion), "Insufficient materials were marked as paid");
        pouch.EndBrew(); owner = Reload(owner); pouch = HerbalistPouch.Of(owner)!;
        potion = owner.PotionSlots.OfType<PotionModel>().Single();
        pouch.Add(Reagent.Bloodmoss, 3); await pouch.CompleteSavedBrews(); await pouch.CompleteSavedBrews();
        Require(!HerbalistPouch.HasBrewCost(potion) && pouch.Count(Reagent.Bloodmoss) == 1, "Saved brew debt did not settle exactly once");
    }

    private static Task BrewLateThrow()
    {
        var owner = Fixture(); var pouch = HerbalistPouch.Of(owner)!;
        pouch.Reagents = new int[AlchemyRules.ReagentCount]; pouch.Add(Reagent.Bloodmoss, 3);
        var potion = RequiemPotion.Create(Brew.Health, 1)!;
        pouch.BeginBrew([(potion, new[] { Reagent.Bloodmoss, Reagent.Bloodmoss })]); owner.AddPotionInternal(potion);
        void Fail() => throw new InvalidOperationException("late pouch display notification");
        pouch.DisplayAmountChanged += Fail;
        try
        {
            try { pouch.CompleteBrew(); throw new InvalidOperationException("Expected display failure"); }
            catch (InvalidOperationException e) when (e.Message == "late pouch display notification") { }
        }
        finally { pouch.DisplayAmountChanged -= Fail; }
        pouch.CompleteBrew(); pouch.EndBrew();
        Require(pouch.Count(Reagent.Bloodmoss) == 1 && !HerbalistPouch.HasBrewCost(potion), "Late brew notification charged materials twice");
        return Task.CompletedTask;
    }

    private static async Task EnchantLateThrow()
    {
        var owner = Fixture(); var card = DeckCard(owner); var gold = owner.Gold;
        async Task<bool> Pay() { await PlayerCmd.LoseGold(20, owner); return true; }
        var rejected = await CardEnchantRestSiteOption.EnchantSelected(owner, card, ModelDb.Enchantment<FlamingEnchantment>(), 1, () => false, Pay);
        Require(rejected.Status == OperationStatus.Rejected && card.Enchantment == null && owner.Gold == gold, "Stale enchant price changed state");
        void Fail() => throw new InvalidOperationException("late card enchant notification");
        card.EnchantmentChanged += Fail;
        try
        {
            var partial = await CardEnchantRestSiteOption.EnchantSelected(owner, card, ModelDb.Enchantment<FlamingEnchantment>(), 1, () => true, Pay);
            Require(partial.Status == OperationStatus.PartialCommit && card.Enchantment is FlamingEnchantment && owner.Gold == gold - 20,
                "Post-enchant exception lost payment/effect classification");
        }
        finally { card.EnchantmentChanged -= Fail; }
        owner = Fixture();
        var item = (OakStaff)ModelDb.Relic<OakStaff>().ToMutable(); owner.AddRelicInternal(item, silent: true); item.IsStowed = false;
        var before = owner.Gold;
        item.DisplayAmountChanged += Fail;
        try
        {
            var partial = await EnchantRestSiteOption.EnchantSelected(owner, item, Enchant.FireBrand, 1, () => true,
                async () => { await PlayerCmd.LoseGold(20, owner); return true; });
            Require(partial.Status == OperationStatus.PartialCommit && item.EnchantSlots.Any(s => s.Enchant == Enchant.FireBrand)
                && owner.Gold == before - 20, "Equipment enchant late exception did not finalize its committed effect");
        }
        finally { item.DisplayAmountChanged -= Fail; }
    }

    private static async Task CampLateThrow()
    {
        var owner = Fixture(); var hero = HeroRecord.Of(owner)!; hero.CampPoints = 2;
        ((RunState)owner.RunState).PushRoom(new RestSiteRoom());
        var before = owner.Gold;
        Require(await Camp.ExecuteOption(owner, CampAction.ShortRest, 1,
            async () => { await PlayerCmd.LoseGold(1, owner); throw new InvalidOperationException("foreign camp late hook"); })
            && hero.CampPoints == 1 && owner.Gold == before - 1 && InventoryGate.For(owner).Blocked,
            "A foreign late commit could repeat without paying its camp action");
    }

    private static Task SavedBlock()
    {
        var owner = Fixture(); InventoryGate.Block(owner, "injected unresolved partial");
        owner = Reload(owner);
        Require(Pack(owner).OperationBlocked && InventoryGate.For(owner).Blocked && InventoryGate.For(owner).TryEnter() == null,
            "Continue silently reset an unresolved partial result");
        return Task.CompletedTask;
    }

    private static async Task CardStoreInterruptedSave()
    {
        var owner = Fixture(); var card = DeckCard(owner); var key = InventoryIdentity.CardKey(card);
        SerializablePlayer? saved = null;
        void SaveAndFail(CardModel _) { saved = RoundTrip(owner.ToSerializable()); throw new InvalidOperationException("save in store detach"); }
        owner.Deck.CardRemoved += SaveAndFail;
        try { Require((await Pack(owner).StoreCards([card]))[0].Status == OperationStatus.PartialCommit, "Expected late store failure"); }
        finally { owner.Deck.CardRemoved -= SaveAndFail; }
        Require(saved != null, "Store detach save not captured");
        owner = Player.FromSerializable(saved!); RunState.CreateForTest([owner], seed: "STR-G1-INTEGRITY");
        Require(owner.Deck.Cards.Count == 0 && Pack(owner).StoredCards.Count == 1, "A save inside store lost both copies");
        Require((await Pack(owner).TakeCards(new[] { 0 }))[0].Succeeded && InventoryIdentity.FindCard(owner, key) != null,
            "Written-ahead card source could not be restored");
    }

    private static async Task RelicStoreInterruptedSave()
    {
        var owner = Fixture(); var item = (OakStaff)ModelDb.Relic<OakStaff>().ToMutable();
        owner.AddRelicInternal(item, silent: true); item.IsStowed = true; item.Upgrades = 2; item.ChargesUsed = item.MaxCharges;
        var key = InventoryIdentity.RelicKey(item); var payload = Json(item.ToSerializable());
        SerializablePlayer? saved = null;
        void SaveAndFail(RelicModel relic)
        {
            if (relic != item) return;
            saved = RoundTrip(owner.ToSerializable()); throw new InvalidOperationException("save in relic store detach");
        }
        owner.RelicRemoved += SaveAndFail;
        try { Require((await Pack(owner).StoreItem(item)).Status == OperationStatus.PartialCommit, "Expected late item store failure"); }
        finally { owner.RelicRemoved -= SaveAndFail; }
        Require(saved != null, "Relic detach save not captured");
        owner = Player.FromSerializable(saved!); RunState.CreateForTest([owner], seed: "STR-G1-INTEGRITY");
        Require((await Pack(owner).TakeItem(key)).Succeeded && Json(InventoryIdentity.FindRelic(owner, key)!.ToSerializable()) == payload,
            "A save inside relic store lost or reset its complete source");
    }

    private static async Task ChestIdentityConflict()
    {
        var owner = Fixture(); await Pack(owner).StoreCards([DeckCard(owner)]);
        var snapshot = Pack(owner).StoredCards[0]; var payload = Json(snapshot);
        var other = CardModel.FromSerializable(snapshot); owner.RunState.AddCard(other, owner); owner.Deck.AddInternal(other, silent: true);
        CardCmd.Upgrade(other, CardPreviewStyle.None);
        Require((await Pack(owner).TakeCards(new[] { 0 }))[0].Status == OperationStatus.PartialCommit
            && Pack(owner).StoredCards.Count == 1 && Json(Pack(owner).StoredCards[0]) == payload && owner.Deck.Cards.Count == 1
            && InventoryGate.For(owner).Blocked, "A mismatching saved instance key authorized deleting a chest source");
    }

    private static async Task InterruptedMutation()
    {
        var owner = Fixture(); SerializablePlayer saved;
        var before = ShopOperations.Digest(owner, ShopAction.DiscardCard);
        using (var lease = await InventoryGate.For(owner).Enter())
        using (var outer = InventoryGate.Track(owner, lease!, "fixture:outer"))
        {
            using (var inner = InventoryGate.Track(owner, lease!, "fixture:inner"))
            {
                Require(Pack(owner).OperationPending == "fixture:outer" && !InventoryGate.For(owner).Blocked
                    && ShopOperations.Digest(owner, ShopAction.DiscardCard) == before,
                    "Nested scope erased the outer marker, blocked a live operation or changed the peer inventory digest");
                saved = RoundTrip(owner.ToSerializable()); inner.Complete();
            }
            Require(Pack(owner).OperationPending == "fixture:outer", "Nested completion cleared an unfinished outer scope");
            outer.Complete();
        }
        Require(Pack(owner).OperationPending.Length == 0 && !InventoryGate.For(owner).Blocked, "Completed operation left a pending marker");
        owner = Player.FromSerializable(saved); RunState.CreateForTest([owner], seed: "STR-G1-INTEGRITY");
        Require(InventoryGate.For(owner).Blocked && Pack(owner).OperationBlocked && Pack(owner).OperationPending == "fixture:outer",
            "Continue treated an intermediate async save as a new safe transaction");
        var voluntary = owner.RunState.CreateCard<Bash>(owner);
        Require(!(await CardAcquisition.AddSafely(voluntary, PileType.Deck, skipVisuals: true)).success, "Interrupted-save guard allowed a new voluntary grant");
        var forced = owner.RunState.CreateCard<Injury>(owner);
        Require((await CardAcquisition.AddSafely(forced, PileType.Deck, skipVisuals: true)).success,
            "The explicit forced-curse policy was replaced by a global mutation veto");
    }

    private static Player ShopFixture(ShopType shop)
    {
        var owner = Fixture(); ((RunState)owner.RunState).PushRoom(new MerchantRoom());
        var ledger = MerchantLedger.Of(owner)!; ledger.CurrentShopIndex = (int)shop; ledger.CurrentFloor = owner.RunState.TotalFloor;
        owner.Gold = 1000; Pack(owner).OperationRunId = "1234567890abcdef1234567890abcdef";
        return owner;
    }

    private static (ShopRequest Request, ShopQuote Quote) Quote(Player owner, ShopAction kind, int a, int b, string s = "")
    {
        Require(ShopActionPolicy.TryQuote(owner, kind, a, b, s, out var quote, out var error), $"Fixture quote rejected: {error}");
        var run = ShopOperations.RunKey(owner);
        return (new(run, OperationIdentity.Id(run, 0), 0, ShopOperations.Digest(owner, kind), kind, a, b, s), quote);
    }

    private static async Task ShopStoneLateThrow()
    {
        var owner = ShopFixture(ShopType.Arcanist); var hero = HeroRecord.Of(owner)!;
        var power = ShopRules.SoulStonePower(Acts.ActKinds.Tier(owner.RunState));
        var price = ShopRules.ApplyTrust(ShopRules.SoulStonePrice(power), MerchantLedger.Of(owner)!.CurrentTrust);
        var (request, quote) = Quote(owner, ShopAction.BuyStone, price, power);
        var gold = owner.Gold; var stones = hero.Stones(power); SerializablePlayer? saved = null;
        void SaveAndFail() { saved = RoundTrip(owner.ToSerializable()); throw new InvalidOperationException("stone display after raw grant"); }
        hero.DisplayAmountChanged += SaveAndFail;
        try
        {
            using var lease = await InventoryGate.For(owner).Enter();
            var result = await ShopOperations.ExecuteGuarded(owner, request, quote, lease!);
            Require(result.Status == OperationStatus.PartialCommit && hero.Stones(power) == stones + 1 && owner.Gold == gold - price
                && result.Gold == price && MerchantLedger.Of(owner)!.Spent[(int)ShopType.Arcanist] == price && InventoryGate.For(owner).Blocked,
                "Stone service charged before the grant, missed a late commit, or credited trust from a quote");
        }
        finally { hero.DisplayAmountChanged -= SaveAndFail; }
        Require(saved != null && Pack(owner).OperationPending.Length == 0, "Known result did not complete its pending marker");
        owner = Player.FromSerializable(saved!); RunState.CreateForTest([owner], seed: "STR-G1-INTEGRITY");
        Require(InventoryGate.For(owner).Blocked && Pack(owner).OperationPending == request.Id,
            "The saved pre-payment stone grant was silently reusable after Continue");
    }

    private static async Task ShopKitLateThrow()
    {
        var owner = ShopFixture(ShopType.Herbalist); var pouch = HerbalistPouch.Of(owner)!;
        pouch.Reagents = new int[AlchemyRules.ReagentCount];
        var kit = ShopServices.ReagentKit(owner);
        var price = ShopRules.ApplyTrust(ShopRules.ReagentKitPrice(Acts.ActKinds.Tier(owner.RunState)), MerchantLedger.Of(owner)!.CurrentTrust);
        var encoded = string.Join(",", kit.Select(r => ((int)r).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var (request, quote) = Quote(owner, ShopAction.BuyReagents, price, 0, encoded); var gold = owner.Gold;
        void Fail() => throw new InvalidOperationException("kit display after all raw materials");
        pouch.DisplayAmountChanged += Fail;
        try
        {
            using var lease = await InventoryGate.For(owner).Enter();
            var result = await ShopOperations.ExecuteGuarded(owner, request, quote, lease!);
            Require(result.Status == OperationStatus.PartialCommit && owner.Gold == gold - price && pouch.Total == kit.Count
                && kit.Distinct().All(r => pouch.Count(r) == kit.Count(k => k == r)), "Kit notification left a paid partial material array");
        }
        finally { pouch.DisplayAmountChanged -= Fail; }
    }

    private static async Task ShopStalePrice()
    {
        var owner = ShopFixture(ShopType.Arcanist); var hero = HeroRecord.Of(owner)!;
        var power = ShopRules.SoulStonePower(Acts.ActKinds.Tier(owner.RunState));
        var price = ShopRules.ApplyTrust(ShopRules.SoulStonePrice(power), MerchantLedger.Of(owner)!.CurrentTrust);
        var (request, quote) = Quote(owner, ShopAction.BuyStone, price, power); var before = hero.TotalStones;
        owner.Gold = 0;
        using var lease = await InventoryGate.For(owner).Enter();
        var result = await ShopOperations.ExecuteGuarded(owner, request, quote, lease!);
        Require(result.Status == OperationStatus.Rejected && hero.TotalStones == before && owner.Gold == 0
            && MerchantLedger.Of(owner)!.Spent.All(n => n == 0) && !InventoryGate.For(owner).Blocked && Pack(owner).OperationPending.Length == 0,
            "Stale service affordability changed inventory, money, trust or durable operation state");
    }

    private static async Task LegacyCardInterruptedTake()
    {
        var owner = Fixture(); Pack(owner).StoredCards = [new SerializableCard { Id = ModelDb.Card<Bash>().Id, CurrentUpgradeLevel = 1 }];
        SerializablePlayer? saved = null;
        void SaveAndFail(CardModel _) { saved = RoundTrip(owner.ToSerializable()); throw new InvalidOperationException("legacy card insertion save"); }
        owner.Deck.CardAdded += SaveAndFail;
        try { Require((await Pack(owner).TakeCards(new[] { 0 }))[0].Status == OperationStatus.PartialCommit, "Legacy card insertion did not reach the fault"); }
        finally { owner.Deck.CardAdded -= SaveAndFail; }
        Require(saved != null, "No intermediate legacy-card save captured");
        owner = Player.FromSerializable(saved!); RunState.CreateForTest([owner], seed: "STR-G1-INTEGRITY");
        var key = InventoryIdentity.CardKey(Pack(owner).StoredCards.Single());
        Require(key.Length > 0 && InventoryIdentity.FindCard(owner, key) != null
            && (await Pack(owner).TakeCards(new[] { 0 }))[0].Succeeded && owner.Deck.Cards.Count == 1
            && owner.Deck.Cards[0].IsUpgraded && Pack(owner).StoredCards.Count == 0,
            "Legacy card metadata was assigned after insertion or the saved destination was added twice");
    }

    private static async Task LegacyRelicInterruptedTake()
    {
        var owner = Fixture(); Pack(owner).StoredItems = "OakStaff,OakStaff";
        SerializablePlayer? saved = null;
        void SaveAndFail(RelicModel _) { saved = RoundTrip(owner.ToSerializable()); throw new InvalidOperationException("legacy relic insertion save"); }
        owner.RelicObtained += SaveAndFail;
        try { Require((await Pack(owner).TakeItem("legacy:1:OakStaff")).Status == OperationStatus.PartialCommit, "Legacy relic insertion did not reach the fault"); }
        finally { owner.RelicObtained -= SaveAndFail; }
        Require(saved != null, "No intermediate legacy-relic save captured");
        owner = Player.FromSerializable(saved!); RunState.CreateForTest([owner], seed: "STR-G1-INTEGRITY");
        Require(StoredItemCodec.TryRead(Pack(owner).StoredItemData, Pack(owner).StoredItems, out var entries, out _) && entries.Count == 2,
            "Legacy relic migration discarded a different duplicate");
        var migrated = entries.Single(e => e.Payload.Length > 0);
        Require(migrated.Key.StartsWith("r:", StringComparison.Ordinal) && InventoryIdentity.FindRelic(owner, migrated.Key) is EquipmentRelic { IsStowed: true }
            && (await Pack(owner).TakeItem(migrated.Key)).Succeeded && owner.Relics.OfType<OakStaff>().Count() == 1
            && Pack(owner).StoredCount == 1 && !(await Pack(owner).TakeItem("legacy:1:OakStaff")).Changed,
            "Legacy relic remained a repeatable name after an insertion save");
    }

    private static async Task AbandonedMutation()
    {
        var owner = Fixture();
        using (var lease = await InventoryGate.For(owner).Enter())
        using (InventoryGate.Track(owner, lease!, "fixture:abandoned")) { /* deliberate unwind without Complete */ }
        Require(InventoryGate.For(owner).Blocked && Pack(owner).OperationBlocked, "Unfinished scope allowed new operations");
        owner = Reload(owner);
        Require(InventoryGate.For(owner).Blocked && InventoryGate.For(owner).TryEnter() == null, "Abandoned mutation was unblocked by JSON reload");
    }

    private static async Task ShopPaymentLateThrow()
    {
        var owner = ShopFixture(ShopType.Arcanist); var hero = HeroRecord.Of(owner)!;
        var power = ShopRules.SoulStonePower(Acts.ActKinds.Tier(owner.RunState));
        var price = ShopRules.ApplyTrust(ShopRules.SoulStonePrice(power), MerchantLedger.Of(owner)!.CurrentTrust);
        var (request, quote) = Quote(owner, ShopAction.BuyStone, price, power); var stones = hero.Stones(power); var gold = owner.Gold;
        void Fail() => throw new InvalidOperationException("GoldChanged after payment mutation");
        owner.GoldChanged += Fail;
        try
        {
            using var lease = await InventoryGate.For(owner).Enter();
            var result = await ShopOperations.ExecuteGuarded(owner, request, quote, lease!);
            Require(result.Status == OperationStatus.PartialCommit && result.Gold == price && owner.Gold == gold - price
                && hero.Stones(power) == stones + 1 && MerchantLedger.Of(owner)!.Spent[(int)ShopType.Arcanist] == price
                && InventoryGate.For(owner).Blocked, "Late payment was recharged, lost its effect, or failed to credit actual trust");
        }
        finally { owner.GoldChanged -= Fail; }
    }

    private static async Task DiscardStaleTarget()
    {
        var owner = Fixture(); var card = owner.RunState.CreateCard<Anger>(owner); owner.Deck.AddInternal(card, silent: true);
        FillToExcess(owner, 1);
        var key = InventoryIdentity.CardKey(card); var (request, quote) = Quote(owner, ShopAction.DiscardCard, int.MaxValue, 0, key);
        Require(UI.DeckDiscard.CanExecute(owner, card) && !UI.DeckDiscard.CanExecute(owner, ModelDb.Card<Anger>()),
            "Discard did not distinguish a live owned instance from a canonical model");
        var other = Fixture(); var foreign = other.RunState.CreateCard<Anger>(other); other.Deck.AddInternal(foreign, silent: true);
        Require(!UI.DeckDiscard.CanExecute(owner, foreign), "Discard accepted another owner's card");
        var filler = owner.Deck.Cards.First(c => !ReferenceEquals(c, card));
        filler.RemoveFromCurrentPile(silent: true); owner.RunState.RemoveCard(filler);
        var gold = owner.Gold;
        using var lease = await InventoryGate.For(owner).Enter();
        Require((await ShopOperations.ExecuteGuarded(owner, request, quote, lease!)).Status == OperationStatus.Rejected
            && owner.Deck.Cards.Contains(card) && owner.Gold == gold && !InventoryGate.For(owner).Blocked,
            "A stale discard/index bypassed the executor's current-overload rule");
    }

    private static async Task BlessingLateThrow()
    {
        var owner = Fixture();
        foreach (var blessing in owner.Relics.OfType<Pantheon.CelestialBlessing>().ToList()) owner.RemoveRelicInternal(blessing, silent: true);
        var old = Pantheon.CelestialBlessing.Create(Pantheon.CelestialBlessing.Blessings[0]); owner.AddRelicInternal(old, silent: true);
        void Fail(RelicModel _) => throw new InvalidOperationException("blessing inventory notification");
        owner.RelicObtained += Fail;
        try
        {
            try { await Pantheon.CelestialBlessing.Grant(owner, (Celestial)1); }
            catch (InvalidOperationException e) when (e.Message.StartsWith("Blessing change PartialCommit:", StringComparison.Ordinal)) { }
            Require(owner.Relics.OfType<Pantheon.CelestialBlessing>().Single().God == (Celestial)1 && !owner.Relics.Contains(old)
                && InventoryGate.For(owner).Blocked, "Blessing change lost both relics or left a repeatable unreported late obtain");
        }
        finally { owner.RelicObtained -= Fail; }
    }

    private static async Task CardChestCallbackUnrelated()
    {
        var owner = Fixture(); var pack = Pack(owner);
        var first = DeckCard(owner); var second = DeckCard(owner);
        var key = InventoryIdentity.CardKey(first);
        Require((await pack.StoreCards([first, second])).All(r => r.Succeeded), "Could not stage callback fixture");
        var other = pack.StoredCards[1];
        var unknown = new SerializableCard
        {
            Id = new ModelId(ModelId.SlugifyCategory<CardModel>(), "SLAYTHEREQUIEM-REMOVED_CALLBACK_FIXTURE"),
            Props = new SavedProperties { ints = [new("CardModel_SlayTheRequiem_Instance", 1000000)],
                strings = [new("OpaqueCallbackState", "keep this unknown card")] },
        };
        void ChangeOther(CardModel _)
        {
            other.CurrentUpgradeLevel = 1;
            pack.StoredCards.Insert(0, unknown); // invalidate the selected index without changing its source
        }
        owner.Deck.CardAdded += ChangeOther;
        try { Require((await pack.TakeCards(new[] { 0 }))[0].Succeeded, "Unrelated chest change incorrectly prevented exact source finalization"); }
        finally { owner.Deck.CardAdded -= ChangeOther; }
        Require(owner.Deck.Cards.Count == 1 && InventoryIdentity.FindCard(owner, key) != null && pack.StoredCards.Count == 2
            && ReferenceEquals(pack.StoredCards[0], unknown) && ReferenceEquals(pack.StoredCards[1], other) && other.CurrentUpgradeLevel == 1,
            "Index-based finalization erased a callback's unknown/unrelated source");
        var payload = Json(pack.StoredCards);
        owner = Reload(owner);
        Require(Json(Pack(owner).StoredCards) == payload, "Callback's unknown card state did not survive player JSON reload");
    }

    private static async Task CardChestCallbackSource()
    {
        foreach (var replace in new[] { false, true })
        {
            var owner = Fixture(); var pack = Pack(owner);
            await pack.StoreCards([DeckCard(owner)]);
            var source = pack.StoredCards.Single();
            SerializableCard? changed = null;
            void ChangeSource(CardModel _)
            {
                changed = replace ? RoundTrip(source) : source;
                changed.CurrentUpgradeLevel = 1;
                if (replace) pack.StoredCards[0] = changed;
            }
            owner.Deck.CardAdded += ChangeSource;
            try { Require((await pack.TakeCards(new[] { 0 }))[0].Status == OperationStatus.PartialCommit, "Changed saved source was reported as safely finalized"); }
            finally { owner.Deck.CardAdded -= ChangeSource; }
            Require(changed != null && ReferenceEquals(pack.StoredCards.Single(), changed) && changed.CurrentUpgradeLevel == 1
                && owner.Deck.Cards.Count == 1 && !owner.Deck.Cards[0].IsUpgraded && InventoryGate.For(owner).Blocked,
                "A callback's replacement/mutated source was deleted or aliased into the live destination");
            var payload = Json(pack.StoredCards);
            owner = Reload(owner);
            Require(Json(Pack(owner).StoredCards) == payload && InventoryGate.For(owner).Blocked,
                "Continue forgot the source-finalization conflict");
        }
    }

    private static async Task ItemChestCallbackUnrelated()
    {
        var owner = Fixture(); var pack = Pack(owner);
        var item = (OakStaff)ModelDb.Relic<OakStaff>().ToMutable(); item.IsStowed = true;
        owner.AddRelicInternal(item, silent: true);
        var key = InventoryIdentity.RelicKey(item);
        Require((await pack.StoreItem(item)).Succeeded, "Could not stage item callback fixture");
        Require(StoredItemCodec.TryRead(pack.StoredItemData, pack.StoredItems, out var initial, out _), "Invalid fixture envelope");
        initial.Add(new("opaque:other", "{\"unavailable_type\":\"keep\",\"charges\":[4,7]}"));
        pack.StoredItemData = StoredItemCodec.Write(initial);
        var changed = new StoredItemRecord("opaque:other", "{\"unavailable_type\":\"updated\",\"charges\":[1,3,8]}");
        var added = new StoredItemRecord("opaque:added", "not even a known SerializableRelic; retain verbatim");
        void ChangeOther(RelicModel ignored)
        {
            Require(StoredItemCodec.TryRead(pack.StoredItemData, pack.StoredItems, out var current, out _), "Callback could not read fixture envelope");
            current[current.FindIndex(e => e.Key == changed.Key)] = changed;
            current.Insert(0, added);
            pack.StoredItemData = StoredItemCodec.Write(current);
        }
        owner.RelicObtained += ChangeOther;
        try { Require((await pack.TakeItem(key)).Succeeded, "Unrelated item callback prevented exact source finalization"); }
        finally { owner.RelicObtained -= ChangeOther; }
        Require(StoredItemCodec.TryRead(pack.StoredItemData, pack.StoredItems, out var remaining, out _)
            && remaining.SequenceEqual(new[] { added, changed }) && InventoryIdentity.FindRelic(owner, key) is OakStaff { IsStowed: true },
            "Item finalizer overwrote callback changes or temporarily equipped its destination");
        var data = pack.StoredItemData;
        owner = Reload(owner);
        Require(Pack(owner).StoredItemData == data, "Opaque callback data changed during player JSON reload");
    }

    private static async Task ItemChestCallbackSource()
    {
        var owner = Fixture(); var pack = Pack(owner);
        var item = (OakStaff)ModelDb.Relic<OakStaff>().ToMutable(); item.IsStowed = true; item.Upgrades = 1;
        owner.AddRelicInternal(item, silent: true);
        var key = InventoryIdentity.RelicKey(item);
        Require((await pack.StoreItem(item)).Succeeded, "Could not stage source-change fixture");
        StoredItemRecord? changed = null;
        void ChangeSource(RelicModel ignored)
        {
            Require(StoredItemCodec.TryRead(pack.StoredItemData, pack.StoredItems, out var current, out _), "Callback could not read fixture");
            var i = current.FindIndex(e => e.Key == key);
            var snapshot = JsonSerializer.Deserialize(current[i].Payload, JsonSerializationUtility.GetTypeInfo<SerializableRelic>())!;
            snapshot.Props!.ints!.RemoveAll(p => p.name == nameof(EquipmentRelic.Upgrades));
            snapshot.Props.ints.Add(new(nameof(EquipmentRelic.Upgrades), 2));
            changed = current[i] with { Payload = Json(snapshot) };
            current[i] = changed;
            pack.StoredItemData = StoredItemCodec.Write(current);
        }
        owner.RelicObtained += ChangeSource;
        try { Require((await pack.TakeItem(key)).Status == OperationStatus.PartialCommit, "Changed item source was reported as safely finalized"); }
        finally { owner.RelicObtained -= ChangeSource; }
        Require(changed != null && StoredItemCodec.TryRead(pack.StoredItemData, pack.StoredItems, out var remaining, out _)
            && remaining.Single() == changed && InventoryIdentity.FindRelic(owner, key) is OakStaff { Upgrades: 1, IsStowed: true }
            && InventoryGate.For(owner).Blocked, "A callback's changed item source was overwritten/erased");
    }

    private static async Task InvalidChestIdentities()
    {
        foreach (var kind in new[] { "duplicate-record", "negative-id", "duplicate-id-field" })
        {
            var owner = Fixture(); var pack = Pack(owner);
            await pack.StoreCards([DeckCard(owner)]);
            var source = pack.StoredCards.Single();
            if (kind == "duplicate-record") pack.StoredCards.Add(RoundTrip(source));
            else
            {
                const string field = "CardModel_SlayTheRequiem_Instance";
                source.Props!.ints!.RemoveAll(p => p.name == field);
                source.Props.ints.Add(new(field, kind == "negative-id" ? -1 : 700));
                if (kind == "duplicate-id-field") source.Props.ints.Add(new(field, 701));
            }
            var payload = Json(pack.StoredCards);
            Require((await pack.TakeCards(new[] { 0 }))[0].Status == OperationStatus.PartialCommit && owner.Deck.Cards.Count == 0
                && Json(pack.StoredCards) == payload && InventoryGate.For(owner).Blocked,
                $"Invalid identity ({kind}) was repaired ambiguously, consumed, or used to add a card");
        }
    }

    private static async Task InvalidItemEnvelope()
    {
        var duplicate = StoredItemCodec.Write([new("r:7", "first"), new("r:7", "second")]);
        foreach (var data in new[] { duplicate, StoredItemCodec.Write([new("  ", "opaque")]),
            "{\"Version\":99,\"Items\":[]}", "{broken" })
        {
            var owner = Fixture(); var pack = Pack(owner);
            pack.StoredItemData = data; pack.StoredItems = "RemovedEquipment";
            var item = (OakStaff)ModelDb.Relic<OakStaff>().ToMutable(); item.IsStowed = true; owner.AddRelicInternal(item, silent: true);
            Require((await pack.StoreItem(item)).Status == OperationStatus.Rejected && !(await pack.TakeItem("r:7")).Changed
                && pack.LoseHalf(_ => 0) == 0 && owner.Relics.Contains(item) && pack.StoredItemData == data && pack.StoredItems == "RemovedEquipment",
                "Corrupt/future/ambiguous item envelope was rewritten as a writable empty chest");
        }
    }

    private static async Task ShopTradeLateThrow()
    {
        var owner = ShopFixture(ShopType.Caravan);
        foreach (var stowed in ShopServices.Tradeable(owner)) owner.RemoveRelicInternal(stowed, silent: true);
        var given = (OakStaff)ModelDb.Relic<OakStaff>().ToMutable(); given.IsStowed = true; owner.AddRelicInternal(given, silent: true);
        var expected = ShopServices.TradeResult(owner, given) ?? throw new InvalidOperationException("No fixture trade result");
        Require(ShopServices.Tradeable(owner).Count == 1, "Trade fixture would open a remote selector");
        var (request, quote) = Quote(owner, ShopAction.Trade, 0, 0);
        var gold = owner.Gold;
        void Fail(RelicModel relic) { if (ReferenceEquals(relic, given)) throw new InvalidOperationException("trade source notification after detach"); }
        owner.RelicRemoved += Fail;
        try
        {
            using var lease = await InventoryGate.For(owner).Enter();
            var result = await ShopOperations.ExecuteGuarded(owner, request, quote, lease!);
            Require(result.Status == OperationStatus.PartialCommit && !owner.Relics.Contains(given)
                && owner.Relics.Count(r => r.GetType() == expected.GetType()) == 1 && MerchantLedger.Of(owner)!.TradedHere(owner.RunState)
                && owner.Gold == gold && InventoryGate.For(owner).Blocked,
                "Late trade-source removal skipped the once-per-visit finalizer or lost its new destination");
        }
        finally { owner.RelicRemoved -= Fail; }
    }

    private static Task ShoutRegistration()
    {
        var owner = Fixture(); owner.ResetCombatState();
        var combat = new CombatState(runState: owner.RunState); combat.AddPlayer(owner);
        var shelf = (ShoutShelfPower)ModelDb.Power<ShoutShelfPower>().ToMutable();
        shelf.ApplyInternal(owner.Creature, 1, silent: true);
        var card = combat.CreateCard<UnrelentingForce>(owner);
        owner.PlayerCombatState!.PlayPile.AddInternal(card, silent: true);
        Require(shelf.Put(card, 1) && !shelf.Put(card, 1), "Repeated play duplicated the shelf instance");
        card.RemoveFromState(); // normal None cleanup in 0.107.1
        Require(shelf.PrepareReturn(card, combat) && card.Owner == owner && !card.HasBeenRemovedFromState, "Native removed card could not return");
        card.RemoveFromState(); combat.RemoveCard(card); // explicit scope removal also clears Owner
        Require(card.Owner == null && shelf.PrepareReturn(card, combat) && card.Owner == owner && combat.ContainsCard(card), "Captured null-owner entitlement was discarded");
        var clone = (ShoutShelfPower)shelf.MutableClone();
        Require(clone.Count == 0 && shelf.Count == 1, "Cloned shelf shared physical card entitlements");
        owner.PlayerCombatState.ExhaustPile.AddInternal(card, silent: true);
        Require(!shelf.PrepareReturn(card, combat), "Shelf pulled a card out of Exhaust");
        card.RemoveFromCurrentPile(silent: true);
        var foreign = Fixture(); combat.RemoveCard(card); combat.AddCard(card, foreign);
        Require(!shelf.PrepareReturn(card, combat) && card.Owner == foreign, "Shelf stole a foreign card");
        return Task.CompletedTask;
    }

    private static async Task MandatorySourcePending()
    {
        var owner = Fixture(); FillToExcess(owner, 11);
        await CardAcquisition.GrantOnceOrDefer(Pack(owner), owner.RunState.CreateCard<Bash>(owner), "adapter-once");
        Require(Pack(owner).PendingCards.Count == 1, "Mandatory veto lost its source");
        var restored = Reload(owner);
        await CardAcquisition.GrantOnceOrDefer(Pack(restored), restored.RunState.CreateCard<Bash>(restored), "adapter-once");
        Require(Pack(restored).PendingCards.Count == 1 && !restored.Deck.Cards.Any(c => c is Bash && CardAcquisition.IsDeferredClaim(c)),
            "Reload replayed a prepared mandatory source");
    }

    private static async Task MandatorySourceCompleted()
    {
        var owner = Fixture();
        await CardAcquisition.GrantOnceOrDefer(Pack(owner), owner.RunState.CreateCard<Bash>(owner), "adapter-once");
        Require(owner.Deck.Cards.Count == 1 && Pack(owner).PendingCards.Count == 0, "Mandatory acquisition did not finish");
        var granted = owner.Deck.Cards[0]; granted.RemoveFromCurrentPile(silent: true); owner.RunState.RemoveCard(granted);
        var restored = Reload(owner);
        await CardAcquisition.GrantOnceOrDefer(Pack(restored), restored.RunState.CreateCard<Bash>(restored), "adapter-once");
        Require(restored.Deck.Cards.Count == 0 && Pack(restored).PendingCards.Count == 0, "Consumed completed grant was issued again");
    }

    private static Task LegacyStartNone()
    {
        var owner = Fixture(); var grimoire = Mastery.GrimoireOf(owner)!;
        var hp = owner.Creature.MaxHp; var deck = owner.Deck.Cards.Count;
        grimoire.StartProtocol = 0; grimoire.StartPhase = 0; grimoire.OriginGranted = true;
        grimoire.CreationData = ""; grimoire.MasterySourceData = ""; // a real pre-v2 fixture, not mixed current metadata
        grimoire.MigrateLegacyMastery(); grimoire.MigrateLegacyStart();
        Require(grimoire.StartingChoicesComplete && grimoire.OriginChoiceId == "none" && grimoire.BackgroundChoiceId == "none",
            "Legacy completed 'none' decision became unfinished");
        var restored = Reload(owner); var after = Mastery.GrimoireOf(restored)!; after.MigrateLegacyStart();
        Require(after.StartingChoicesComplete && restored.Creature.MaxHp == hp && restored.Deck.Cards.Count == deck,
            "Continue replayed a declared-completed legacy startup");
        return Task.CompletedTask;
    }

    private static Task FutureSchemaReject()
    {
        var owner = Fixture(); var save = new SerializableRun { Players = [owner.ToSerializable()] };
        var pack = save.Players[0].Relics.Single(r => r.Id == Pack(owner).Id);
        SavedPropertyWrite.Int(pack.Props ??= new SavedProperties(), nameof(TravelPack.DataSchema), RunDiagnostics.SaveSchema + 1);
        var before = Json(save);
        try { Config.RunSettings.ValidateSaved(save); throw new InvalidOperationException("Future schema was accepted"); }
        catch (FormatException) { Require(before == Json(save), "Read-only saved validation mutated the original native object"); }
        return Task.CompletedTask;
    }

    private static Task TimelineOpaqueProperties()
    {
        var owner = Fixture(); var save = new SerializableRun { Players = [owner.ToSerializable()] };
        var pack = save.Players[0].Relics.Single(r => r.Id == Pack(owner).Id);
        var props = pack.Props ??= new SavedProperties();
        SavedPropertyWrite.Text(props, "unavailable_external_field", "opaque-future-payload");
        SavedPropertyWrite.Int(props, nameof(TravelPack.OperationRevision), 19);
        OperationRecovery.RotateTimeline(save);
        Require(props.strings!.Single(p => p.name == "unavailable_external_field").value == "opaque-future-payload"
            && props.ints!.Single(p => p.name == nameof(TravelPack.OperationRevision)).value == 0
            && props.strings.Single(p => p.name == nameof(TravelPack.OperationRunId)).value.Length > 0,
            "Timeline rotation round-tripped away an unrelated saved property");
        return Task.CompletedTask;
    }

    private static async Task ReceiptDisposal()
    {
        var owner = Fixture();
        using var lease = await InventoryGate.For(owner).Enter();
        using var mutation = InventoryGate.Track(owner, lease!, "adapter-receipt");
        InventoryGate.Block(owner, "receipt timeout");
        var epoch = InventoryGate.FailureEpoch(owner);
        InventoryGate.ClearReceiptBlock(owner, epoch);
        Require(InventoryGate.For(owner).Blocked, "Receipt cleared an active mutation");
        mutation.Complete(); mutation.Dispose();
        InventoryGate.ClearReceiptBlock(owner, epoch);
        Require(!InventoryGate.For(owner).Blocked && !Pack(owner).OperationBlocked, "Matching receipts stayed blocked after mutation disposal");
        InventoryGate.Block(owner, "new independent partial failure");
        InventoryGate.ClearReceiptBlock(owner, epoch);
        Require(InventoryGate.For(owner).Blocked, "Stale receipt cleared a newer failure");
    }

    private static Task ServiceCloneIsolation()
    {
        var owner = Fixture(); var other = Fixture();
        var pouch = HerbalistPouch.Of(owner)!; pouch.Add(Reagent.Bloodmoss, 2);
        var copy = (HerbalistPouch)pouch.MutableClone(); copy.Reagents[0] = 7;
        Require(pouch.Count(Reagent.Bloodmoss) == 2 && HerbalistPouch.Of(other)!.Total == 0, "Pouch clones/players share reagents");
        var ledger = MerchantLedger.Of(owner)!; ledger.Spent[0] = 100;
        var ledgerCopy = (MerchantLedger)ledger.MutableClone(); ledgerCopy.Spent[0] = 200;
        Require(ledger.Spent[0] == 100 && MerchantLedger.Of(other)!.Spent[0] == 0, "Merchant clones/players share payment counters");
        var record = HeroRecord.Of(owner)!; _ = record.Learned;
        var recordCopy = (HeroRecord)record.MutableClone();
        Require(!ReferenceEquals(record.Learned, recordCopy.Learned), "HeroRecord clones share the learned-skill set");
        return Task.CompletedTask;
    }

    private static async Task PathPendingSign()
    {
        var owner = Fixture();
        var essence = (PaladinEssence)ModelDb.Relic<PaladinEssence>().ToMutable();
        PathGrants.Prepare(essence, owner);
        owner.AddRelicInternal(essence, silent: true); // fixture has confirmed the pure Paladin issuer, not an OnObtained replay
        essence.SignGrant = RelicGrantState.Read(essence.SignGrant)!.IssuerCompleted().Encode();
        var originalKey = RelicGrantState.Read(essence.SignGrant)!.Key;
        owner = Reload(owner);
        await Paths.Grant(owner, HeroPath.Paladin);
        essence = owner.Relics.OfType<PaladinEssence>().Single();
        var sign = owner.Relics.OfType<RadiantCuirass>().Single();
        Require(InventoryIdentity.RelicKey(sign) == originalKey && RelicGrantState.Read(essence.SignGrant)!.Phase == RelicGrantState.Completed,
            "An existing essence suppressed/lost its ready companion entitlement");
        owner.RemoveRelicInternal(sign, silent: true); // deliberate loss is NOT a fresh entitlement after Continue
        owner = Reload(owner);
        await Paths.Grant(owner, HeroPath.Paladin);
        Require(!owner.Relics.OfType<RadiantCuirass>().Any() && owner.Relics.OfType<PaladinEssence>().Count() == 1,
            "Completed path replayed its sign or issuer after deliberate loss/reload");
    }

    private static async Task PathSignLateInsert()
    {
        var owner = Fixture();
        var essence = (PaladinEssence)ModelDb.Relic<PaladinEssence>().ToMutable();
        PathGrants.Prepare(essence, owner); owner.AddRelicInternal(essence, silent: true);
        essence.SignGrant = RelicGrantState.Read(essence.SignGrant)!.IssuerCompleted().Encode();
        void Fail(RelicModel relic) { if (relic is RadiantCuirass) throw new InvalidOperationException("injected late sign insertion"); }
        owner.RelicObtained += Fail;
        try { Require((await PathGrants.CompleteSign(essence)).Status == OperationStatus.PartialCommit, "Late sign obtain was reported as a safe veto"); }
        finally { owner.RelicObtained -= Fail; }
        Require(owner.Relics.OfType<RadiantCuirass>().Count() == 1 && InventoryGate.For(owner).Blocked
            && RelicGrantState.Read(essence.SignGrant)!.Phase == RelicGrantState.Completed, "Late sign insertion left a repeatable source");
        owner = Reload(owner);
        await Paths.Grant(owner, HeroPath.Paladin);
        Require(owner.Relics.OfType<RadiantCuirass>().Count() == 1 && InventoryGate.For(owner).Blocked, "Continue duplicated/unblocked a partial path transition");
    }

    private static Task RawSavePreflight()
    {
        var owner = Fixture();
        var save = new SerializableRun { SchemaVersion = SaveManager.Instance.GetLatestSchemaVersion<SerializableRun>(), Players = [owner.ToSerializable()] };
        var original = Json(save);
        var bytes = System.Text.Encoding.UTF8.GetBytes(original);
        Require(RunSaveReadGuard.ReadStrict(bytes).Players.Count == 1 && original == Json(save), "Strict native preflight changed a valid input");
        var document = System.Text.Json.Nodes.JsonNode.Parse(original)!;
        document["schema_version"] = save.SchemaVersion + 1;
        try { RunSaveReadGuard.ReadStrict(System.Text.Encoding.UTF8.GetBytes(document.ToJsonString())); throw new InvalidOperationException("Future native schema accepted"); }
        catch (FormatException) { Require(original == Json(save), "Refusal changed the native input object"); }
        document["schema_version"] = save.SchemaVersion;
        document["players"]![0]!["future_native_field"] = 7;
        try { RunSaveReadGuard.ReadStrict(System.Text.Encoding.UTF8.GetBytes(document.ToJsonString())); throw new InvalidOperationException("Unknown nested native field accepted"); }
        catch (JsonException) { Require(original == Json(save), "Nested-field refusal changed the input"); }
        return Task.CompletedTask;
    }

    private static async Task BoonHpWriteAhead()
    {
        var owner = Fixture(); var hp = owner.Creature.MaxHp;
        var omen = (Pantheon.OmenOfTheBlade)ModelDb.Relic<Pantheon.OmenOfTheBlade>().ToMutable();
        owner.AddRelicInternal(omen, silent: true);
        SerializablePlayer? during = null;
        void SaveInside(int before, int after) { during ??= RoundTrip(owner.ToSerializable()); }
        owner.Creature.MaxHpChanged += SaveInside;
        try { await omen.SyncMaxHp(); }
        finally { owner.Creature.MaxHpChanged -= SaveInside; }
        Require(during != null && omen.GrantedMaxHp == omen.ActiveBoon.MaxHp && owner.Creature.MaxHp == hp + omen.GrantedMaxHp,
            "Passive boon HP failed to apply its delta");
        await omen.SyncMaxHp();
        Require(owner.Creature.MaxHp == hp + omen.GrantedMaxHp, "Repeated boon reconciliation applied the same delta twice");
        var interrupted = Player.FromSerializable(during!);
        RunState.CreateForTest([interrupted], seed: "STR-G1-INTEGRITY");
        var savedHp = interrupted.Creature.MaxHp;
        await interrupted.Relics.OfType<Pantheon.OmenOfTheBlade>().Single().SyncMaxHp();
        Require(Pack(interrupted).OperationPending.StartsWith("boon-hp:", StringComparison.Ordinal)
            && InventoryGate.For(interrupted).Blocked && interrupted.Creature.MaxHp == savedHp,
            "A save inside MaxHpChanged silently replayed an uncertain HP delta on Continue");
        await omen.Revoke();
        Require(omen.GrantedMaxHp == 0 && owner.Creature.MaxHp == hp && Pack(owner).OperationPending.Length == 0,
            "Normal boon revoke lost its granted HP accounting or left a false pending marker");
    }

    private static Task WireRoundTrip()
    {
        foreach (var control in new[] { StartupState.LobbyQuery, StartupState.LobbyOffer, StartupState.LobbyAgreement })
        {
            var protocol = new RunSettingsMessage { data = control };
            var protocolWriter = new PacketWriter(); protocol.Serialize(protocolWriter);
            var protocolReader = new PacketReader(); protocolReader.Reset(protocolWriter.Buffer);
            var protocolCopy = new RunSettingsMessage(); protocolCopy.Deserialize(protocolReader);
            Require(protocolCopy.data == control && protocolReader.BitPosition == protocolWriter.BitPosition,
                "Existing settings-string envelope changed during startup negotiation");
        }
        var run = OperationIdentity.RunKey("seed", "saved-nonce", ulong.MaxValue);
        var request = new ShopRequest(run, OperationIdentity.Id(run, 17), 17, OperationIdentity.Hash("before"), ShopAction.Sell, -4, 9, "r:19,r:20");
        var message = new ShopActionMessage { location = new RunLocation(2, null, 1), request = request };
        var writer = new PacketWriter(); message.Serialize(writer);
        var reader = new PacketReader(); reader.Reset(writer.Buffer); var copy = new ShopActionMessage(); copy.Deserialize(reader);
        Require(copy.request == request && copy.location == message.location && reader.BitPosition == writer.BitPosition, "Request wire layout changed");
        var receipt = new ShopActionReceiptMessage { location = message.location, playerId = ulong.MaxValue, operationId = request.Id,
            status = (int)OperationStatus.PartialCommit, gold = -35, digest = OperationIdentity.Hash("after") };
        writer.Reset(); receipt.Serialize(writer); reader.Reset(writer.Buffer); var ack = new ShopActionReceiptMessage(); ack.Deserialize(reader);
        Require(ack.location == receipt.location && ack.playerId == receipt.playerId && ack.operationId == receipt.operationId
            && ack.status == receipt.status && ack.gold == receipt.gold && ack.digest == receipt.digest && reader.BitPosition == writer.BitPosition,
            "Receipt wire layout changed");
        var stone = new MemoryStoneMessage { run = OperationIdentity.Hash("attempt"), phase = (int)StonePhase.Go, round = 23, value = 1, extra = 3 };
        writer.Reset(); stone.Serialize(writer); reader.Reset(writer.Buffer); var stoneCopy = new MemoryStoneMessage(); stoneCopy.Deserialize(reader);
        Require(stoneCopy.run == stone.run && stoneCopy.phase == stone.phase && stoneCopy.round == stone.round
            && stoneCopy.value == stone.value && stoneCopy.extra == stone.extra && reader.BitPosition == writer.BitPosition,
            "Authenticated Memory Stone packet lost its attempt identity or phase");
        return Task.CompletedTask;
    }
}
