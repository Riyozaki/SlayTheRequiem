using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Context;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>Voluntary acquisition still honors EVERY ShouldAddToDeck hook. Curses/statuses keep their own game path.</summary>
public static class CardAcquisition
{
    // Obtain modifiers (Eggs/Fresnel Lens) clone cards. The claim/debt follows THAT acquisition clone, while the
    // inventory instance ID deliberately does not. Completed claims are cleared before ordinary combat copying.
    private static readonly SpireField<CardModel, string> Replacement =
        new SavedSpireField<CardModel, string>(() => "", "SlayTheRequiem_DeferredReplacement").CopyOnClone();
    private static readonly SavedSpireField<CardModel, string> SavedClaim = new(() => "", "SlayTheRequiem_DeferredClaim");
    private static readonly SpireField<CardModel, string> Claim = SavedClaim.CopyOnClone();
    private sealed record AddTarget(CardModel Card);
    private static readonly ConditionalWeakTable<CardModel, AddTarget> AddTargets = new();
    public static void Initialize() { _ = Replacement; _ = Claim; }
    internal static void BeginAdd(CardModel card) => AddTargets.Remove(card);
    internal static string ClaimKey(CardModel card) => Claim[card] ?? "";
    internal static void MarkClaim(CardModel card, string key) => Claim[card] = key;
    internal static bool IsDeferredClaim(CardModel card) => !string.IsNullOrEmpty(ClaimKey(card));
    internal static bool HasOutstandingClaim(MegaCrit.Sts2.Core.Entities.Players.Player owner, string key) => key.Length > 0
        && (TravelPack.Of(owner)?.PendingCards.Any(s => ClaimKey(s) == key) == true
            || owner.PotionSlots.OfType<Shops.RequiemScroll>().Any(s => s.RewriteClaimKey == key));
    internal static string ClaimKey(SerializableCard snapshot) =>
        snapshot.Props?.strings?.FirstOrDefault(p => p.name == SavedClaim.Name).value ?? "";

    internal static void ObserveAddTarget(CardModel original, CardModel actual)
    {
        AddTargets.Remove(original);
        AddTargets.Add(original, new(actual));
        if (ReferenceEquals(original, actual)) return;
        // An obtain modifier may create a DIFFERENT model instead of MutableClone. The obligation still belongs
        // to this acquisition, not to the implementation detail of the modifier. Never copy its inventory ID.
        if (IsDeferredClaim(original)) Claim[actual] = ClaimKey(original);
        if (!string.IsNullOrEmpty(Replacement[original])) Replacement[actual] = Replacement[original];
        Debugging.CardRewardTrace.Card("add:modified", actual, $"source={original.Id}");
    }

    internal static void CleanUncommitted(CardModel source, MegaCrit.Sts2.Core.Entities.Players.Player owner)
    {
        // A veto/throwing modifier can register an unattached clone. Do not leave either preview in the run scope,
        // but never remove an observed destination, including one inserted before a later hook exception.
        if (AddTargets.TryGetValue(source, out var target) && !ReferenceEquals(source, target.Card)) Clean(target.Card);
        Clean(source);
        void Clean(CardModel card)
        {
            if (!owner.Deck.Cards.Contains(card) && card.Pile == null && owner.RunState.ContainsCard(card)) owner.RunState.RemoveCard(card);
        }
    }

    // Do not detach the source from RunState here: the native loop still uses its holder/NCard for the fly VFX
    // immediately after RemoveAll. Scoped callers clean previews after the reward task, not between UI steps.
    internal static int RemoveSelectedOptions(List<CardCreationResult> options, Predicate<CardCreationResult> selected) =>
        options.RemoveAll(option => selected(option) || option.Card.Owner is { } owner
            && CommittedCard(option.Card, owner) is { } actual && !ReferenceEquals(actual, option.Card)
            && selected(new CardCreationResult(actual)));

