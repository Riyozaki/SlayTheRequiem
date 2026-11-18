using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Players;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Shared by custom shop/discard and camp/mule chest operations. Keyed by the actual player of this run, never a
/// canonical relic/NetId shared across runs. Native game callbacks are not silently serialized or exempted here;
/// their changes are revalidated by the adapter and included in peer digests. A nested chest needs an explicit lease.
/// </summary>
public static class InventoryGate
{
    private sealed class State
    {
        internal readonly OperationGate Gate = new();
        internal int Mutations;
        internal long FailureEpoch;
    }
    private static readonly ConditionalWeakTable<Player, State> Gates = new();
    public static OperationGate For(Player player)
    {
        var state = Gates.GetValue(player, _ => new State());
        if (Alchemy.TravelPack.Of(player) is { } pack)
        {
            if (state.Mutations == 0 && pack.OperationPending.Length > 0 && !pack.OperationBlocked)
            {
                // A NEW player object after Continue has no executing scope. Do not turn a save taken in the
                // middle of an obtain/payment callback into an apparently successful, repeatable transaction.
                pack.OperationBlocked = true;
                pack.OperationFailure = $"Interrupted operation: {pack.OperationPending}. Restore the safe checkpoint using the run badge.";
                MainFile.Logger.Error($"[InventoryOperation] Interrupted saved operation player={player.NetId}: {pack.OperationPending}");
            }
            if (pack.OperationBlocked) state.Gate.Blocked = true;
        }
        return state.Gate;
    }

    /// <summary>
    /// Write-ahead uncertainty marker, NOT an atomic disk transaction or an automatic payment repair. A scope
    /// owns an existing lease; nested scopes preserve the outer marker. Complete only after observing/finalizing
    /// the result. Reloading an intermediate save fails closed; a completed normal save has no pending marker.
    /// </summary>
    internal sealed class Mutation : IDisposable
    {
        private readonly Player _owner;
        private readonly State _state;
        private readonly Alchemy.TravelPack _pack;
        private readonly string _previous;
        private bool _complete, _disposed;
        internal Mutation(Player player, OperationGate.Lease held, string target)
        {
            if (!held.IsFor(For(player))) throw new InvalidOperationException("Mutation requires a live owned inventory lease");
            _owner = player;
            _state = Gates.GetValue(player, _ => new State());
            _pack = Alchemy.TravelPack.Of(player) ?? throw new InvalidOperationException("Missing durable operation marker store");
            _previous = _pack.OperationPending;
            if (_previous.Length == 0) OperationRecovery.Capture(player, target);
            _state.Mutations++;
            if (_previous.Length == 0) _pack.OperationPending = target;
        }
        internal void Complete() => _complete = true;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { if (!_complete) Block(_owner, $"Mutation scope exited without a known final result: {_pack.OperationPending}"); }
            finally { _pack.OperationPending = _previous; _state.Mutations--; }
        }
    }

    internal static Mutation Track(Player player, OperationGate.Lease held, string target) => new(player, held, target);
    internal static bool IsMutating(Player player) => Gates.TryGetValue(player, out var state) && state.Mutations > 0;

    // Only ShopOperations' matching immutable receipts may use this. Partial effects require checkpoint restore.
    internal static long FailureEpoch(Player player) => Gates.GetValue(player, _ => new State()).FailureEpoch;

    internal static void ClearReceiptBlock(Player player, long expectedEpoch)
    {
        var state = Gates.GetValue(player, _ => new State());
        if (state.FailureEpoch != expectedEpoch || IsMutating(player)) return;
        if (Alchemy.TravelPack.Of(player) is { } pack)
        {
            pack.OperationBlocked = false;
            pack.OperationFailure = "";
        }
        state.Gate.Blocked = false;
        MainFile.Logger.Info($"[InventoryOperation] Receipt-only timeout reconciled player={player.NetId}; no effect replayed");
    }

    internal static void Block(Player player, string reason)
    {
        var gate = For(player);
        var first = !gate.Blocked;
        Gates.GetValue(player, _ => new State()).FailureEpoch++;
        gate.Blocked = true;
        if (Alchemy.TravelPack.Of(player) is { } pack)
        {
            pack.OperationBlocked = true;
            pack.OperationFailure = reason.Length > 4096 ? reason[..4096] : reason;
        }
        MainFile.Logger.Error($"[InventoryOperation] BLOCKED player={player.NetId}: {reason}");
        if (first && MegaCrit.Sts2.Core.Context.LocalContext.IsMe(player))
        {
            try { UI.NRequiemToast.Show(UI.RequiemUi.TipText("OPERATION_BLOCKED.title")); }
            catch (Exception e) { MainFile.Logger.Warn($"[InventoryOperation] Block notification unavailable: {e.Message}"); }
        }
    }
}
