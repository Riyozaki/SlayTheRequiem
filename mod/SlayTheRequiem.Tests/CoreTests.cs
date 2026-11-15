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
    [InlineData(3, 0.25)]  // III — максимум, 4+ — иммунитет
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

public class EnemyRulesTests
{
    [Theory]
    [InlineData(EnemyRank.Normal, 12, 3)]
    [InlineData(EnemyRank.Normal, 44, 6)]   // 5.5 → 6
    [InlineData(EnemyRank.Normal, 400, 20)]
    [InlineData(EnemyRank.Elite, 80, 13)]
    [InlineData(EnemyRank.Boss, 300, 0)]
    public void Poise(EnemyRank rank, int hp, int expected) => Assert.Equal(expected, EnemyRules.Poise(rank, hp));

    [Fact]
    public void StaggerTriggersAndRaisesThreshold()
    {
        Assert.Equal(new StaggerRules.Result(false, 4, 6), StaggerRules.Accumulate(4, 6));
        Assert.Equal(new StaggerRules.Result(true, 0, 9), StaggerRules.Accumulate(7, 6));
    }

    [Theory]
    [InlineData(1, 6, 1)]
    [InlineData(3, 15, 2)]
    [InlineData(1, 24, 2)]
    public void AttackCost(int hits, int total, int expected) => Assert.Equal(expected, EnemyRules.AttackCost(hits, total));

    [Fact]
    public void ImmunityAndVulnerableStacks()
    {
        Assert.Equal(0m, CombatMath.ResistanceMultiplier(4));
        Assert.Equal(0, CombatMath.ResistStacks(5, 4));
        Assert.Equal(8, CombatMath.ResistStacks(5, -1));
    }
}

public class MonsterTableTests
{
    [Fact]
    public void ParsesShippedCsv()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "monsters_requiem.csv");
        var table = MonsterTable.Parse(File.ReadAllText(path));
        Assert.True(table.Count > 100);
        var knight = table.Get("MechaKnight");
        Assert.Equal("construct", knight.Faction);
        Assert.Equal(4, knight.Plating);
        Assert.Equal(CombatMath.ImmuneTier, knight.Resistances[DamageType.Poison]);
        Assert.Same(MonsterTable.Default, table.Get("NoSuchMonster"));
    }
}

public class EquipmentRulesTests
{
    private static EquipItem Armor(EquipSlot slot, ArmorClass cls, int tier = 2) => new($"{slot}{cls}", slot, 2, tier, cls);

    [Fact]
    public void HeavySet_GivesPlatingPoiseAndManaPenalty()
    {
        var items = new[]
        {
            Armor(EquipSlot.Head, ArmorClass.Heavy), Armor(EquipSlot.Body, ArmorClass.Heavy),
            Armor(EquipSlot.Hands, ArmorClass.Heavy), Armor(EquipSlot.Feet, ArmorClass.Heavy),
        };
        var b = EquipmentRules.Evaluate(items);
        Assert.Equal(2, b.Plating);
        Assert.Equal(4, b.PoiseBonus);
        Assert.Equal(1, b.ManaRegenPenalty);
        Assert.Equal(0, b.DodgeCharges);
        Assert.True(b.Monk);
    }

    [Fact]
    public void TierFourHeavy_AddsPlatingPerPiece()
    {
        var items = new[] { Armor(EquipSlot.Head, ArmorClass.Heavy, 4), Armor(EquipSlot.Body, ArmorClass.Heavy, 4) };
        Assert.Equal(3, EquipmentRules.Evaluate(items).Plating);
    }

    [Fact]
    public void LightArmor_DodgeDisabledByThreeHeavy()
    {
        var mixed = new[]
        {
            Armor(EquipSlot.Head, ArmorClass.Heavy), Armor(EquipSlot.Body, ArmorClass.Heavy),
            Armor(EquipSlot.Hands, ArmorClass.Heavy), Armor(EquipSlot.Feet, ArmorClass.Light),
        };
        Assert.Equal(0, EquipmentRules.Evaluate(mixed).DodgeCharges);
        var light = new[] { Armor(EquipSlot.Head, ArmorClass.Light), Armor(EquipSlot.Body, ArmorClass.Light) };
        var b = EquipmentRules.Evaluate(light);
        Assert.Equal(1, b.DodgeCharges);
        Assert.Equal(1, b.StaminaPoolBonus);
    }

    [Fact]
    public void TwoHanded_DisplacesOffHand_AndViceVersa()
    {
        var shield = new EquipItem("Shield", EquipSlot.OffHand, 3, Weapon: WeaponKind.Shield);
        var sword = new EquipItem("Sword", EquipSlot.MainHand, 2, Weapon: WeaponKind.Sword);
        var hammer = new EquipItem("Hammer", EquipSlot.MainHand, 5, Weapon: WeaponKind.TwoHanded);
        var displaced = EquipmentRules.Displaced(new[] { sword, shield }, hammer);
        Assert.Contains(sword, displaced);
        Assert.Contains(shield, displaced);
        Assert.Equal(new[] { hammer }, EquipmentRules.Displaced(new[] { hammer }, shield));
    }

    [Fact]
    public void Rings_HaveTwoSlots()
    {
        var r1 = new EquipItem("R1", EquipSlot.Ring, 0);
        var r2 = new EquipItem("R2", EquipSlot.Ring, 0);
        var r3 = new EquipItem("R3", EquipSlot.Ring, 0);
        Assert.Empty(EquipmentRules.Displaced(new[] { r1 }, r2));
        Assert.Equal(new[] { r1 }, EquipmentRules.Displaced(new[] { r1, r2 }, r3));
    }

    [Theory]
    [InlineData(WeaponKind.Dagger, 1, 0)]
    [InlineData(WeaponKind.Sword, 5, 4)]
    [InlineData(WeaponKind.TwoHanded, 2, 4)]
    [InlineData(WeaponKind.Crossbow, 1, 4)]
    public void WeaponDamage_ByTier(WeaponKind kind, int tier, int expected) =>
        Assert.Equal(expected, EquipmentRules.WeaponDamage(kind, tier));

    [Fact]
    public void Duelist_WhenOffHandEmpty()
    {
        var b = EquipmentRules.Evaluate(new[] { new EquipItem("Dagger", EquipSlot.MainHand, 1, 1, Weapon: WeaponKind.Dagger) });
        Assert.True(b.Duelist);
        Assert.Equal(1, b.StaminaRegenBonus);
        Assert.Equal(0, EquipmentRules.WeaponAttackCost(1, b));
    }
}

public class ManaAndMasteryTests
{
    [Theory]
    [InlineData(10, 5)]
    [InlineData(3, 1)]
    [InlineData(5, 2)]
    public void StartAmount_IsHalfPool(int pool, int expected) => Assert.Equal(expected, ManaRules.StartAmount(pool));

    [Theory]
    [InlineData(0, 3, 1, 1)]
    [InlineData(3, 3, 1, 0)]
    [InlineData(9, 10, 2, 1)]
    [InlineData(14, 10, 2, 0)]
    public void RegenGain_CapsAtPool_DoesNotCutOverflow(int current, int pool, int regen, int expected) =>
        Assert.Equal(expected, ManaRules.RegenGain(current, pool, regen));

    [Fact]
    public void StartingMastery_PerHero()
    {
        Assert.Equal(0, SpellAffinity.StartingMastery("Ironclad", SpellSchool.Destruction));
        Assert.Equal(1, SpellAffinity.StartingMastery("Silent", SpellSchool.Illusion));
        Assert.Equal(0, SpellAffinity.StartingMastery("Silent", SpellSchool.Destruction));
        Assert.Equal(2, SpellAffinity.StartingMastery("Regent", SpellSchool.Destruction));
        Assert.Equal(1, SpellAffinity.StartingMastery("Regent", SpellSchool.Conjuration));
    }

    [Fact]
    public void RewardShare_PlatebearerGrowsWithMastery()
    {
        Assert.Equal(0.10m, SpellAffinity.RewardShare("Ironclad", 0));
        Assert.Equal(0.15m, SpellAffinity.RewardShare("Ironclad", 3));
        Assert.Equal(0.45m, SpellAffinity.RewardShare("Regent", 0));
    }

    [Theory]
    [InlineData(SpellTier.Novice, 0, false)]
    [InlineData(SpellTier.Apprentice, 1, true)]
    [InlineData(SpellTier.Adept, 1, false)]
    public void AllowedInRewards_ByMastery(SpellTier tier, int mastery, bool expected) =>
        Assert.Equal(expected, SpellAffinity.AllowedInRewards(tier, mastery));

    [Theory]
    [InlineData(4, 0.75, 3)]
    [InlineData(1, 0.75, 1)]
    [InlineData(6, 1.0, 6)]
    public void ScaleEffect_Strained(int amount, double mult, int expected) =>
        Assert.Equal(expected, SpellAffinity.ScaleEffect(amount, (decimal)mult));
}

public class EnemyCasterTests
{
    [Fact]
    public void CasterNumbers()
    {
        Assert.Equal(4, EnemyRules.CasterManaPool(EnemyRank.Normal));
        Assert.Equal(8, EnemyRules.CasterManaPool(EnemyRank.Boss));
        Assert.Equal(2, EnemyRules.SpellCost(1, 8));
        Assert.Equal(3, EnemyRules.SpellCost(3, 9));
        Assert.True(EnemyRules.IsCaster(DamageType.Fire));
        Assert.False(EnemyRules.IsCaster(DamageType.Physical));
        Assert.False(EnemyRules.IsCaster(DamageType.Poison));
    }
}

public class SpellCostDeltaTests
{
    // SpellRules добавляет к цене только надбавку предрасположенности от базовой цены (скидки не масштабируются).
    [Theory]
    [InlineData(3, SpellTier.Adept, 2, 2)]   // ×1.5 вверх: 5 − 3
    [InlineData(1, SpellTier.Novice, 0, 1)]  // 2 − 1
    [InlineData(6, SpellTier.Master, 5, 0)]
    [InlineData(2, SpellTier.Novice, 3, -1)] // «легко»: −1
    public void AffinityDelta(int baseCost, SpellTier tier, int mastery, int delta)
    {
        var cast = SpellAffinity.Evaluate(baseCost, tier, mastery);
        Assert.Equal(delta, cast.ManaCost - baseCost);
    }
}

public class Stage3Tests
{
    [Theory]
    [InlineData(30, 32, 0, 0, 0, true)]
    [InlineData(37, 32, 5, 1, 0, true)]
    [InlineData(38, 32, 6, 1, 1, true)]
    [InlineData(43, 32, 11, 1, 1, false)]
    public void Overload(int weight, int capacity, int excess, int regen, int hand, bool pickUp) =>
        Assert.Equal(new BurdenRules.Overload(excess, regen, hand, pickUp), BurdenRules.Evaluate(weight, capacity));

