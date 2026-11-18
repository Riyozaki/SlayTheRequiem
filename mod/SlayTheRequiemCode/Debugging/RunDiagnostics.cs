using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rollback;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Debugging;

/// <summary>No saves or local paths in the passport. Runtime facts are distinct from build and visual acceptance.</summary>
public static class RunDiagnostics
{
    public const int SaveSchema = 4;
    private static object? _binaries;
    private static string FileHash(string path)
    {
        try
        {
            if (!System.IO.File.Exists(path)) return "missing";
            using var stream = System.IO.File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception e) { return "unavailable:" + e.GetType().Name; }
    }
    // First call reads/hashes installed artifacts; subsequent calls reuse the same process-lifetime snapshot.
    // This establishes recorded hash consistency, not signed provenance and not a no-IO performance guarantee.
    internal static object BinaryIdentity() => Binaries();

    private static object Binaries()
    {
        if (_binaries != null) return _binaries;
        var assembly = typeof(MainFile).Assembly;
        string? Meta(string key) => assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value;
        var directory = System.IO.Path.GetDirectoryName(assembly.Location) ?? "";
        var dllHash = FileHash(assembly.Location);
        var pckHash = FileHash(System.IO.Path.Combine(directory, MainFile.ModId + ".pck"));
        var passportPath = System.IO.Path.Combine(directory, MainFile.ModId + ".build.json");
        object build = new { state = "no verified build passport" };
        try
        {
            if (System.IO.File.Exists(passportPath))
            {
                using var json = JsonDocument.Parse(System.IO.File.ReadAllText(passportPath));
                var root = json.RootElement;
                var artifacts = root.GetProperty("artifacts");
                string? Expected(string suffix) => artifacts.GetProperty(MainFile.ModId + suffix).GetProperty("sha256").GetString();
                build = new
                {
                    state = "recorded (hash consistency, not signed provenance)",
                    dirty = root.GetProperty("dirty").GetBoolean(),
                    exported_at_utc = root.GetProperty("exported_at_utc").GetString(),
                    godot = root.GetProperty("godot").GetString(),
                    dependency_versions = new { game = root.GetProperty("game_version").GetString(), baselib = root.GetProperty("baselib_version").GetString() },
                    contracts_match_dll = root.GetProperty("save_schema").GetInt32() == SaveSchema
                        && root.GetProperty("shop_protocol").GetInt32() == Shops.ShopOperations.Protocol
                        && root.GetProperty("session_protocol").GetInt32() == Core.StartupState.SessionProtocol
                        && root.GetProperty("creation_schema").GetInt32() == Core.StartupState.Protocol
                        && root.GetProperty("economy_revision").GetInt32() == Core.EconomyRules.CurrentRevision
                        && Meta("SessionProtocol") == Core.StartupState.SessionProtocol.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        && Meta("CreationSchema") == Core.StartupState.Protocol.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        && Meta("EconomyRevision") == Core.EconomyRules.CurrentRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        && Meta("SaveSchema") == SaveSchema.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        && Meta("ShopProtocol") == Shops.ShopOperations.Protocol.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    source_matches_dll = root.GetProperty("git_sha").GetString() == Meta("GitCommit")
                        && root.GetProperty("source_tree_sha256").GetString() == Meta("SourceTreeHash"),
                    files_match_passport = string.Equals(dllHash, Expected(".dll"), StringComparison.OrdinalIgnoreCase)
                        && string.Equals(pckHash, Expected(".pck"), StringComparison.OrdinalIgnoreCase)
                        && string.Equals(FileHash(System.IO.Path.Combine(directory, MainFile.ModId + ".json")), Expected(".json"), StringComparison.OrdinalIgnoreCase),
                };
            }
        }
        catch (Exception e) { build = new { state = "invalid passport", error = e.GetType().Name }; }
        return _binaries = new
        {
            git_sha = Meta("GitCommit") ?? "unrecorded", source_tree_sha256 = Meta("SourceTreeHash") ?? "unrecorded",
            dirty = Meta("GitDirty") ?? "unrecorded", dll_sha256 = dllHash, pck_sha256 = pckHash,
            build_passport_sha256 = FileHash(passportPath), build,
            loaded_mods = ModManager.GetLoadedMods().Select(m => new { id = m.manifest?.id, version = m.manifest?.version,
                dll_sha256 = m.assembly == null ? "none" : FileHash(m.assembly.Location) }).ToArray(),
            save_schema = SaveSchema, shop_protocol = Shops.ShopOperations.Protocol,
            session_protocol = Core.StartupState.SessionProtocol, economy_policy_current = Core.EconomyRules.CurrentRevision,
        };
    }

