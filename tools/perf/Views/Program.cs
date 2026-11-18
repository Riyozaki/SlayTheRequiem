using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SlayTheRequiem.SlayTheRequiemCode.Core;

// Deliberately a collection/cache MICROBENCHMARK, not Godot/frame/FPS evidence. Baseline mirrors
// NEnemyTraitChips at 65af8b3: collect + rank-sort + Aggregate signature on EVERY visible frame.
// The candidate uses the ACTUAL production InvalidatedListView<T> with one cached builder delegate and two reusable lists.
// No game assembly, Godot node, rendered canvas, equipment hook or real DisplayAmount event is executed.
const int Enemies = 5, Powers = 24, Frames = 12000, Repetitions = 7;
var cases = new[] { new Workload("idle", 0, 0), new Workload("one-change-per-60-frames", 60, 0),
    new Workload("rebind-every-120-frames", 0, 120), new Workload("change-every-frame", 1, 0) };
var rows = new List<object>();
var raw = new List<object>();
foreach (var workload in cases)
{
    VerifyViews(workload, Frames); // exact synthetic membership/rank/amount EVERY frame, OUTSIDE all timed trials
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
        frame_count_checksums_equal = true, synthetic_views_equal_each_frame = true });
}
var source = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "working-tree";
string HashBytes(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
string Hash(string path) => HashBytes(File.ReadAllBytes(path));
var options = new JsonSerializerOptions { WriteIndented = true };
var trialsText = JsonSerializer.Serialize(raw, options) + "\n";
var trialsBytes = System.Text.Encoding.UTF8.GetBytes(trialsText);
var summary = new
{
    schema = 1, producer = "actual-csharp-cache-microbenchmark", source_commit = source,
    scope = "not-engine-not-FPS-not-native-event-validation", baseline_source_commit = "65af8b30bb19ed86fd7e3a1ae45d319df45958d6",
    baseline_is_collection_reimplementation = true, tiered_compilation_config = false,
    tiered_env_override = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? Environment.GetEnvironmentVariable("COMPlus_TieredCompilation"),
    cache_sha256 = Hash("mod/SlayTheRequiemCode/Core/InvalidatedView.cs"),
    list_cache_sha256 = Hash("mod/SlayTheRequiemCode/Core/InvalidatedListView.cs"),
    benchmark_sha256 = Hash("tools/perf/Views/Program.cs"), trials_sha256 = HashBytes(trialsBytes),
    runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
    architecture = RuntimeInformation.ProcessArchitecture.ToString(), logical_processors = Environment.ProcessorCount,
    enemies = Enemies, powers_per_enemy = Powers, frames_per_trial = Frames, trials = Repetitions,
    unit = "whole-workload milliseconds; current-thread allocated bytes; actual GC counts",
    cases = rows,
};
var directory = args.Contains("--out") ? args[Array.IndexOf(args, "--out") + 1] : ".arena-ci/perf-views";
Directory.CreateDirectory(directory);
File.WriteAllText(Path.Combine(directory, "summary.json"), JsonSerializer.Serialize(summary, options) + "\n");
File.WriteAllText(Path.Combine(directory, "trials.json"), trialsText);
Console.WriteLine(JsonSerializer.Serialize(summary, options));
if (args.Contains("--annotate"))
{
    // A small, complete result survives even when artifact downloads are unavailable. Full trials are an artifact.
    var notice = JsonSerializer.Serialize(summary);
    if (notice.Length > 2800) throw new InvalidOperationException("Microbenchmark notice exceeds its bounded transport");
    Console.WriteLine("::notice title=STR_VIEW_BENCH_V1::" + notice.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A"));
    // Preserve all 7 actual trials, not just medians, through the GitHub API even without artifact-host access.
    using var compressed = new MemoryStream();
    using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true)) gzip.Write(trialsBytes);
    var data = Convert.ToBase64String(compressed.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    var packet = JsonSerializer.Serialize(new { schema = 1, source_commit = source, encoding = "gzip-base64url",
        sha256 = HashBytes(trialsBytes), data });
    if (packet.Length > 3500) throw new InvalidOperationException("Raw microbenchmark trials exceed their bounded transport");
    Console.WriteLine("::notice title=STR_VIEW_TRIALS_V1::" + packet);
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
    var builds = 0;
    var actors = CreateActors(() => builds++);
    // Setup and forced cleanup are OUTSIDE measurement. Real in-game GC is not inferred from these trials.
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    var bytes = GC.GetAllocatedBytesForCurrentThread();
    var g0 = GC.CollectionCount(0); var g1 = GC.CollectionCount(1); var g2 = GC.CollectionCount(2);
    var start = Stopwatch.GetTimestamp();
    var checksum = 0;
    for (var frame = 0; frame < frames; frame++)
    {
        Mutate(actors, work, frame);
        foreach (var actor in actors)
        {
            if (optimized) actor.Cache.Refresh();
            else
            {
                builds++;
                CollectBaseline(actor);
            }
            // Every frame consumes a view, as _Draw would. No enumeration of the whole power model collection here.
            // Consume only a deterministic, order-independent count so baseline equal-rank ordering is irrelevant.
            checksum = unchecked(checksum * 31 + (optimized ? actor.Cache.Value.Count : actor.Scratch.Count));
        }
    }
    var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    return new(elapsed, GC.GetAllocatedBytesForCurrentThread() - bytes, GC.CollectionCount(0) - g0,
        GC.CollectionCount(1) - g1, GC.CollectionCount(2) - g2, builds, checksum);
}

static Actor[] CreateActors(Action built)
{
    var actors = Enumerable.Range(0, Enemies).Select(_ => new Actor(Powers)).ToArray();
    foreach (var actor in actors)
        actor.Cache = new(items =>
        {
            built();
            foreach (var power in actor.Powers) if (power.Trait && power.Amount != 0) items.Add(power);
            items.Sort(static (a, b) => { var rank = a.Rank.CompareTo(b.Rank); return rank != 0 ? rank : a.Key.CompareTo(b.Key); });
        });
    return actors;
}

static void Mutate(Actor[] actors, Workload work, int frame)
{
    if (frame > 0 && work.MutationPeriod > 0 && frame % work.MutationPeriod == 0)
    {
        var actor = actors[(frame / work.MutationPeriod) % actors.Length];
        var index = (frame / work.MutationPeriod) % (Powers / 2);
        actor.Powers[index].Amount = actor.Powers[index].Amount == 0 ? 1 + frame % 5 : 0;
        actor.Cache.Invalidate(); // simulated event; not the actual Godot/native subscription adapter
    }
    if (frame > 0 && work.RebindPeriod > 0 && frame % work.RebindPeriod == 0)
        foreach (var actor in actors) actor.Cache.Reset();
}

static void CollectBaseline(Actor actor)
{
    actor.Scratch.Clear();
    foreach (var power in actor.Powers) if (power.Trait && power.Amount != 0) actor.Scratch.Add(power);
    actor.Scratch.Sort(static (a, b) => a.Rank.CompareTo(b.Rank));
    actor.Signature = actor.Scratch.Aggregate(17, static (h, p) => unchecked(h * 31 + p.Key * 7 + p.Amount));
}

static void VerifyViews(Workload work, int frames)
{
    var baseline = CreateActors(static () => { }); var candidate = CreateActors(static () => { });
    for (var frame = 0; frame < frames; frame++)
    {
        Mutate(baseline, work, frame); Mutate(candidate, work, frame);
        for (var i = 0; i < Enemies; i++)
        {
            CollectBaseline(baseline[i]); candidate[i].Cache.Refresh();
            var before = baseline[i].Scratch; var after = candidate[i].Cache.Value;
            if (before.Count != after.Count) throw new InvalidOperationException("Stale membership: " + work.Name);
            foreach (var expected in before)
            {
                var found = 0;
                foreach (var actual in after)
                    if (actual.Key == expected.Key && actual.Rank == expected.Rank && actual.Amount == expected.Amount) found++;
                if (found != 1) throw new InvalidOperationException("Missing/duplicate/wrong trait: " + work.Name);
            }
            for (var j = 1; j < after.Count; j++)
                if (after[j - 1].Rank > after[j].Rank || after[j - 1].Rank == after[j].Rank && after[j - 1].Key >= after[j].Key)
                    throw new InvalidOperationException("Non-deterministic candidate ordering: " + work.Name);
        }
    }
}

sealed record Workload(string Name, int MutationPeriod, int RebindPeriod);
sealed record Sample(double Milliseconds, long AllocatedBytes, int Gen0, int Gen1, int Gen2, int Builds, int Checksum);
sealed class Power(int key) { internal readonly int Key = key, Rank = key % 5; internal readonly bool Trait = key < 12; internal int Amount = 3; }
sealed class Actor(int count)
{
    internal readonly Power[] Powers = Enumerable.Range(0, count).Select(i => new Power(i)).ToArray();
    internal readonly List<Power> Scratch = new();
    internal InvalidatedListView<Power> Cache = null!;
    internal int Signature;
}