    [Fact]
    public void OriginNumbers()
    {
        Assert.Equal(new[] { 0, 0, 2, 4, 0 }, new[] { 80, 40, 39, 19, 0 }.Select(h => OriginRules.MarshbarkHeal(h, 80)));
        Assert.Equal(new[] { 0, 1, 2, 3 }, new[] { 95, 89, 69, 49 }.Select(h => OriginRules.KnightPlating(h, 100)));
        Assert.Equal(new[] { 0, 1, 2, 4 }, new[] { 10, 7, 5, 0 }.Select(m => OriginRules.WizardSpellBonus(m, 10)));
        Assert.Equal(3, OriginRules.FugitiveBonus(5));
        Assert.Equal(2, OriginRules.WinddancerBonus(7));
        Assert.Equal((20, 7), OriginRules.BeastSpirit(4));
        // Кланы Латоносца (M4 п. 1) — те же числа, что в tools/sim/test_requiem_rules.py.
        Assert.False(OriginRules.ForgeFuryActive(2));
        Assert.True(OriginRules.ForgeFuryActive(3));
        Assert.Equal(new[] { 0, 0, 1, 2, 2 }, new[] { 9, 7, 6, 4, 0 }.Select(s => OriginRules.DuneRhythmRegen(s, 9)));
        Assert.Equal(new[] { 0, 0, 1, 2, 5, 5 }, new[] { 1, 2, 3, 6, 15, 20 }.Select(OriginRules.TrainingBonusPoints));
        Assert.Equal(new[] { 0, 3, 4, 5, 9 }, new[] { 0, 2, 3, 4, 7 }.Select(OriginRules.ConspiratorPotency));
        Assert.Equal(new[] { 0, 0, 1, 2 }, new[] { 0, 1, 3, 4 }.Select(OriginRules.BladeSpiritBonus));
        Assert.Equal((10, 2), OriginRules.LoyalGuard(1));
        Assert.Equal((16, 3), OriginRules.LoyalGuard(4));
        Assert.Equal((38, 5), OriginRules.LoyalGuard(15));
    }

    [Fact]
    public void Brewing()
    {
        var pouch = new int[AlchemyRules.ReagentCount];
        pouch[(int)Reagent.Bloodmoss] = 3;
        var first = AlchemyRules.Available(pouch)[0];
        Assert.Equal(Brew.Health, first.Recipe.Brew);
        Assert.Equal(3, first.Tier);

        pouch = new int[AlchemyRules.ReagentCount];
        pouch[(int)Reagent.Nightcap] = 2;
        pouch[(int)Reagent.AshSalt] = 1;
        var brews = AlchemyRules.Available(pouch).ToDictionary(o => o.Recipe.Brew, o => o.Tier);
        Assert.Equal(new Dictionary<Brew, int> { [Brew.SnakeBite] = 3, [Brew.Toxin] = 1 }, brews);

        Assert.Equal(3, AlchemyRules.Tier(2, 1));
        Assert.Equal(5, AlchemyRules.Tier(3, 5));
        Assert.Empty(AlchemyRules.Available(new int[AlchemyRules.ReagentCount]));
        Assert.Equal(24, AlchemyRules.Recipes.Count);
        Assert.Equal(8, AlchemyRules.Recipes.Count(r => r.IsPoison));
    }

    [Fact]
    public void DropsAndPoisons()
    {
        Assert.Equal(3, AlchemyRules.DropCount(EnemyRank.Boss));
        Assert.Equal(2, AlchemyRules.Fit(10, 3));
        Assert.Equal(0, AlchemyRules.Fit(12, 1));
        Assert.Equal(new[] { 2, 4, 7 }, new[] { 1, 3, 5 }.Select(t => AlchemyRules.PoisonPotency(Brew.Toxin, t)));
        Assert.Equal(100, AlchemyRules.PoisonPotency(Brew.Paralytic, 5));
    }

    [Theory]
    [InlineData(EnemyRank.Elite, 0, 99, 72, 1)]
    [InlineData(EnemyRank.Elite, 0, 0, 25, 0)]   // свита Атаманши
    [InlineData(EnemyRank.Normal, 0, 0, 30, 0)]
    [InlineData(EnemyRank.Normal, 1, 24, 30, 1)]
    [InlineData(EnemyRank.Normal, 1, 25, 30, 0)]
    [InlineData(EnemyRank.Boss, 3, 0, 300, 0)]
    public void AffixCount(EnemyRank rank, int act, int roll, int hp, int expected) =>
        Assert.Equal(expected, EnemyRules.AffixCount(rank, act, roll, hp));
}

public class M5CampTests
{
    [Fact]
    public void MuleAndTradeName()
    {
        Assert.Equal(10, CampRules.MuleCapacity(false));
        Assert.Equal(15, CampRules.MuleCapacity(true));
        Assert.Equal(new[] { 0, 1, 1, 2, 4 }, new[] { 0, 1, 2, 3, 7 }.Select(CampRules.ChestLoss));
        Assert.Equal(new[] { 1, 4, 4 }, new[] { 0, 3, 5 }.Select(t => ShopRules.RefillsPerVisit(t, true)));
        Assert.Equal(2, ShopRules.RefillsPerVisit(2, false));
    }

    [Fact]
    public void Buildings()
    {
        Assert.False(CampRules.HasBuilding(0, CampBuilding.Kettle));
        Assert.True(CampRules.HasBuilding(1, CampBuilding.Kettle));
        Assert.False(CampRules.HasBuilding(3, CampBuilding.Altar));
        Assert.True(CampRules.HasBuilding(5, CampBuilding.HerbalistTent));
        Assert.Equal(new[] { 1, 5, 5 }, new[] { 0, 4, 5 }.Select(CampRules.UnlockAfterVictory));
        Assert.Equal(1, CampRules.Cost(CampAction.Forge, false, true));
        Assert.Equal(2, CampRules.Cost(CampAction.Enchant, false, true));
        Assert.Equal(2, CampRules.Cost(CampAction.Forge, false, false));
    }
}

public class M5ShopTests
{
    [Fact]
    public void Placement()
    {
        Assert.Equal(ShopType.Fence, ShopRules.GuaranteedShop(ActKind.BloodCourt, 0));
        Assert.Equal(ShopType.Arcanist, ShopRules.GuaranteedShop(ActKind.StarTower, 1));
        Assert.Equal(ShopType.Temple, ShopRules.GuaranteedShop(ActKind.Glory, 0));
        Assert.Null(ShopRules.GuaranteedShop(ActKind.StarTower, 2));
        Assert.DoesNotContain(ShopType.Caravan, ShopRules.ActPool(ActKind.Hive));
        for (var h = 0u; h < 200u; h++)
        {
            Assert.NotEqual(ShopType.Caravan, ShopRules.RollShopType(h, ActKind.FrozenBarrows, 1, 2, null, caravanSeen: true));
            Assert.NotEqual(ShopType.Fence, ShopRules.RollShopType(h, ActKind.Foothills, 0, 1, null));
        }
        Assert.Contains(Enumerable.Range(0, 200), h => ShopRules.RollShopType((uint)h, ActKind.Foothills, 0, 1, null, outlaw: true) == ShopType.Fence);
        Assert.Equal((4, 5), ShopRules.EquipmentTiers(ShopType.Fence, 0));
        Assert.Equal((3, 4), ShopRules.EquipmentTiers(ShopType.Jeweler, 2));
    }

    [Fact]
    public void Prices()
    {
        Assert.Equal(new[] { 25, 45, 70 }, new[] { 1, 2, 3 }.Select(ShopRules.ScrollPrice));
        Assert.Equal(new[] { 70, 120, 180, 240, 320 }, Enumerable.Range(1, 5).Select(ShopRules.StaffPrice));
        Assert.Equal((2, 3), ShopRules.StaffTiers(1));
        Assert.Equal(110, ShopRules.EnchantServicePrice(2, ownStone: true));
        Assert.Equal(210, ShopRules.EnchantServicePrice(3, ownStone: false));
        Assert.Equal(35, ShopRules.CleansePrice(true));
        Assert.Equal(48, ShopRules.FenceSellValue(80));
        Assert.False(ShopRules.CanPayCurseRemoval(6));
        Assert.Equal((3, 4), ShopRules.TradeTiers(4));
        Assert.Equal(new int?[] { 100, 150, 200, null }, Enumerable.Range(0, 4).Select(b => ShopRules.MentorSkillPrice(b, true)));
        Assert.Equal(225, ShopRules.MentorSkillPrice(1, false));
    }

    [Fact]
    public void ClearNameAndLessons()
    {
        Assert.Equal(1, ShopRules.ClearNameTarget(new[] { 450, 0, 300, 10, 150, 150, 0, 150 }));
        Assert.Equal(-1, ShopRules.ClearNameTarget(new[] { 150, 150, 150, 150, 150, 150, 0, 150 }));
        var mask = ProgressionRules.WithLesson(0, WeaponKind.TwoHanded);
        Assert.False(ProgressionRules.IsClumsy("Regent", WeaponKind.TwoHanded, false, false, mask));
        Assert.True(ProgressionRules.IsClumsy("Regent", WeaponKind.Crossbow, false, false, mask));
        Assert.True(ProgressionRules.IsClumsy("Regent", WeaponKind.Sword, true, false, mask));
        Assert.False(ProgressionRules.IsClumsy("Regent", WeaponKind.Sword, true, false, ProgressionRules.WithLesson(mask, WeaponKind.Shield)));
    }
}

public class Stage35Tests
{
    [Fact]
    public void ShopTypes()
    {
        Assert.Equal(ShopType.Wanderer, ShopRules.RollShopType(12345u, 0, 0, null));
        Assert.DoesNotContain(ShopType.Arcanist, ShopRules.ActPool(0));
        Assert.Contains(ShopType.Arcanist, ShopRules.ActPool(1));
        for (var h = 0u; h < 50u; h++)
            Assert.NotEqual(ShopType.Armorer, ShopRules.RollShopType(h, 1, 2, ShopType.Armorer));
        Assert.Equal(ShopRules.Fnv1a("ABC"), ShopRules.Fnv1a("ABC"));
        Assert.NotEqual(ShopRules.Fnv1a("ABC", 1), ShopRules.Fnv1a("ABC", 2));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(149, 0)]
    [InlineData(150, 1)]
    [InlineData(449, 2)]
    [InlineData(450, 3)]
    [InlineData(9999, 3)]
    public void Trust(int spent, int expected) => Assert.Equal(expected, ShopRules.Trust(spent));

