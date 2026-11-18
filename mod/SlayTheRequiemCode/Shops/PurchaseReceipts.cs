using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

/// <summary>
/// Trust cannot be minted with an arbitrary TrustSpent operand. The local purchase and the game's remote
/// GoldLost synchronization both pass through LoseGold; only an observed, unclaimed, actual delta is creditable.
/// Service payments execute inside ShopOperations and are credited there, not a second time here.
/// </summary>
public static class PurchaseReceipts
{
    private sealed record Receipt(string Id, int Amount, int Floor);
    private static readonly ConditionalWeakTable<Player, List<Receipt>> Pending = new();
    private sealed class Suppression { public int Depth; }
    private static readonly ConditionalWeakTable<Player, Suppression> Suppressed = new();
    private sealed class Scope(Suppression state) : IDisposable
    {
        private int _disposed;
        public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) state.Depth--; }
    }
    // A surcharge executed on every peer is credited directly and must never leave a second claimable receipt.
    internal static IDisposable Suppress(Player player)
    {
        var state = Suppressed.GetValue(player, _ => new Suppression());
        state.Depth++;
        return new Scope(state);
    }

    internal static void Observe(Player player, int before, GoldLossType lossType)
    {
        if (Suppressed.GetValue(player, _ => new Suppression()).Depth > 0 || ShopOperations.IsExecuting(player) || player.RunState.CurrentRoom is not MerchantRoom
            || !MerchantLedger.Active(player) || MerchantLedger.Of(player) is not { } ledger) return;
        // Native card removal (and DeathEcho's surcharge) executes Spent on ALL peers; ordinary shop
        // purchases execute Spent locally and arrive as Lost via RewardSynchronizer on remote peers.
        if (lossType is not (GoldLossType.Spent or GoldLossType.Lost)) return;
        var actual = before - player.Gold;
        if (actual <= 0) return;
        ledger.PurchaseReceiptSequence = checked(ledger.PurchaseReceiptSequence + 1);
        var list = Pending.GetOrCreateValue(player);
        list.RemoveAll(r => r.Floor != player.RunState.TotalFloor);
        list.Add(new($"purchase:{player.RunState.TotalFloor}:{ledger.PurchaseReceiptSequence}", actual, player.RunState.TotalFloor));
    }

    public static string? Find(Player player, int actual) => Pending.GetOrCreateValue(player)
        .FirstOrDefault(r => r.Amount == actual && r.Floor == player.RunState.TotalFloor)?.Id;

    public static bool Has(Player player, string id, int actual) => actual > 0 && Pending.GetOrCreateValue(player)
        .Any(r => r.Id == id && r.Amount == actual && r.Floor == player.RunState.TotalFloor);

    public static bool Consume(Player player, string id, int actual)
    {
        var list = Pending.GetOrCreateValue(player);
        var index = list.FindIndex(r => r.Id == id && r.Amount == actual && r.Floor == player.RunState.TotalFloor);
        if (index < 0) return false;
        list.RemoveAt(index);
        return true;
    }
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.LoseGold))]
public static class PurchaseReceiptPatch
{
    [HarmonyPrefix]
    public static void Prefix(Player player, out int __state) => __state = player.Gold;
    [HarmonyFinalizer]
    public static Exception? Finalizer(Player player, GoldLossType goldLossType, int __state, Exception? __exception)
    {
        // 0.107.1 LoseGold mutates synchronously before returning Task.CompletedTask. A later patch exception
        // must not erase an actually paid receipt, nor may this diagnostic replace the original failure.
        try { PurchaseReceipts.Observe(player, __state, goldLossType); }
        catch (Exception e) { MainFile.Logger.Error($"[ShopOperation] Payment receipt could not be recorded: {e}"); }
        return __exception;
    }
}
