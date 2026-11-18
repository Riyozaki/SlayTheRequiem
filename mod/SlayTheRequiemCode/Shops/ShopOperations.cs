using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

public sealed record ShopRequest(string Run, string Id, int Revision, string Before, ShopAction Kind, int A, int B, string S);

/// <summary>
/// Owner-authenticated requests, saved replay high-water mark and peer receipts. Retains the game's synchronized
/// selectors instead of introducing a second host-only choice protocol. Shared with the camp/mule inventory gate.
/// A disagreement/missing acknowledgement stops further custom mutations; it is never called synchronized success.
/// Reload is only a recovery from the HOST's save, not proof that an unresolved partial effect was repaired.
/// </summary>
public static class ShopOperations
{
    public const int Protocol = 2;
    private sealed class State
    {
        public bool LocalBusy;
        public bool Executing;
        public bool ReceiptTimeoutOnly;
        public bool RetryingReceipt;
        public long ReceiptFailureEpoch;
        public ShopRequest? PendingConfirmation;
        public RunLocation ConfirmationLocation;
        public readonly OperationJournal<ShopRequest, Receipt> Local = new();
        public readonly Dictionary<string, Dictionary<ulong, Receipt>> Peers = new(StringComparer.Ordinal);
    }
    private sealed record Receipt(OperationResult Result, string Digest);
    private static readonly ConditionalWeakTable<Player, State> States = new();
    private static State For(Player p) => States.GetValue(p, _ => new State());
    public static bool IsExecuting(Player player) => For(player).Executing;
    public static bool IsBlocked(Player player) => InventoryGate.For(player).Blocked;
    private static bool IsCurrent(Player player) => ReferenceEquals(player.RunState, RequiemNet.State);
    public static string RunKey(Player player) => OperationIdentity.RunKey(player.RunState.Rng.StringSeed,
        TravelPack.Of(player)?.OperationRunId, player.NetId);
    private static string Id(Player player, int revision) => OperationIdentity.Id(RunKey(player), revision);

    public static string Digest(Player player, ShopAction kind)
    {
        InventoryIdentity.Ensure(player);
        var sb = new StringBuilder().Append(player.Gold).Append('|').Append(player.Creature.CurrentHp).Append('|').Append(player.Creature.MaxHp);
        // A vanilla purchase's async obtain/sync may still be finishing on peers. Its trust receipt concerns the
        // observed payment + ledger only; subsequent inventory actions use the full precondition below.
        if (kind == ShopAction.TrustSpent)
        {
            if (MerchantLedger.Of(player) is { } ledger)
                sb.Append('|').Append(JsonSerializer.Serialize(ledger.ToSerializable(), JsonSerializationUtility.GetTypeInfo<SerializableRelic>()));
            sb.Append('|').Append(TravelPack.Of(player)?.OperationRevision);
            return OperationIdentity.Hash(sb.ToString());
        }
        foreach (var c in player.Deck.Cards)
            sb.Append('|').Append(JsonSerializer.Serialize(c.ToSerializable(), JsonSerializationUtility.GetTypeInfo<SerializableCard>()));
        foreach (var r in player.Relics)
        {
            var snapshot = r.ToSerializable();
            // Durable uncertainty is LOCAL bookkeeping: peers can be at different await points of the SAME
            // operation. Compare inventory + revision + persisted Block, never the lifetime of their async scope.
            // Edit only this freshly serialized list, not the live pack/saved chest contents.
            if (r is TravelPack && snapshot.Props?.strings is { } strings)
            {
                strings.RemoveAll(p => p.name is nameof(TravelPack.OperationPending) or nameof(TravelPack.OperationFailure));
                if (strings.Count == 0) snapshot.Props.strings = null; // match native omission, not a new empty array
            }
            if (r is TravelPack && snapshot.Props?.bools is { } flags)
            {
                flags.RemoveAll(p => p.name == nameof(TravelPack.OperationBlocked));
                if (flags.Count == 0) snapshot.Props.bools = null;
            }
            sb.Append('|').Append(JsonSerializer.Serialize(snapshot, JsonSerializationUtility.GetTypeInfo<SerializableRelic>()));
        }
        for (var slot = 0; slot < player.PotionSlots.Count; slot++)
            if (player.PotionSlots[slot] is { } p)
                sb.Append('|').Append(JsonSerializer.Serialize(p.ToSerializable(slot), JsonSerializationUtility.GetTypeInfo<SerializablePotion>()));
        return OperationIdentity.Hash(sb.ToString());
    }

