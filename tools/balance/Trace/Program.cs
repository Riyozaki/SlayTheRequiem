using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SlayTheRequiem.Balance;
using SlayTheRequiem.SlayTheRequiemCode.Core;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
var jsonPath = ".arena-ci/balance/core-scenarios.json";
var reportPath = ".arena-ci/balance/core-scenarios.md";
var annotate = false;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--json" && i + 1 < args.Length) jsonPath = args[++i];
    else if (args[i] == "--report" && i + 1 < args.Length) reportPath = args[++i];
    else if (args[i] == "--annotate") annotate = true;
    else throw new ArgumentException("Usage: dotnet run --project tools/balance/Trace -- [--json file] [--report file] [--annotate]");
}
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true };
var batch = Scenario.FullBatch();
void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
Check(batch.Runs.Length == 60, "Expected paired 10-seed/3-hero ledgers, not native runs");
Check(batch.Runs.Select(r => (r.Acts[1].Kind, r.Acts[3].Kind)).Distinct().Count() == 4, "Reference workload should cover four branch combinations");
foreach (var run in batch.Runs)
{
    Check(run.Acts.Count == 5, "Missing an act in the five-act ledger");
    foreach (var row in run.Acts)
    {
        Check(row.BudgetEnd == row.BudgetOpen + row.PreBossGold + row.BossGold + row.Sold - row.Spent, "Gold ledger does not reconcile");
        Check(row.Rooms == row.Normals + row.Elites + row.Camps + row.Shops + row.Events + row.Treasures, "Synthetic room workload grew");
        Check(row.BudgetEnd >= 0 && row.PlannedCampSpent <= row.CampBudget, "Unaffordable hypothetical action");
        Check(row.Excess <= 5 && row.DesiredCards == row.AcceptedCards + row.SkippedCards, "Managed Burden policy violated");
    }
    Check(run.Acts[2].PathDue, "Level/Path window missed in this reference workload");
}
foreach (var hero in Scenario.Heroes)
foreach (var seed in Enumerable.Range(0, Scenario.SeedCount))
{
    var before = batch.Runs.Single(r => r.Hero == hero && r.Seed == Scenario.Seed(seed) && r.Revision == 0);
    var after = batch.Runs.Single(r => r.Hero == hero && r.Seed == Scenario.Seed(seed) && r.Revision == 1);
    foreach (var (b, a) in before.Acts.Zip(after.Acts))
        Check((b.Rooms, b.Normals, b.Elites, b.PreBossGold, b.BossGold, b.DesiredCards) ==
            (a.Rooms, a.Normals, a.Elites, a.PreBossGold, a.BossGold, a.DesiredCards), "Paired seed workload drifted");
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(jsonPath))!);
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
var json = JsonSerializer.Serialize(batch, options) + "\n";
File.WriteAllText(jsonPath, json, new UTF8Encoding(false));
var groups = (from hero in Scenario.Heroes from revision in new[] { 0, EconomyRules.CurrentRevision }
    let runs = batch.Runs.Where(r => r.Hero == hero && r.Revision == revision).ToArray()
    select new
    {
        hero, revision, scenarios = runs.Length,
        end_gold_min = runs.Min(r => r.Acts[^1].BudgetEnd), end_gold_max = runs.Max(r => r.Acts[^1].BudgetEnd),
        spent_min = runs.Min(r => r.Acts.Sum(a => a.Spent)), spent_max = runs.Max(r => r.Acts.Sum(a => a.Spent)),
        end_level_min = runs.Min(r => r.Acts[^1].Level), end_level_max = runs.Max(r => r.Acts[^1].Level),
        level_at_boss3_min = runs.Min(r => r.Acts[2].Level),
        skipped_cards_total = runs.Sum(r => r.Acts.Sum(a => a.SkippedCards)),
        missing_gear_baskets = runs.Sum(r => r.Acts.Count(a => !a.GearBought)),
        end_excess_max = runs.Max(r => r.Acts[^1].Excess),
    }).ToArray();
var summary = new
{
    schema = 1, producer = "core-scenario", actual_native_runs = 0,
    source_commit = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "unrecorded-local",
    paired_seeds_per_hero = Scenario.SeedCount, paired_comparisons = batch.Runs.Length / 2,
    scenarios = batch.Runs.Length, act_rows = batch.Runs.Sum(r => r.Acts.Count),
    baseline = "Requiem 1, five acts, HP 110%; all victories assumed; not native maps/rewards/battles or winrate",
    json_sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant(), groups,
};
var summaryPath = Path.ChangeExtension(jsonPath, "summary.json");
File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, options) + "\n", new UTF8Encoding(false));
var report = new StringBuilder("# B2.5 — actual Core arithmetic on limited economic scenarios\n\n");
report.AppendLine("**NOT native gameplay.** 60 synthetic ledgers / 30 same-seed revision pairs / 300 act rows, not 60 played runs. Every fight is assumed won.\n");
report.AppendLine("Only two policy groups changed: five-stage stock timing and the second act HP overlay on authored bosses.");
report.AppendLine("Gold/price/Burden/stat/resource/difficulty numbers stay unchanged. Revision 0 is the saved legacy contract.\n");
report.AppendLine("## Assumptions\n");
foreach (var assumption in batch.Assumptions) report.AppendLine("- " + assumption);
report.AppendLine("\n## Same-seed workload: before vs after\n");
report.AppendLine("| Hero | Revision | End gold min–max | Total spent min–max | End level | Min level boss 3 | Desired cards skipped (10 seeds) | Gear baskets not acquired | End excess max |\n|---|---|---|---|---|---|---|---|---|");
foreach (var g in groups)
    report.AppendLine($"| {g.hero} | {g.revision} | {g.end_gold_min}–{g.end_gold_max} | {g.spent_min}–{g.spent_max} | {g.end_level_min}–{g.end_level_max} | {g.level_at_boss3_min} | {g.skipped_cards_total} | {g.missing_gear_baskets} | {g.end_excess_max} |");
