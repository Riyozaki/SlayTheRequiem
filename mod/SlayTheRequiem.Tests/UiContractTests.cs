using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

/// <summary>Layout arithmetic used by the camp container, NOT screenshots/Godot focus/input acceptance.</summary>
public class UiContractTests
{
    [Theory]
    [InlineData(640, 190, 3)]
    [InlineData(1000, 220, 4)]
    [InlineData(1180, 190, 5)]
    [InlineData(400, 500, 1)]
    [InlineData(0, 0, 1)]
    public void Camp_wraps_columns_instead_of_shrinking_unbounded_rows(double width, double cell, int expected) =>
        Assert.Equal(expected, UiLayoutRules.Columns(width, cell));

    [Theory]
    [InlineData(0, 600, 900, 400, 1000, 500)]
    [InlineData(500, 100, 390, 400, 1000, 100)]
    [InlineData(500, 540, 700, 400, 1000, 500)]
    [InlineData(50, 0, 120, 400, 120, 0)]
    [InlineData(-20, 0, 200, 400, 1000, 0)]
    public void Focus_scroll_is_bounded_and_keeps_the_whole_cost_row_visible(double offset, double top, double bottom,
        double height, double content, double expected) =>
        Assert.Equal(expected, UiLayoutRules.ScrollIntoView(offset, top, bottom, height, content));

    [Theory]
    [InlineData(1280, 720, 1)]
    [InlineData(1920, 1080, 1.5)]
    [InlineData(2560, 1440, 2)]
    public void Camp_can_reach_every_row_at_small_logical_viewports(double width, double height, double uiScale)
    {
        var logicalWidth = width / uiScale * .9;
        var logicalHeight = height / uiScale * .47;
        var columns = UiLayoutRules.Columns(logicalWidth, 220);
        var content = 5 * (36 + Math.Ceiling(8d / columns) * 260);
        double offset = 0;
        for (var y = 36d; y < content - 260; y += 260)
        {
            offset = UiLayoutRules.ScrollIntoView(offset, y, y + 242, logicalHeight, content);
            Assert.InRange(offset, 0, Math.Max(0, content - logicalHeight));
            Assert.True(y >= offset && y + 242 <= offset + logicalHeight);
        }
    }

    [Fact]
    public void Shelf_ui_snapshot_cannot_remove_or_change_live_entitlements()
    {
        var shelf = new CooldownShelf<object, object>();
        var combat = new object(); var other = new object(); var card = new object();
        Assert.True(shelf.Put(card, combat, 3));
        var before = shelf.Snapshot(combat);
        Assert.Empty(shelf.Snapshot(other));
        Assert.Same(card, Assert.Single(before).Card);
        shelf.Tick(combat);
        Assert.Equal(3, before[0].Turns);
        Assert.Equal(2, Assert.Single(shelf.Snapshot(combat)).Turns);
        Assert.True(shelf.Contains(card));
    }
    [Fact]
    public void Third_identical_ring_displaces_only_the_oldest_physical_slot()
    {
        var first = new EquipItem("SameRing", EquipSlot.Ring, 1);
        var second = first with { };
        Assert.Equal(first, second); Assert.NotSame(first, second);
        Assert.Equal(new[] { 0 }, EquipmentRules.DisplacedIndices([first, second], first with { }));
        Assert.Same(first, Assert.Single(EquipmentRules.Displaced([first, second], first with { })));
        Assert.Equal(new[] { 0, 1 }, EquipmentRules.DisplacedIndices([first, second, first with { }], first with { }));
    }

}
