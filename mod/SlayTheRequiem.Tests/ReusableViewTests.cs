using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

/// <summary>Actual double-buffer production helper; not native power subscriptions or Godot leak acceptance.</summary>
public class ReusableViewTests
{
    [Fact]
    public void Dirty_rebuilds_reuse_two_buffers_and_clean_frames_do_not_enter_the_builder()
    {
        var calls = 0;
        var view = new InvalidatedListView<int>(target => target.Add(++calls));
        Assert.True(view.Refresh());
        var first = view.Value;
        for (var i = 0; i < 1000; i++) Assert.False(view.Refresh());
        Assert.Equal(1, calls);
        view.Invalidate(); Assert.True(view.Refresh());
        var second = view.Value;
        Assert.NotSame(first, second);
        Assert.Empty(first); // a borrowed view is deliberately not a persistent snapshot
        Assert.Equal(2, Assert.Single(second));
        view.Invalidate(); Assert.True(view.Refresh());
        Assert.Same(first, view.Value);
        Assert.Equal(3, Assert.Single(view.Value));
        Assert.Empty(second);
    }

    [Fact]
    public void A_partial_failed_build_preserves_the_published_list_then_retries_from_empty()
    {
        var fail = false;
        var view = new InvalidatedListView<int>(target =>
        {
            target.Add(5);
            if (fail) throw new InvalidOperationException("synthetic builder failure");
            target.Add(8);
        });
        view.Refresh(); var original = view.Value;
        fail = true; view.Invalidate();
        Assert.Throws<InvalidOperationException>(() => view.Refresh());
        Assert.Same(original, view.Value);
        Assert.Equal(new[] { 5, 8 }, view.Value);
        Assert.True(view.IsDirty);
        fail = false;
        Assert.True(view.Refresh());
        Assert.Equal(new[] { 5, 8 }, view.Value); // no partial duplicate 5 from the failed attempt
        Assert.Empty(original);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_failed_rebuild_releases_unpublished_references_immediately_even_after_reset(bool resetDuringBuild)
    {
        InvalidatedListView<object> view = null!;
        List<object> scratch = null!; // test instrumentation only; production builders must not retain this buffer
        var fail = false;
        var owner = new object();
        var stale = new object();
        view = new(target =>
        {
            scratch = target;
            target.Add(owner);
            if (!fail) return;
            if (resetDuringBuild) view.Reset();
            target.Add(stale); // also exercise a continuation AFTER Reset cleared the buffer
            view.Invalidate();
            Assert.False(view.Refresh());
            Assert.Same(stale, target[^1]); // nested refresh must not clean an outer builder's live buffer
            throw new InvalidOperationException("synthetic retained-reference regression");
        });
        Assert.True(view.Refresh());
        var published = view.Value;
        fail = true; view.Invalidate();
        Assert.Throws<InvalidOperationException>(() => view.Refresh());
        var rejected = scratch;
        Assert.NotSame(published, rejected);
        Assert.Empty(rejected); // no later Reset/Refresh/GC is needed to release these element references
        Assert.Same(published, view.Value);
        if (resetDuringBuild) Assert.Empty(view.Value);
        else Assert.Same(owner, Assert.Single(view.Value));
        Assert.True(view.IsDirty);
        fail = false;
        Assert.True(view.Refresh());
        Assert.Same(rejected, view.Value); // capacity/buffer is still reused, not discarded to avoid cleanup
        Assert.Same(owner, Assert.Single(view.Value));
        Assert.Empty(published);
    }

    [Fact]
    public void Reset_during_build_does_not_publish_or_retain_the_old_owners_buffer()
    {
        InvalidatedListView<string> view = null!;
        List<string> scratch = null!; // inspect rejected storage, not only the public view
        var rebind = true;
        view = new(target =>
        {
            scratch = target;
            if (rebind)
            {
                target.Add("old owner");
                view.Reset();
                target.Add("stale continuation");
            }
            else target.Add("new owner");
        });
        Assert.False(view.Refresh());
        Assert.Empty(view.Value);
        Assert.Empty(scratch); // a rejected non-throwing builder must release post-Reset additions as well
        Assert.True(view.IsDirty);
        rebind = false;
        Assert.True(view.Refresh());
        Assert.Equal("new owner", Assert.Single(view.Value));
    }

    [Fact]
    public void A_reset_followed_by_a_builder_exception_is_still_empty_and_retryable()
    {
        InvalidatedListView<object> view = null!;
        var fail = true;
        view = new(target =>
        {
            target.Add(new object());
            if (fail) { view.Reset(); throw new InvalidOperationException("after reset"); }
        });
        Assert.Throws<InvalidOperationException>(() => view.Refresh());
        Assert.Empty(view.Value);
        Assert.True(view.IsDirty);
        fail = false;
        Assert.True(view.Refresh());
        Assert.Single(view.Value);
    }

    [Fact]
    public void Events_inside_a_build_coalesce_without_reentrant_partial_publication()
    {
        InvalidatedListView<int> view = null!;
        var calls = 0;
        view = new(target =>
        {
            target.Add(++calls);
            if (calls != 1) return;
            view.Invalidate(); view.Invalidate();
            Assert.False(view.Refresh());
            Assert.Empty(view.Value);
        });
        Assert.True(view.Refresh());
        Assert.Equal(1, Assert.Single(view.Value));
        Assert.True(view.IsDirty);
        Assert.True(view.Refresh());
        Assert.Equal(2, Assert.Single(view.Value));
        Assert.False(view.IsDirty);
        Assert.False(view.Refresh());
    }

    [Fact]
    public void Reset_clears_both_buffers_and_hundred_rebinds_do_not_mix_creatures()
    {
        var owner = new object();
        var view = new InvalidatedListView<object>(target => target.Add(owner));
        view.Refresh(); var a = view.Value;
        view.Invalidate(); view.Refresh(); var b = view.Value;
        view.Reset();
        Assert.Empty(a); Assert.Empty(b); Assert.Empty(view.Value);
        for (var cycle = 0; cycle < 100; cycle++)
        {
            owner = new object();
            view.Reset();
            Assert.Empty(view.Value);
            Assert.True(view.Refresh());
            Assert.Same(owner, Assert.Single(view.Value));
            Assert.True(ReferenceEquals(view.Value, a) || ReferenceEquals(view.Value, b));
        }
    }

    [Fact]
    public void Growth_shrink_zero_and_sort_use_current_membership_without_replacement_collections()
    {
        var powers = new List<int> { 4, 0, 7, 2 };
        var view = new InvalidatedListView<int>(target =>
        {
            foreach (var amount in powers) if (amount != 0) target.Add(amount);
            target.Sort();
        });
        view.Refresh(); Assert.Equal(new[] { 2, 4, 7 }, view.Value);
        powers.Clear(); for (var i = 100; i >= 1; i--) powers.Add(i);
        view.Invalidate(); view.Refresh(); Assert.Equal(Enumerable.Range(1, 100), view.Value);
        powers.Clear(); view.Invalidate(); view.Refresh(); Assert.Empty(view.Value);
        powers.Add(6); view.Invalidate(); view.Refresh(); Assert.Equal(6, Assert.Single(view.Value));
    }

    [Fact]
    public void Independent_views_do_not_share_buffers_or_invalidation()
    {
        var a = new InvalidatedListView<int>(items => items.Add(1));
        var b = new InvalidatedListView<int>(items => items.Add(9));
        a.Refresh(); b.Refresh();
        Assert.NotSame(a.Value, b.Value);
        a.Reset();
        Assert.Empty(a.Value);
        Assert.Equal(9, Assert.Single(b.Value));
        Assert.False(b.Refresh());
    }
}
