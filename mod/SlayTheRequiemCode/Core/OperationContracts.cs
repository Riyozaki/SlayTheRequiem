using System.Text.Json;

namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Outcome is about committed state, not about whether an animation/hook threw.</summary>
public enum OperationStatus { Committed, Rejected, Cancelled, Error, PartialCommit, Duplicate, Stale, Busy }

public sealed record OperationResult(OperationStatus Status, string Target = "", string Detail = "", int Gold = 0)
{
    public bool Changed => Status is OperationStatus.Committed or OperationStatus.PartialCommit;
    public bool Succeeded => Status == OperationStatus.Committed;
    public static OperationResult Reject(string target, string detail) => new(OperationStatus.Rejected, target, detail);
}

/// <summary>
/// The adapter supplies an observable commit predicate. A command may throw AFTER changing its destination.
/// Never delete the source on a veto; never keep a second source after an observed commit. No rollback of hooks
/// is pretended: a post-commit exception is explicitly reported as PartialCommit.
/// </summary>
public static class TransferProtocol
{
    public static Task<OperationResult> Move(string target, Func<Task<bool>> command,
        Func<bool> destinationCommitted, Action finalizeSource, Action? cleanUncommitted = null) =>
        MoveAsync(target, command, destinationCommitted,
            () => { finalizeSource(); return Task.CompletedTask; },
            cleanUncommitted == null ? null : () => { cleanUncommitted(); return Task.CompletedTask; });

    public static async Task<OperationResult> MoveAsync(string target, Func<Task<bool>> command,
        Func<bool> destinationCommitted, Func<Task> finalizeSource, Func<Task>? cleanUncommitted = null)
    {
        Exception? error = null;
        var accepted = false;
        try { accepted = await command(); }
        catch (Exception e) { error = e; }

        bool committed;
        try { committed = destinationCommitted(); }
        catch (Exception e)
        {
            // Unknown is NOT a veto. Cleanup could destroy a destination that actually committed.
            return new(OperationStatus.PartialCommit, target,
                $"Commit could not be observed; source retained, reconciliation required. {error}\n{e}");
        }
        if (committed)
        {
            try { await finalizeSource(); }
            catch (Exception e)
            {
                return new(OperationStatus.PartialCommit, target,
                    $"Destination committed; source finalization failed, reconciliation required. {error}\n{e}");
            }
            return new(error == null && accepted ? OperationStatus.Committed : OperationStatus.PartialCommit,
                target, error?.ToString() ?? (accepted ? "" : "Command rejected after destination changed"));
        }
        try { if (cleanUncommitted != null) await cleanUncommitted(); }
        catch (Exception e)
        {
            return new(OperationStatus.Error, target, $"Destination not committed; source retained, cleanup failed. {error}\n{e}");
        }
        return error != null
            ? new(OperationStatus.Error, target, error.ToString())
            : OperationResult.Reject(target, accepted ? "Command reported success without a destination commit" : "Destination rejected transfer");
    }

    /// <summary>After a destination commits, attempt every required finalizer and report ALL failures, not just the first.</summary>
    public static async Task Finish(params Func<Task>[] steps)
    {
        List<Exception>? errors = null;
        foreach (var step in steps)
        {
            try { await step(); }
            catch (Exception e) { (errors ??= new()).Add(e); }
        }
        if (errors != null) throw new AggregateException("Committed destination has incomplete finalization", errors);
    }
}

/// <summary>
/// One in-flight custom inventory operation per owner. Nested code must explicitly borrow a live lease; a global
/// "executing" flag is NOT permission for an unrelated callback to enter. No AsyncLocal re-entrancy across awaits.
/// The adapter persists the revision separately, so eviction/reload cannot make an old request executable again.
/// </summary>
public sealed class OperationGate
{
    private readonly SemaphoreSlim _serial = new(1, 1);
    private object? _active;
    public bool IsHeld => _active != null;
    public bool Blocked { get; set; }

    public sealed class Lease : IDisposable
    {
        private readonly OperationGate _gate;
        private readonly object _token;
        private readonly bool _owns;
        private int _disposed;
        internal Lease(OperationGate gate, object token, bool owns) { _gate = gate; _token = token; _owns = owns; }
        internal bool IsFor(OperationGate gate) => Volatile.Read(ref _disposed) == 0 && ReferenceEquals(_gate, gate) && ReferenceEquals(_token, gate._active);
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (!_owns) return;
            _gate._active = null;
            _gate._serial.Release();
        }
    }

    public async Task<Lease?> Enter(Lease? held = null, bool allowBlocked = false)
    {
        if (held != null)
        {
            if (!held.IsFor(this)) throw new InvalidOperationException("Foreign or expired inventory lease");
            // A nested finalizer may complete a known operation after Blocked was set; new operations may not enter.
            return new Lease(this, _active!, false);
        }
        if (Blocked && !allowBlocked) return null;
        await _serial.WaitAsync();
        if (Blocked && !allowBlocked) { _serial.Release(); return null; }
        _active = new object();
        return new Lease(this, _active, true);
    }

    public Lease? TryEnter()
    {
        if (Blocked || !_serial.Wait(0)) return null;
        _active = new object();
        return new Lease(this, _active, true);
    }

    public static OperationStatus? Validate(int current, int expected, string operationId, string expectedId)
    {
        if (current < 0 || expected < 0 || !string.Equals(operationId, expectedId, StringComparison.Ordinal)) return OperationStatus.Stale;
        if (expected < current) return OperationStatus.Duplicate;
        return expected == current ? null : OperationStatus.Stale;
    }
}

/// <summary>Opaque relic payload: preserve unavailable models and their properties instead of dropping them.</summary>
public sealed record StoredItemRecord(string Key, string Payload, string LegacyType = "");

public static class StoredItemCodec
{
    public const int Version = 2;
    private sealed record Envelope(int Version, List<StoredItemRecord> Items);

    public static bool TryRead(string? json, string? legacy, out List<StoredItemRecord> items, out string error)
    {
        items = new();
        error = "";
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<Envelope>(json);
                if (envelope == null || envelope.Version != Version || envelope.Items == null)
                    throw new FormatException("Unknown chest schema; original data kept");
                items = envelope.Items;
                if (items.Any(i => i == null || string.IsNullOrWhiteSpace(i.Key) || i.Payload == null || i.LegacyType == null)
                    || items.Select(i => i.Key).Distinct(StringComparer.Ordinal).Count() != items.Count)
                    throw new FormatException("Invalid or duplicate chest instance key; original data kept");
            }
            catch (Exception e) when (e is JsonException or FormatException or NotSupportedException)
            {
                items = new();
                error = e.Message;
                return false;
            }
        }
        var names = (legacy ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < names.Length; i++)
        {
            // Class names are legacy data, never executable type names. Duplicate classes remain distinct entries.
            var key = $"legacy:{i}:{names[i]}";
            if (!items.Any(x => x.Key == key)) items.Add(new(key, "", names[i]));
        }
        return true;
    }

    public static string Write(IEnumerable<StoredItemRecord> items) => JsonSerializer.Serialize(new Envelope(Version, items.ToList()));
}

public static class AcquisitionPolicy
{
    /// <summary>Only an actual add may complete an ordinary choice. Skip/alternatives are separate decisions.</summary>
    public static void AfterAdd(bool success, ref bool rewardComplete, ref bool endSelection)
    {
        if (success) return;
        rewardComplete = false;
        endSelection = false;
    }

    public static bool CanDiscard(bool inSystem, bool inCombat, bool ownsDeckCard, bool removableKind, int excess) =>
        inSystem && !inCombat && ownsDeckCard && removableKind && excess > 0;
}