report.AppendLine("\nLarge gold leftovers here do NOT prove that gameplay is easy: shops are late, HP/healing/real choices/deaths are absent. Skipped sample cards do NOT measure actual refusals.\n");
report.AppendLine("## Actual policy schedule\n");
report.AppendLine("| Act | Armorer rev 0 → 1 | Wanderer rev 0 → 1 | Staff rev 0 → 1 | Brew rev 0 → 1 | Primary boss reference factor rev 0 → 1 | Optional authored-boss factor rev 0 → 1 |\n|---|---|---|---|---|---|---|");
string Range((int Min, int Max) r) => $"{r.Min}–{r.Max}";
for (var act = 0; act < 5; act++)
{
    var old = new EconomyRoute(0, act, 5, true, false); var current = old with { Revision = 1 };
    var sample = batch.Runs.First(r => r.Hero == "Ironclad" && r.Revision == 0).Acts[act];
    var changed = batch.Runs.First(r => r.Hero == "Ironclad" && r.Revision == 1).Acts[act];
    report.AppendLine($"| {act + 1} | {Range(EconomyRules.EquipmentTiers(ShopType.Armorer, old))} → {Range(EconomyRules.EquipmentTiers(ShopType.Armorer, current))} | {Range(EconomyRules.EquipmentTiers(ShopType.Wanderer, old))} → {Range(EconomyRules.EquipmentTiers(ShopType.Wanderer, current))} | {Range(EconomyRules.StaffTiers(old))} → {Range(EconomyRules.StaffTiers(current))} | {EconomyRules.BrewTier(old)} → {EconomyRules.BrewTier(current)} | {sample.PrimaryBossHpMultiplier} → {changed.PrimaryBossHpMultiplier} | {sample.AuthoredBossHpMultiplier} → {changed.AuthoredBossHpMultiplier} |");
}
report.AppendLine("\n## Native comparison still required at the end of the release cycle\n");
report.AppendLine("Enable str_trace on and preserve str_passport. RequiemBalance JSON snapshots have producer=native-game and explicit event boundaries; this file has producer=core-scenario.");
report.AppendLine("Use tools/balance/compare_traces.py for bounded observations matched by seed/hero/economy revision. It must never relabel this scenario as native evidence.");
report.AppendLine("No native traces have been supplied/executed by this tool. Manual/visual/two-client tests remain final-cycle work.\n");
report.AppendLine($"Scenario JSON SHA256: `{summary.json_sha256}`.\n");
File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
var compact = JsonSerializer.Serialize(summary);
Console.WriteLine("B2.5 Core scenarios PASS: 60 ledgers, 30 same-seed revision pairs, 300 act rows. Native gameplay executions: 0.");
Console.WriteLine("Summary: " + compact);
if (annotate)
{
    Console.WriteLine("::notice title=B2.5 Core economic scenarios::" + compact.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A"));
    // Bounded, public synthetic data only. An API-readable copy when artifact storage cannot be reached;
    // this transports the actual C# output, not a locally reconstructed Python result or native trace.
    var bundle = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string>
    {
        ["core-scenarios.json"] = json,
        ["core-scenarios.summary.json"] = JsonSerializer.Serialize(summary, options) + "\n",
        ["core-scenarios.md"] = report.ToString(),
    });
    using var compressed = new MemoryStream();
    using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true)) gzip.Write(bundle);
    // Workflow-command annotations may be truncated near 4 KB and render escaped '+' differently.
    // Small URL-safe ASCII parts survive that path; reassembly must verify every part and the bundle hash.
    // A step retains at most ten notice annotations: one summary + at most nine parts.
    // Keep each part well below the observed ~3.8 KB transport truncation as well.
    const int chunkChars = 2800, maxParts = 9;
    var encoded = Convert.ToBase64String(compressed.ToArray()).Replace('+', '-').Replace('/', '_');
    var parts = (encoded.Length + chunkChars - 1) / chunkChars;
    var bundleHash = Convert.ToHexString(SHA256.HashData(bundle)).ToLowerInvariant();
    if (parts <= maxParts)
        for (var part = 0; part < parts; part++)
        {
            var data = encoded.Substring(part * chunkChars, Math.Min(chunkChars, encoded.Length - part * chunkChars));
            Console.WriteLine($"::notice title=B2.5 Core economic evidence {part + 1}/{parts}::schema=1;producer=core-scenario;encoding=gzip-base64url;sha256={bundleHash};part={part + 1};total={parts};data={data}");
        }
    else Console.WriteLine("::warning::Core economy evidence exceeds the bounded annotation budget; use the uploaded artifact instead.");
}
