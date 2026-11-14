using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

public class StaminaRulesTests
{
    [Theory]
    [InlineData(8, 8, 2, StaminaState.Rested)]
    [InlineData(4, 8, 2, StaminaState.Rested)]   // ровно половина — ещё бодр
    [InlineData(3, 8, 2, StaminaState.Tired)]
    [InlineData(1, 8, 2, StaminaState.Exhausted)]
    [InlineData(0, 8, 0, StaminaState.Tired)]    // 0-цена при пустом пуле: устал, но не изнурён
    public void StateFor(int current, int pool, int cost, StaminaState expected) =>
        Assert.Equal(expected, StaminaRules.StateFor(current, pool, cost));

    [Fact]
    public void OverstrainCostsHp()
    {
        var p = StaminaRules.Pay(current: 1, pool: 8, cost: 3);
        Assert.Equal(1, p.Spent);
        Assert.Equal(2, p.Shortfall);
        Assert.Equal(4, p.HpLoss);
        Assert.Equal(0.5m, p.DamageMultiplier);
    }

    [Fact]
    public void RegenIsCappedByPool() => Assert.Equal(8, StaminaRules.Regenerate(7, 8, 3));
}

public class CombatMathTests
{
    [Theory]
    [InlineData(6, 2, 0, 0, 4)]
    [InlineData(2, 5, 0, 0, 1)]   // не ниже 1
    [InlineData(6, 3, 2, 0, 5)]   // Пробитие 2
    [InlineData(6, 3, 5, 0, 6)]   // Пробитие больше Лат
    [InlineData(6, 2, 0, 4, 8)]   // Раскол увёл Латы в −2
    [InlineData(0, 2, 0, 0, 0)]
    public void Plating(int dmg, int plating, int pierce, int sunder, int expected) =>
        Assert.Equal(expected, CombatMath.ApplyPlating(dmg, plating, pierce, sunder));

    [Theory]
    [InlineData(-1, 1.5)]
    [InlineData(0, 1.0)]
    [InlineData(2, 0.5)]
    [InlineData(5, 0.25)]  // кап III
    public void Resistance(int tier, double expected) =>
        Assert.Equal((decimal)expected, CombatMath.ResistanceMultiplier(tier));

    [Fact]
    public void ResistStacksRoundsDown() => Assert.Equal(4, CombatMath.ResistStacks(6, 1));
}

public class StatusMathTests
{
    [Fact]
    public void BurningExampleFromDoc() => Assert.Equal(22, StatusMath.BurningTotal(12)); // 12+6+3+1

    [Fact]
    public void PoisonExampleFromDoc() => Assert.Equal(78, StatusMath.PoisonTotal(12));

    [Theory]
    [InlineData(5, 0, 0, 5)]
    [InlineData(5, 4, 4, 3)]    // 4 Блока гасят 2 урона и сгорают
    [InlineData(5, 20, 10, 0)]  // много Блока: сгорает 10, урона нет
    public void BurningMeltsBlock(int dmg, int block, int blockLost, int hp) =>
        Assert.Equal((blockLost, hp), StatusMath.BurningVsBlock(dmg, block));

    [Fact]
    public void StaticOverload() => Assert.Equal((2, 3, 4), StatusMath.StaticTick(5, 2));
}

public class SpellAffinityTests
{
    [Theory]
    [InlineData(SpellTier.Novice, 3, CastState.Ease, 1)]
    [InlineData(SpellTier.Apprentice, 2, CastState.Free, 2)]
    [InlineData(SpellTier.Adept, 2, CastState.Clumsy, 3)]
    [InlineData(SpellTier.Adept, 1, CastState.Strained, 4)]
    [InlineData(SpellTier.Expert, 0, CastState.Impossible, 2)]
    public void GapTable(SpellTier tier, int mastery, CastState state, int mana)
    {
        var c = SpellAffinity.Evaluate(2, tier, mastery);
        Assert.Equal(state, c.State);
        Assert.Equal(mana, c.ManaCost);
    }

    [Fact]
    public void PlatebearerCanReadScrolls()
    {
        var c = SpellAffinity.Evaluate(4, SpellTier.Master, mastery: 0, fromScroll: true);
        Assert.True(c.Castable);
        Assert.Equal(0, c.ManaCost);
    }
}

public class StaminaTableTests
{
    [Fact]
    public void ParsesShippedCsv()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "vanilla_attacks_stamina.csv");
        var table = StaminaTable.Parse(File.ReadAllText(path));
        Assert.Equal(131, table.Count);
        Assert.True(table.TryGet("StrikeIronclad", out var strike));
        Assert.Equal(1, strike.Stamina);
    }

    [Fact]
    public void QuotedCells() =>
        Assert.Equal(new[] { "a", "b,c", "d\"e" }, StaminaTable.SplitCsvLine("a,\"b,c\",\"d\"\"e\""));
}
