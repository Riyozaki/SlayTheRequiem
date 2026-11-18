using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SlayTheRequiem.SlayTheRequiemCode.Core;

// Deliberately a collection/cache MICROBENCHMARK, not Godot/frame/FPS evidence. Baseline mirrors
// NEnemyTraitChips at 65af8b3: collect + rank-sort + Aggregate signature on EVERY visible frame.
// The candidate uses the ACTUAL production InvalidatedView<T> with one cached builder delegate.
// No game assembly, Godot node, rendered canvas, equipment hook or real DisplayAmount event is executed.
const int Enemies = 5, Powers = 24, Frames = 12000, Repetitions = 7;
var cases = new[] { new Workload("idle", 0, 0), new Workload("one-change-per-60-frames", 60, 0),
    new Workload("rebind-every-120-frames", 0, 120), new Workload("change-every-frame", 1, 0) };
var rows = new List<object>();
var raw = new List<object>();
foreach (var workload in cases)
{
    Run(workload, optimized: false, frames: 2000); // warm BOTH implementations/JIT before any recorded trial
    Run(workload, optimized: true, frames: 2000);
    var baseline = new List<Sample>(); var candidate = new List<Sample>();
    for (var trial = 0; trial < Repetitions; trial++)
    {
        Sample before, after;
        if (trial % 2 == 0) { before = Run(workload, false, Frames); after = Run(workload, true, Frames); }
        else { after = Run(workload, true, Frames); before = Run(workload, false, Frames); }
        if (before.Checksum != after.Checksum) throw new InvalidOperationException("The views disagree: " + workload.Name);
        if (before.Builds != Enemies * Frames) throw new InvalidOperationException("Baseline must rebuild each frame");
        var expected = workload.MutationPeriod == 1 ? Enemies + Frames - 1
            : workload.MutationPeriod > 0 ? Enemies + (Frames - 1) / workload.MutationPeriod
            : workload.RebindPeriod > 0 ? Enemies * (1 + (Frames - 1) / workload.RebindPeriod) : Enemies;
        if (after.Builds != expected) throw new InvalidOperationException($"Candidate invalidation count {after.Builds} != {expected}");
        baseline.Add(before); candidate.Add(after);
        raw.Add(new { workload = workload.Name, trial, first = trial % 2 == 0 ? "baseline" : "candidate", baseline = before, candidate = after });
    }
    rows.Add(new { workload = workload.Name, baseline = Summary(baseline), candidate = Summary(candidate),
        final_view_checksums_equal = true });
}
var source = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "working-tree";
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
var summary = new
{
    schema = 1, producer = "actual-csharp-cache-microbenchmark", source_commit = source,
    scope = "not-engine-not-FPS-not-native-event-validation", baseline_source_commit = "65af8b30bb19ed86fd7e3a1ae45d319df45958d6",
    cache_sha256 = Hash("mod/SlayTheRequiemCode/Core/InvalidatedView.cs"),
    benchmark_sha256 = Hash("tools/perf/Views/Program.cs"),
    runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
    architecture = RuntimeInformation.ProcessArchitecture.ToString(), logical_processors = Environment.ProcessorCount,
    enemies = Enemies, powers_per_enemy = Powers, frames_per_trial = Frames, trials = Repetitions,
    unit = "whole-workload milliseconds; current-thread allocated bytes; actual GC counts",
    cases = rows,
};
var directory = args.Contains("--out") ? args[Array.IndexOf(args, "--out") + 1] : ".arena-ci/perf-views";
Directory.CreateDirectory(directory);
var options = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(Path.Combine(directory, "summary.json"), JsonSerializer.Serialize(summary, options) + "\n");
File.WriteAllText(Path.Combine(directory, "trials.json"), JsonSerializer.Serialize(raw, options) + "\n");
Console.WriteLine(JsonSerializer.Serialize(summary, options));
if (args.Contains("--annotate"))
{
    // A small, complete result survives even when artifact downloads are unavailable. Full trials are an artifact.
    var notice = JsonSerializer.Serialize(summary);
    if (notice.Length > 2800) throw new InvalidOperationException("Microbenchmark notice exceeds its bounded transport");
    Console.WriteLine("::notice title=STR_VIEW_BENCH_V1::" + notice.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A"));
}

static object Summary(List<Sample> samples) => new
{
    median_ms = Math.Round(samples.OrderBy(s => s.Milliseconds).ElementAt(samples.Count / 2).Milliseconds, 3),
    allocated_bytes = samples.OrderBy(s => s.AllocatedBytes).ElementAt(samples.Count / 2).AllocatedBytes,
    builds = samples[0].Builds,
    gc_collections = samples.Sum(s => s.Gen0 + s.Gen1 + s.Gen2),
};

static Sample Run(Workload work, bool optimized, int frames)
{
    var actors = Enumerable.Range(0, Enemies).Select(_ => new Actor(Powers)).ToArray();
    var builds = 0;
    foreach (var actor in actors)
        actor.Build = () =>
        {
            builds++;
            var items = new List<Power>();
            foreach (var power in actor.Powers) if (power.Trait && power.Amount != 0) items.Add(power);
            items.Sort(static (a, b) => { var rank = a.Rank.CompareTo(b.Rank); return rank != 0 ? rank : a.Key.CompareTo(b.Key); });
            return items.ToArray();
        };
    // Setup and forced cleanup are OUTSIDE measurement. Real in-game GC is not inferred from these trials.
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    var bytes = GC.GetAllocatedBytesForCurrentThread();
    var g0 = GC.CollectionCount(0); var g1 = GC.CollectionCount(1); var g2 = GC.CollectionCount(2);
    var start = Stopwatch.GetTimestamp();
    var checksum = 0;
    for (var frame = 0; frame < frames; frame++)
    {
        if (frame > 0 && work.MutationPeriod > 0 && frame % work.MutationPeriod == 0)
        {
            var actor = actors[(frame / work.MutationPeriod) % actors.Length];
            var index = (frame / work.MutationPeriod) % (Powers / 2);
            actor.Powers[index].Amount = actor.Powers[index].Amount == 0 ? 3 : 0;
            actor.Cache.Invalidate();
        }
        if (frame > 0 && work.RebindPeriod > 0 && frame % work.RebindPeriod == 0)
            foreach (var actor in actors) actor.Cache.Reset([]);
        foreach (var actor in actors)
        {
            if (optimized) actor.Cache.Refresh(actor.Build);
            else
            {
                builds++;
                actor.Scratch.Clear();
                foreach (var power in actor.Powers) if (power.Trait && power.Amount != 0) actor.Scratch.Add(power);
                actor.Scratch.Sort(static (a, b) => a.Rank.CompareTo(b.Rank));
                actor.Signature = actor.Scratch.Aggregate(17, static (h, p) => unchecked(h * 31 + p.Key * 7 + p.Amount));
            }
            // Every frame consumes a view, as _Draw would. No enumeration of the whole power model collection here.
            // Consume only a deterministic, order-independent count so baseline equal-rank ordering is irrelevant.
            checksum = unchecked(checksum * 31 + (optimized ? actor.Cache.Value.Length : actor.Scratch.Count));
        }
    }
    var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    return new(elapsed, GC.GetAllocatedBytesForCurrentThread() - bytes, GC.CollectionCount(0) - g0,
        GC.CollectionCount(1) - g1, GC.CollectionCount(2) - g2, builds, checksum);
}

sealed record Workload(string Name, int MutationPeriod, int RebindPeriod);
sealed record Sample(double Milliseconds, long AllocatedBytes, int Gen0, int Gen1, int Gen2, int Builds, int Checksum);
sealed class Power(int key) { internal readonly int Key = key, Rank = key % 5; internal readonly bool Trait = key < 12; internal int Amount = 3; }
sealed class Actor(int count)
{
    internal readonly Power[] Powers = Enumerable.Range(0, count).Select(i => new Power(i)).ToArray();
    internal readonly List<Power> Scratch = new();
    internal readonly InvalidatedView<Power[]> Cache = new([]);
    internal Func<Power[]> Build = null!;
    internal int Signature;
}
