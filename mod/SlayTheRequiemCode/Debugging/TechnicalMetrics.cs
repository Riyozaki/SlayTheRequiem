using System.Diagnostics;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Debugging;

/// <summary>Opt-in, bounded measurements for the final playtest. No frame logs, no fabricated baseline numbers.</summary>
public static class TechnicalMetrics
{
    public static bool Enabled { get; set; }
    private static readonly MetricWindow Frame = new(), RewardRefresh = new();
    public static void SampleFrame(double seconds) { if (Enabled) Frame.Add(seconds * 1000); }
    internal static void SampleRefresh(long started) { if (started != 0) RewardRefresh.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    public static void Reset() { Frame.Clear(); RewardRefresh.Clear(); }
    public static string Describe() => JsonSerializer.Serialize(new
    {
        enabled = Enabled, window = 256, unit = "milliseconds", frame = Frame.Snapshot(), reward_refresh = RewardRefresh.Snapshot(),
        note = "Opt-in samples on this machine, not an FPS guarantee or gameplay acceptance. Capture str_passport with the same run.",
    });
}

[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.RefreshOptions))]
public static class RewardRefreshTimingPatch
{
    [HarmonyPrefix]
    public static void Prefix(out long __state) => __state = TechnicalMetrics.Enabled ? Stopwatch.GetTimestamp() : 0;
    [HarmonyFinalizer]
    public static Exception? Finalizer(long __state, Exception? __exception)
    {
        TechnicalMetrics.SampleRefresh(__state);
        return __exception;
    }
}