    [Fact]
    public void Prices()
    {
        Assert.Equal(85, ShopRules.ApplyTrust(100, 3));
        Assert.Equal(48, ShopRules.ApplyTrust(50, 1));
        Assert.Equal(28, ShopRules.SellValue(80, 0));
        Assert.Equal(40, ShopRules.SellValue(80, 3));
        Assert.Equal(new[] { 45, 80, 130, 190, 260 }, Enumerable.Range(1, 5).Select(t => ShopRules.EquipmentPrice(EquipCategory.Weapon, t)));
        Assert.Equal(220, ShopRules.EquipmentPrice(EquipCategory.Armor, 9));
        Assert.Equal(new[] { 41, 63, 99, 144, 207 }, Enum.GetValues<SpellTier>().Select(t => ShopRules.SpellPrice(t, single: true)));
        Assert.Equal(110, ShopRules.SpellPrice(SpellTier.Adept, single: false));
        Assert.Equal(new[] { 40, 75, 130 }, new[] { 1, 3, 5 }.Select(t => ShopRules.BrewPrice(t, poison: false)));
        Assert.Equal(new[] { 35, 70, 125 }, new[] { 1, 3, 5 }.Select(t => ShopRules.BrewPrice(t, poison: true)));
        Assert.Equal(new[] { 120, 160, 200 }, new[] { 0, 1, 2 }.Select(ShopRules.TreatisePrice));
        Assert.Equal((1, 2), ShopRules.EquipmentTiers(ShopType.Wanderer, 2));
        Assert.Equal((3, 4), ShopRules.EquipmentTiers(ShopType.Armorer, 2));
        Assert.Equal(new[] { 1, 3, 5 }, new[] { 0, 1, 2 }.Select(ShopRules.BrewTier));
        Assert.Equal(ArmorClass.Robe, ShopRules.PreferredArmor("Regent"));
    }

    [Fact]
    public void CombatGoldAndSpellShelf()
    {
        Assert.Equal(new[] { 17, 26, 33, 46, 124, 137 }, new[] { 13, 20, 25, 35, 95, 105 }.Select(ShopRules.CombatGold));
        Assert.True(SpellAffinity.AllowedInShop(SpellTier.Apprentice, 0));
        Assert.False(SpellAffinity.AllowedInShop(SpellTier.Adept, 0));
        Assert.True(SpellAffinity.AllowedInShop(SpellTier.Expert, 3));
    }

    [Fact]
    public void MemoryStone()
    {
        Assert.Equal(new[] { MemoryStoneRules.Infinite, 3, 3, 2, 2, 1, 1, 0 },
            new[] { 0, 1, 4, 5, 7, 8, 9, 10 }.Select(MemoryStoneRules.CandlesFor));
        Assert.Equal(MemoryStoneRules.Infinite, MemoryStoneRules.CandlesLeft(0, 99));
        Assert.Equal(2, MemoryStoneRules.CandlesLeft(3, 1));
        Assert.Equal(0, MemoryStoneRules.CandlesLeft(3, 5));
        Assert.Equal(1, MemoryStoneRules.CandlesLeft(9, 1, bonus: 1));
        Assert.Equal(0, MemoryStoneRules.CandlesLeft(0, 0, configOverride: 0));
        Assert.Equal(new[] { 75, 65, 9, 1, 1 }, new[] { 80, 70, 10, 2, 1 }.Select(MemoryStoneRules.FleshScarMaxHp));
        Assert.Equal(93, MemoryStoneRules.PurseScarGold(155));
        Assert.Equal(0.70, MemoryStoneRules.ScoreMultiplier(2), 6);
        Assert.Equal(2, MemoryStoneRules.ScarsToApply(1, true, 0));
        Assert.Equal(0, MemoryStoneRules.ScarsToApply(1, false, 1));
        Assert.True(MemoryStoneRules.IsSnapshotCompatible(7, 7));
        Assert.False(MemoryStoneRules.IsSnapshotCompatible(6, 7));
    }
}

public class Stage36Tests
{
    [Fact]
    public void TrustAndOrders()
    {
        Assert.Equal(new[] { 0, 1, 2, 3, 3 }, new[] { 0, 1, 2, 3, 5 }.Select(ShopRules.RefillsPerVisit));
        Assert.False(ShopRules.CanOrder(2));
        Assert.True(ShopRules.CanOrder(3));
        Assert.Equal(66, ShopRules.OrderPrice(131));
        Assert.Equal(2, ShopRules.OrderTier(ShopType.Wanderer, 0));
        Assert.Equal(5, ShopRules.OrderTier(ShopType.Armorer, 3));
        Assert.Equal(80, ShopRules.OrderItemPrice(OrderCategory.Weapon, 0));
        Assert.Equal(130, ShopRules.OrderItemPrice(OrderCategory.Potion, 0));
        Assert.Equal(new[] { OrderCategory.Weapon, OrderCategory.Armor, OrderCategory.Shield }, ShopRules.OrderOptions(ShopType.Armorer));
    }

    [Theory]
    [InlineData(0, 80)]
    [InlineData(49, 80)]
    [InlineData(50, 70)]
    [InlineData(149, 60)]
    [InlineData(400, 0)]
    public void TheftChance(int price, int expected) => Assert.Equal(expected, ShopRules.TheftChancePercent(price));

    [Fact]
    public void Theft()
    {
        Assert.True(ShopRules.TheftSucceeds(79u, 40));
        Assert.False(ShopRules.TheftSucceeds(80u, 40));
        Assert.Equal(16, ShopRules.TheftFailDamage(80));
        Assert.Equal(15, ShopRules.TheftFailDamage(71));
        Assert.Equal(1, ShopRules.TheftFailDamage(0));
    }

    [Fact]
    public void Services()
    {
        Assert.Equal(new[] { 60, 60, 90, 130, 130 }, Enumerable.Range(1, 5).Select(ShopRules.SharpenPrice));
        Assert.Equal(new[] { 20, 25, 30, 35, 35 }, Enumerable.Range(0, 5).Select(ShopRules.ReagentKitPrice));
        Assert.Equal(15, ShopRules.ReagentSellValue(5));
        Assert.Equal(125, ShopRules.RemovalPrice(2));
        Assert.Equal(40, ShopRules.DeathEchoSurcharge(100, 40));
        Assert.Equal(100, ShopRules.DeathEchoSurcharge(100, 500));
    }

    [Fact]
    public void UpgradesBeyondV()
    {
        Assert.Equal(4, EquipmentRules.WeaponDamage(WeaponKind.Sword, 5));
        Assert.Equal(6, EquipmentRules.WeaponDamage(WeaponKind.Sword, 7));
        Assert.Equal(10, EquipmentRules.WeaponDamage(WeaponKind.TwoHanded, 6));
        Assert.Equal(0, EquipmentRules.WeaponDamage(WeaponKind.None, 7));
        var item = new EquipItem("Saber", EquipSlot.MainHand, 2, 3, ArmorClass.None, WeaponKind.Sword, null, 0, 2);
        Assert.Equal(5, item.EffectiveTier);
    }

    [Fact]
    public void CoopStone()
    {
        Assert.True(MemoryStoneRules.VoteApproved(1, 1));
        Assert.False(MemoryStoneRules.VoteApproved(1, 2));
        Assert.Equal(1, MemoryStoneRules.SharedCandlesLeft(new[] { 3, 1, 2 }));
        Assert.Equal(0, MemoryStoneRules.SharedCandlesLeft(Array.Empty<int>()));
        Assert.Equal(MemoryStoneRules.Infinite, MemoryStoneRules.SharedCandlesLeft(new[] { MemoryStoneRules.Infinite }));
        Assert.Equal(2, MemoryStoneRules.SharedCandlesUsed(new[] { 0, 2, 1 }));
        Assert.Equal("act_1_map_requiem_1", MemoryStoneRules.MapRngName(0, 1));
        Assert.Equal(Scar.Purse, MemoryStoneRules.DefaultScar);
    }
}

public class Stage4Tests
{
    [Fact]
    public void PietySteps()
    {
        Assert.Equal(Piety.Tainted, PietyRules.Sin(Piety.Righteous));
        Assert.Equal(Piety.Apostate, PietyRules.Sin(Piety.Tainted));
        Assert.Equal(Piety.Apostate, PietyRules.GraveSin(Piety.Righteous));
        Assert.Equal(Piety.Tainted, PietyRules.Redeem(Piety.Apostate));
        Assert.Equal(Piety.Righteous, PietyRules.Redeem(Piety.Tainted));
    }

    [Fact]
    public void EffectivePietyAndStrength()
    {
        Assert.Equal(Piety.Tainted, PietyRules.Effective(Piety.Righteous, true, false, false));
        Assert.Equal(Piety.Apostate, PietyRules.Effective(Piety.Righteous, false, true, false));
        Assert.Equal(Piety.Apostate, PietyRules.Effective(Piety.Tainted, false, false, true));
        Assert.Equal(BlessingStrength.Full, PietyRules.Strength(Piety.Righteous, false));
        Assert.Equal(BlessingStrength.Dimmed, PietyRules.Strength(Piety.Tainted, false));
        Assert.Equal(BlessingStrength.None, PietyRules.Strength(Piety.Apostate, false));
        Assert.Equal(BlessingStrength.None, PietyRules.Strength(Piety.Righteous, true));
    }

    [Fact]
    public void Redemption()
    {
        Assert.Equal(75, PietyRules.RedemptionCost(1));
        Assert.Equal(60, PietyRules.RobberyGold(1));
        Assert.True(PietyRules.CanRedeem(Piety.Tainted, -1, 1, false));
        Assert.False(PietyRules.CanRedeem(Piety.Tainted, 1, 1, false));
        Assert.False(PietyRules.CanRedeem(Piety.Righteous, -1, 1, false));
        Assert.False(PietyRules.CanRedeem(Piety.Apostate, -1, 1, true));
    }

    [Fact]
    public void UnknownRooms()
    {
        Assert.Equal(0, PantheonRules.UnknownRoomRoll(0, 0));
        Assert.Equal(1, PantheonRules.UnknownRoomRoll(1, 13));
        Assert.Equal(2, PantheonRules.UnknownRoomRoll(1, 14));
        Assert.Equal(3, PantheonRules.UnknownRoomRoll(2, 29));
        Assert.Equal(0, PantheonRules.UnknownRoomRoll(3, 25));
        Assert.Equal(0, PantheonRules.UnknownRoomRoll(1, 30));
    }

    [Fact]
    public void OmensAndShards()
    {
        Assert.Equal(3, OmenRules.OmenOfferCount(0));
        Assert.Equal(10, OmenRules.OmenOfferCount(OmenRules.AllOmensLevel));
        Assert.Equal(6, OmenRules.OmenBoon(Omen.Blade).MaxHp);
        Assert.Equal(-1, OmenRules.OmenBoon(Omen.Sponge).ManaRegen);
        Assert.Equal(-1, OmenRules.ShardBoon(EbbShard.Wind).Plating);
        Assert.Equal(1, OmenRules.ShardBoon(EbbShard.Beast).Pierce);
        var picks = OmenRules.PickDistinct(10, 3, n => n - 1);
        Assert.Equal(3, picks.Distinct().Count());
        Assert.DoesNotContain(4, OmenRules.PickDistinct(6, 5, _ => 0, new List<int> { 4 }));
    }

