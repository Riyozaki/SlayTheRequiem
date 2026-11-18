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

    internal static void CopyExact(string path, string destination) => CopyBytes(ReadBytes(path), destination);

    private static void CopyBytes(byte[] bytes, string destination)
    {
        if (File.Exists(destination) && new FileInfo(destination).Length == bytes.Length
            && File.ReadAllBytes(destination).AsSpan().SequenceEqual(bytes)) return; // menu reads must not rotate backups
        AtomicSaveFile.WriteBytes(destination, bytes);
    }

    internal static SerializableRun ReadStrict(byte[] original)
    {
        var fields = JsonSerializationUtility.GetTypeInfo<SerializableRun>().Properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var latest = SaveManager.Instance.GetLatestSchemaVersion<SerializableRun>();
        var version = RawRunDocument.Validate(original, fields, latest);
        // Native migration failure can rename the file BEFORE a postfix sees the result. Old GAME schemas
        // require their matching game build; legacy MOD schemas in the current native format migrate separately.
        if (version != latest) throw new FormatException("Different native game save schema; open its retained original with the matching game build");
        var save = RawRunDocument.DeserializeStrict<SerializableRun>(original, JsonSerializationUtility.Options);
        Config.RunSettings.ValidateSaved(save);
        return save;
    }

    internal static bool BeforeRead(ISaveStore store, IProfileIdProvider profiles, bool coop,
        ref ReadSaveResult<SerializableRun> result, out bool guarded)
    {
        guarded = false;
        try
        {
            var path = ProjectSettings.GlobalizePath(store.GetFullPath(RunSaveManager.GetRunSavePath(profiles.CurrentProfileId, NativeFile(coop))));
            // Non-disk native test stores are not redirected to a user's profile by this adapter.
            var primary = File.Exists(path) ? ReadBytes(path) : null;
            var backup = File.Exists(path + ".backup") ? ReadBytes(path + ".backup") : null;
            if (!(primary != null && RawRunDocument.Mentions(primary, MainFile.ModId))
                && !(backup != null && RawRunDocument.Mentions(backup, MainFile.ModId))) return true;
            guarded = true;
            var directory = Path.GetDirectoryName(path)!;
            var name = "requiem_before_load" + (coop ? "_coop" : "");
            if (primary != null) CopyBytes(primary, Path.Combine(directory, name + ".save"));
            if (backup != null) CopyBytes(backup, Path.Combine(directory, name + ".native-backup.save"));
            // Use the SAME inspected bytes, not a second native read with a repair/fallback/rename side effect.
            // A missing primary may use its exact backup. A corrupt existing primary is an explicit refusal:
            // never silently roll the player back to an older file just because its newer save did not parse.
            result = new ReadSaveResult<SerializableRun>(ReadStrict(primary ?? backup!));
            return false;
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

    internal static ReadSaveResult<SerializableRun> Reject(Exception e)
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

/// <summary>Native canonicalization renames both files on a model/ID error; a guarded load must remain read-only.</summary>
[CriticalPatch]
[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.LoadAndCanonicalizeMultiplayerRunSave))]
public static class GuardMultiplayerCanonicalization
{
    [HarmonyPrefix]
    public static bool Prefix(ulong localPlayerId, ISaveStore ____saveStore, IProfileIdProvider ____profileIdProvider,
        ref ReadSaveResult<SerializableRun> __result)
    {
        if (RunSaveReadGuard.BeforeRead(____saveStore, ____profileIdProvider, true, ref __result, out _)) return true;
        if (!__result.Success || __result.SaveData == null) return false;
        try
        {
            var canonical = MegaCrit.Sts2.Core.Runs.RunManager.CanonicalizeSave(__result.SaveData, localPlayerId);
            Config.RunSettings.ValidateSaved(canonical);
            __result = new ReadSaveResult<SerializableRun>(canonical);
        }
        catch (Exception e) { __result = RunSaveReadGuard.Reject(e); }
        return false;
    }
}
