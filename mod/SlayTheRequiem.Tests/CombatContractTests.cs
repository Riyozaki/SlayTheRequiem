using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

/// <summary>Production Core contracts, not native battle traces. Engine/IL adapters have a separate fixture.</summary>
public class CombatContractTests
{
    [Theory]
    [InlineData(10, 3, 0, 0, 7)]
    [InlineData(15, 3, 0, 0, 12)]
    [InlineData(10, 3, 0, 5, 12)]
    [InlineData(10, 3, 2, 0, 9)]
    [InlineData(10, 3, 20, 5, 12)] // Pierce cannot manufacture negative armor after Sunder
    [InlineData(0, -3, 0, 5, 0)]  // immunity stays immunity
    [InlineData(10, 3, -4, -7, 7)]
    public void Armor_reference_packets(int damage, int armor, int pierce, int sunder, int expected) =>
        Assert.Equal(expected, CombatMath.ArmorStage(damage, true, DamageType.Physical, armor, pierce, sunder));

    [Fact]
    public void Fractional_damage_is_not_truncated_before_armor_or_raised_by_a_floor()
    {
        Assert.Equal(0.5m, CombatMath.ArmorStage(0.5m, true, DamageType.Fire, 8));
        Assert.Equal(4.5m, CombatMath.ArmorStage(7.5m, true, DamageType.Frost, 3));
        Assert.Equal(0m, CombatMath.ApplyPlating(0m, -20, sunder: 20));
        Assert.Equal(1m, Math.Min(1m, CombatMath.ArmorStage(10m, true, DamageType.Physical, 3, sunder: 8)));
    }

    [Theory]
    [InlineData(DamageType.True, true)]
    [InlineData(DamageType.Fire, false)]
    [InlineData(DamageType.Magic, false)]
    public void Only_powered_nontrue_hits_use_armor(DamageType type, bool powered) =>
        Assert.Equal(10m, CombatMath.ArmorStage(10m, powered, type, 7, sunder: 20));

    [Fact]
    public void Every_multihit_has_its_own_armor_and_cap_then_native_block()
    {
        var hit = CombatMath.ArmorStage(6m, true, DamageType.Physical, 2, sunder: 3);
        Assert.Equal(21m, hit * 3);
        Assert.Equal(3m, Math.Min(hit, 1m) * 3);
        Assert.Equal(0m, Math.Max(0, Math.Min(hit, 1m) - 2m)); // cap then Block, not armor after cap
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(0.9, 10, 0)]
    [InlineData(4.9, 10, 4)]
    [InlineData(10, 3, 3)]
    [InlineData(10, -2, 0)]
    public void Spell_ward_uses_only_whole_actual_damage_or_status_units(double amount, int ward, int expected) =>
        Assert.Equal(expected, SpellDefenseRules.WardUsed((decimal)amount, ward));

    [Fact]
    public void Mana_ward_never_spends_more_than_mana_or_its_turn_budget()
    {
        Assert.Equal(2, SpellDefenseRules.ManaWardUsed(12.5m, 5, 2));
        Assert.Equal(0, SpellDefenseRules.ManaWardUsed(12m, 5, -1));
        Assert.Equal(3, SpellDefenseRules.ManaWardUsed(12m, 3, 20));
    }

    [Fact]
    public void Absorption_is_once_per_cast_target_and_replay_is_a_new_cast()
    {
        var first = new SpellDefenseRules.Cast<string>();
        Assert.False(first.IsAbsorbed("A"));
        Assert.True(first.Absorb("A"));
        Assert.False(first.Absorb("A"));
        Assert.True(first.IsAbsorbed("A"));
        Assert.False(first.IsAbsorbed("B"));
        Assert.True(first.Absorb("B"));
        Assert.False(new SpellDefenseRules.Cast<string>().IsAbsorbed("A"));
        Assert.False(SpellDefenseRules.CanAbsorb(true, DamageType.Light, 20));
        Assert.False(SpellDefenseRules.CanAbsorb(false, DamageType.Magic, 1));
        Assert.False(SpellDefenseRules.CanAbsorb(true, DamageType.Fire, 0));
        Assert.True(SpellDefenseRules.CanAbsorb(true, DamageType.Frost, 1));
    }

    [Theory]
    [InlineData(EnemyRole.Normal, true, true, true)]
    [InlineData(EnemyRole.Elite, true, true, true)]
    [InlineData(EnemyRole.Boss, true, false, false)]
    [InlineData(EnemyRole.Minion, true, true, false)]
    [InlineData(EnemyRole.Prop, false, false, false)]
    public void Role_is_individual_not_room_rank(EnemyRole role, bool resources, bool control, bool affixes)
    {
        Assert.Equal(resources, EnemyRoleRules.UsesResources(role));
        Assert.Equal(resources, EnemyRoleRules.ScalesHp(role));
        Assert.Equal(control, EnemyRoleRules.CanControl(role));
        Assert.Equal(control, EnemyRoleRules.CanExecute(role));
        Assert.Equal(control, EnemyRoleRules.CanRaise(role));
        Assert.Equal(affixes, EnemyRoleRules.RollsAffixes(role));
        Assert.Equal(role == EnemyRole.Prop ? EnemyRole.Prop : EnemyRole.Minion, EnemyRoleRules.Resolve(role, true));
    }