    [Fact]
    public void BoonAlgebra()
    {
        var sum = new Boon { MaxHp = 6, Plating = 1 } + new Boon { MaxHp = -5, ImmuneFrozen = true };
        Assert.Equal(1, sum.MaxHp);
        Assert.Equal(1, sum.Plating);
        Assert.True(sum.ImmuneFrozen);
        Assert.Equal(12, new Boon { MaxHp = 6 }.Scaled(2).MaxHp);
    }

    [Fact]
    public void CelestialSet()
    {
        var dimmed = PantheonRules.CelestialTotal(Celestial.AnvilFather, BlessingStrength.Dimmed, true, true);
        Assert.Equal(6, dimmed.MaxHp);
        Assert.Equal(0, dimmed.Plating);                       // тусклое — только первый бонус
        var full = PantheonRules.CelestialTotal(Celestial.AnvilFather, BlessingStrength.Full, true, true);
        Assert.Equal(1, full.Plating);
        Assert.Equal(2, full.HeavyAttackDamage);
        Assert.Equal(Boon.None, PantheonRules.CelestialTotal(Celestial.AnvilFather, BlessingStrength.None, true, true));
        Assert.True(PantheonRules.HasDivineSpirit(BlessingStrength.Full, true, true, true));
        Assert.False(PantheonRules.HasDivineSpirit(BlessingStrength.Full, true, true, false));
        Assert.True(PantheonRules.ForbidsNecromancy(Celestial.MercyShield));
    }

    [Fact]
    public void PrincesAndDiseases()
    {
        Assert.True(PantheonRules.IsTribunal(AbyssPrince.DuskStar));
        Assert.False(PantheonRules.IsTribunal(AbyssPrince.GateBreaker));
        Assert.Equal(3, PantheonRules.VowTarget(AbyssPrince.PlagueLord));
        Assert.Equal(-5, PantheonRules.Gift(AbyssPrince.TwilightWidow).MaxHp);
        Assert.Equal(4, PantheonRules.OutcastAfterCombatHeal(true));
        var before = PantheonRules.Diseases(new[] { (int)Disease.StoneGout, (int)Disease.StoneGout }, false);
        Assert.Equal(-2, before.AttackDamage);
        Assert.Equal(0, before.Plating);
        var after = PantheonRules.Diseases(new[] { (int)Disease.StoneGout }, true);
        Assert.Equal(1, after.Plating);
        Assert.Equal(2, PantheonRules.Count(new[] { 4, 4, 1 }, Disease.Bonebreak));
        Assert.Equal(0, PantheonRules.StarMarksAfterAct(3, true));
    }

    [Fact]
    public void TribunalHeirs()
    {
        // Пеплорождённые: ослабленная Цена; Пепельный кочевник: ступень вверх; чужих Князей не трогает.
        Assert.Equal(25, PantheonRules.TribunalGift(AbyssPrince.Treachery, true, false).SummonTakenPercent);
        Assert.Equal(-15, PantheonRules.TribunalGift(AbyssPrince.WebWeaver, true, false).RestHealPercent);
        var nomad = PantheonRules.TribunalGift(AbyssPrince.Treachery, true, true);
        Assert.Equal((3, 2), (nomad.AttackDamage, nomad.Pierce));
        var star = PantheonRules.TribunalGift(AbyssPrince.DuskStar, false, true);
        Assert.Equal((9, 3, 2, 2), (star.MaxHp, star.ManaPool, star.FireRes, star.FrostRes));
        Assert.Equal(PantheonRules.Gift(AbyssPrince.Outcast), PantheonRules.TribunalGift(AbyssPrince.Outcast, true, true));
        Assert.Equal(0, PantheonRules.HeirStarMarks(1));
        Assert.Equal(8, PantheonRules.WebLeechCap(true));
        Assert.Equal(3, PantheonRules.WebBelowHalfBonus(true));
        Assert.Equal(new List<int> { 0, 4 }, PantheonRules.HeirShrinePicks(new[] { 0, 1 }, null, _ => 0));
        Assert.Equal(new List<int> { 0, 2 }, PantheonRules.HeirShrinePicks(new[] { 0, 1 }, 4, _ => 0));
        Assert.True(PantheonRules.OrdinatorMayAccept(AbyssPrince.DuskStar));
        Assert.False(PantheonRules.OrdinatorMayAccept(AbyssPrince.PlagueLord));
    }

    [Fact]
    public void OrdinatorForms()
    {
        Assert.False(TransformationRules.CanTakeWorldview(FormKind.None, FormKind.Righteous, true, true, false));
        Assert.False(TransformationRules.CanTakeWorldview(FormKind.None, FormKind.Vampire, false, true, false));
        Assert.False(TransformationRules.CanTakeWorldview(FormKind.Herald, FormKind.Lich, false, true, false));
        Assert.True(TransformationRules.CanTakeWorldview(FormKind.None, FormKind.DarkPact, false, true, false));
        Assert.True(TransformationRules.PathAllowed(HeroPath.Paladin, FormKind.None, false, false, ordinator: true));
        Assert.False(TransformationRules.PathAllowed(HeroPath.BlackKnight, FormKind.None, true, false, ordinator: true));
    }

    [Fact]
    public void Pierce()
    {
        Assert.Equal(5, CombatMath.ApplyPlating(6, 2, pierce: 1));
        Assert.Equal(6, CombatMath.ApplyPlating(6, 1, pierce: 1));
        Assert.Equal(7, CombatMath.ApplyPlating(6, -1));
    }
}

public class Stage5Tests
{
    [Fact]
    public void RenownAndLevels()
    {
        Assert.Equal(1, ProgressionRules.RenownFor(EnemyRank.Normal));
        Assert.Equal(3, ProgressionRules.RenownFor(EnemyRank.Elite));
        Assert.Equal(6, ProgressionRules.RenownFor(EnemyRank.Boss));
        Assert.Equal(0, ProgressionRules.RenownToReach(1));
        Assert.Equal(3, ProgressionRules.RenownToReach(2));
        Assert.Equal(7, ProgressionRules.RenownToReach(3));
        Assert.Equal(133, ProgressionRules.RenownToReach(15));
        Assert.Equal(1, ProgressionRules.LevelFor(2));
        Assert.Equal(2, ProgressionRules.LevelFor(3));
        Assert.Equal(15, ProgressionRules.LevelFor(1000));
        Assert.Equal(4, ProgressionRules.RenownToNext(3));
        Assert.Equal(0, ProgressionRules.RenownToNext(133));
        Assert.Equal(28, ProgressionRules.StatPointsFor(15));
        Assert.Equal(7, ProgressionRules.SkillPointsFor(15));
        Assert.Equal(0, ProgressionRules.SkillPointsFor(1));
        Assert.Equal(15, ProgressionRules.RetainedRenown(10, 20));
        Assert.Equal(10, ProgressionRules.RetainedRenown(10, 5));
    }

    [Fact]
    public void TiersAndMeditation()
    {
        Assert.Equal(0, ProgressionRules.Tier(2));
        Assert.Equal(1, ProgressionRules.Tier(3));
        Assert.Equal(3, ProgressionRules.Tier(20));
        Assert.Equal(3, ProgressionRules.WithMeditation(3, true));
        Assert.Equal(2, ProgressionRules.WithMeditation(1, true));
        var t = ProgressionRules.Tiers(3, 3, 3);
        Assert.Equal(1, t.PoisonResistance);
        Assert.Equal(6, t.BurdenBonus);
        Assert.Equal(1, t.Plating);
        Assert.True(t.FreeFirstAttack);
        Assert.Equal(3, t.Ward);
        var none = ProgressionRules.Tiers(0, 1, 2);
        Assert.Equal(0, none.PoisonResistance);
        Assert.Equal(1, none.StaminaRegen);
        Assert.False(none.FreeFirstAttack);
        Assert.Equal(1, none.FirstSpellDiscount);
        Assert.Equal(0, none.Ward);
    }

    [Fact]
    public void ClumsyGrip()
    {
        Assert.True(ProgressionRules.IsClumsy("Regent", WeaponKind.Crossbow, false, false));
        Assert.True(ProgressionRules.IsClumsy("Regent", WeaponKind.Staff, true, false));
        Assert.False(ProgressionRules.IsClumsy("Regent", WeaponKind.Crossbow, false, true));
        Assert.True(ProgressionRules.IsClumsy("Silent", WeaponKind.TwoHanded, false, false));
        Assert.False(ProgressionRules.IsClumsy("Silent", WeaponKind.Crossbow, true, false));
        Assert.False(ProgressionRules.IsClumsy("Ironclad", WeaponKind.TwoHanded, true, false));
    }

    [Fact]
    public void SkillTreeShapeAndCosts()
    {
        Assert.Equal(69, SkillTree.Nodes.Count);
        Assert.Equal(Enum.GetValues<Skill>().Length, SkillTree.Nodes.Count);
        Assert.Equal(3, SkillTree.Node(Skill.HerbLore).Rank);
        Assert.Equal(2, SkillTree.Node(Skill.Wayfarer).Rank);
        Assert.Equal(1, SkillTree.Cost(SkillTree.Node(Skill.Fighter), false));
        Assert.Equal(2, SkillTree.Cost(SkillTree.Node(Skill.Destruction1), false));
        Assert.Equal(1, SkillTree.Cost(SkillTree.Node(Skill.Destruction1), true));
        Assert.True(SkillTree.GrantsMastery(SkillTree.Node(Skill.Phantasm)));
        Assert.False(SkillTree.GrantsMastery(SkillTree.Node(Skill.CampKettle)));
    }

    [Fact]
    public void SkillTreeOrder()
    {
        var none = new HashSet<Skill>();
        var learnable = SkillTree.Learnable("Ironclad", none, 1, _ => false).Select(n => n.Skill).ToList();
        Assert.Contains(Skill.Fighter, learnable);
        Assert.DoesNotContain(Skill.Fencer, learnable);
        Assert.DoesNotContain(Skill.Bloodletter, learnable);       // ветка Безмолвной
        Assert.DoesNotContain(Skill.Destruction1, learnable);      // чужая школа — 2 очка
        Assert.Contains(Skill.CampKettle, learnable);
        Assert.DoesNotContain(Skill.Wayfarer, learnable);          // ярус II требует 2 узла Выживания
        var two = new HashSet<Skill> { Skill.CampKettle, Skill.FirstAid };
        Assert.Contains(Skill.Wayfarer, SkillTree.Learnable("Ironclad", two, 1, _ => false).Select(n => n.Skill));
        Assert.Contains(Skill.Destruction1, SkillTree.Learnable("Regent", none, 1, _ => true).Select(n => n.Skill));
        var parsed = SkillTree.Parse(SkillTree.Serialize(new[] { Skill.Fighter, Skill.HerbLore }) + ",Bogus");
        Assert.Equal(2, parsed.Count);
    }

