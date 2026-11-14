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
