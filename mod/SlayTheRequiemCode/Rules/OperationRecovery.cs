using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Rollback;
using SlayTheRequiem.SlayTheRequiemCode.UI;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Explicit recovery of an UNKNOWN result. Never clears a partial transaction by fiat or reapplies its effect.
/// A whole native run checkpoint is written BEFORE a root mutation. Restoring discards all subsequent changes,
/// rotates the operation timeline and preserves the failed state as a separate file. Co-op uses the host save.
/// </summary>
public static class OperationRecovery
{
    private static bool _offering;
    private static int _generation;
    internal static void ResetForNewRun() { _generation++; _offering = false; }
    private static string PathFor(string name, bool? coop = null) => ProjectSettings.GlobalizePath(SaveManager.Instance.GetProfileScopedPath(
        Path.Combine(UserDataPathProvider.SavesDir, name + ((coop ?? RequiemNet.IsMultiplayer) ? "_coop" : "") + ".save")));
    private static string Timeline(RunState state) => state.Players.Select(TravelPack.Of).OfType<TravelPack>()
        .Select(p => p.OperationRunId).FirstOrDefault(s => s.Length > 0) ?? "";

    /// <summary>Isolated adapter fixtures never touch real player files. Failure to back up rejects BEFORE mutation.</summary>
    internal static void Capture(MegaCrit.Sts2.Core.Entities.Players.Player owner, string operation)
    {
        if (RequiemNet.IsClient || !ReferenceEquals(owner.RunState, RequiemNet.State)) return;
        var state = RequiemNet.State;
        if (state == null || state.Map == null || MemoryStone.Restoring || CombatManager.Instance.IsInProgress || !RunManager.Instance.ShouldSave) return;
        var packs = state.Players.Select(TravelPack.Of).OfType<TravelPack>().ToList();
        // Concurrent owners form one recovery group. Never replace its known-safe checkpoint with a mid-effect save.
        if (packs.Any(p => p.OperationPending.Length > 0 || p.OperationBlocked || InventoryGate.IsMutating(p.Owner))) return;
        var timeline = Timeline(state);
        if (timeline.Length == 0) return; // startup has its own native pre-room save, before the run identity exists
        var save = RunManager.Instance.ToSave(state.CurrentRoom?.IsPreFinished == true ? state.CurrentRoom : null);
        var checkpoint = RunCheckpoint.Create(state.Rng.StringSeed, save.StartTime, timeline,
            state.CurrentActIndex, state.TotalFloor, operation, JsonSerializationUtility.ToJson(save));
        AtomicSaveFile.Write(PathFor("requiem_inventory_checkpoint"), checkpoint.Encode());
        MainFile.Logger.Info($"[InventoryRecovery] checkpoint op={operation} floor={state.TotalFloor} digest={checkpoint.Digest}");
    }

    /// <summary>Keep the entire pre-migration payload, including fields this version cannot interpret.</summary>
    internal static void BackupBeforeMigration(SerializableRun save, bool coop)
    {
        // Called before native IncrementNumReloads, even though NetService may still be absent. Read the
        // ORIGINAL disk bytes, not ToJson(save): the latter has already discarded unknown native JSON fields.
        var native = ProjectSettings.GlobalizePath(SaveManager.Instance.GetProfileScopedPath(
            Path.Combine(UserDataPathProvider.SavesDir, RunSaveReadGuard.NativeFile(coop))));
        var source = File.Exists(native) ? native : native + ".backup";
        if (File.Exists(source))
        {
            RunSaveReadGuard.CopyExact(source, PathFor("requiem_before_migration", coop));
            if (File.Exists(native + ".backup"))
                RunSaveReadGuard.CopyExact(native + ".backup", PathFor("requiem_before_migration_native_backup", coop));
        }
        else
        {
            // An in-memory host/replay input has no original disk document. Name it honestly, never pass
            // native reserialization off as a byte-for-byte backup of an on-disk save.
            AtomicSaveFile.Write(PathFor("requiem_before_migration_native_object", coop), JsonSerializationUtility.ToJson(save));
        }
    }

