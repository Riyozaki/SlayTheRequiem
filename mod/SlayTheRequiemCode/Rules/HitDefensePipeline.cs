using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Fixed post-Block order, not power insertion order. A native damage command owns its reservations; a pure
/// HP-loss query outside that command never spends or reserves anything. Receipts detach before callbacks,
/// and abandoned reservations are cancelled on command failure. Native caps/Osty/special immunity stay native.
/// </summary>
public static class HitDefensePipeline
{
    public sealed class DamageFrame
    {
        internal DamageFrame? Parent;
        internal List<DeferredEffect>? Building;
        internal readonly Stack<IReadOnlyList<DeferredEffect>> Pending = new();
    }
    private static readonly AsyncLocal<DamageFrame?> Current = new();
    internal static bool WillCommit => Current.Value?.Building != null;

    internal static DamageFrame Enter()
    {
        var frame = new DamageFrame { Parent = Current.Value };
        Current.Value = frame;
        return frame;
    }
    internal static void Restore(DamageFrame frame) => Current.Value = frame.Parent;
    internal static void Cancel(DamageFrame frame)
    {
        while (frame.Pending.TryPop(out var effects)) foreach (var effect in effects) effect.Cancel();
    }

    internal static void Defer(Func<Task> commit, Action cancel) =>
        (Current.Value?.Building ?? throw new InvalidOperationException("Reservation outside an actual damage command"))
            .Add(new DeferredEffect(commit, cancel));

    internal static decimal Resolve(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? card)
    {
        if (amount < 1m) return amount; // native conversion is zero: no dodge/shield charge for a fractional no-hit
        var original = amount;
        var frame = Current.Value;
        var previous = frame?.Building;
        var receipts = new List<DeferredEffect>();
        if (frame != null) frame.Building = receipts;
        try
        {
            if (dealer?.GetPower<ConfusionPower>() is { } confusion) amount = confusion.MitigateHit(target, amount, props, dealer, card);
            if (target.GetPower<SpectralGuardPower>() is { } spectral) amount = spectral.MitigateHit(target, amount, props, dealer, card);
            if (target.GetPower<SpellShieldPower>() is { } shield) amount = shield.MitigateHit(target, amount, props, dealer, card);
            if (target.GetPower<DodgePower>() is { } dodge) amount = dodge.MitigateHit(target, amount, props, dealer, card);
            if (target.GetPower<WardPower>() is { } ward) amount = ward.MitigateHit(target, amount, props, dealer, card);
            foreach (var summon in target.Powers.OfType<SummonPower>().ToList()) amount = summon.MitigateHit(target, amount, props, dealer, card);
            if (frame != null && decimal.Truncate(amount) != decimal.Truncate(original)) frame.Pending.Push(receipts);
            else foreach (var effect in receipts) effect.Cancel();
            return Math.Max(0m, amount);
        }
        catch { foreach (var effect in receipts) effect.Cancel(); throw; }
        finally { if (frame != null) frame.Building = previous; }
    }

    internal static async Task Commit()
    {
        var frame = Current.Value;
        if (frame == null || !frame.Pending.TryPop(out var effects)) return;
        // Pop BEFORE the first await. Nested damage owns another frame and cannot flush its parent.
        try { foreach (var effect in effects) await effect.Commit(); }
        finally { foreach (var effect in effects) effect.Cancel(); }
    }
}

[CriticalPatch]
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
    typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel),
    typeof(CardPlay))]
public static class DamageDefenseScopePatch
{
    [HarmonyPrefix]
    public static void Prefix(out HitDefensePipeline.DamageFrame __state) => __state = HitDefensePipeline.Enter();

    [HarmonyPostfix]
    public static void Postfix(ref Task<IEnumerable<DamageResult>> __result, HitDefensePipeline.DamageFrame __state)
    {
        HitDefensePipeline.Restore(__state); // restore the caller, not the context captured by the native async body
        __result = Finish(__result, __state);
    }

    private static async Task<IEnumerable<DamageResult>> Finish(Task<IEnumerable<DamageResult>> original, HitDefensePipeline.DamageFrame frame)
    {
        try { return await original; }
        finally { HitDefensePipeline.Cancel(frame); }
    }

    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, HitDefensePipeline.DamageFrame? __state)
    {
        if (__exception == null || __state == null) return;
        HitDefensePipeline.Restore(__state);
        HitDefensePipeline.Cancel(__state);
    }
}
