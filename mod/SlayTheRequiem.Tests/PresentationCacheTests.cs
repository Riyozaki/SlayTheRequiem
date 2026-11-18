using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

/// <summary>Actual production cache/arithmetic, not Godot events, canvas/frame-time or native-load acceptance.</summary>
public class PresentationCacheTests
{
    [Fact]
    public void A_clean_visible_frame_does_not_collect_sort_hash_or_allocate_a_replacement()
    {
        var builds = 0;
        var expected = new[] { 2, 4, 7 };
        var view = new InvalidatedView<int[]>([]);
        Func<int[]> build = () => { builds++; return expected; };
        Assert.True(view.Refresh(build));
        for (var i = 0; i < 10000; i++) Assert.False(view.Refresh(build));
        Assert.Equal(1, builds);
        Assert.Same(expected, view.Value);
        Assert.False(view.IsDirty);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    public void Same_frame_event_bursts_are_coalesced(int events)
    {
        var view = new InvalidatedView<int>(0);
        var builds = 0;
        Func<int> build = () => ++builds;
        view.Refresh(build);
        for (var i = 0; i < events; i++) view.Invalidate();
        Assert.True(view.Refresh(build));
        Assert.False(view.Refresh(build));
        Assert.Equal(2, builds);
        Assert.Equal(2, view.Value);
    }

    [Fact]
    public void An_invalidation_raised_inside_the_builder_is_not_lost()
    {
        var view = new InvalidatedView<int>(0);
        Assert.True(view.Refresh(() => { view.Invalidate(); return 1; }));
        Assert.True(view.IsDirty);
        Assert.Equal(1, view.Value);
        Assert.True(view.Refresh(() => 2));
        Assert.False(view.IsDirty);
        Assert.Equal(2, view.Value);
    }

    [Fact]
    public void A_failed_builder_does_not_publish_partial_state_and_can_retry()
    {
        var original = new[] { 9 };
        var view = new InvalidatedView<int[]>(original);
        Assert.Throws<InvalidOperationException>(() => view.Refresh(() => throw new InvalidOperationException("fixture")));
        Assert.Same(original, view.Value);
        Assert.True(view.IsDirty);
        Assert.True(view.Refresh(() => [3, 8]));
        Assert.Equal(new[] { 3, 8 }, view.Value);
    }

    [Fact]
    public void Reentrant_refresh_cannot_recursively_rebuild_or_erase_an_event()
    {
        var view = new InvalidatedView<int>(0);
        Assert.True(view.Refresh(() =>
        {
            view.Invalidate();
            Assert.False(view.Refresh(() => throw new Exception("must not enter a second builder")));
            return 1;
        }));
        Assert.True(view.IsDirty);
        Assert.True(view.Refresh(() => 2));
    }

    [Fact]
    public void Rebind_during_a_builder_never_publishes_the_old_owners_view()
    {
        var view = new InvalidatedView<string[]>(["old"]);
        Assert.False(view.Refresh(() => { view.Reset(["new owner"]); return ["stale old owner"]; }));
        Assert.Equal(new[] { "new owner" }, view.Value);
        Assert.True(view.IsDirty);
        Assert.True(view.Refresh(() => ["new owner's powers"]));
        Assert.Equal(new[] { "new owner's powers" }, view.Value);
    }

    [Fact]
    public void A_hundred_close_rebind_cycles_start_dirty_without_sharing_views()
    {
        var view = new InvalidatedView<int[]>([]);
        int[]? previous = null;
        for (var owner = 0; owner < 100; owner++)
        {
            view.Reset([]);
            Assert.Empty(view.Value);
            Assert.True(view.IsDirty);
            var captured = owner;
            Assert.True(view.Refresh(() => [captured]));
            Assert.Equal(owner, Assert.Single(view.Value));
            Assert.NotSame(previous, view.Value);
            previous = view.Value;
        }
    }

    [Fact]
    public void Equipment_or_loaded_model_events_change_numbers_remove_zero_and_replace_identity()
    {
        var powers = new List<(object Identity, int Amount)> { (new object(), 3), (new object(), 2) };
        var view = new InvalidatedView<(object Identity, int Amount)[]>([]);
        Func<(object Identity, int Amount)[]> build = () => powers.Where(p => p.Amount != 0).ToArray();
        view.Refresh(build);
        var old = view.Value;
        powers[0] = (powers[0].Identity, 6); view.Invalidate();
        powers[1] = (powers[1].Identity, 0); view.Invalidate();
        view.Refresh(build);
        Assert.Equal(3, old[0].Amount); // immutable published numeric snapshot
        Assert.Equal(6, Assert.Single(view.Value).Amount);
        var loaded = new object();
        powers.Clear(); powers.Add((loaded, 4)); view.Reset([]);
        view.Refresh(build);
        Assert.Same(loaded, Assert.Single(view.Value).Identity);
        Assert.Equal(4, Assert.Single(view.Value).Amount);
    }

    [Fact]
    public void Independent_creatures_do_not_invalidate_each_other()
    {
        var a = new InvalidatedView<int>(0); var b = new InvalidatedView<int>(0);
        a.Refresh(() => 1); b.Refresh(() => 7);
        a.Invalidate();
        Assert.True(a.Refresh(() => 2));
        Assert.False(b.Refresh(() => throw new Exception("not this creature")));
        Assert.Equal(7, b.Value);
    }
}
