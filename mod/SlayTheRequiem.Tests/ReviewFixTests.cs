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
        data.Personal[76561198000000001UL] = new PersonalChoices(2, 0, 6, 3);
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
        Assert.Equal(new PersonalChoices(2, 0, 6, 3), back.ChoicesFor(76561198000000001UL));
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
        var back = RunSettingsData.Decode("v=9;hp=95;future=3;p=7:1,2,3,4|bad|8:1,2")!;
        Assert.Equal(95, back.EnemyHpPercent);
        Assert.True(back.FiveActs);
        Assert.Equal(new PersonalChoices(1, 2, 3, 4), back.ChoicesFor(7));
        Assert.Equal(PersonalChoices.Random, back.ChoicesFor(8));
    }
}
