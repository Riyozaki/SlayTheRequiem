namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Bounded opt-in timing samples. No allocation/logging per sample after construction.</summary>
public sealed class MetricWindow(int capacity = 256)
{
    private readonly double[] _values = new double[capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity))];
    private int _next, _count;
    public void Add(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0) return;
        _values[_next] = milliseconds;
        _next = (_next + 1) % _values.Length;
        _count = Math.Min(_count + 1, _values.Length);
    }
    public void Clear() { _next = _count = 0; }
    public MetricSummary Snapshot()
    {
        if (_count == 0) return new(0, 0, 0, 0);
        var sorted = _values.Take(_count).Order().ToArray();
        return new(_count, sorted[(int)Math.Ceiling(_count * 0.5) - 1], sorted[(int)Math.Ceiling(_count * 0.95) - 1], sorted[^1]);
    }
}
public sealed record MetricSummary(int Samples, double MedianMs, double P95Ms, double MaxMs);
