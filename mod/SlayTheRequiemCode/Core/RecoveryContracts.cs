using System.Globalization;
using System.Text.Json;

namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>A checksummed whole-run checkpoint, not a promise to undo arbitrary side effects one field at a time.</summary>
public sealed record RunCheckpoint(int Version, string Seed, long StartTime, string Timeline, int Act, int Floor,
    string Operation, string SaveJson, string Digest)
{
    public const int CurrentVersion = 1;
    public static RunCheckpoint Create(string seed, long startTime, string timeline, int act, int floor, string operation, string saveJson)
    {
        var record = new RunCheckpoint(CurrentVersion, seed, startTime, timeline, act, floor, operation, saveJson, "");
        return record with { Digest = record.ComputeDigest() };
    }
    private string ComputeDigest() => OperationIdentity.Hash(string.Join("\n", Version.ToString(CultureInfo.InvariantCulture),
        Seed, StartTime.ToString(CultureInfo.InvariantCulture), Timeline, Act.ToString(CultureInfo.InvariantCulture),
        Floor.ToString(CultureInfo.InvariantCulture), Operation, SaveJson));
    public string Encode() => JsonSerializer.Serialize(this);
    public bool Matches(string seed, long startTime, string timeline) =>
        Seed == seed && StartTime == startTime && Timeline == timeline && !string.IsNullOrEmpty(timeline);
    public static RunCheckpoint? Decode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 32 * 1024 * 1024) return null;
        try
        {
            var r = JsonSerializer.Deserialize<RunCheckpoint>(json);
            return r is { Version: CurrentVersion, Act: >= 0, Floor: >= 0, SaveJson.Length: > 0, Timeline.Length: > 0 }
                && r.Seed != null && r.Operation != null && r.Digest == r.ComputeDigest() ? r : null;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or ArgumentException) { return null; }
    }
}

/// <summary>Same-directory atomic replacement. Never truncate a working save; keep its previous generation.</summary>
public static class AtomicSaveFile
{
    /// <summary>Try complete generations in order; a damaged/foreign current file must not hide a valid backup.</summary>
    public static T? ReadValidated<T>(string path, Func<string, T?> decode, int maxBytes = 32 * 1024 * 1024) where T : class
    {
        foreach (var candidate in new[] { path, path + ".previous" })
        {
            try
            {
                if (!File.Exists(candidate) || new FileInfo(candidate).Length > maxBytes) continue;
                var value = decode(File.ReadAllText(candidate));
                if (value != null) return value;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or JsonException) { }
        }
        return null;
    }

    public static void Write(string path, string text, bool keepPrevious = true) =>
        WriteBytes(path, System.Text.Encoding.UTF8.GetBytes(text), keepPrevious);

    public static void WriteBytes(string path, ReadOnlySpan<byte> bytes, bool keepPrevious = true)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + ".tmp-" + Guid.NewGuid().ToString("N");
        var previous = full + ".previous";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (keepPrevious && File.Exists(full)) File.Copy(full, previous, overwrite: true);
            File.Move(temp, full, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

public sealed record OperationReceiptProof(OperationStatus Status, int Gold, string Digest);
public static class ReceiptRecoveryPolicy
{
    public static bool CanClear(long expectedEpoch, long currentEpoch, int activeMutations, string pending) =>
        expectedEpoch == currentEpoch && activeMutations == 0 && pending.Length == 0;

    /// <summary>Only a receipt-only timeout, never a partial effect or stale operand, is recoverable without reloading.</summary>
    public static bool CanResume(bool receiptTimeoutOnly, int requestRevision, int savedRevision, string currentDigest,
        OperationReceiptProof local, int expectedPeers, IReadOnlyList<OperationReceiptProof> peers) =>
        receiptTimeoutOnly && requestRevision >= 0 && savedRevision == (long)requestRevision + 1
        && local.Status is not (OperationStatus.PartialCommit or OperationStatus.Stale or OperationStatus.Busy)
        && OperationIdentity.IsDigest(local.Digest) && local.Digest == currentDigest
        && expectedPeers >= 0 && peers.Count == expectedPeers && peers.All(p => p == local);
}