    internal static CardModel? CommittedCard(CardModel source, MegaCrit.Sts2.Core.Entities.Players.Player owner)
    {
        if (owner.Deck.Cards.Contains(source)) return source;
        if (AddTargets.TryGetValue(source, out var target) && owner.Deck.Cards.Contains(target.Card)) return target.Card;
        var key = ClaimKey(source);
        return key.Length == 0 ? null : owner.Deck.Cards.FirstOrDefault(c => ClaimKey(c) == key);
    }

    /// <summary>One-time grants cannot disappear on a burden veto. Keep a durable claim, not an orphan in RunState.</summary>
    public static async Task<bool> AddOrDefer(CardModel card, CardModel? replacing = null)
    {
        var owner = card.Owner ?? throw new InvalidOperationException("Card grant has no owner");
        if (replacing != null) Replacement[card] = InventoryIdentity.CardKey(replacing);
        // Write the ENTITLEMENT before the first obtain/veto hook, not only its key on an unattached preview.
        // A save before insertion must still contain the grant; a save after an Egg clone can reconcile both views.
        var pack = TravelPack.Of(owner);
        var snapshot = pack?.PrepareDeferredCard(card);
        var expectedPayload = snapshot == null ? "" : TravelPack.CardPayload(snapshot);
        var result = await AddSafely(card, PileType.Deck);
        if (result.success && CommittedCard(card, owner) is { } committed)
        {
            try
            {
                if (await CompleteReplacement(committed))
                {
                    if (snapshot != null) pack!.ConsumePending(snapshot, expectedPayload);
                    MarkClaim(committed, "");
                }
            }
            catch (Exception e)
            {
                // The card exists. Keep its claim/source for reconciliation, never present this as a fresh grant.
                InventoryGate.Block(owner, $"Card grant {card.Id} committed with incomplete finalization: {e}");
            }
            finally { CleanUncommitted(card, owner); }
            return true;
        }
        if (pack == null) throw new InvalidOperationException("Card grant rejected and no durable claim store exists");
        // Do not reserialize a rejected/throwing obtain hook over the original model/upgrade roll.
        CleanUncommitted(card, owner);
        MainFile.Logger.Warn($"[CardReward] phase=grant-deferred card={card.Id} {Debugging.CardRewardTrace.Context(owner)}");
        if (LocalContext.IsMe(owner)) UI.NRequiemToast.Show(UI.RequiemUi.TipText("GRANT_DEFERRED.title"));
        return false;
    }

    /// <returns>True only when the saved replacement obligation has actually completed.</returns>
    internal static async Task<bool> CompleteReplacement(CardModel card)
    {
        var key = Replacement[card];
        if (string.IsNullOrEmpty(key)) return true;
        if (!card.Owner.Deck.Cards.Contains(card)) return false;
        var old = InventoryIdentity.FindCard(card.Owner, key);
        if (old == null)
        {
            // Unloading a starter while its grant is deferred is not a free second card. Keep the debt until that
            // exact saved source returns to the deck (or is explicitly lost). Never erase an unknown chest model.
            if (TravelPack.Of(card.Owner)?.StoredCards.Any(s => InventoryIdentity.CardKey(s) == key) == true) return false;
            Replacement[card] = "";
            return true;
        }
        if (old == card || !old.IsRemovable)
        {
            MainFile.Logger.Warn($"[CardReward] phase=replacement-pending new={card.Id} old={key}: target cannot be removed");
            return false;
        }
        try { await CardPileCmd.RemoveFromDeck(old, showPreview: false); }
        catch (Exception e) { MainFile.Logger.Error($"[CardReward] phase=replacement-partial new={card.Id} old={key}: {e}"); }
        if (card.Owner.Deck.Cards.Contains(old)) return false; // Saved obligation survives a failure before removal.
        if (card.Owner.RunState.ContainsCard(old)) card.Owner.RunState.RemoveCard(old);
        Replacement[card] = "";
        return true;
    }