    [Fact]
    public void Shipped_roles_cover_both_primary_crab_bodies_and_standalone_rift()
    {
        var table = MonsterTable.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "monsters_requiem.csv")));
        Assert.Equal(201, table.Count); // 113 native + 88 authored, including the two counterplay bodies
        foreach (var name in new[] { "Crusher", "Rocket", "WorldEater", "KingUnderBarrow" }) Assert.Equal(EnemyRole.Boss, table.Get(name).Role);
        foreach (var name in new[] { "TorchHeadAmalgam", "Wriggler", "KinFollower", "CrimsonBandit", "DevouredSoulGhost" }) Assert.Equal(EnemyRole.Minion, table.Get(name).Role);
        foreach (var name in new[] { "SteamValve", "DragonEgg", "ShadowSoul", "AbyssGateObject", "SolarStandard" }) Assert.Equal(EnemyRole.Prop, table.Get(name).Role);
        Assert.Equal(EnemyRole.Normal, table.Get("Dremora").Role);
        Assert.Equal(EnemyRole.Normal, table.Get("AbyssScamp").Role);
        Assert.Equal(EnemyRole.Elite, table.Get("ShopGuard").Role);
        Assert.Equal(EnemyRole.Normal, EnemyRoleRules.Parse("")); // old table does not inherit a boss room
        Assert.Throws<FormatException>(() => EnemyRoleRules.Parse("boss-room"));
    }

    [Fact]
    public void Approved_boss_cadences_and_thresholds()
    {
        Assert.Equal(new[] { 3, 7, 11 }, Enumerable.Range(1, 12).Where(t => BossTimingRules.ReaperMoveAt(t) == ReaperMove.Raise));
        Assert.Equal(new[] { 4, 8, 12 }, Enumerable.Range(1, 12).Where(t => BossTimingRules.ReaperMoveAt(t) == ReaperMove.Cry));
        Assert.Throws<ArgumentOutOfRangeException>(() => BossTimingRules.ReaperMoveAt(0));
        Assert.True(BossTimingRules.WandererGuardsAfterTurn(false));
        Assert.False(BossTimingRules.WandererGuardsAfterTurn(true));
        Assert.Equal(3, BossTimingRules.WandererGuardPlating);
        Assert.Equal(1, BossTimingRules.WandererGuardResistance);
        Assert.True(BossTimingRules.ThresholdReached(30, 100, 30));
        Assert.False(BossTimingRules.ThresholdReached(31, 100, 30));
        Assert.False(BossTimingRules.ThresholdReached(0, 100, 30));
        Assert.True(BossTimingRules.ThresholdReached(int.MaxValue / 2, int.MaxValue, 90));
    }

    [Theory]
    [InlineData("FROST_SCYTHE_2", DamageType.Frost)]
    [InlineData("STEAM_EXHALE", DamageType.Fire)]
    [InlineData("ABYSS_HAMMER", DamageType.Magic)]
    [InlineData("LEY_SURGE", DamageType.Shock)]
    [InlineData("PRESSURE_RELEASE", DamageType.Fire)]
    [InlineData("DAWN_RAY", DamageType.Light)]
    [InlineData("DEVOUR_GHOST_TOUCH", DamageType.Magic)]
    [InlineData("UNKNOWN_MOD_MOVE", DamageType.Physical)]
    public void Static_move_types_match_actual_commands(string move, DamageType expected) => Assert.Equal(expected, MonsterMoveRules.TypeOf(move));

    [Fact]
    public void Action_prices_do_not_depend_on_block_resistance_or_number_of_players()
    {
        Assert.Equal(1, MonsterMoveRules.ActionCost(false, 1, 19));
        Assert.Equal(2, MonsterMoveRules.ActionCost(false, 1, 20));
        Assert.Equal(2, MonsterMoveRules.ActionCost(false, 3, 3));
        Assert.Equal(3, MonsterMoveRules.ActionCost(true, 3, 24));
        Assert.Equal(2, MonsterMoveRules.ActionCost(true, 1, 8));
        Assert.Equal(0.5m, EnemyRules.InsufficientManaMultiplier(2, 3));
        Assert.Equal(1m, EnemyRules.InsufficientManaMultiplier(3, 3));
        Assert.Equal(0, StaminaRules.Pay(-2, 5, -1).Spent);
        Assert.Equal(int.MaxValue, StaminaRules.Pay(0, 5, int.MaxValue).HpLoss);
    }

    [Fact]
    public async Task Deferred_effect_detaches_before_nested_await_and_is_not_replayed()
    {
        var calls = 0; var cancelled = 0;
        DeferredEffect? receipt = null;
        receipt = new DeferredEffect(async () => { calls++; await receipt!.Commit(); receipt.Cancel(); }, () => cancelled++);
        await receipt.Commit(); await receipt.Commit(); receipt.Cancel();
        Assert.Equal(1, calls); Assert.Equal(0, cancelled); Assert.Equal(DeferredEffect.Phase.Completed, receipt.State);
    }

    [Fact]
    public async Task Cancelled_or_faulted_effects_cannot_spend_again()
    {
        var spent = 0; var releases = 0;
        var cancelled = new DeferredEffect(() => { spent++; return Task.CompletedTask; }, () => releases++);
        cancelled.Cancel(); cancelled.Cancel(); await cancelled.Commit();
        Assert.Equal(0, spent); Assert.Equal(1, releases);
        var fault = new DeferredEffect(() => { spent++; throw new InvalidOperationException("after mutation"); }, () => releases++);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fault.Commit());
        await fault.Commit(); fault.Cancel();
        Assert.Equal(1, spent); Assert.Equal(1, releases); Assert.Equal(DeferredEffect.Phase.Faulted, fault.State);
    }
}
