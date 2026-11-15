using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

/// <summary>Переработка ванильных реликвий (docs/design/07 §7): разбор vanilla_relics.csv, вес, штраф прироста, бижутерия.</summary>
public class VanillaRelicTableTests
{
    private static VanillaRelicTable Shipped() =>
        VanillaRelicTable.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vanilla_relics.csv")));

    [Fact]
    public void ParsesShippedCsv()
    {
        var table = Shipped();
        Assert.True(table.Count >= 290);
        Assert.True(table.IsCut("Vajra"));
        Assert.True(table.IsCut("Anchor"));
        Assert.False(table.IsCut("PenNib"));
        Assert.Equal(1, table.Weight("PenNib"));
        Assert.Equal(1, table.Weight("IceCream"));
        Assert.Equal(0, table.Weight("BurningBlood"));
        Assert.Null(table.Weight("NoSuchRelic"));
        Assert.Equal(RelicChange.RegenMinusOne, table.Change("Sozu"));
        Assert.Equal(12, table.CountOf(RelicBucket.Rework));
        Assert.All(table.Cut, e => Assert.False(string.IsNullOrEmpty(e.Replacement)));
    }

    [Fact]
    public void RegenPenaltyByHero()
    {
        var table = Shipped();
        string[] relics = ["Sozu", "Ectoplasm", "PenNib"];
        Assert.Equal((2, 0), table.RegenPenalty("Ironclad", relics));
        Assert.Equal((2, 0), table.RegenPenalty("Silent", relics));
        Assert.Equal((0, 2), table.RegenPenalty("Regent", relics));
        Assert.Equal((0, 0), table.RegenPenalty("Regent", ["PenNib"]));
    }

    [Fact]
    public void BadRowsFail()
    {
        const string head = "key,pool,rarity,bucket,weight,change,replacement,note\n";
        Assert.Throws<FormatException>(() => VanillaRelicTable.Parse(head + "X,Shared,Common,maybe,1,,,"));
        Assert.Throws<FormatException>(() => VanillaRelicTable.Parse(head + "X,Shared,Common,rework,1,regen-9,,"));
        Assert.Equal(3, VanillaRelicTable.Parse(head + "X,Shared,Common,keep,9,,,").Weight("X")); // вес — в 0–3
    }

    [Fact]
    public void Jewelry()
    {
        Assert.True(JewelryRules.IronSkinActive(1));
        Assert.False(JewelryRules.IronSkinActive(0));
        Assert.Equal(1, JewelryRules.FuryStrikeDamage);
        Assert.Equal(2, JewelryRules.ThornsNecklace);
    }
}
