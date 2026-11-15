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

    [Fact]
    public void JewelryNumbers()
    {
        Assert.Equal(3, JewelryRules.DrunkardPotency(2));   // +50 % вверх
        Assert.Equal(15, JewelryRules.DrunkardPotency(10));
        Assert.Equal(0, JewelryRules.DrunkardPotency(0));
        Assert.Equal(-2, JewelryRules.DrunkardPotency(-2));
        Assert.Equal(3, JewelryRules.LightningRodMana(7));   // половина вниз
        Assert.Equal(0, JewelryRules.LightningRodMana(-5));
        Assert.True(JewelryRules.RegenerationActive(19, 40));
        Assert.False(JewelryRules.RegenerationActive(20, 40));
        Assert.True(JewelryRules.StarstoneTriggers(0, false));
        Assert.False(JewelryRules.StarstoneTriggers(0, true));
        Assert.False(JewelryRules.StarstoneTriggers(1, false));
        Assert.Equal(7, JewelryRules.SteamDamage(4, 3));
        Assert.Equal(0, JewelryRules.SteamDamage(4, 0));
        Assert.Equal(1, JewelryRules.ViceroyReplays(false));
        Assert.Equal(2, JewelryRules.ViceroyReplays(true));
    }

    [Fact]
    public void RingCombos()
    {
        Assert.Empty(RingComboRules.Active(["RingOfThePyromancer"]));
        Assert.Equal(new[] { RingCombo.Steam }, RingComboRules.Active(["RingOfHoarfrost", "RingOfThePyromancer"]));
        Assert.Equal(new[] { RingCombo.QuickFury }, RingComboRules.Active(["RingOfFuryStrike", "RingOfQuickBreath"]));
        Assert.Equal(new[] { RingCombo.ClearViceroy }, RingComboRules.Involving("RingOfTheViceroy"));
        var mask = RingComboRules.Mark(0, [RingCombo.Steam]);
        Assert.True(RingComboRules.Known(mask, RingCombo.Steam));
        Assert.False(RingComboRules.Known(mask, RingCombo.QuickFury));
        Assert.Equal(mask, RingComboRules.Mark(mask, [RingCombo.Steam]));
    }

    [Fact]
    public void JewelryReplacementsShipped()
    {
        var table = VanillaRelicTable.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vanilla_relics.csv")));
        Assert.True(table.IsCut("BloodVial"));
        Assert.True(table.IsCut("MeatOnTheBone"));
        Assert.Equal(6, table.CountOf(RelicBucket.Jewelry));
    }
}

/// <summary>Реликвии рюкзака (07 §7 «Прочее», M4 п. 5).</summary>
public class BackpackRulesTests
{
    [Fact]
    public void KettleBoostsOnlyPresentEffects()
    {
        var stew = FoodRules.Boosted(FoodRules.Effect(Dish.BloodmossStew));
        Assert.Equal(7, stew.MaxHp);
        Assert.Equal(0, stew.StaminaRegen);
        var sausage = FoodRules.Boosted(FoodRules.Effect(Dish.SpicedSausage));
        Assert.Equal(2, sausage.FrostResistance);
        Assert.Equal(3, sausage.StartStamina);
        Assert.Equal(new DishEffect(), FoodRules.Boosted(new DishEffect()));
    }

    [Fact]
    public void BarrowMapIndicesWrap()
    {
        Assert.Equal(new[] { 2, 0, 1 }, BackpackRules.UpcomingIndices(2, 3));
        Assert.Equal(new[] { 1, 0 }, BackpackRules.UpcomingIndices(3, 2));
        Assert.Empty(BackpackRules.UpcomingIndices(0, 0));
    }

    [Fact]
    public void HangoverRolls()
    {
        Assert.Equal(5, BackpackRules.Outcomes.Length);
        Assert.Equal(Hangover.Heal, BackpackRules.Roll(0));
        Assert.Equal(Hangover.Headache, BackpackRules.Roll(99));
        Assert.Equal(8, BackpackRules.FeatherBurden);
    }
}
