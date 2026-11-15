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
