namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// Bounded replay receipts. Entries bind the complete immutable request, not just an ID an altered request can reuse.
/// Access under the owner's OperationGate. The saved revision, NOT this diagnostic window, prevents old effects.
/// A cache hit must still send its receipt again: the first acknowledgement may have been lost.
/// </summary>
public sealed class OperationJournal<TRequest, TReceipt> where TRequest : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<string, (TRequest Request, TReceipt Receipt)> _entries = new(StringComparer.Ordinal);
    private readonly Queue<string> _order = new();
    public OperationJournal(int capacity = 256)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int Count => _entries.Count;
    public bool TryGet(string id, TRequest request, out TReceipt receipt, out bool conflict)
    {
        if (_entries.TryGetValue(id, out var entry))
        {
            conflict = !EqualityComparer<TRequest>.Default.Equals(entry.Request, request);
            receipt = entry.Receipt;
            return true;
        }
        receipt = default!;
        conflict = false;
        return false;
    }

    public bool Contains(string id) => _entries.ContainsKey(id);
    public bool TryGetReceipt(string id, out TReceipt receipt)
    {
        if (_entries.TryGetValue(id, out var entry)) { receipt = entry.Receipt; return true; }
        receipt = default!;
        return false;
    }

    public string? Add(string id, TRequest request, TReceipt receipt)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentException("Operation ID required", nameof(id));
        if (_entries.ContainsKey(id)) throw new InvalidOperationException("A completed operation receipt cannot be overwritten");
        _entries.Add(id, (request, receipt));
        _order.Enqueue(id);
        if (_entries.Count <= _capacity) return null;
        var expired = _order.Dequeue();
        _entries.Remove(expired);
        return expired;
    }
}

/// <summary>Shared accounting rules used by the game adapter; quotes never substitute for observed gold changes.</summary>
public static class OperationAccounting
{
    public static int Paid(int before, int after) => Math.Max(0, before - after);
    public static OperationStatus SaleStatus(int expected, int actual, bool effectError) =>
        expected > 0 && actual == expected && !effectError ? OperationStatus.Committed : OperationStatus.PartialCommit;
}
