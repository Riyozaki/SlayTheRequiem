using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

/// <summary>Этап 7: уровни «Реквием», пять актов, правила актов, аффиксы элит, супербоссы (зеркало — tools/sim).</summary>
public class RequiemLevelTests
{
    [Theory]
    [InlineData(-1, 0, 1)]
    [InlineData(-1, 20, 10)]
    [InlineData(0, 9, 0)]
    [InlineData(12, 0, 10)]
    public void Resolve(int setting, int ascension, int expected) =>
        Assert.Equal(expected, RequiemLevelRules.Resolve(setting, ascension));

    [Fact]
    public void Multipliers()
    {
        Assert.Equal(0.75m, RequiemLevelRules.DamageTakenMultiplier(0));
        Assert.Equal(1m, RequiemLevelRules.DamageTakenMultiplier(6));
        Assert.Equal(1.25m, RequiemLevelRules.DamageTakenMultiplier(7));
        Assert.Equal(0.8m, RequiemLevelRules.DamageDealtMultiplier(10));
        Assert.Equal(0.75m, RequiemLevelRules.EnemyHpMultiplier(0));
        Assert.False(RequiemLevelRules.CandlesAllowed(10));
        Assert.Equal(0, MemoryStoneRules.CandlesFor(10));
        Assert.True(RequiemLevelRules.SuperbossesMandatory(10));
    }

    [Theory]
    [InlineData(1, 10, 0, 1)]
    [InlineData(7, 10, 0, 3)]
    [InlineData(7, 60, 0, 2)]
    [InlineData(10, 10, 1, 4)]
    public void EliteAffixes(int level, int roll, int bonus, int expected) =>
        Assert.Equal(expected, EnemyRules.AffixCount(EnemyRank.Elite, 4, roll, 100, level, bonus));

    [Fact]
    public void EliteMinionsGetNoAffixes() => Assert.Equal(0, EnemyRules.AffixCount(EnemyRank.Elite, 4, 0, 30, 10, 1));

    [Fact]
    public void AffixPickKeepsOneElement()
    {
        var picked = AffixRules.Pick(3, new[] { 1, 1, 1 });
        Assert.Equal(EnemyAffix.Fiery, picked[0]);
        Assert.Single(picked, AffixRules.IsElemental);
        Assert.Equal(3, picked.Distinct().Count());
    }
}

public class ActRulesTests
{
    [Fact]
    public void Tiers()
    {
        Assert.Equal(new[] { 0, 1, 1, 2, 2 }, Enumerable.Range(0, 5).Select(i => ActRules.VanillaTier(i, 5)));
        Assert.Equal(2, ActRules.VanillaTier(5, 6));
        Assert.Equal(new[] { 0, 1, 2 }, Enumerable.Range(0, 3).Select(i => ActRules.VanillaTier(i, 3)));
        Assert.Equal(new[] { 0, 2, 4 }, Enumerable.Range(0, 3).Select(i => ActRules.SlotOf(i, 3)));
        Assert.Equal(new[] { 0, 2, 4 }, ActRules.Slots(shortRun: true));
    }

    [Fact]
    public void ActShape()
    {
        Assert.Equal(9, ActRules.MapWidth(ActKind.StarTower));
        Assert.Equal(7, ActRules.MapWidth(ActKind.Glory));
        Assert.Equal(35, ActRules.EnemyHpBonusPercent(ActKind.ClockworkDepths, true));
        Assert.Equal(0, ActRules.EnemyHpBonusPercent(ActKind.Glory, false));
        Assert.Equal(1, ActRules.EliteAffixBonus(ActKind.Glory, true));
        Assert.Equal(ActRule.DayNight, ActRules.RuleOf(ActKind.BloodCourt));
    }

    [Fact]
    public void NightAndOverheat()
    {
        Assert.Equal(new[] { false, false, false, true, true, true, false }, Enumerable.Range(1, 7).Select(ActRules.IsNight));
        Assert.True(ActRules.IsOverheatRound(8));
        Assert.True(ActRules.IsOverheatWarningRound(7));
        Assert.True(ActRules.IsHiddenByDarkness(5, 2, false));
        Assert.False(ActRules.IsHiddenByDarkness(5, 2, true));
    }

    [Fact]
    public void Gates()
    {
        Assert.True(ActRules.IsRiftTrigger(ActRule.AbyssRifts, 6));
        Assert.False(ActRules.IsRiftTrigger(ActRule.AbyssRifts, 4));
        Assert.True(ActRules.CanBecomeGate(ActRule.AbyssInvasion, 5, 4, false, false, false));
        Assert.False(ActRules.CanBecomeGate(ActRule.AbyssRifts, 5, 4, false, false, false));
        Assert.Equal(new List<int> { 2, 4 }, ActRules.PickDistinct(5, 2, new[] { 7, 7 }));
        Assert.Equal((4, 8, 12), MapMarks.Decode(MapMarks.Encode(4, 8, 12)));
    }

    [Fact]
    public void Superbosses()
    {
        Assert.Equal(700, ActRules.SuperbossHp(Superboss.NamelessShadow));
        Assert.Equal(Superboss.WorldEater, ActRules.SuperbossFor(ActKind.Glory));
        Assert.Null(ActRules.SuperbossFor(ActKind.NamelessLimit));
        var five = Enumerable.Range(0, 5).Aggregate(0, (m, b) => m | (1 << b));
        Assert.Equal(5, ActRules.CountSuperbosses(five | (1 << (int)Superboss.NamelessShadow)));
        Assert.True(ActRules.NamelessUnlocked(five, fullRun: true));
        Assert.False(ActRules.NamelessUnlocked(five, fullRun: false));
        Assert.True(ActRules.RollRareElite(14, false));
        Assert.False(ActRules.RollRareElite(14, true));
    }

    [Fact]
    public void WarmIsFree() => Assert.Equal(0, CampRules.Cost(CampAction.Warm, false));

    [Fact]
    public void SoulStonesBySlot() => Assert.Equal(3, CampRules.SoulStoneFor(EnemyRank.Elite, ActRules.SlotOf(2, 3)));
}