    [Fact]
    public void CampPointsAndCosts()
    {
        Assert.Equal(2, CampRules.Budget(false, false));
        Assert.Equal(4, CampRules.Budget(true, true));
        Assert.Equal(2, CampRules.Cost(CampAction.Rest, false));
        Assert.Equal(1, CampRules.Cost(CampAction.Forge, true));
        Assert.Equal(1, CampRules.Cost(CampAction.Enchant, true));
        Assert.Equal(0, CampRules.Cost(CampAction.Chest, false));
        Assert.Equal(2, CampRules.Cost(CampAction.Other, true));
        Assert.True(CampRules.IsCraft(CampAction.Brew));
        Assert.False(CampRules.IsCraft(CampAction.Cook));
        Assert.Equal(20, CampRules.ShortRestPercent(true));
        Assert.Equal(15m, CampRules.HealAmount(100, 15));
    }

    [Fact]
    public void FatigueAndSoulStones()
    {
        Assert.False(CampRules.FatigueEnabled(4));
        Assert.Equal(0, CampRules.FatigueStacks(9, false));
        Assert.Equal(2, CampRules.FatigueStacks(7, true));
        Assert.Equal(3, CampRules.FatigueStacks(30, true));
        Assert.Equal(0, CampRules.SoulStoneFor(EnemyRank.Normal, 0));
        Assert.Equal(1, CampRules.SoulStoneFor(EnemyRank.Elite, 0));
        Assert.Equal(2, CampRules.SoulStoneFor(EnemyRank.Boss, 1));
        Assert.Equal(3, CampRules.SoulStoneFor(EnemyRank.Boss, 4));
    }

    [Fact]
    public void Food()
    {
        var meals = FoodRules.Eat(new List<Meal>(), Dish.VenisonRoast, 1);
        meals = FoodRules.Eat(meals, Dish.FishSoup, 1);
        Assert.Single(meals);
        Assert.Equal(Dish.FishSoup, meals[0].Dish);
        var two = FoodRules.Eat(meals, Dish.VenisonRoast, FoodRules.Slots(true));
        Assert.Equal(2, two.Count);
        Assert.Equal(1, FoodRules.Total(two, 0).StaminaRegen);
        Assert.Equal(1, FoodRules.Total(two, 2).Regen);
        var after = FoodRules.AfterCombat(new List<Meal> { new(Dish.Feast, 1), new(Dish.SweetRoll, 3) });
        Assert.Single(after);
        Assert.Equal(2, after[0].Fights);
        var feast = FoodRules.Effect(Dish.Feast);
        Assert.Equal(6, feast.MaxHp);
        Assert.Equal(1, feast.OpeningDraw);
        Assert.Equal(1, FoodRules.DurationOf(Dish.Feast));
        Assert.Equal(meals, FoodRules.Parse(FoodRules.Serialize(meals)));
    }

    [Fact]
    public void Enchanting()
    {
        Assert.True(EnchantRules.Allowed(Enchant.Health, EquipSlot.Head));
        Assert.False(EnchantRules.Allowed(Enchant.Health, EquipSlot.Ring));
        Assert.True(EnchantRules.Allowed(Enchant.FireBrand, EquipSlot.MainHand));
        Assert.Equal(12, EnchantRules.Value(Enchant.Health, 3));
        Assert.Equal(2, EnchantRules.Value(Enchant.FrostResistance, 3));
        Assert.Equal(1, EnchantRules.Value(Enchant.FrostResistance, 2));
        Assert.Equal(16, EnchantRules.Charges(3));
        var slots = EnchantRules.Apply(new List<EnchantSlot>(), Enchant.Health, 1, 1);
        slots = EnchantRules.Apply(slots, Enchant.Magic, 2, 1);
        Assert.Single(slots);
        Assert.Equal(Enchant.Magic, slots[0].Enchant);
        var brand = EnchantRules.Apply(new List<EnchantSlot>(), Enchant.FireBrand, 2, 2);
        Assert.Equal(12, brand[0].Charges);
        Assert.Equal(brand, EnchantRules.Parse(EnchantRules.Serialize(brand)));
    }

    [Fact]
    public void ShoutsAndRevel()
    {
        Assert.Equal(3, ShoutRules.Cooldown(3, 0));
        Assert.Equal(1, ShoutRules.Cooldown(3, 5));
        Assert.Equal(2, ShoutRules.ShelfCapacity(true));
        Assert.True(ShoutRules.SoulTearExecutes(24, 100, false));
        Assert.False(ShoutRules.SoulTearExecutes(24, 100, true));
        Assert.Equal((PantheonRules.DrinkBonus.Mana, 2), PantheonRules.RevelSecondProperty(Brew.SecondWind, 3));
        Assert.Equal((PantheonRules.DrinkBonus.Block, 9), PantheonRules.RevelSecondProperty(Brew.Giant, 5));
        Assert.Equal((PantheonRules.DrinkBonus.None, 0), PantheonRules.RevelSecondProperty(Brew.SnakeBite, 1));
    }
}

public class Stage6Tests
{
    [Fact]
    public void Compatibility()
    {
        Assert.True(TransformationRules.CanTakeWorldview(FormKind.None, FormKind.Vampire, false, false, false));
        Assert.False(TransformationRules.CanTakeWorldview(FormKind.Vampire, FormKind.Werewolf, false, false, false));
        Assert.False(TransformationRules.CanTakeWorldview(FormKind.None, FormKind.Righteous, false, false, false));
        Assert.True(TransformationRules.CanTakeWorldview(FormKind.None, FormKind.Righteous, true, false, false));
        Assert.True(TransformationRules.CanTakeWorldview(FormKind.DarkPact, FormKind.Herald, false, false, false));
        Assert.False(TransformationRules.CanTakeWorldview(FormKind.None, FormKind.Lich, false, false, false));
        Assert.False(TransformationRules.CanTakeWorldview(FormKind.None, FormKind.Vampire, false, false, true));
        Assert.True(TransformationRules.CanTakeClockwork(FormKind.Righteous, false));
        Assert.False(TransformationRules.CanTakeClockwork(FormKind.Vampire, false));
        Assert.True(TransformationRules.IsApostateForm(FormKind.Lich));
        Assert.False(TransformationRules.IsWorldview(FormKind.Clockwork));
    }

    [Fact]
    public void RighteousAndVampire()
    {
        Assert.True(TransformationRules.HaloTriggers(39, 80, false));
        Assert.False(TransformationRules.HaloTriggers(40, 80, false));
        Assert.Equal(2, TransformationRules.RighteousStepAfterAct(2, true));
        Assert.Equal(1, TransformationRules.RighteousStepAfterAct(1, false));
        Assert.Equal(-1, TransformationRules.Thirst(0).FireResistance);
        Assert.Equal(1.5m, TransformationRules.Thirst(3).FireLightTakenMultiplier);
        Assert.True(TransformationRules.Thirst(9).HealingOnlyByFeeding);
        Assert.Equal(4, TransformationRules.ThirstAfterCombat(4, false));
        Assert.Equal(0, TransformationRules.ThirstAfterCombat(3, true));
        Assert.Equal(2, TransformationRules.Lifesteal(9, 25));
        Assert.True(TransformationRules.BecomesBloodLord(3));
    }

    [Fact]
    public void BeastAndPact()
    {
        Assert.Equal(5, TransformationRules.MoonCallDuration(3));
        Assert.Equal(2, TransformationRules.HitsPerExtensionFor(2));
        Assert.Equal(2, TransformationRules.ExtensionTurns(2, 7, 3));
        Assert.Equal(8, TransformationRules.BeastAttackBonusFor(3));
        Assert.Equal(12, TransformationRules.AlphaWolfHp(2));
        Assert.Equal(9, TransformationRules.BeastNodesEarned(40));
        Assert.Equal(HeraldStance.Plague, TransformationRules.NextStance(HeraldStance.Decay));
        Assert.Equal(22, TransformationRules.LichMaxHpLoss(75));
        Assert.Equal(0, TransformationRules.LichMaxHpLoss(1));
        Assert.True(TransformationRules.CanBecomeLich(FormKind.Herald, 10));
        Assert.False(TransformationRules.CanBecomeLich(FormKind.DarkPact, 99));
        Assert.Equal(21, TransformationRules.PhylacteryReviveHp(70));
    }

    [Fact]
    public void Healing()
    {
        Assert.Equal(-100, TransformationRules.HealPercent(new[] { -50, -100 }));
        Assert.Equal(15m, TransformationRules.ApplyHeal(10, 50, false));
        Assert.Equal(3m, TransformationRules.ApplyHeal(7, -50, false));
        Assert.Equal(0m, TransformationRules.ApplyHeal(10, 0, true));
    }

    [Fact]
    public void FormEvents()
    {
        Assert.Equal(new[] { FormKind.Werewolf, FormKind.DarkPact }, TransformationRules.EventFormsFor(0, 3));
        Assert.Contains(FormKind.Lich, TransformationRules.EventFormsFor(3, 5));
        Assert.DoesNotContain(FormKind.Lich, TransformationRules.EventFormsFor(2, 5));
        Assert.Equal(FormKind.None, TransformationRules.UnknownRoomEvent(0, 3, 12, 0, 0));
        Assert.Equal(FormKind.DarkPact, TransformationRules.UnknownRoomEvent(0, 3, 0, 1, 0));
        var seen = (1 << (int)FormKind.Werewolf) | (1 << (int)FormKind.DarkPact);
        Assert.Equal(FormKind.None, TransformationRules.UnknownRoomEvent(0, 3, 0, 0, seen));
    }

    [Fact]
    public void Paths()
    {
        Assert.Equal(2, TransformationRules.PathOfferActIndex(5));
        Assert.True(TransformationRules.PathOfferDue(8, 2, 5, false));
        Assert.False(TransformationRules.PathOfferDue(7, 2, 5, false));
        Assert.False(TransformationRules.PathAllowed(HeroPath.Paladin, FormKind.Vampire, true, false));
        Assert.True(TransformationRules.PathAllowed(HeroPath.BlackKnight, FormKind.Vampire, true, false));
        var offer = TransformationRules.OfferPaths("Ironclad", FormKind.None, false, false, _ => 0);
        Assert.Equal(3, offer.Count);
        Assert.Equal(HeroPath.Champion, offer[0]);
        Assert.Equal(HeroPath.Barbarian, offer[1]);
        Assert.Equal(8, TransformationRules.MonkUnarmedDamage(8));
        Assert.True(TransformationRules.FearTurn(4));
        Assert.Equal(4, TransformationRules.Reflected(9));
        Assert.Equal(2, TransformationRules.ChampionUnshakeableStagger(3));
    }