    /// <summary>Durable pre-application rollback state: a scar callback may save before the loaded map is assigned.</summary>
    internal static void CapturePreparedRun(SerializableRun save, string operation, bool coop)
    {
        Config.RunSettings.ValidateSaved(save);
        var packId = ModelDb.Relic<TravelPack>().Id;
        var timelines = save.Players.SelectMany(p => p.Relics).Where(r => r.Id == packId)
            .SelectMany(r => r.Props?.strings ?? new List<SavedProperties.SavedProperty<string>>())
            .Where(p => p.name == nameof(TravelPack.OperationRunId)).Select(p => p.value).Distinct(StringComparer.Ordinal).ToList();
        if (timelines.Count != 1 || timelines[0].Length == 0) throw new FormatException("Prepared recovery has no unique timeline");
        var seed = save.SerializableRng?.Seed ?? throw new FormatException("Prepared recovery has no RNG seed");
        var floor = save.MapPointHistory.Sum(act => act.Count);
        var cp = RunCheckpoint.Create(seed, save.StartTime, timelines[0], save.CurrentActIndex, floor,
            operation, JsonSerializationUtility.ToJson(save));
        AtomicSaveFile.Write(PathFor("requiem_inventory_checkpoint", coop), cp.Encode());
    }

    private static RunCheckpoint? Read(RunState state)
    {
        try
        {
            var path = PathFor("requiem_inventory_checkpoint");
            var live = RunManager.Instance.ToSave(null);
            return AtomicSaveFile.ReadValidated(path, json =>
            {
                var cp = RunCheckpoint.Decode(json);
                return cp?.Matches(state.Rng.StringSeed, live.StartTime, Timeline(state)) == true ? cp : null;
            });
        }
        catch (Exception e) { MainFile.Logger.Error($"[InventoryRecovery] Checkpoint unavailable: {e}"); return null; }
    }

    public static string Describe(RunState? state)
    {
        if (state == null) return "Inventory recovery: no run.";
        var blocked = state.Players.Select(TravelPack.Of).OfType<TravelPack>().Where(p => p.OperationBlocked || p.OperationPending.Length > 0).ToList();
        if (blocked.Count == 0) return "Inventory recovery: no unresolved operations.";
        var lines = blocked.Select(p => $"player={p.Owner.NetId} pending={p.OperationPending} reason={p.OperationFailure}");
        var checkpoint = RequiemNet.IsClient ? null : Read(state);
        return string.Join("\n", lines) + "\n" + (RequiemNet.IsClient ? "Recovery authority: host; retry receipts or ask the host to restore."
            : checkpoint == null ? "No matching safe checkpoint. Keep the current save; use its backup or Memory Stone. No unsafe unlock."
            : $"Safe checkpoint: act={checkpoint.Act + 1} floor={checkpoint.Floor} op={checkpoint.Operation} sha256={checkpoint.Digest}. Restore cancels ALL later actions.");
    }

