using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

/// <summary>M5: «две двери» перед актами 2 и 4 (зеркало — tools/sim, класс ActDoors).</summary>
public class ActDoorTests
{
    [Theory]
    [InlineData(1, 5, true)]
    [InlineData(3, 5, true)]
    [InlineData(3, 6, true)]
    [InlineData(2, 5, false)]
    [InlineData(0, 5, false)]
    [InlineData(1, 3, false)]
    public void HasDoors(int actIndex, int actCount, bool expected) =>
        Assert.Equal(expected, ActDoorRules.HasDoors(actIndex, actCount));

    [Fact]
    public void Options()
    {
        Assert.Equal([ActKind.FrozenBarrows, ActKind.ClockworkDepths], ActDoorRules.Options(1));
        Assert.Equal([ActKind.BloodCourt, ActKind.StarTower], ActDoorRules.Options(3));
        Assert.Empty(ActDoorRules.Options(2));
        Assert.Equal(1, ActDoorRules.DoorOf(3, ActKind.StarTower));
        Assert.Equal(-1, ActDoorRules.DoorOf(1, ActKind.StarTower));
    }

    [Theory]
    [InlineData(new[] { 0, 0, 1 }, 1, 1, 0)]
    [InlineData(new[] { 1 }, 0, 0, 1)]
    [InlineData(new[] { -1, -1 }, 1, 0, 1)]
    [InlineData(new int[0], 0, 1, 0)]
    [InlineData(new[] { 0, 1 }, 0, 1, 1)]
    [InlineData(new[] { 0, 1, -1, -1 }, 1, 0, 0)]
    [InlineData(new[] { 1, -1 }, 0, 0, 1)]
    public void Resolve(int[] votes, int current, int tieBreak, int expected) =>
        Assert.Equal(expected, ActDoorRules.Resolve(votes, current, tieBreak));

    [Fact]
    public void TieBreakIsStable()
    {
        Assert.Equal(ActDoorRules.TieBreak("ABC123", 1), ActDoorRules.TieBreak("ABC123", 1));
        Assert.InRange(ActDoorRules.TieBreak("SEED", 3), 0, 1);
        // Совпадает с зеркалом tools/sim/requiem_rules.py (door_tie_break).
        Assert.Equal(1, ActDoorRules.TieBreak("ABC123", 1));
        Assert.Equal(0, ActDoorRules.TieBreak("SEED", 3));
    }

    [Fact]
    public void AllVoted()
    {
        Assert.True(ActDoorRules.AllVoted(2, 2));
        Assert.False(ActDoorRules.AllVoted(1, 2));
        Assert.False(ActDoorRules.AllVoted(0, 0));
    }
}