    public static async Task<OperationResult> Perform(Player player, ShopAction kind, int a, int b, string s)
    {
        var state = For(player);
        if (!LocalContext.IsMe(player) || !IsCurrent(player)) return OperationResult.Reject("", "Only the current run's owner can initiate an operation");
        if (state.LocalBusy || IsBlocked(player)) return Notify(player, new(OperationStatus.Busy, "", "Operation in progress or synchronization blocked"));
        if (TravelPack.Of(player) is not { } pack) return OperationResult.Reject("", "Missing operation journal");
        if (string.IsNullOrEmpty(RunKey(player)))
            return Notify(player, OperationResult.Reject("", "No shared run identity; host settings snapshot required"));
        state.LocalBusy = true; // Set BEFORE awaiting the common gate: rapid double input cannot enqueue a second request.
        var issued = false;
        var operation = "";
        try
        {
            // UI commands must not queue behind an open camp selector (the user may need to close it first).
            // Remote delivery still awaits the gate in Apply; conflicting local input is rejected immediately.
            using var lease = InventoryGate.For(player).TryEnter();
            if (lease == null) return Notify(player, new(OperationStatus.Busy, "", "Inventory operation in progress or synchronization blocked"));
            if (!IsCurrent(player)) return OperationResult.Reject("", "Run changed before execution");
            var revision = pack.OperationRevision;
            var location = RequiemNet.CurrentLocation; // Receipts/retries remain addressed to the originating location.
            var request = new ShopRequest(RunKey(player), Id(player, revision), revision, Digest(player, kind), kind, a, b, s);
            operation = request.Id;
            OperationRecovery.Capture(player, operation); // must succeed BEFORE dispatch can mutate another peer
            issued = true;
            RequiemNet.SendTargetedOrThrow(new ShopActionMessage { location = location, request = request });
            return Notify(player, await Apply(player, request, location, lease));
        }
        catch (Exception e)
        {
            if (issued) Block(player, $"Delivery/executor outcome unknown: {e}");
            else MainFile.Logger.Error($"[ShopOperation] Failed before dispatch: {e}");
            return Notify(player, new(issued ? OperationStatus.PartialCommit : OperationStatus.Error, operation, e.Message));
        }
        finally { state.LocalBusy = false; }
    }

    /// <summary>Real selected-operation adapter, also used by str_integrity without impersonating a live network owner.</summary>
    internal static async Task<OperationResult> ExecuteGuarded(Player player, ShopRequest request, ShopQuote quote, OperationGate.Lease held)
    {
        if (IsBlocked(player) || !ShopActionPolicy.TryQuote(player, request.Kind, request.A, request.B, request.S, out var current, out var error)
            || current != quote) return OperationResult.Reject(request.Id, "Selected operation/price became stale before execution");
        using var mutation = InventoryGate.Track(player, held, request.Id);
        var before = Digest(player, request.Kind);
        var gold = player.Gold;
        OperationResult result;
        try { result = await ShopActions.Execute(player, request, quote, held); }
        catch (Exception e)
        {
            bool changed;
            try { changed = before != Digest(player, request.Kind); }
            catch { changed = true; }
            result = new(changed ? OperationStatus.PartialCommit : OperationStatus.Error, request.Id, e.ToString());
        }
        var after = Digest(player, request.Kind);
        if (!result.Changed && before != after)
            result = result with { Status = OperationStatus.PartialCommit,
                Detail = result.Detail + " Uncommitted outcome contradicted by observed state changes; no automatic retry" };
        if (result.Status is OperationStatus.PartialCommit or OperationStatus.Stale) Block(player, $"{result.Status}: {result.Detail}");
        mutation.Complete();
        return result with { Gold = gold - player.Gold };
    }

