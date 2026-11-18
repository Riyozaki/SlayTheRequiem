using System.Text;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Guard the DISK boundary, not just RunState.FromSerializable. The native reader can scavenge a future schema
/// with data loss, and MP canonicalization can rename an incompatible save before SetUpSaved* is reached.
/// Preserve exact original bytes (also native .backup), then fail read-only with a normal native error result.
/// </summary>
internal static class RunSaveReadGuard
{
    internal static string NativeFile(bool coop) => coop ? RunSaveManager.multiplayerRunSaveFileName : RunSaveManager.runSaveFileName;

    internal static byte[] ReadBytes(string path)
    {
        if (new FileInfo(path).Length > RawRunDocument.MaximumBytes)
            throw new FormatException("Run save exceeds the safety limit; original file not changed");
        return File.ReadAllBytes(path);
    }

    internal static void CopyExact(string path, string destination)
    {
        var bytes = ReadBytes(path);
        if (File.Exists(destination) && new FileInfo(destination).Length == bytes.Length
            && File.ReadAllBytes(destination).AsSpan().SequenceEqual(bytes)) return; // menu reads must not rotate backups
        AtomicSaveFile.WriteBytes(destination, bytes);
    }

    internal static bool BeforeRead(ISaveStore store, IProfileIdProvider profiles, bool coop,
        ref ReadSaveResult<SerializableRun> result, out bool guarded)
    {
        guarded = false;
        try
        {
            var path = ProjectSettings.GlobalizePath(store.GetFullPath(RunSaveManager.GetRunSavePath(profiles.CurrentProfileId, NativeFile(coop))));
            var selected = File.Exists(path) ? path : path + ".backup";
            // Non-disk native test stores are not redirected to a user's profile by this adapter.
            if (!File.Exists(selected)) return true;
            var original = ReadBytes(selected);
            if (!Encoding.UTF8.GetString(original).Contains(MainFile.ModId, StringComparison.OrdinalIgnoreCase)) return true;
            guarded = true;
            var directory = Path.GetDirectoryName(path)!;
            var name = "requiem_before_load" + (coop ? "_coop" : "");
            if (File.Exists(path)) CopyExact(path, Path.Combine(directory, name + ".save"));
            if (File.Exists(path + ".backup")) CopyExact(path + ".backup", Path.Combine(directory, name + ".native-backup.save"));
            var fields = JsonSerializationUtility.GetTypeInfo<SerializableRun>().Properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            RawRunDocument.Validate(original, fields, SaveManager.Instance.GetLatestSchemaVersion<SerializableRun>());
            return true;
        }
        catch (Exception e)
        {
            guarded = false;
            result = Reject(e);
            return false; // never enter native recovery, canonicalization or reload-counter writes on a refused document
        }
    }

    internal static void AfterRead(bool guarded, ref ReadSaveResult<SerializableRun> result)
    {
        if (!guarded || !result.Success) return;
        try
        {
            if (result.Status is ReadSaveStatus.RecoveredWithDataLoss or ReadSaveStatus.JsonRepaired || result.SaveData == null)
                throw new FormatException("Requiem save needs a lossy native repair; use the retained raw copy with the matching build");
            Config.RunSettings.ValidateSaved(result.SaveData);
        }
        catch (Exception e) { result = Reject(e); }
    }

    private static ReadSaveResult<SerializableRun> Reject(Exception e)
    {
        var message = "Requiem: сохранение не изменено / save not modified. " + e.Message
            + ". Keep current_run[ _mp].save and requiem_before_load files; use the matching game/mod build or a known-safe backup.";
        MainFile.Logger.Error("[SavedRunGuard] " + message);
        return new ReadSaveResult<SerializableRun>(ReadSaveStatus.ValidationFailed, message);
    }
}

[CriticalPatch]
[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.LoadRunSave))]
public static class GuardSingleplayerSaveRead
{
    [HarmonyPrefix]
    public static bool Prefix(ISaveStore ____saveStore, IProfileIdProvider ____profileIdProvider,
        ref ReadSaveResult<SerializableRun> __result, out bool __state) =>
        RunSaveReadGuard.BeforeRead(____saveStore, ____profileIdProvider, false, ref __result, out __state);
    [HarmonyPostfix]
    public static void Postfix(bool __state, ref ReadSaveResult<SerializableRun> __result) => RunSaveReadGuard.AfterRead(__state, ref __result);
}

[CriticalPatch]
[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.LoadMultiplayerRunSave))]
public static class GuardMultiplayerSaveRead
{
    [HarmonyPrefix]
    public static bool Prefix(ISaveStore ____saveStore, IProfileIdProvider ____profileIdProvider,
        ref ReadSaveResult<SerializableRun> __result, out bool __state) =>
        RunSaveReadGuard.BeforeRead(____saveStore, ____profileIdProvider, true, ref __result, out __state);
    [HarmonyPostfix]
    public static void Postfix(bool __state, ref ReadSaveResult<SerializableRun> __result) => RunSaveReadGuard.AfterRead(__state, ref __result);
}