    public static async Task OfferRestore()
    {
        if (_offering || MemoryStone.Restoring) return;
        _offering = true;
        var generation = _generation;
        try
        {
            var state = RequiemNet.State;
            if (state == null || CombatManager.Instance.IsInProgress) return;
            if (RequiemNet.IsClient) { NRequiemToast.Show(RequiemUi.TipText("RECOVERY_HOST.title")); return; }
            var cp = Read(state);
            if (cp == null) { NRequiemToast.Show(RequiemUi.TipText("RECOVERY_UNAVAILABLE.title")); return; }
            if (state.Players.Select(TravelPack.Of).OfType<TravelPack>().Any(p => InventoryGate.IsMutating(p.Owner)))
            { NRequiemToast.Show(RequiemUi.TipText("RECOVERY_BUSY.title")); return; }
            var popup = NGenericPopup.Create();
            if (popup == null || NModalContainer.Instance == null) return;
            NModalContainer.Instance.Add(popup);
            var body = RequiemUi.Tip("RECOVERY_BODY.title"); body.Add("Floor", cp.Floor);
            if (!await popup.WaitForConfirmation(body, RequiemUi.Tip("RECOVERY_HEADER.title"),
                RequiemUi.Tip("RUN_PASSPORT_CLOSE.title"), RequiemUi.Tip("RECOVERY_RESTORE.title"))) return;
            if (generation != _generation || MemoryStone.Restoring || !ReferenceEquals(state, RequiemNet.State) || CombatManager.Instance.IsInProgress) return;
            if (state.Players.Select(TravelPack.Of).OfType<TravelPack>().Any(p => InventoryGate.IsMutating(p.Owner))) return;
            var parsed = JsonSerializationUtility.FromJson<SerializableRun>(cp.SaveJson);
            if (!parsed.Success || parsed.SaveData == null) throw new InvalidOperationException("Checkpoint native run payload is invalid");
            var save = parsed.SaveData;
            Config.RunSettings.ValidateSaved(save);
            if (!MemoryStoneRules.IsSnapshotCompatible(save.SchemaVersion, SaveManager.Instance.GetLatestSchemaVersion<SerializableRun>()))
                throw new InvalidOperationException("Checkpoint game schema differs; original data retained");
            // Preserve the failure BEFORE touching the native current_run save or disconnecting anyone.
            AtomicSaveFile.Write(PathFor("requiem_inventory_failed"), JsonSerializationUtility.ToJson(RunManager.Instance.ToSave(null)));
            RotateTimeline(save);
            if (RequiemNet.IsMultiplayer)
            {
                using var transition = MemoryStone.TryBeginRestore(); // preserve the prepared checkpoint across all callbacks
                if (transition == null) return;
                CapturePreparedRun(save, "inventory-recovery", coop: true);
                await SaveManager.Instance.IncrementNumReloads(save, isMultiplayer: true);
                if (!ReferenceEquals(state, RequiemNet.State) || !RequiemNet.IsHost) return;
                CustomMessageWrapper.Send(new InventoryRecoveryMessage { timeline = Timeline(state), seed = state.Rng.StringSeed });
                await Task.Delay(500);
                if (!ReferenceEquals(state, RequiemNet.State) || !RequiemNet.IsHost) return;
                await NGame.Instance.ReturnToMainMenu();
                if (generation != _generation || RequiemNet.State != null) return;
                NGame.Instance.MainMenu?.OpenMultiplayerSubmenu()?.StartHost(save);
            }
            else await MemoryStone.ReturnSingleplayer(save, "inventory-recovery");
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"[InventoryRecovery] Restore failed, copies retained: {e}");
            NRequiemToast.Show(RequiemUi.TipText("RECOVERY_UNAVAILABLE.title"));
        }
        finally { if (generation == _generation) _offering = false; }
    }

    internal static void RotateTimeline(SerializableRun save)
    {
        var id = ModelDb.Relic<TravelPack>().Id;
        var timeline = Guid.NewGuid().ToString("N");
        foreach (var player in save.Players)
        {
            var source = player.Relics.FirstOrDefault(r => r.Id == id);
            if (source == null) continue;
            var props = source.Props ??= new SavedProperties();
            SavedPropertyWrite.Text(props, nameof(TravelPack.OperationRunId), timeline);
            SavedPropertyWrite.Int(props, nameof(TravelPack.OperationRevision), 0);
            SavedPropertyWrite.Text(props, nameof(TravelPack.OperationPending), "");
            SavedPropertyWrite.Bool(props, nameof(TravelPack.OperationBlocked), false);
            SavedPropertyWrite.Text(props, nameof(TravelPack.OperationFailure), "");
        }
    }

    internal static void OnHostRestore(string timeline, string seed, ulong sender)
    {
        if (_offering || RequiemNet.State is not { } state || !RequiemNet.IsClient
            || RunManager.Instance.NetService is not INetClientGameService { NetClient: { } client }
            || sender != client.HostNetId || state.Rng.StringSeed != seed || Timeline(state) != timeline || timeline.Length == 0) return;
        _offering = true; // authenticated duplicates cannot start a second menu transition
        TaskHelper.RunSafely(Rejoin(sender, _generation));
    }
    private static async Task Rejoin(ulong host, int generation)
    {
        try { await CoopMemoryStone.RejoinAsClient(host); }
        finally { if (generation == _generation) _offering = false; }
    }
}
