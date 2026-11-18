using System.Text.Json;
using SlayTheRequiem.Balance;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

public sealed class EconomyCalibrationTests
{
    private static EconomyRoute Full(int act, int revision = EconomyRules.CurrentRevision) => new(revision, act, 5, true, false);

    [Theory]
    [InlineData(0, 1, 2, 1, 2, 1, 1, 1)]
    [InlineData(1, 2, 3, 1, 2, 2, 2, 1)]
    [InlineData(2, 3, 4, 2, 3, 2, 3, 3)]
    [InlineData(3, 4, 5, 3, 4, 3, 4, 3)]
    [InlineData(4, 4, 5, 4, 5, 4, 5, 5)]
    public void Five_actual_stages_are_not_the_native_three_tiers(int act, int lo, int hi, int wanderLo, int wanderHi, int staffLo, int staffHi, int brew)
    {
        var route = Full(act);
        Assert.Equal((lo, hi), EconomyRules.EquipmentTiers(ShopType.Armorer, route));
        Assert.Equal((wanderLo, wanderHi), EconomyRules.EquipmentTiers(ShopType.Wanderer, route));
        Assert.Equal((staffLo, staffHi), EconomyRules.StaffTiers(route));
        Assert.Equal((lo, hi), EconomyRules.EquipmentTiers(ShopType.Jeweler, route));
        Assert.Equal((lo, hi), EconomyRules.EquipmentTiers(ShopType.Caravan, route));
        Assert.Equal((4, 5), EconomyRules.EquipmentTiers(ShopType.Fence, route));
        Assert.Equal(act <= 1 ? 1 : act <= 3 ? 2 : 3, EconomyRules.ScrollTier(route));
        Assert.Equal(EconomyRules.ScrollTier(route), EconomyRules.SoulStonePower(route));
        Assert.Equal(brew, EconomyRules.BrewTier(route));
        Assert.Equal(hi, EconomyRules.OrderTier(ShopType.Armorer, route));
        Assert.Equal(ShopRules.EquipmentPrice(EquipCategory.Weapon, hi), EconomyRules.OrderItemPrice(OrderCategory.Weapon, route));
        Assert.Equal(ShopRules.ReagentKitPrice(route.NativeTier), EconomyRules.ReagentKitPrice(route));
    }

    [Fact]
    public void Saved_legacy_short_and_vanilla_stock_remain_exactly_unchanged()
    {
        foreach (var route in Enumerable.Range(0, 5).Select(a => Full(a, 0))
            .Concat(Enumerable.Range(0, 3).Select(a => new EconomyRoute(1, a, 3, true, true)))
            .Concat(Enumerable.Range(0, 3).Select(a => new EconomyRoute(1, a, 3, false, false))))
        {
            Assert.False(route.UsesFiveStageStock);
            foreach (var shop in Enum.GetValues<ShopType>())
            {
                Assert.Equal(ShopRules.EquipmentTiers(shop, route.NativeTier), EconomyRules.EquipmentTiers(shop, route));
                Assert.Equal(ShopRules.OrderTier(shop, route.NativeTier), EconomyRules.OrderTier(shop, route));
            }
            Assert.Equal(ShopRules.BrewTier(route.NativeTier), EconomyRules.BrewTier(route));
            Assert.Equal(ShopRules.StaffTiers(route.NativeTier), EconomyRules.StaffTiers(route));
            Assert.Equal(ShopRules.ScrollTier(route.NativeTier), EconomyRules.ScrollTier(route));
            Assert.Equal(ShopRules.SoulStonePower(route.NativeTier), EconomyRules.SoulStonePower(route));
            foreach (var order in Enum.GetValues<OrderCategory>())
                Assert.Equal(ShopRules.OrderItemPrice(order, route.NativeTier), EconomyRules.OrderItemPrice(order, route));
        }
        Assert.False(new EconomyRoute(1, 1, 3, false, true).ExplicitShort);
        Assert.Equal(2, new EconomyRoute(1, 1, 3, true, true).Slot);
        Assert.Equal(4, new EconomyRoute(1, 5, 6, true, false).Slot);
    }

    [Theory]
    [InlineData(ActKind.FrozenBarrows, EnemyRank.Boss, true, false, 0, 35)]
    [InlineData(ActKind.FrozenBarrows, EnemyRank.Boss, true, false, 1, 0)]
    [InlineData(ActKind.FrozenBarrows, EnemyRank.Boss, false, false, 1, 35)]
    [InlineData(ActKind.ClockworkDepths, EnemyRank.Elite, true, false, 1, 35)]
    [InlineData(ActKind.BloodCourt, EnemyRank.Normal, false, true, 1, 45)]
    [InlineData(ActKind.BloodCourt, EnemyRank.Elite, false, true, 1, 45)]
    [InlineData(ActKind.BloodCourt, EnemyRank.Boss, true, true, 1, 0)]
    [InlineData(ActKind.StarTower, EnemyRank.Boss, true, false, 1, 0)]
    [InlineData(ActKind.Glory, EnemyRank.Boss, false, false, 1, 15)]
    public void Only_authored_bosses_lose_the_accidental_second_act_budget(ActKind kind, EnemyRank rank, bool authored, bool night, int revision, int expected) =>
        Assert.Equal(expected, EconomyRules.ActHpBonusPercent(kind, true, rank, authored, night, revision));