    [Fact]
    public void CardEnchants()
    {
        Assert.True(TransformationRules.CanEnchantCard(CardEnchant.Lightened, false, false));
        Assert.False(TransformationRules.CanEnchantCard(CardEnchant.Flaming, false, true));
        Assert.False(TransformationRules.CanEnchantCard(CardEnchant.Double, true, false));
        foreach (var a in new[] { CardEnchant.Flaming, CardEnchant.Frost, CardEnchant.Storm, CardEnchant.Lightened, CardEnchant.Breathing })
        foreach (var b in new[] { CardEnchant.Flaming, CardEnchant.Frost, CardEnchant.Storm, CardEnchant.Lightened, CardEnchant.Breathing })
            if (TransformationRules.ValidDouble(a, b))
                Assert.Equal((a, b), TransformationRules.DecodeDouble(TransformationRules.EncodeDouble(a, b)));
        Assert.Equal(0, TransformationRules.CardWeightAfter(0, true));
        Assert.Equal(1, TransformationRules.StaminaAfter(2, true));
    }

    [Fact]
    public void ScoreMultiplier()
    {
        Assert.Equal(1.0, MemoryStoneRules.ScoreMultiplier(0), 3);
        Assert.Equal(0.7, MemoryStoneRules.ScoreMultiplier(2), 3);
        Assert.Equal(0.0, MemoryStoneRules.ScoreMultiplier(9), 3);
    }
}

public class M5RollbackAndNodeTests
{
    [Fact]
    public void CryptCandleAndRejoin()
    {
        Assert.True(MemoryStoneRules.CanTakeCryptCandle(true, true, true, 2, 0));
        Assert.False(MemoryStoneRules.CanTakeCryptCandle(true, true, true, 2, 1));
        Assert.False(MemoryStoneRules.CanTakeCryptCandle(true, true, true, MemoryStoneRules.Infinite, 0));
        Assert.False(MemoryStoneRules.CanTakeCryptCandle(false, true, true, 2, 0));
        Assert.False(MemoryStoneRules.CanTakeCryptCandle(true, true, false, 0, 0));
        Assert.Equal(new[] { 0, 2000, 2500, 3500, 5000, 6000, 6000 }, new[] { -1, 0, 1, 2, 3, 4, 9 }.Select(MemoryStoneRules.RejoinDelayMs));
        Assert.Equal(49000, MemoryStoneRules.RejoinTotalMs());
        Assert.True(MemoryStoneRules.ShouldRetryRejoin(0, false, true));
        Assert.False(MemoryStoneRules.ShouldRetryRejoin(MemoryStoneRules.RejoinAttempts, false, true));
        Assert.False(MemoryStoneRules.ShouldRetryRejoin(1, true, true));
        Assert.False(MemoryStoneRules.ShouldRetryRejoin(1, false, false));
    }

    private static readonly (ActKind Kind, int Slot)[] FullRun =
    [
        (ActKind.Foothills, 0), (ActKind.FrozenBarrows, 1), (ActKind.ClockworkDepths, 1), (ActKind.Hive, 2),
        (ActKind.BloodCourt, 3), (ActKind.StarTower, 3), (ActKind.Glory, 4),
    ];

    [Fact]
    public void TwentySubtypesAllGenerated()
    {
        Assert.Equal(NodeRules.SubtypeCount, NodeRules.All.Count);
        Assert.Equal(20, NodeRules.All.Select(NodeRules.IconName).Distinct().Count());
        foreach (var sub in NodeRules.All)
            Assert.True(FullRun.Any(a => NodeRules.AvailableIn(sub, a.Kind, a.Slot)), sub.ToString());

        var generated = new HashSet<NodeSubtype>();
        foreach (var mark in Enum.GetValues<MapMark>())
            if (NodeRules.FromMark(mark) is { } m) generated.Add(m);
        foreach (var shop in Enum.GetValues<ShopType>())
            if (NodeRules.FromShop(shop) is { } s) generated.Add(s);
        foreach (var roll in new[] { 0, 1, 2, 3 })
            foreach (var lib in new[] { false, true })
                if (NodeRules.FromUnknownRoll(roll, lib) is { } u) generated.Add(u);
        generated.UnionWith(NodeRules.All.Where(s => NodeRules.RevealOf(s) == NodeReveal.Fixed));
        generated.Add(NodeSubtype.Tavern);
        Assert.Equal(NodeRules.All.ToHashSet(), generated);
        Assert.Equal(NodeReveal.Hidden, NodeRules.RevealOf(NodeSubtype.Ambush));
    }

    [Fact]
    public void AllSevenShopTypesRoll()
    {
        var seen = new HashSet<ShopType>();
        foreach (var (kind, slot) in FullRun)
            for (var h = 0; h < 400; h++)
                for (var n = 0; n < 3; n++)
                    seen.Add(ShopRules.RollShopType(ShopRules.Fnv1a("s", h), kind, Math.Min(slot, 2), n, null));
        seen.Remove(ShopType.Wanderer);
        Assert.Equal(7, seen.Count);
    }

    [Fact]
    public void NodeNumbers()
    {
        Assert.Equal(new[] { 1, 1, 0, 0, 0 }, Enumerable.Range(0, 5).Select(NodeRules.LairCount));
        Assert.Equal(new[] { 0, 2, 1 }, new[] { NodeRules.TrialCount(0, 3), NodeRules.TrialCount(1, 3), NodeRules.TrialCount(1, 4) });
        Assert.Equal(new[] { 0, 2, 1 }, new[] { NodeRules.AmbushCount(0, 1), NodeRules.AmbushCount(2, 1), NodeRules.AmbushCount(2, 2) });
        Assert.Equal(new[] { 70, 7, 29, 1 }, new[] { 100, 10, 41, 1 }.Select(NodeRules.LairHp));
        Assert.False(NodeRules.RollLibrary(0, 0));
        Assert.True(NodeRules.RollLibrary(1, 8));
        Assert.False(NodeRules.RollLibrary(1, 9));
        Assert.Equal(new[] { 30, 12, 55, 2 }, new[] { NodeRules.CacheGold(0), NodeRules.CacheTrap(4), NodeRules.AmbushGold(4), NodeRules.TrialStrength(3) });
    }
}

public class M6MapGraphTests
{
    /// <summary>Карта 7 × 15 из четырёх непересекающихся путей (ряды 1–14, последний — лагеря).</summary>
    private static GraphView Sample()
    {
        int[][] paths =
        [
            [1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1],
            [3, 2, 2, 3, 3, 2, 2, 3, 3, 2, 2, 3, 3, 3],
            [4, 5, 5, 4, 4, 5, 5, 4, 4, 5, 5, 4, 4, 3],
            [6, 6, 6, 6, 5, 6, 6, 6, 5, 6, 6, 6, 6, 5],
        ];
        var g = new GraphView(7, 15);
        foreach (var path in paths)
            for (var i = 0; i < path.Length; i++)
            {
                var row = i + 1;
                var kind = row switch
                {
                    14 => NodeKind.Rest,
                    8 => NodeKind.Treasure,
                    6 or 10 => NodeKind.Elite,
                    3 or 7 or 11 => NodeKind.Unknown,
                    _ => NodeKind.Monster,
                };
                g.Node(path[i], row, kind);
                if (i > 0) g.Edge(path[i - 1], row - 1, path[i], row);
            }
        return g;
    }

    private static Func<int, int> Seq(int seed)
    {
        var rnd = new Random(seed);
        return n => n <= 0 ? 0 : rnd.Next(n);
    }

    [Fact]
    public void SampleHasNoCrossings()
    {
        var g = Sample();
        foreach (var e in g.Edges)
            Assert.DoesNotContain(g.Edges, x => MapGraphRules.Crosses(e.From, e.To, x.From, x.To));
    }

