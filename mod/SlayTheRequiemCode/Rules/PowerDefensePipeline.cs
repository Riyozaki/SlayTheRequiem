using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>One actual Apply/ModifyAmount command owns its status-defense receipt. A veto before notification,
/// exception, nested application or later reuse of the same stacking power cannot strand/flush another attempt.</summary>
public static class PowerDefensePipeline
{
    internal sealed record Attempt(PowerModel Power, Creature Target, Creature? Dealer, CardModel? Card, bool HostileSpell);
    public sealed class Frame
    {
        internal Frame? Parent;
        internal Attempt? Attempt;
        internal DeferredEffect? Effect;
    }
    private static readonly AsyncLocal<Frame?> Current = new();
    internal static Frame Enter()
    {
        var frame = new Frame { Parent = Current.Value };
        Current.Value = frame;
        return frame;
    }
    internal static void Restore(Frame frame) => Current.Value = frame.Parent;
    internal static void Cancel(Frame frame) => frame.Effect?.Cancel();
    internal static void Track(PowerModel power, Creature target, Creature? dealer, CardModel? card, bool spell)
    {
        if (Current.Value is { } frame) frame.Attempt = new(power, target, dealer, card, spell);
    }
    internal static Attempt? Take(PowerModel power)
    {
        if (Current.Value is not { Attempt: { } attempt } frame || !ReferenceEquals(attempt.Power, power)) return null;
        frame.Attempt = null;
        return attempt;
    }
    internal static void Defer(Func<Task> commit, Action cancel)
    {
        var frame = Current.Value ?? throw new InvalidOperationException("Status reservation outside native power command");
        if (frame.Effect != null) throw new InvalidOperationException("Duplicate status defense in one command");
        frame.Effect = new DeferredEffect(commit, cancel);
    }
    internal static async Task Commit()
    {
        var frame = Current.Value;
        if (frame?.Effect is not { } effect) return;
        frame.Effect = null; // detach before remove/reflect, which issue nested PowerCmd commands
        await effect.Commit();
    }
}

[CriticalPatch]
[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Apply), typeof(PlayerChoiceContext), typeof(PowerModel),
    typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool))]
public static class PowerApplyDefenseScopePatch
{
    [HarmonyPrefix]
    public static void Prefix(out PowerDefensePipeline.Frame __state) => __state = PowerDefensePipeline.Enter();
    [HarmonyPostfix]
    public static void Postfix(ref Task __result, PowerDefensePipeline.Frame __state)
    {
        PowerDefensePipeline.Restore(__state);
        __result = Finish(__result, __state);
    }
    private static async Task Finish(Task task, PowerDefensePipeline.Frame frame)
    {
        try { await task; }
        finally { PowerDefensePipeline.Cancel(frame); }
    }
    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, PowerDefensePipeline.Frame? __state)
    {
        if (__exception == null || __state == null) return;
        PowerDefensePipeline.Restore(__state); PowerDefensePipeline.Cancel(__state);
    }
}

[CriticalPatch]
[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.ModifyAmount), typeof(PlayerChoiceContext), typeof(PowerModel),
    typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool))]
public static class PowerModifyDefenseScopePatch
{
    [HarmonyPrefix]
    public static void Prefix(out PowerDefensePipeline.Frame __state) => __state = PowerDefensePipeline.Enter();
    [HarmonyPostfix]
    public static void Postfix(ref Task<int> __result, PowerDefensePipeline.Frame __state)
    {
        PowerDefensePipeline.Restore(__state);
        __result = Finish(__result, __state);
    }
    private static async Task<int> Finish(Task<int> task, PowerDefensePipeline.Frame frame)
    {
        try { return await task; }
        finally { PowerDefensePipeline.Cancel(frame); }
    }
    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, PowerDefensePipeline.Frame? __state)
    {
        if (__exception == null || __state == null) return;
        PowerDefensePipeline.Restore(__state); PowerDefensePipeline.Cancel(__state);
    }
}
