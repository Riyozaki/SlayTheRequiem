namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Logical viewport contracts; no 1080p assumptions or shrinking text/buttons to fit an unbounded list.</summary>
public static class UiLayoutRules
{
    public static double PanelWidth(double viewport) => Math.Max(0, viewport) * 0.9;
    public static int Columns(double width, double cellWidth, double gap = 16) =>
        Math.Max(1, (int)Math.Floor((Math.Max(0, width) + Math.Max(0, gap)) / Math.Max(1, cellWidth + Math.Max(0, gap))));
    public static double ScrollIntoView(double offset, double top, double bottom, double height, double contentHeight)
    {
        var max = Math.Max(0, contentHeight - Math.Max(0, height));
        var result = Math.Clamp(offset, 0, max);
        if (top < result) result = top;
        else if (bottom > result + height) result = bottom - height;
        return Math.Clamp(result, 0, max);
    }
    public static int Neighbor(int index, int count, int columns, int dx, int dy)
    {
        if (count <= 0) return -1;
        index = Math.Clamp(index, 0, count - 1);
        columns = Math.Max(1, columns);
        var next = index + dx + dy * columns;
        if (next < 0) return count - 1;
        if (next >= count) return dy > 0 ? index % columns : 0;
        return next;
    }
}