    public static async Task<OperationResult> Apply(Player player, ShopRequest request, RunLocation location, OperationGate.Lease? held = null)
    {
        var state = For(player);
        if (!IsCurrent(player)) return OperationResult.Reject(request.Id, "Run changed; no mutation");
        if (TravelPack.Of(player) is not { } pack || request.Run != RunKey(player)
            || !OperationIdentity.TryRevision(request.Run, request.Id, out var revision) || revision != request.Revision
            || !OperationIdentity.IsDigest(request.Before) || request.S == null || request.S.Length > 4096)
            return OperationResult.Reject(request.Id, "Foreign run or malformed request; no mutation");

        if (state.Local.TryGet(request.Id, request, out var previous, out var conflict))
        {
            if (!conflict) { SendReceipt(player, request.Id, previous, location); return previous.Result; }
            Block(player, $"Completed operation ID reused with different operands: {request.Id}");
            var collision = new OperationResult(OperationStatus.Stale, request.Id, "Operation ID/operands conflict; no mutation");
            SendReceipt(player, request.Id, new(collision, previous.Digest), location);
            return collision;
        }
        // A locally completed duplicate must resend its immutable receipt even while the original Apply is
        // holding the gate to await peer agreement. Waiting for that same gate would defeat receipt retries.
        using var lease = await InventoryGate.For(player).Enter(held, allowBlocked: true);
        if (!IsCurrent(player)) return OperationResult.Reject(request.Id, "Run changed while awaiting inventory lease; no mutation");
        // Another delivery may have completed while this one awaited the lease.
        if (state.Local.TryGet(request.Id, request, out previous, out conflict))
        {
            if (!conflict) { SendReceipt(player, request.Id, previous, location); return previous.Result; }
            Block(player, $"Queued operation ID reused with different operands: {request.Id}");
            return Refuse(player, request, OperationStatus.Stale, "Operation ID/operands conflict; no mutation", location);
        }
        if (IsBlocked(player)) return Refuse(player, request, OperationStatus.Busy, "Synchronization blocked; restore the host save and inspect the partial result", location);
        if (OperationGate.Validate(pack.OperationRevision, revision, request.Id, Id(player, revision)) is { } invalid)
            return Refuse(player, request, invalid, "Revision mismatch (or expired receipt); no mutation", location);
        if (pack.OperationRevision == int.MaxValue)
            return Refuse(player, request, OperationStatus.Stale, "Operation journal exhausted; no mutation", location);

        // Let already-received vanilla reward-sync callbacks finish. No OTHER custom mutation can overtake this lease.
        var timer = Stopwatch.StartNew();
        while (IsCurrent(player) && request.Before != Digest(player, request.Kind) && !LocalContext.IsMe(player)
            && timer.Elapsed < TimeSpan.FromSeconds(3)) await Task.Delay(40);
        if (!IsCurrent(player)) return OperationResult.Reject(request.Id, "Run changed while waiting for precondition");
        var ready = request.Before == Digest(player, request.Kind);
        // Cover execution AND peer acknowledgement on every participant, not just ShopActions' effect. A host
        // save taken after its local result but before receipts must remain visibly unconfirmed after Continue.
        using var mutation = InventoryGate.Track(player, lease!, request.Id);
        pack.OperationRevision++; // consume even a rejected/cancelled request; the persisted high-water is authoritative
        var before = Digest(player, request.Kind);
        var beforeGold = player.Gold;
        OperationResult result;
        try
        {
            state.Executing = true;
            if (!ready) result = new(OperationStatus.Stale, request.Id, "Inventory precondition mismatch");
            else if (!ShopActionPolicy.TryQuote(player, request.Kind, request.A, request.B, request.S, out var quote, out var error))
                result = OperationResult.Reject(request.Id, error);
            else result = await ExecuteGuarded(player, request, quote, lease!);
        }
        catch (Exception e)
        {
            bool changed;
            try { changed = before != Digest(player, request.Kind); }
            catch { changed = true; } // Unknown is never reported as an uncommitted error.
            result = new(changed ? OperationStatus.PartialCommit : OperationStatus.Error, request.Id, e.ToString(), beforeGold - player.Gold);
        }
        finally { state.Executing = false; }
        // Include every observed delta (e.g. a DeathEcho surcharge), including effects that throw after payment.
        result = result with { Gold = beforeGold - player.Gold };
        Receipt receipt;
        try
        {
            var after = Digest(player, request.Kind);
            if (!result.Changed && after != before)
                result = result with { Status = OperationStatus.PartialCommit,
                    Detail = result.Detail + " Uncommitted outcome contradicted by observed state changes; no automatic retry" };
            if (result.Status is OperationStatus.PartialCommit or OperationStatus.Stale) Block(player, $"{result.Status}: {result.Detail}");
            receipt = new(result, Digest(player, request.Kind));
        }
        catch (Exception e)
        {
            Block(player, $"Post-state unobservable op={request.Id}: {e}");
            // A deliberately invalid digest cannot be mistaken for peer agreement.
            return new(OperationStatus.PartialCommit, request.Id, "Operation finished but its post-state could not be observed", beforeGold - player.Gold);
        }
        var expired = state.Local.Add(request.Id, request, receipt);
        if (expired != null) state.Peers.Remove(expired);
        MainFile.Logger.Info($"[ShopOperation] id={request.Id} kind={request.Kind} revision={revision} status={result.Status} gold={result.Gold} digest={receipt.Digest} {result.Detail}");
        CheckPeers(player, request.Id);
        SendReceipt(player, request.Id, receipt, location);
        state.PendingConfirmation = request;
        state.ConfirmationLocation = location;
        if (RequiemNet.IsMultiplayer && !await ConfirmPeers(player, request, location))
            result = new(OperationStatus.PartialCommit, request.Id, "Local result exists, multiplayer agreement NOT confirmed; retry receipts or restore checkpoint from run badge", result.Gold);
        else state.PendingConfirmation = null;
        mutation.Complete(); // uncertainty is now represented by the persisted Block, or all peer receipts matched
        mutation.Dispose(); // dispose BEFORE reconciliation: the last receipt may have arrived during the scope
        CheckPeers(player, request.Id); // no new packet is necessary to reconcile an already complete receipt set
        Debugging.BalanceTrace.Snapshot("shop:result", player, $"{request.Kind}:{result.Status}:gold_delta={result.Gold}");
        return result;
    }

