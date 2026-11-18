using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

/// <summary>Исправления по ревью docs/review/full-review-2026-11.md (F-03 и далее).</summary>
public class RunSettingsDataTests
{
    [Fact]
    public void DefaultsRoundTrip()
    {
        var data = new RunSettingsData();
        var back = RunSettingsData.Decode(data.Encode());
        Assert.NotNull(back);
        Assert.Equal(data.Encode(), back!.Encode());
        Assert.True(back.EnableRequiemSystems);
        Assert.Equal(110, back.EnemyHpPercent);
        Assert.Equal(-1, back.RequiemLevelSetting);
    }

    [Fact]
    public void ValuesAndPersonalChoicesRoundTrip()
    {
        var data = new RunSettingsData
        {
            EnableRequiemSystems = false,
            EnemyHpPercent = 135,
            FiveActs = false,
            ShortRun = true,
            ActVariant = 1,
            RequiemLevelSetting = 7,
            Superbosses = 2,
            AllowTheft = true,
            CampBuildingsUnlocked = 3,
            MemoryStoneCandles = 0,
        };
        data.Personal[76561198000000001UL] = new PersonalChoices(2, 0, 5, 3);
        data.Personal[5] = new PersonalChoices(0, 1, 0, 4);
        var back = RunSettingsData.Decode(data.Encode())!;
        Assert.False(back.EnableRequiemSystems);
        Assert.Equal(135, back.EnemyHpPercent);
        Assert.False(back.FiveActs);
        Assert.True(back.ShortRun);
        Assert.Equal(1, back.ActVariant);
        Assert.Equal(7, back.RequiemLevelSetting);
        Assert.Equal(2, back.Superbosses);
        Assert.True(back.AllowTheft);
        Assert.Equal(3, back.CampBuildingsUnlocked);
        Assert.Equal(0, back.MemoryStoneCandles);
        Assert.Equal(new PersonalChoices(2, 0, 5, 3), back.ChoicesFor(76561198000000001UL));
        Assert.Equal(new PersonalChoices(0, 1, 0, 4), back.ChoicesFor(5));
        Assert.Equal(PersonalChoices.Random, back.ChoicesFor(42));
    }

    [Fact]
    public void EncodingIsOrderIndependent()
    {
        var a = new RunSettingsData();
        a.Personal[9] = new PersonalChoices(1, 1, 1, 1);
        a.Personal[3] = new PersonalChoices(2, 2, 2, 2);
        var b = new RunSettingsData();
        b.Personal[3] = new PersonalChoices(2, 2, 2, 2);
        b.Personal[9] = new PersonalChoices(1, 1, 1, 1);
        Assert.Equal(a.Encode(), b.Encode());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("sys=1;hp=120")]
    public void RejectsForeignText(string? text) => Assert.Null(RunSettingsData.Decode(text));

    [Fact]
    public void UnknownKeysAndMissingKeysAreTolerated()
    {
        var back = RunSettingsData.Decode("v=1;hp=95;future=3;p=7:1,2,3,4")!;
        Assert.Equal(95, back.EnemyHpPercent);
        Assert.True(back.FiveActs);
        Assert.Equal(new PersonalChoices(1, 2, 3, 4), back.ChoicesFor(7));
        Assert.Equal(PersonalChoices.Random, back.ChoicesFor(8));
    }
}

/// <summary>Ревью F-07: старт боя — пул/2, затем прирост хода (02 §2).</summary>
public class ManaStartTests
{
    [Theory]
    [InlineData(10, 2, 7)] // Регент
    [InlineData(5, 1, 3)] // Безмолвная
    [InlineData(3, 1, 2)] // Латоносец
    [InlineData(0, 1, 0)]
    public void FirstTurn(int pool, int regen, int expected)
    {
        var start = ManaRules.StartAmount(pool);
        Assert.Equal(expected, start + ManaRules.RegenGain(start, pool, regen));
    }
}

/// <summary>Ревью F-04: «Устал» для карт-наград за усталость — ◆ меньше половины, в том числе 0.</summary>
public class WearyPayoffTests
{
    [Theory]
    [InlineData(0, 8, StaminaState.Tired)]
    [InlineData(3, 8, StaminaState.Tired)]
    [InlineData(4, 8, StaminaState.Rested)]
    public void ZeroCostState(int current, int pool, StaminaState expected) =>
        Assert.Equal(expected, StaminaRules.StateFor(current, pool, 0));
}

/// <summary>Ревью F-05: «2» у ванили значит «последний акт».</summary>
public class FinalAwareTierTests
{
    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(1, 3, 1)]
    [InlineData(2, 3, 2)]
    [InlineData(0, 5, 0)]
    [InlineData(1, 5, 1)]
    [InlineData(2, 5, 1)]
    [InlineData(3, 5, 1)]
    [InlineData(4, 5, 2)]
    public void Tier(int index, int count, int expected) => Assert.Equal(expected, ActRules.FinalAwareTier(index, count));
}

/// <summary>Ревью F-10: уязвимость стихии и ванильная Уязвимость не складываются (03).</summary>
public class ElementalVulnerabilityTests
{
    [Theory]
    [InlineData(-1, false, 1.5)]
    [InlineData(-1, true, 1.0)]
    [InlineData(1, true, 0.75)]
    [InlineData(2, false, 0.5)]
    [InlineData(0, true, 1.0)]
    public void Multiplier(int tier, bool vulnerable, double expected) =>
        Assert.Equal((decimal)expected, CombatMath.ElementalMultiplier(tier, vulnerable));
}