    internal static async Task CompletePendingReplacements(MegaCrit.Sts2.Core.Entities.Players.Player owner)
    {
        // These are finalizers of existing saved debts, not permission to start a new acquisition while blocked.
        using var lease = await InventoryGate.For(owner).Enter(allowBlocked: true);
        foreach (var card in owner.Deck.Cards.ToList())
        {
            var complete = await CompleteReplacement(card);
            // A claim still in PendingCards is reconciled there before clearing its saved acquisition key.
            if (complete && IsDeferredClaim(card) && !HasOutstandingClaim(owner, ClaimKey(card)))
                MarkClaim(card, "");
        }
    }

    public static bool CanTake(CardModel card, out AbstractModel? preventer) =>
        Hook.ShouldAddToDeck(card.Owner.RunState, card, out preventer);

    /// <summary>
    /// Explicit relocation policy, NOT a global ShouldAddToDeck bypass. Restore the saved, previously owned card
    /// after every veto check, without a second CardsGained history entry, ModifyCardBeingAddedToDeck transformation
    /// or AfterCardChangedPiles obtain bonus. UI pile events still fire. Caller observes the deck before removing
    /// its durable chest snapshot, including when an event throws after AddInternal.
    /// </summary>
    internal static async Task<CardPileAddResult> RestoreFromChest(CardModel card)
    {
        var owner = card.Owner;
        var result = new CardPileAddResult { cardAdded = card, success = false };
        if (owner == null || !card.IsMutable || card.HasBeenRemovedFromState || owner.Creature.IsDead
            || card.Pile != null || !owner.RunState.ContainsCard(card)) return result;
        if (!CanTake(card, out var preventer))
        {
            if (preventer != null) await preventer.AfterAddToDeckPrevented(card);
            return result;
        }
        owner.Deck.AddInternal(card);
        owner.Deck.InvokeCardAddFinished();
        result.success = owner.Deck.Cards.Contains(card);
        Debugging.CardRewardTrace.Card("chest:restore", card, $"success={result.success} relocation=true obtainHooks=false");
        return result;
    }

    /// <summary>Retain the original model/upgrade roll on veto, and recognize a commit even if a later hook failed.</summary>
    public static async Task<CardPileAddResult> AddSafely(CardModel card, PileType pile,
        CardPilePosition position = CardPilePosition.Bottom, AbstractModel? clonedBy = null, bool skipVisuals = false)
    {
        var owner = card.Owner;
        BeginAdd(card);
        try
        {
            var result = await CardPileCmd.Add(card, pile, position, clonedBy, skipVisuals);
            if (pile == PileType.Deck)
            {
                var actual = owner == null ? null : CommittedCard(card, owner);
                if (actual == null && owner != null && owner.Deck.Cards.Contains(result.cardAdded)) actual = result.cardAdded;
                if (result.success != (actual != null))
                {
                    MainFile.Logger.Error($"[CardReward] Add reported success={result.success}, observed commit={actual != null}: {card.Id}");
                    if (owner != null && actual != null) InventoryGate.Block(owner, $"Card Add reported a veto after committing {actual.Id}; inspect the partial result");
                }
                // A false result AFTER insertion must not allow a second copy either. Transactional adapters use
                // the raw command + TransferProtocol so this inconsistency is still classified as PartialCommit.
                result.success = actual != null;
                result.cardAdded = actual ?? card;
            }
            return result;
        }
        catch (Exception e)
        {
            var actual = pile == PileType.Deck && owner != null ? CommittedCard(card, owner) : null;
            MainFile.Logger.Error($"[CardReward] Add exception committed={actual != null} card={card.Id}: {e}");
            if (pile != PileType.Deck) throw;
            if (actual != null && owner != null) InventoryGate.Block(owner, $"Card Add committed before a later hook failed: {card.Id}: {e}");
            return new CardPileAddResult { success = actual != null, cardAdded = actual ?? card };
        }
    }

    public static Task<CardPileAddResult> AddRewardSafely(CardModel card, PileType pile, CardPilePosition position,
        AbstractModel? clonedBy, bool skipVisuals, CardReward reward) =>
        DeferredRewardScope.TryGet(reward, out var pending)
            ? pending.Add(card, pile, position, clonedBy, skipVisuals)
            : RewardPurchase.Add(card, pile, position, clonedBy, skipVisuals, reward);

