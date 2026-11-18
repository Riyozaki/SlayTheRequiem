namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// Main-thread, double-buffered presentation list. Only the inactive list is rebuilt; a failed/reentrant
/// builder cannot publish a partial replacement. Retains capacities across invalidation and Reset, but
/// clears element references after swap/exit. No List/ToArray/delegate allocation per warm rebuild.
/// Value is a BORROWED read-only view valid only until the next Refresh/Reset, not a retained snapshot.
/// The builder must fill only its supplied buffer, not keep it or mutate the published view.
/// </summary>
public sealed class InvalidatedListView<T>
{
    private readonly InvalidatedView<List<T>> _view = new(new List<T>());
    private List<T> _spare = new();
    private readonly Action<List<T>> _build;
    private readonly Func<List<T>> _buildNext;
    private bool _refreshing;

    public InvalidatedListView(Action<List<T>> build)
    {
        _build = build ?? throw new ArgumentNullException(nameof(build));
        _buildNext = BuildNext;
    }

    public IReadOnlyList<T> Value => _view.Value;
    public bool IsDirty => _view.IsDirty;
    public void Invalidate() => _view.Invalidate();

    public void Reset()
    {
        _view.Value.Clear();
        _spare.Clear();
        _view.Reset(_view.Value); // generation prevents a builder running for the old owner from publishing
    }

    public bool Refresh()
    {
        // Only the outer invocation owns scratch cleanup. A nested refresh must not clear a builder's live buffer.
        if (!_view.IsDirty || _refreshing) return false;
        _refreshing = true;
        var previous = _view.Value;
        try
        {
            if (!_view.Refresh(_buildNext)) return false;
            _spare = previous;
            return true;
        }
        finally
        {
            // On success this is the former published list; on rejection/exception it is the unpublished scratch.
            // A builder may continue adding references AFTER Reset, so Reset's own Clear is not sufficient.
            _spare.Clear();
            _refreshing = false;
        }
    }

    private List<T> BuildNext()
    {
        var target = _spare;
        target.Clear();
        _build(target);
        return target;
    }
}