    [Fact]
    public void TowerPortalsEndInFights()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var g = Sample();
            var plan = MapGraphRules.Plan(g, ActKind.StarTower, [], Seq(seed));
            var portals = plan.Edges.Where(e => e.Kind == EdgeKind.Portal).ToList();
            Assert.InRange(portals.Count, 2, 3);
            Assert.All(portals, p =>
            {
                Assert.Equal(p.From.Row + 2, p.To.Row);
                Assert.Contains(g.Nodes[p.To], new[] { NodeKind.Monster, NodeKind.Elite });
            });
            Assert.Single(plan.Edges, e => e.Kind == EdgeKind.Danger);
            Assert.NotNull(plan.LastRest);
            Assert.Equal(14, plan.LastRest!.Value.Row);
        }
    }

    [Fact]
    public void ValvesNeverCutTheRoad()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            var g = Sample();
            var valves = new List<Cell> { new(0, 3), new(5, 7), new(2, 11) };
            var plan = MapGraphRules.Plan(g, ActKind.ClockworkDepths, valves, Seq(seed));
            Assert.Contains(plan.Edges, e => e.Kind == EdgeKind.ValveClose);
            for (var mask = 0; mask < 8; mask++)
            {
                var m = mask;
                var state = new TravelState(false, false, v => (m & (1 << valves.IndexOf(v))) != 0, _ => false);
                Assert.True(MapGraphRules.ReachesLastRow(g, (a, b) => MapGraphRules.IsBlocked(plan.EdgeOf(a, b), false, b, state)),
                    $"seed {seed}, valves {mask}");
            }
        }
    }

    [Fact]
    public void PocketsAndSecretsAreAdditive()
    {
        var g = Sample();
        var before = g.Edges.ToHashSet();
        var plan = MapGraphRules.Plan(g, ActKind.FrozenBarrows, [], Seq(3));
        Assert.All(before, e => Assert.Contains(e, g.Edges));
        Assert.InRange(plan.Pockets.Count, 1, 2);
        foreach (var pocket in plan.Pockets)
        {
            Assert.Equal(NodeKind.Treasure, g.Nodes[pocket]);
            Assert.Contains(plan.Edges, e => e.Kind == EdgeKind.PocketIn && e.To == pocket && e.From.Row == pocket.Row - 1);
            Assert.Contains(plan.Edges, e => e.Kind == EdgeKind.PocketOut && e.From == pocket && e.To.Row == pocket.Row + 1);
        }
        Assert.InRange(plan.Edges.Count(e => e.Kind == EdgeKind.Secret), 1, 2);
        Assert.DoesNotContain(plan.Edges, e => e.Kind is EdgeKind.Portal or EdgeKind.ValveOpen or EdgeKind.ValveClose);
        var noSight = new TravelState(false, false, _ => false, _ => false);
        Assert.True(MapGraphRules.ReachesLastRow(g, (a, b) => MapGraphRules.IsBlocked(plan.EdgeOf(a, b), false, b, noSight)));
    }

    [Fact]
    public void PlanIsDeterministicAndRoundTrips()
    {
        var a = MapGraphRules.Plan(Sample(), ActKind.StarTower, [], Seq(7)).Encode();
        var b = MapGraphRules.Plan(Sample(), ActKind.StarTower, [], Seq(7)).Encode();
        Assert.Equal(a, b);
        Assert.Equal(a, MapPlan.Decode(a).Encode());
        var depths = MapGraphRules.Plan(Sample(), ActKind.ClockworkDepths, [new Cell(0, 3)], Seq(1));
        Assert.Equal(depths.Encode(), MapPlan.Decode(depths.Encode()).Encode());
        Assert.True(MapGraphRules.Plan(Sample(), ActKind.Hive, [], Seq(1)).IsEmpty);
        Assert.True(MapPlan.Decode("garbage;Z:1,2>3,4;P:x").IsEmpty);
    }

    [Fact]
    public void BlockingAndSight()
    {
        var to = new Cell(1, 2);
        var none = new TravelState(false, false, _ => false, _ => false);
        var all = new TravelState(true, true, _ => true, _ => false);
        var secret = new PlanEdge(EdgeKind.Secret, new Cell(1, 1), to);
        var open = new PlanEdge(EdgeKind.ValveOpen, new Cell(1, 1), to, new Cell(0, 0));
        var close = new PlanEdge(EdgeKind.ValveClose, new Cell(1, 1), to, new Cell(0, 0));
        Assert.True(MapGraphRules.IsBlocked(secret, false, to, none));
        Assert.False(MapGraphRules.IsBlocked(secret, false, to, all));
        Assert.True(MapGraphRules.IsBlocked(open, false, to, none));
        Assert.False(MapGraphRules.IsBlocked(open, false, to, all));
        Assert.False(MapGraphRules.IsBlocked(close, false, to, none));
        Assert.True(MapGraphRules.IsBlocked(close, false, to, all));
        Assert.True(MapGraphRules.IsBlocked(null, true, to, none));
        Assert.False(MapGraphRules.IsBlocked(null, true, to, all));
        Assert.False(MapGraphRules.IsBlocked(null, true, to, none with { VaultOpened = _ => true }));
        Assert.False(MapGraphRules.IsVisible(secret, false));
        Assert.True(MapGraphRules.IsVisible(open, false));
        Assert.True(MapGraphRules.NightSight(false, false, true, false));
        Assert.True(MapGraphRules.SecretSight(false, true, false));
        Assert.False(MapGraphRules.SecretSight(false, false, false));
        Assert.Equal(110, MapGraphRules.LastRestBossHp(100));
        Assert.Equal(56, MapGraphRules.LastRestBossHp(50) + 1);
        Assert.Equal(NodeGroup.Trade, NodeRules.GroupOf(NodeSubtype.Armorer));
        Assert.Equal(NodeGroup.Danger, NodeRules.GroupOf(NodeSubtype.Ambush));
        Assert.Equal(5, NodeRules.Groups.Count);
        Assert.Equal(NodeSubtype.Shrine, NodeRules.FromMark(MapMark.CelestialBanner));
    }
}

public class M6SummonTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 3)]
    [InlineData(-3, 1)]
    public void Slots(int bonus, int expected) => Assert.Equal(expected, SummonRules.Slots(bonus));

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 2, 0)]
    [InlineData(3, 2, 2)]
    [InlineData(0, 0, 0)]
    public void Dismissals(int others, int slots, int expected) => Assert.Equal(expected, SummonRules.Dismissals(others, slots));

    [Theory]
    [InlineData(10, 8, 0, 8)]
    [InlineData(5, 8, 0, 5)]
    [InlineData(5, 8, 6, 2)]
    [InlineData(5, 8, 9, 0)]
    public void Absorb(int incoming, int hp, int pending, int expected) => Assert.Equal(expected, SummonRules.Absorb(incoming, hp, pending));

    [Theory]
    [InlineData(10, 0, 10)]
    [InlineData(10, 50, 15)]
    [InlineData(3, 50, 5)]
    [InlineData(0, 50, 0)]
    public void ScaledLoss(int taken, int percent, int expected) => Assert.Equal(expected, SummonRules.ScaledLoss(taken, percent));

    [Fact]
    public void RaiseDead()
    {
        Assert.Equal(20, SummonRules.RaisedDeadHp(40, SummonRules.RaiseDeadPercent));
        Assert.Equal(26, SummonRules.RaisedDeadHp(40, SummonRules.RaiseDeadPercent + SummonRules.RaiseDeadPercentUpgrade));
        Assert.Equal(1, SummonRules.RaisedDeadHp(1, 50));
        Assert.Equal(0, SummonRules.RaisedDeadHp(0, 50));
        Assert.True(SummonRules.CanRaise(isEnemy: true, isBoss: false));
        Assert.False(SummonRules.CanRaise(isEnemy: true, isBoss: true));
        Assert.False(SummonRules.CanRaise(isEnemy: false, isBoss: false));
    }

    [Fact]
    public void KindsAndMoves()
    {
        Assert.Equal(SummonKind.BeastWolf, SummonRules.BeastKind(0));
        Assert.Equal(SummonKind.BeastSabertooth, SummonRules.BeastKind(1));
        Assert.Equal(SummonKind.BeastBear, SummonRules.BeastKind(4));
        Assert.Equal(SummonMove.Slumber, SummonRules.MoveFor(SummonKind.AbyssGuardian, 0));
        Assert.Equal(SummonMove.Strike, SummonRules.MoveFor(SummonKind.AbyssGuardian, 1));
        Assert.Equal(SummonMove.Strike, SummonRules.MoveFor(SummonKind.SpiritWolf, 0));
        Assert.True(SummonRules.IsUndead(SummonKind.BoneThrall));
        Assert.False(SummonRules.IsUndead(SummonKind.SpectralKnight));
        Assert.True(SummonRules.IsCompanion(SummonKind.LoyalGuard));
        var keys = Enum.GetValues<SummonKind>().Select(SummonRules.ArtKey).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(keys, k => Assert.StartsWith("summon_", k));
    }

    [Fact]
    public void Layout()
    {
        Assert.Equal(160f, SummonRules.LayoutX(100f, 0, false));
        Assert.Equal(680f, SummonRules.LayoutX(100f, 2, true));
    }
}

public class M6HeroLookTests
{
    [Theory]
    [InlineData(FormKind.Lich, false, HeroLook.Lich)]
    [InlineData(FormKind.Vampire, false, HeroLook.Vampire)]
    [InlineData(FormKind.Werewolf, false, HeroLook.Werewolf)]
    [InlineData(FormKind.Righteous, true, HeroLook.Clockwork)]
    [InlineData(FormKind.None, true, HeroLook.Clockwork)]
    [InlineData(FormKind.Herald, false, HeroLook.Hero)]
    [InlineData(FormKind.None, false, HeroLook.Hero)]
    public void LookFor(FormKind worldview, bool clockwork, HeroLook expected) =>
        Assert.Equal(expected, HeroLookRules.LookFor(worldview, clockwork));

    [Fact]
    public void SpritesAndModels()
    {
        Assert.True(HeroLookRules.BorrowsModel(HeroLook.Lich));
        Assert.True(HeroLookRules.BorrowsModel(HeroLook.Clockwork));
        Assert.False(HeroLookRules.BorrowsModel(HeroLook.Vampire));
        Assert.Equal("hero_vampire", HeroLookRules.SpriteKey(HeroLook.Vampire));
        Assert.Equal("hero_werewolf", HeroLookRules.SpriteKey(HeroLook.Werewolf));
        Assert.Null(HeroLookRules.SpriteKey(HeroLook.Lich));
        Assert.Equal("beast", HeroLookRules.SpriteVariant(HeroLook.Werewolf, true, false));
        Assert.Equal("lord", HeroLookRules.SpriteVariant(HeroLook.Vampire, false, true));
        Assert.Null(HeroLookRules.SpriteVariant(HeroLook.Werewolf, false, true));
        Assert.True(HeroLookRules.KeepsTint(HeroLook.Hero));
        Assert.False(HeroLookRules.KeepsTint(HeroLook.Lich));
        Assert.Equal(1f, HeroLookRules.BeastScaleFor(HeroLook.Werewolf));
        Assert.Equal(HeroLookRules.BeastScale, HeroLookRules.BeastScaleFor(HeroLook.Hero));
    }

    [Fact]
    public void PathSigns()
    {
        Assert.Equal("black_knight", HeroLookRules.PathSignKey(HeroPath.BlackKnight));
        Assert.Equal("element_lord", HeroLookRules.PathSignKey(HeroPath.ElementLord));
        Assert.Null(HeroLookRules.PathSignKey(HeroPath.None));
        Assert.Null(HeroLookRules.PathSignKey(HeroPath.Reborn));
        var keys = Enum.GetValues<HeroPath>().Select(HeroLookRules.PathSignKey).Where(k => k != null).ToList();
        Assert.Equal(12, keys.Distinct().Count());
    }

    [Fact]
    public void Orbs()
    {
        Assert.Equal(new[] { Element.Fire, Element.Shock, Element.Frost }, HeroLookRules.OrbSequence(1, 1, 1));
        Assert.Equal(TransformationRules.MaxOrbs, HeroLookRules.OrbSequence(0, 9, 0).Count);
        Assert.Empty(HeroLookRules.OrbSequence(-1, 0, 0));
        Assert.Equal("PlasmaOrb", HeroLookRules.OrbArt(Element.Fire));
        var (x0, y0) = HeroLookRules.OrbSlot(0, 3, true);
        Assert.InRange(x0, 194.5f, 195.2f);
        Assert.InRange(y0, -112.8f, -112.2f);
        var (x2, _) = HeroLookRules.OrbSlot(2, 3, true);
        Assert.InRange(x2, -204.2f, -203.6f);
        Assert.Equal(HeroLookRules.OrbSlot(0, 1, true), HeroLookRules.OrbSlot(4, 1, true));
    }
}

public class M6PaperDollTests
{
    [Theory]
    [InlineData(0, ItemMaterial.Iron)]
    [InlineData(1, ItemMaterial.Iron)]
    [InlineData(2, ItemMaterial.Steel)]
    [InlineData(3, ItemMaterial.Silver)]
    [InlineData(4, ItemMaterial.Obsidian)]
    [InlineData(5, ItemMaterial.EternalIce)]
    [InlineData(9, ItemMaterial.EternalIce)]
    public void MaterialByTier(int tier, ItemMaterial expected) =>
        Assert.Equal(expected, PaperDollRules.MaterialFor(tier, false));

