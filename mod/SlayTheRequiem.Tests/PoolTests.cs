using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

/// <summary>Пулы карт (docs/design/19): разбор requiem_pools.csv и формула доли своих карт.</summary>
public class PoolTableTests
{
    [Fact]
    public void ParsesShippedCsv()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "requiem_pools.csv");
        var table = PoolTable.Parse(File.ReadAllText(path));
        Assert.True(table.IsCut("Ironclad", "StoneArmor"));
        Assert.True(table.IsCut("Regent", "VoidForm"));
        Assert.False(table.IsCut("Regent", "SovereignBlade"));
        Assert.False(table.IsCut("Silent", "StoneArmor")); // изъятие — по пулу
        Assert.Contains(("Necrobinder", "BlightStrike"), table.Gated("Pact"));
        Assert.Empty(table.Gated("Nope"));
    }

    [Fact]
    public void UnknownDecisionFails() =>
        Assert.Throws<FormatException>(() => PoolTable.Parse("pool,key,decision,gate,criterion\nRegent,X,maybe,,"));

    [Theory]
    [InlineData("IroncladCardPool", "Ironclad")]
    [InlineData("ColorlessCardPool", "Colorless")]
    [InlineData("Regent", "Regent")]
    public void PoolKey(string type, string expected) => Assert.Equal(expected, PoolTable.PoolKey(type));

    [Theory]
    [InlineData(12, 68)]
    [InlineData(20, 77)]
    [InlineData(1, 30)]
    public void ReplaceChanceReachesTarget(int own, int vanilla)
    {
        var p = PoolRules.OwnReplaceChance(own, vanilla, 30);
        var f = own / (double)(own + vanilla);
        Assert.Equal(0.30, f + (1 - f) * p, 6); // доля своих после замены
    }

    [Theory]
    [InlineData(0, 50)]   // своих нет
    [InlineData(50, 0)]   // ванильных нет
    [InlineData(40, 60)]  // уже больше цели
    public void ReplaceChanceZero(int own, int vanilla) => Assert.Equal(0, PoolRules.OwnReplaceChance(own, vanilla, 30));
}