    public static bool CheckRewardAdd(bool success, ref bool rewardComplete, ref bool endSelection)
    {
        AcquisitionPolicy.AfterAdd(success, ref rewardComplete, ref endSelection);
        return success;
    }
}

/// <summary>Bind the original request to the final obtain-modified model, including a later Add exception.</summary>
[CriticalPatch]
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyCardBeingAddedToDeck))]
public static class CardObtainTargetPatch
{
    [HarmonyPrefix]
    public static void Prefix(CardModel card, out CardModel __state) => __state = card;
    [HarmonyPostfix]
    public static void Postfix(CardModel __state, CardModel __result) => CardAcquisition.ObserveAddTarget(__state, __result);
}

/// <summary>
/// Narrow, fail-closed patch of the game's async choice loop. Keep alternatives, skips, hooks, history, visuals,
/// multiplayer choice IDs and rerolls in the original implementation. Only a failed Add resets completion flags.
/// Exact match count and state-machine field discovery are checked at startup, not silently assumed.
/// </summary>
[CriticalPatch]
[HarmonyPatch]
public static class CardRewardCommitPatch
{
    private static Type StateMachine => AccessTools.Method(typeof(CardReward), "OnSelect")
        .GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
        ?? throw new MissingMemberException("CardReward.OnSelect async state machine missing");

    [HarmonyTargetMethod]
    public static MethodBase TargetMethod() => AccessTools.Method(StateMachine, "MoveNext")
        ?? throw new MissingMethodException("CardReward.OnSelect.MoveNext");

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var fields = StateMachine.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo Flag(string name) => fields.Single(f => f.FieldType == typeof(bool) && f.Name.Contains($"<{name}>"));
        var complete = Flag("rewardComplete");
        var end = Flag("endSelection");
        var rewardThis = fields.Single(f => f.FieldType == typeof(CardReward));
        var success = AccessTools.Field(typeof(CardPileAddResult), nameof(CardPileAddResult.success));
        var add = AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.Add),
            [typeof(CardModel), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool)]);
        var safe = AccessTools.Method(typeof(CardAcquisition), nameof(CardAcquisition.AddRewardSafely));
        var check = AccessTools.Method(typeof(CardAcquisition), nameof(CardAcquisition.CheckRewardAdd));
        var remove = AccessTools.Method(typeof(List<CardCreationResult>), nameof(List<CardCreationResult>.RemoveAll));
        var removeSelected = AccessTools.Method(typeof(CardAcquisition), nameof(CardAcquisition.RemoveSelectedOptions));
        var code = instructions.ToList();
        if (code.Count(i => i.LoadsField(success)) != 1 || code.Count(i => i.Calls(add)) != 1 || code.Count(i => i.Calls(remove)) != 1)
            throw new InvalidOperationException("CardReward commit IL changed: expected exactly one Add, success check and option removal");
        var patched = new List<CodeInstruction>();
        foreach (var instruction in code)
        {
            if (instruction.Calls(add))
            {
                var loadThis = new CodeInstruction(OpCodes.Ldarg_0);
                loadThis.labels.AddRange(instruction.labels);
                instruction.labels.Clear();
                patched.Add(loadThis);
                patched.Add(new(OpCodes.Ldfld, rewardThis));
                instruction.operand = safe;
            }
            if (instruction.Calls(remove))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = removeSelected; // Remove the selected SOURCE too when obtain returned a clone.
            }
            patched.Add(instruction);
            if (!instruction.LoadsField(success)) continue;
            patched.Add(new(OpCodes.Ldarg_0));
            patched.Add(new(OpCodes.Ldflda, complete));
            patched.Add(new(OpCodes.Ldarg_0));
            patched.Add(new(OpCodes.Ldflda, end));
            patched.Add(new(OpCodes.Call, check));
        }
        return patched;
    }
}