    private static async Task<bool> ConfirmPeers(Player player, ShopRequest request, RunLocation location)
    {
        var state = For(player);
        var peers = player.RunState.Players.Select(p => p.NetId).Where(id => id != RequiemNet.LocalNetId).ToHashSet();
        bool Complete() => peers.Count == 0 || state.Peers.TryGetValue(request.Id, out var receipts) && peers.All(receipts.ContainsKey);
        var timer = Stopwatch.StartNew();
        var retryAt = TimeSpan.FromSeconds(2);
        while (IsCurrent(player) && !IsBlocked(player) && !Complete() && timer.Elapsed < TimeSpan.FromSeconds(8))
        {
            if (LocalContext.IsMe(player) && timer.Elapsed >= retryAt)
            {
                // The owner only re-sends its original immutable request. No new local effect/revision.
                if (state.Local.TryGetReceipt(request.Id, out var local)) SendReceipt(player, request.Id, local, location);
                RequiemNet.SendTargetedOrThrow(new ShopActionMessage { location = location, request = request });
                retryAt += TimeSpan.FromSeconds(2);
            }
            await Task.Delay(40);
        }
        if (IsCurrent(player) && !IsBlocked(player) && Complete()) return true;
        var onlyTimeout = IsCurrent(player) && !IsBlocked(player)
            && state.Local.TryGetReceipt(request.Id, out var localResult)
            && localResult.Result.Status is not (OperationStatus.PartialCommit or OperationStatus.Stale or OperationStatus.Busy);
        Block(player, $"Missing or mismatched receipt op={request.Id}. Retry receipts; otherwise restore the host checkpoint using the run badge.");
        state.ReceiptTimeoutOnly = onlyTimeout;
        state.ReceiptFailureEpoch = InventoryGate.FailureEpoch(player);
        return false;
    }

    private static OperationResult Refuse(Player player, ShopRequest request, OperationStatus status, string detail, RunLocation location)
    {
        var result = new OperationResult(status, request.Id, detail);
        SendReceipt(player, request.Id, new(result, Digest(player, request.Kind)), location);
        return result;
    }

    private static void SendReceipt(Player player, string id, Receipt receipt, RunLocation location)
    {
        try
        {
            RequiemNet.SendTargetedOrThrow(new ShopActionReceiptMessage
            {
                location = location, playerId = player.NetId, operationId = id,
                status = (int)receipt.Result.Status, gold = receipt.Result.Gold, digest = receipt.Digest,
            });
        }
        catch (Exception e) { Block(player, $"Receipt send failed: {e.Message}"); }
    }