    [Fact]
    public void MaterialOverrides()
    {
        Assert.Equal(ItemMaterial.Abyssal, PaperDollRules.MaterialFor(5, true));
        Assert.Equal(ItemMaterial.ScaledSteel, PaperDollRules.MaterialFor(1, true, ItemMaterial.ScaledSteel));
        Assert.Equal(PaperDollRules.MaterialColor(ItemMaterial.Iron), PaperDollRules.MaterialColor(ItemMaterial.None));
        Assert.True(PaperDollRules.HasGlint(ItemMaterial.Silver));
        Assert.False(PaperDollRules.HasGlint(ItemMaterial.Iron));
        Assert.True(PaperDollRules.HasSmoke(ItemMaterial.Abyssal));
        Assert.False(PaperDollRules.HasSmoke(ItemMaterial.Obsidian));
    }

    [Fact]
    public void Aura()
    {
        Assert.False(PaperDollRules.AuraFor([]).IsVisible);
        var aura = PaperDollRules.AuraFor([
            (ArmorClass.Heavy, 1, ItemMaterial.None),
            (ArmorClass.Heavy, 3, ItemMaterial.None),
            (ArmorClass.Light, 5, ItemMaterial.None)]);
        Assert.Equal(ArmorClass.Heavy, aura.Class);
        Assert.Equal(ItemMaterial.Silver, aura.Material);
        Assert.InRange(aura.Strength, 0.849f, 0.851f);
        Assert.Equal(ArmorClass.Light, PaperDollRules.AuraFor([(ArmorClass.Robe, 2, ItemMaterial.None), (ArmorClass.Light, 1, ItemMaterial.None)]).Class);
        Assert.Equal(1f, PaperDollRules.AuraFor(Enumerable.Repeat((ArmorClass.Light, 1, ItemMaterial.Iron), 4)).Strength);
        Assert.Equal(AuraStyle.Flicker, PaperDollRules.StyleFor(ArmorClass.Light));
        Assert.True(PaperDollRules.AuraPulseSeconds(AuraStyle.Flicker) < PaperDollRules.AuraPulseSeconds(AuraStyle.Steady));
    }

    [Fact]
    public void Hands()
    {
        Assert.Equal("greatsword", PaperDollRules.WeaponArt(WeaponKind.TwoHanded));
        Assert.Null(PaperDollRules.WeaponArt(WeaponKind.None));
        Assert.True(PaperDollRules.BlocksOffHand(WeaponKind.Bow));
        Assert.False(PaperDollRules.BlocksOffHand(WeaponKind.Sword));
        Assert.True(PaperDollRules.ShowsWeapons(false, false));
        Assert.False(PaperDollRules.ShowsWeapons(true, false));
        Assert.False(PaperDollRules.ShowsWeapons(false, true));
        Assert.Equal((70, 130), PaperDollRules.Pivot(WeaponKind.Shield));
        Assert.Equal((70, 190), PaperDollRules.Pivot(WeaponKind.Sword));
        Assert.True(PaperDollRules.WeaponScale(WeaponKind.Dagger) < PaperDollRules.WeaponScale(WeaponKind.TwoHanded));
    }
}

public class M6ShopUiTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(10, 2)]
    [InlineData(11, 3)]
    public void OverloadTier(int excess, int tier) => Assert.Equal(tier, ShopUiRules.OverloadTier(excess));

    [Fact]
    public void TierMatchesBurdenRules()
    {
        for (var excess = 0; excess <= 15; excess++)
        {
            var overload = BurdenRules.Evaluate(20 + excess, 20);
            var tier = ShopUiRules.OverloadTier(excess);
            Assert.Equal(overload.OpeningHandPenalty > 0, tier >= 2);
            Assert.Equal(!overload.CanPickUp, tier == 3);
        }
    }

    [Theory]
    [InlineData(18, 20, 2, false)]
    [InlineData(19, 20, 2, true)]
    [InlineData(22, 20, 3, false)]
    [InlineData(25, 20, 1, true)]
    [InlineData(31, 20, -2, false)]
    [InlineData(40, 20, 0, false)]
    public void OverloadWarning(int weight, int capacity, int delta, bool warn) =>
        Assert.Equal(warn, ShopUiRules.NeedsOverloadWarning(ShopUiRules.Preview(weight, capacity, delta)));

    [Fact]
    public void PreviewSell()
    {
        var preview = ShopUiRules.Preview(31, 20, -2);
        Assert.Equal(29, preview.After);
        Assert.Equal(9, preview.ExcessAfter);
        Assert.True(preview.Improves);
        Assert.Equal(0, ShopUiRules.Preview(1, 20, -5).After);
    }

    [Fact]
    public void Tabs()
    {
        Assert.True(ShopUiRules.TabEnabled(ShopTab.Buy, 0, 0));
        Assert.False(ShopUiRules.TabEnabled(ShopTab.Sell, 0, 3));
        Assert.True(ShopUiRules.TabEnabled(ShopTab.Services, 0, 1));
        Assert.Equal(0, ShopUiRules.TabBadge(ShopTab.Buy, 2, 5));
        Assert.Equal(2, ShopUiRules.TabBadge(ShopTab.Sell, 2, 5));
        Assert.Equal(5, ShopUiRules.TabBadge(ShopTab.Services, 2, 5));
    }

    [Fact]
    public void SellOrderAndPercent()
    {
        Assert.Equal(new[] { 2, 1, 0, 3 }, ShopUiRules.SellOrder([(10, 1), (30, 2), (30, 4), (10, 1)]));
        Assert.Equal(35, ShopRules.SellPercent(2, false));
        Assert.Equal(50, ShopRules.SellPercent(3, false));
        Assert.Equal(60, ShopRules.SellPercent(0, true));
    }
}

public class M7SuperbossLookTests
{
    [Theory]
    [InlineData(Superboss.BlackWanderer, true, false, false, "guard")]
    [InlineData(Superboss.BlackWanderer, false, true, true, null)]
    [InlineData(Superboss.BloodCourtLord, false, true, false, "cocoon")]
    [InlineData(Superboss.BloodCourtLord, true, false, true, null)]
    [InlineData(Superboss.WorldEater, false, false, true, null)]
    [InlineData(Superboss.WorldEater, false, false, false, "landed")]
    [InlineData(Superboss.NamelessShadow, false, true, false, null)]
    [InlineData(Superboss.NamelessShadow, false, false, false, "exposed")]
    [InlineData(Superboss.GatebreakerEnvoy, true, true, false, null)]
    public void Variant(Superboss boss, bool guarding, bool invulnerable, bool flying, string? expected) =>
        Assert.Equal(expected, SuperbossLookRules.Variant(boss, guarding, invulnerable, flying));

    [Fact]
    public void EveryVariantPreloaded()
    {
        foreach (var boss in Enum.GetValues<Superboss>())
        {
            var seen = new HashSet<string>();
            foreach (var g in new[] { false, true })
            foreach (var i in new[] { false, true })
            foreach (var f in new[] { false, true })
                if (SuperbossLookRules.Variant(boss, g, i, f) is { } v) seen.Add(v);
            Assert.Equal(seen.OrderBy(x => x), SuperbossLookRules.Variants(boss).OrderBy(x => x));
        }
    }

    [Fact]
    public void ArtKeysUnique()
    {
        var keys = Enum.GetValues<Superboss>().Select(SuperbossLookRules.ArtKey).Append(SuperbossLookRules.EchoKnightArt).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Equal("world_eater", SuperbossLookRules.ArtKey(Superboss.WorldEater));
    }

    [Fact]
    public void CourtTints()
    {
        foreach (var boss in Enum.GetValues<Superboss>())
        {
            var t = SuperbossLookRules.TintFor(boss);
            Assert.InRange(t.Hue, 0f, 0.999f);
            foreach (var c in new[] { t.R, t.G, t.B }) Assert.InRange(c, SuperbossLookRules.MinTintChannel, 1f);
        }
        Assert.Equal(Superboss.BloodCourtLord, SuperbossLookRules.CourtOf("GargoyleServant"));
        Assert.Equal(Superboss.FirstSummoner, SuperbossLookRules.CourtOf("CultistThrall"));
        Assert.Null(SuperbossLookRules.CourtOf("AbyssGateObject"));
        Assert.Equal(0.01f, SuperbossLookRules.CombinedHue(0.96f, 0.05f), 4);
        Assert.Equal(0.6f, SuperbossLookRules.CombinedHue(0.6f, 0f), 4);
    }
}

public class M7ActLookTests
{
    [Theory]
    [InlineData(ActKind.FrozenBarrows, "frozen_barrows")]
    [InlineData(ActKind.ClockworkDepths, "clockwork_depths")]
    [InlineData(ActKind.BloodCourt, "blood_court")]
    [InlineData(ActKind.StarTower, "star_tower")]
    [InlineData(ActKind.NamelessLimit, "nameless_limit")]
    [InlineData(ActKind.Foothills, null)]
    [InlineData(ActKind.Hive, null)]
    [InlineData(ActKind.Glory, null)]
    [InlineData(ActKind.Other, null)]
    public void ArtKey(ActKind kind, string? expected) => Assert.Equal(expected, ActLookRules.ArtKey(kind));

    [Fact]
    public void PiecePathsAndTints()
    {
        Assert.Equal("acts/star_tower/combat_fg.png", ActLookRules.PiecePath(ActKind.StarTower, ActLookRules.CombatFore));
        Assert.Null(ActLookRules.PiecePath(ActKind.Glory, ActLookRules.Camp));
        Assert.Equal(6, ActLookRules.Pieces.Distinct().Count());
        foreach (var kind in Enum.GetValues<ActKind>())
            Assert.Equal(ActLookRules.ArtKey(kind) != null, ActLookRules.ChestTint(kind) != null);
    }

    [Theory]
    [InlineData("RestSiteLighting", "RestSiteBG", true)]
    [InlineData("RestSiteFireLogs3", "RestSiteBG", true)]
    [InlineData("light_small3", "RestSiteBG2", true)]
    [InlineData("RestSiteBG2", "RestSiteBG2", true)]
    [InlineData("RestSiteBG", "RestSiteBG2", false)]
    [InlineData("RestSiteForeground", "RestSiteBG", false)]
    [InlineData("overlay_vfx", "RestSiteBG", false)]
    [InlineData("Foliage3", "RestSiteBG", false)]
    public void CampKeeps(string node, string target, bool keep) => Assert.Equal(keep, ActLookRules.CampKeeps(node, target));

    [Fact]
    public void CampTargetAndAspect()
    {
        Assert.Equal("RestSiteBG2", ActLookRules.CampTarget("underdocks"));
        Assert.Equal("RestSiteBG", ActLookRules.CampTarget("hive"));
        Assert.Equal(2048f / 960f, ActLookRules.CombatHalfWidth / ActLookRules.CombatHalfHeight, 3);
        var r = ActLookRules.CampRect;
        Assert.Equal(3072f / 1440f, (r[2] - r[0]) / (r[3] - r[1]), 2);
    }
}