    [Fact]
    public void User_HP_and_difficulty_are_applied_once_not_deleted_by_the_fix()
    {
        var old = EconomyRules.ActHpBonusPercent(ActKind.FrozenBarrows, true, EnemyRank.Boss, true, false, 0);
        var tuned = EconomyRules.ActHpBonusPercent(ActKind.FrozenBarrows, true, EnemyRank.Boss, true, false, 1);
        Assert.Equal(446, EnemyRules.ScaledMaxHp(300, EconomyRules.EnemyHpMultiplier(110, 1, old)));
        Assert.Equal(330, EnemyRules.ScaledMaxHp(300, EconomyRules.EnemyHpMultiplier(110, 1, tuned)));
        Assert.Equal(248, EnemyRules.ScaledMaxHp(300, EconomyRules.EnemyHpMultiplier(110, 0, tuned)));
        foreach (var level in Enumerable.Range(0, 11))
            Assert.Equal(1.10m * RequiemLevelRules.EnemyHpMultiplier(level), EconomyRules.EnemyHpMultiplier(110, level, 0));
    }

    [Theory]
    [InlineData("v=1;hp=110", 0)]
    [InlineData("v=1;hp=110;economy=0", 0)]
    [InlineData("v=1;hp=110;economy=1", 1)]
    public void Continue_does_not_upgrade_a_saved_economy(string code, int revision)
    {
        var settings = RunSettingsData.Decode(code)!;
        Assert.Equal(revision, settings.EconomyRevision);
        Assert.Equal(revision, settings.Clone().EconomyRevision);
        Assert.Equal(revision, SavedRulesSnapshot.Read([settings.Encode()]).Rules!.EconomyRevision);
    }

    [Theory]
    [InlineData("v=1;economy=2")]
    [InlineData("v=1;economy=-1")]
    [InlineData("v=1;economy=one")]
    [InlineData("v=1;economy=1;economy=1")]
    public void Future_or_ambiguous_saved_economy_is_rejected(string code) => Assert.Null(RunSettingsData.Decode(code));

    [Fact]
    public void New_lobby_requires_the_whole_current_choice_and_economy_contract()
    {
        var legacy = RunSettingsData.Decode("v=1;hero_creation=2")!;
        Assert.Throws<FormatException>(() => StartupState.RequireNewRules(legacy));
        StartupState.StampNewRules(legacy);
        StartupState.RequireNewRules(legacy.Clone());
        Assert.Equal(EconomyRules.CurrentRevision, legacy.EconomyRevision);
        Assert.Throws<InvalidOperationException>(() => StartupState.RequireHost(3));
        Assert.Throws<FormatException>(() => new RunSettingsData { EconomyRevision = 2 }.Encode());
    }