    public static void OnReceipt(Player player, string operationId, ulong sender, int status, int gold, string digest)
    {
        if (!IsCurrent(player) || sender == RequiemNet.LocalNetId || !player.RunState.Players.Any(p => p.NetId == sender)
            || !Enum.IsDefined(typeof(OperationStatus), status) || !OperationIdentity.IsDigest(digest)
            || !OperationIdentity.TryRevision(RunKey(player), operationId, out var revision)
            || TravelPack.Of(player) is not { } pack || revision > pack.OperationRevision || revision < Math.Max(0, pack.OperationRevision - 256)) return;
        var state = For(player);
        if (!state.Peers.TryGetValue(operationId, out var receipts))
        {
            if (state.Peers.Count >= 256) { Block(player, "Too many unmatched operation receipts"); return; }
            state.Peers[operationId] = receipts = new();
        }
        var receipt = new Receipt(new((OperationStatus)status, operationId, Gold: gold), digest);
        if (receipts.TryGetValue(sender, out var prior) && prior != receipt)
        { Block(player, $"Peer changed a completed receipt op={operationId} sender={sender}"); return; }
        receipts[sender] = receipt;
        CheckPeers(player, operationId);
    }

    private static void CheckPeers(Player player, string id)
    {
        var state = For(player);
        if (!state.Local.TryGetReceipt(id, out var local) || !state.Peers.TryGetValue(id, out var peers)) return;
        foreach (var (sender, remote) in peers)
            if (remote.Digest != local.Digest || remote.Result.Status != local.Result.Status || remote.Result.Gold != local.Result.Gold)
            {
                state.ReceiptTimeoutOnly = false; // a disagreement is NEVER repaired by clearing a flag
                Block(player, $"Receipt mismatch op={id} peer={sender} local={local.Result.Status}/{local.Digest} remote={remote.Result.Status}/{remote.Digest}");
                return;
            }
        if (!state.ReceiptTimeoutOnly || state.PendingConfirmation is not { } request || request.Id != id
            || InventoryGate.IsMutating(player) || state.ReceiptFailureEpoch != InventoryGate.FailureEpoch(player)
            || TravelPack.Of(player) is not { } pack) return;
        var expected = player.RunState.Players.Select(p => p.NetId).Where(p => p != RequiemNet.LocalNetId).ToList();
        if (!expected.All(peers.ContainsKey)) return;
        var proof = new OperationReceiptProof(local.Result.Status, local.Result.Gold, local.Digest);
        if (!ReceiptRecoveryPolicy.CanResume(state.ReceiptTimeoutOnly, request.Revision, pack.OperationRevision,
            Digest(player, request.Kind), proof, expected.Count,
            expected.Select(p => new OperationReceiptProof(peers[p].Result.Status, peers[p].Result.Gold, peers[p].Digest)).ToList())) return;
        state.ReceiptTimeoutOnly = false;
        state.PendingConfirmation = null;
        InventoryGate.ClearReceiptBlock(player, state.ReceiptFailureEpoch); // no Add/payment/service is repeated
    }

    public static async Task RetryReceipts(Player player)
    {
        var state = For(player);
        if (!LocalContext.IsMe(player) || !IsCurrent(player) || state.RetryingReceipt || !state.ReceiptTimeoutOnly
            || state.PendingConfirmation is not { } request) return;
        state.RetryingReceipt = true;
        try
        {
            if (state.Local.TryGetReceipt(request.Id, out var receipt)) SendReceipt(player, request.Id, receipt, state.ConfirmationLocation);
            RequiemNet.SendTargetedOrThrow(new ShopActionMessage { location = state.ConfirmationLocation, request = request });
            // Receivers resend immutable receipts before checking the block/gate. Bound the UI wait; never replay effects.
            var timer = Stopwatch.StartNew();
            while (IsCurrent(player) && state.ReceiptTimeoutOnly && timer.Elapsed < TimeSpan.FromSeconds(8)) await Task.Delay(40);
        }
        catch (Exception e) { MainFile.Logger.Error($"[ShopOperation] Receipt retry failed: {e}"); }
        finally { state.RetryingReceipt = false; }
    }

    private static OperationResult Notify(Player player, OperationResult result)
    {
        if (LocalContext.IsMe(player) && result.Status is not OperationStatus.Committed)
            UI.NRequiemToast.Show(UI.RequiemUi.TipText("OPERATION_RESULT.title", ("Status", UI.RequiemUi.TipText($"OP_STATUS_{result.Status.ToString().ToUpperInvariant()}.title"))));
        return result;
    }

    private static void Block(Player player, string reason)
    {
        MainFile.Logger.Error($"[ShopOperation] BLOCKED player={player.NetId}: {reason}");
        InventoryGate.Block(player, reason);
    }
}
