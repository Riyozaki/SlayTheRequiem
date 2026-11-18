using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Debugging;

/// <summary>
/// Opt-in, bounded observations for the FINAL same-configuration comparison. No frame logging or disk I/O.
/// Describe/passport serialization itself allocates: turn OFF balance tracing, reset after warmup, do not poll
/// status while capturing, and compare equally instrumented builds. Native results are never filled by a sim.
/// </summary>
public static class TechnicalMetrics
{
    private const int Capacity = 1024;
    private static bool _enabled;
    public static bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (value) Reset();
            else { _stopped = Stopwatch.GetTimestamp(); _end = Memory(); }
        }
    }
    private static readonly MetricWindow Frame = new(Capacity), RewardRefresh = new(Capacity), TraitRebuild = new(Capacity);
    private static long _started, _stopped, _frames, _traitRebuilds, _traitRedraws;
    private static MemorySample? _baseline, _end;
    private sealed record MemorySample(long AllocatedBytes, long ManagedBytes, int Gen0, int Gen1, int Gen2,
        double Nodes, double OrphanNodes, double EngineStaticBytes);

    private static MemorySample Memory() => new(GC.GetTotalAllocatedBytes(precise: false), GC.GetTotalMemory(forceFullCollection: false),
        GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
        Godot.Performance.GetMonitor(Godot.Performance.Monitor.ObjectNodeCount),
        Godot.Performance.GetMonitor(Godot.Performance.Monitor.ObjectOrphanNodeCount),
        Godot.Performance.GetMonitor(Godot.Performance.Monitor.MemoryStatic));

    internal static long Start() => Enabled ? Stopwatch.GetTimestamp() : 0;
    public static void SampleFrame(double seconds) { if (Enabled) { Frame.Add(seconds * 1000); _frames++; } }
    internal static void SampleRefresh(long started) { if (started != 0) RewardRefresh.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    internal static void TraitRebuilt(long started)
    {
        if (started == 0) return;
        _traitRebuilds++;
        TraitRebuild.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }
    internal static void TraitRedraw() { if (Enabled) _traitRedraws++; }

    public static void Reset()
    {
        Frame.Clear(); RewardRefresh.Clear(); TraitRebuild.Clear();
        _frames = _traitRebuilds = _traitRedraws = 0;
        _started = Stopwatch.GetTimestamp(); _stopped = Enabled ? 0 : _started;
        _baseline = Memory(); _end = Enabled ? null : _baseline;
    }

    public static string Describe()
    {
        var after = _end ?? Memory();
        var before = _baseline;
        var elapsed = _started == 0 ? 0 : ((_stopped == 0 ? Stopwatch.GetTimestamp() : _stopped) - _started) / (double)Stopwatch.Frequency;
        return JsonSerializer.Serialize(new
        {
            schema = 2, producer = "native-game-opt-in", enabled = Enabled, window = Capacity, unit = "milliseconds",
            elapsed_seconds = elapsed, total_sampled_frames = _frames,
            frame = Frame.Snapshot(), reward_refresh = RewardRefresh.Snapshot(), trait_rebuild = TraitRebuild.Snapshot(),
            trait_rebuilds = _traitRebuilds, trait_redraw_requests = _traitRedraws,
            memory_before = before, memory_after = after,
            allocated_bytes_delta = before == null ? (long?)null : after.AllocatedBytes - before.AllocatedBytes,
            gc_delta = before == null ? null : new { gen0 = after.Gen0 - before.Gen0, gen1 = after.Gen1 - before.Gen1, gen2 = after.Gen2 - before.Gen2 },
            machine = new { os = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(), logical_processors = Environment.ProcessorCount,
                cpu = Godot.OS.GetProcessorName(), gpu = Godot.RenderingServer.GetVideoAdapterName() },
            passport = JsonSerializer.Deserialize<JsonElement>(RunDiagnostics.Describe(Net.RequiemNet.State, pretty: false)),
            note = "Rolling last 1024 frame/operation samples; allocation/GC/node deltas span reset to off/status and include instrumentation. "
                + "Warm both builds identically, disable str_trace, reset, then capture without status polling. Not an FPS guarantee, window-cycle result or gameplay acceptance.",
        });
    }
}

[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.RefreshOptions))]
public static class RewardRefreshTimingPatch
{
    [HarmonyPrefix]
    public static void Prefix(out long __state) => __state = TechnicalMetrics.Start();
    [HarmonyFinalizer]
    public static Exception? Finalizer(long __state, Exception? __exception)
    {
        TechnicalMetrics.SampleRefresh(__state);
        return __exception;
    }
}