    [Fact]
    public void Economy_cannot_be_shadowed_by_an_extension_or_disagree_between_saved_players()
    {
        var old = RunSettingsData.Decode("v=1;economy=0")!;
        var current = old.Clone();
        current.EconomyRevision = 1;
        Assert.Throws<FormatException>(() => SavedRulesSnapshot.Read([old.Encode(), current.Encode()]));
        old.Extensions["economy"] = "1";
        Assert.Throws<FormatException>(() => old.Encode());
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 5)]
    [InlineData(false, 7)]
    [InlineData(false, 10)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 5)]
    [InlineData(true, 7)]
    [InlineData(true, 10)]
    public void Separate_short_and_difficulty_smoke_is_only_policy_arithmetic_not_combat_calibration(bool shortRun, int difficulty)
    {
        foreach (var hero in Scenario.Heroes)
        {
            var baseline = Scenario.RunLedger(hero, 0, 1, shortRun, 1);
            var smoke = Scenario.RunLedger(hero, 0, 1, shortRun, difficulty);
            foreach (var (b, a) in baseline.Acts.Zip(smoke.Acts))
            {
                Assert.Equal((b.BudgetEnd, b.Weight, b.Level, b.TargetTier, b.BrewTier),
                    (a.BudgetEnd, a.Weight, a.Level, a.TargetTier, a.BrewTier));
                Assert.Equal(b.BorrowedHpMultiplier * RequiemLevelRules.EnemyHpMultiplier(difficulty), a.BorrowedHpMultiplier);
                Assert.Equal(b.PrimaryBossHpMultiplier * RequiemLevelRules.EnemyHpMultiplier(difficulty), a.PrimaryBossHpMultiplier);
                Assert.Equal(b.AuthoredBossHpMultiplier * RequiemLevelRules.EnemyHpMultiplier(difficulty), a.AuthoredBossHpMultiplier);
                if (shortRun)
                {
                    var nativeTier = ActRules.VanillaTier(a.ActIndex, 3);
                    var expected = hero == "Regent" ? ShopRules.StaffTiers(nativeTier) : ShopRules.EquipmentTiers(ShopType.Armorer, nativeTier);
                    Assert.Equal(expected.Max, a.TargetTier);
                    Assert.Equal(ShopRules.BrewTier(nativeTier), a.BrewTier);
                }
            }
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => Scenario.RunLedger("Regent", 0, 1, requiemLevel: 11));
    }

    [Fact]
    public void Synthetic_seed_and_actual_Core_ledger_are_independent_of_host_culture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
            var first = JsonSerializer.Serialize(Scenario.RunLedger("Regent", 9, 1));
            Assert.Equal("STRB2B0009", Scenario.Seed(9));
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(first, JsonSerializer.Serialize(Scenario.RunLedger("Regent", 9, 1)));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void No_purchase_sale_or_reagent_kit_arbitrage_at_any_tier_or_trust()
    {
        foreach (var tier in Enumerable.Range(1, 5))
        foreach (var category in Enum.GetValues<EquipCategory>())
        foreach (var trust in Enumerable.Range(0, 4))
        {
            var price = ShopRules.EquipmentPrice(category, tier);
            Assert.True(ShopRules.SellValue(price, trust) < ShopRules.ApplyTrust(price, trust));
            Assert.True(ShopRules.FenceSellValue(price) < ShopRules.ApplyTrust(price, trust));
        }
        Assert.True(ShopRules.ReagentSellValue(ShopRules.ReagentKitSize) < ShopRules.ReagentKitPrice(0));
        Assert.Equal(2, CampRules.Cost(CampAction.Forge, false));
        Assert.Equal(2, CampRules.Cost(CampAction.Rest, false));
    }

    [Theory]
    [InlineData("Ironclad")]
    [InlineData("Silent")]
    [InlineData("Regent")]
    public void Paired_ten_seed_ledgers_use_actual_Core_rules_and_never_claim_battle_wins(string hero)
    {
        for (var seed = 0; seed < Scenario.SeedCount; seed++)
        {
            var before = Scenario.RunLedger(hero, seed, 0);
            var after = Scenario.RunLedger(hero, seed, 1);
            Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(Scenario.RunLedger(hero, seed, 1)));
            Assert.Equal(5, after.Acts.Count);
            for (var i = 0; i < after.Acts.Count; i++)
            {
                var b = before.Acts[i]; var a = after.Acts[i];
                Assert.Equal((b.Rooms, b.Normals, b.Elites, b.Shops, b.DesiredCards, b.PreBossGold, b.BossGold),
                    (a.Rooms, a.Normals, a.Elites, a.Shops, a.DesiredCards, a.PreBossGold, a.BossGold));
                Assert.Equal(a.Rooms, a.Normals + a.Elites + a.Shops + a.Camps + a.Events + a.Treasures);
                Assert.Equal(a.BudgetOpen + a.PreBossGold + a.BossGold + a.Sold - a.Spent, a.BudgetEnd);
                Assert.True(a.BudgetEnd >= 0 && a.PlannedCampSpent <= a.CampBudget);
                Assert.Equal(a.DesiredCards, a.AcceptedCards + a.SkippedCards);
                Assert.True(a.BurdenPoints <= a.StatPointsEarned);
                Assert.InRange(a.Excess, 0, 5);
                Assert.Equal(0, a.OpeningHandPenalty);
                Assert.Equal(ProgressionRules.LevelFor(a.Renown), a.Level);
                Assert.Equal(ActRules.IsNewAct(Enum.Parse<ActKind>(a.Kind)) ? a.AuthoredBossHpMultiplier : a.BorrowedHpMultiplier, a.PrimaryBossHpMultiplier);
            }
            Assert.True(after.Acts[2].PathDue, "Explicit workload should earn level 8 before full-route Path offer");
            var shortRun = Scenario.RunLedger(hero, seed, 1, shortRun: true);
            Assert.Equal(new[] { 0, 2, 4 }, shortRun.Acts.Select(r => r.RouteSlot));
            Assert.True(shortRun.Acts[0].PathDue);
            Assert.Equal(3, shortRun.Acts.Count);
        }
    }
}
