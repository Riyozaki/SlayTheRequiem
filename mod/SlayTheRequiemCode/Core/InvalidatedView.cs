namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// Main-thread presentation cache. Event bursts coalesce into one rebuild; an invalidation raised DURING
/// the builder is retained for the next refresh. A failed builder never publishes a partial replacement.
/// No timers, polling, gameplay state, equality/hash guesses or allocations on the clean refresh path.
/// </summary>
public sealed class InvalidatedView<T>(T initial)
{
    public T Value { get; private set; } = initial;
    public bool IsDirty { get; private set; } = true;
    private bool _refreshing;
    private long _generation;

    public void Invalidate() => IsDirty = true;

    public void Reset(T initial)
    {
        _generation = unchecked(_generation + 1);
        Value = initial;
        IsDirty = true;
    }

    public bool Refresh(Func<T> build)
    {
        if (!IsDirty || _refreshing) return false;
        _refreshing = true;
        IsDirty = false;
        var generation = _generation;
        try
        {
            var next = build();
            if (_generation != generation) return false; // a rebind/reset during build has a different owner
            Value = next;
            return true;
        }
        catch
        {
            IsDirty = true;
            throw;
        }
        finally { _refreshing = false; }
    }
}