    public static string LaunchKind { get; set; } = "unknown";

    public static string Describe(RunState? state, bool pretty = true)
    {
        var effective = RunSettings.Active?.Clone() ?? RunSettings.FromConfig(LocalContext.NetId);
        effective.EnableRequiemSystems = RunSettings.EnableRequiemSystems;
        var window = NGame.Instance?.GetWindow();
        var viewport = NGame.Instance?.GetViewport();
        var passport = new
        {
            schema = 1, recorded_at_utc = DateTime.UtcNow.ToString("O"), launch = LaunchKind,
            artifacts = Binaries(), game = ReleaseInfoManager.Instance.ReleaseInfo?.Version ?? "unknown",
            seed = state?.Rng.StringSeed, run_id = RunSettings.Active?.RunId, act = state?.CurrentActIndex, floor = state?.TotalFloor,
            network = new { role = Net.RequiemNet.Type.ToString(), local_net_id = Net.RequiemNet.LocalNetId },
            settings_source = RunSettings.SourceName, requested_snapshot = RunSettings.Active?.Encode(),
            effective_settings = effective.Encode(), degraded = PatchReport.Degraded, failed_patches = PatchReport.Failed,
            language = LocManager.Instance.Language,
            presentation = new { reduced_motion = RequiemConfig.ReducedMotion, combat_vfx = RequiemConfig.CombatVfx,
                sounds = RequiemConfig.RequiemSounds, compact_enemy_statuses = RequiemConfig.CompactEnemyStatuses,
                paper_doll = RequiemConfig.ShowPaperDoll, metrics = TechnicalMetrics.Enabled, balance_trace = CardRewardTrace.Enabled },
            display = new { window = DisplayServer.WindowGetSize().ToString(), viewport = viewport?.GetVisibleRect().Size.ToString(),
                content_scale_factor = window?.ContentScaleFactor, canvas_scale = viewport?.GetCanvasTransform().Scale.ToString(),
                vsync = DisplayServer.WindowGetVsyncMode().ToString(), screen_refresh_hz = DisplayServer.ScreenGetRefreshRate(),
                max_fps = Godot.Engine.MaxFps },
            players = state?.Players.Select(p => new
            {
                id = p.NetId, hero = p.Character.Id.Entry, requiem_level = Acts.RequiemLevel.Of(p),
                hero_record_level = Progression.HeroRecord.Of(p)?.Level,
                origin = p.Relics.OfType<OriginRelic>().FirstOrDefault()?.Id.Entry ?? "not-selected",
                background = p.Relics.OfType<BackgroundRelic>().FirstOrDefault()?.Id.Entry ?? "not-selected",
                deferred_cards = TravelPack.Of(p)?.PendingCards.Count ?? 0,
                operation_revision = TravelPack.Of(p)?.OperationRevision ?? 0,
                operation_blocked = InventoryGate.ObserveBlocked(p),
                operation_pending = TravelPack.Of(p)?.OperationPending ?? "",
                pending_brew_costs = p.PotionSlots.OfType<MegaCrit.Sts2.Core.Models.PotionModel>().Count(HerbalistPouch.HasBrewCost),
            }).ToArray(),
        };
        return JsonSerializer.Serialize(passport, new JsonSerializerOptions { WriteIndented = pretty });
    }

    public static void OnLaunch(RunState state)
    {
        try
        {
            foreach (var player in state.Players)
            {
                if (TravelPack.Of(player) is { OperationRunId.Length: 0 } pack && RunSettings.Active?.RunId is { Length: > 0 } id)
                    pack.OperationRunId = id;
                InventoryIdentity.Ensure(player);
            }
            if (LaunchKind != "new")
                LaunchKind = MemoryStone.Restoring || state.Players.Any(p => MemoryStoneRelic.Of(p) is { } stone
                    && stone.PendingFlesh + stone.PendingSoul + stone.PendingPurse > 0) ? "rollback" : "continue";
            Log(state, "launch");
        }
        catch (Exception e) { MainFile.Logger.Error($"[RunPassport] Failed: {e}"); }
    }

    public static void Log(RunState? state, string phase)
    {
        try { MainFile.Logger.Info($"[RunPassport] phase={phase} " + Describe(state, pretty: false)); }
        catch (Exception e) { MainFile.Logger.Warn($"[RunPassport] {e}"); }
    }
}
