using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

/// <summary>Production Core policies/entitlement state; actual CI execution is recorded separately, never native boss acceptance.</summary>
public class BossCounterplayTests
{
    [Fact]
    public void Valve_attack_closes_two_enemy_actions_and_multihit_only_refreshes()
    {
        var turns = BossCounterplayRules.RefreshClosure(0);
        Assert.Equal(2, turns);
        turns = BossCounterplayRules.TurnsAfterEnemyAction(turns);
        Assert.Equal(1, turns);
        for (var hit = 0; hit < 10; hit++) turns = BossCounterplayRules.RefreshClosure(turns);
        Assert.Equal(2, turns);
        Assert.Equal(1, BossCounterplayRules.TurnsAfterEnemyAction(turns));
        Assert.Equal(0, BossCounterplayRules.TurnsAfterEnemyAction(1));
        Assert.Equal(0, BossCounterplayRules.TurnsAfterEnemyAction(0));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 30)]
    [InlineData(2, 30)]
    [InlineData(-1, 0)]
    public void Smelting_is_one_blast_if_any_valve_is_open(int open, int expected) =>
        Assert.Equal(expected, BossCounterplayRules.SmeltingDamage(open, 30));

    [Fact]
    public void World_eater_warns_one_action_before_every_fourth_meteor()
    {
        Assert.Equal(new[] { 4, 8, 12, 16 }, Enumerable.Range(1, 16)
            .Where(t => BossCounterplayRules.WorldEaterMoveAt(t) == WorldEaterMove.Meteors));
        foreach (var turn in new[] { 4, 8, 12, 16 })
            Assert.Equal(WorldEaterMove.Breath, BossCounterplayRules.WorldEaterMoveAt(turn - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BossCounterplayRules.WorldEaterMoveAt(0));
        Assert.Equal(25, BossCounterplayRules.MeteorDamage(false, 25));
        Assert.Equal(0, BossCounterplayRules.MeteorDamage(true, 25));
        Assert.Equal(0, BossCounterplayRules.MeteorDamage(false, -1));
        Assert.Equal(2, BossCounterplayRules.GroundedTurns);
    }

    [Theory]
    [InlineData(0, 3, 30, 100, true)]
    [InlineData(1, 2, 30, 100, true)]
    [InlineData(2, 1, 30, 100, true)]
    [InlineData(3, 1, 30, 100, false)]
    [InlineData(0, 0, 30, 100, false)]
    [InlineData(0, 3, 31, 100, false)]
    [InlineData(0, 3, 0, 100, false)]
    [InlineData(0, 3, 1, 0, false)]
    [InlineData(-1, 3, 30, 100, false)]
    public void Three_visible_eggs_are_the_entire_finite_devour_budget(int used, int living, int hp, int max, bool expected) =>
        Assert.Equal(expected, BossCounterplayRules.CanDevour(used, living, hp, max));

    [Theory]
    [InlineData(DamageType.Fire, -1)]
    [InlineData(DamageType.Frost, -1)]
    [InlineData(DamageType.Shock, -1)]
    [InlineData(DamageType.Poison, -1)]
    [InlineData(DamageType.Blight, -1)]
    [InlineData(DamageType.Magic, -1)]
    [InlineData(DamageType.Light, 0)]
    [InlineData(DamageType.Physical, 0)]
    [InlineData(DamageType.True, 0)]
    public void Aura_is_a_live_source_query_not_a_permanent_stat_subtraction(DamageType type, int expected)
    {
        Assert.Equal(expected, BossCounterplayRules.AuraPenalty(true, true, type));
        Assert.Equal(0, BossCounterplayRules.AuraPenalty(true, false, type));
        Assert.Equal(0, BossCounterplayRules.AuraPenalty(false, true, type));
    }

    [Theory]
    [InlineData(true, false, false, DamageType.Physical, 0.5)]
    [InlineData(true, false, true, DamageType.Physical, 1)]
    [InlineData(true, true, false, DamageType.Physical, 1)]
    [InlineData(false, false, false, DamageType.Physical, 1)]
    [InlineData(true, false, false, DamageType.Fire, 1)]
    [InlineData(true, false, false, DamageType.Light, 1)]
    [InlineData(true, false, false, DamageType.True, 1)]
    public void Flight_does_not_halve_ranged_elements_spells_or_true(bool powered, bool spell, bool ranged, DamageType type, double expected) =>
        Assert.Equal((decimal)expected, BossCounterplayRules.FlightMultiplier(powered, spell, ranged, type));

    [Fact]
    public void Same_window_full_hand_and_failed_add_keep_the_exact_entitlement()
    {
        var queue = new EncounterGrantQueue<int, object, object>(); var combat = new object(); var creates = 0;
        object Create() { creates++; return new(); }
        var first = queue.Request(1, "sunbeam", 1, combat, () => null, Create);
        for (var retry = 0; retry < 20; retry++) Assert.Same(first, queue.Request(1, "sunbeam", 1, combat, () => null, Create));
        Assert.Equal(1, creates);
        Assert.False(first.GenerationAttempted); // waiting for space does not pretend CardGenerated ran
        first.GenerationAttempted = true; // insert/late throw; native adapter observes the actual pile on retry
        Assert.Same(first.Token, queue.Request(1, "sunbeam", 1, combat, () => null, Create).Token);
        Assert.Single(queue.Entries);
    }

    [Fact]
    public void New_phase_reuses_unspent_or_cooling_card_and_preserves_generation_state()
    {
        var queue = new EncounterGrantQueue<int, object, object>(); var combat = new object(); var creates = 0;
        object Create() { creates++; return new(); }
        var pending = queue.Request(1, "skies", 1, combat, () => null, Create);
        var next = queue.Request(1, "skies", 2, combat, () => pending.Token, Create);
        Assert.Same(pending.Token, next.Token);
        Assert.False(next.GenerationAttempted); // still full-hand, not an already generated card
        next.GenerationAttempted = true;
        var cooling = queue.Request(1, "skies", 3, combat, () => next.Token, Create);
        Assert.True(cooling.GenerationAttempted);
        Assert.Equal(1, creates);
    }

    [Fact]
    public void Recipients_kinds_combats_and_exhausted_phase_cards_do_not_share_entitlements()
    {
        var queue = new EncounterGrantQueue<int, object, object>(); var combat = new object();
        var a = queue.Request(1, "sunbeam", 1, combat, () => null, () => new());
        var b = queue.Request(2, "sunbeam", 1, combat, () => null, () => new());
        var c = queue.Request(1, "wing", 1, combat, () => null, () => new());
        Assert.NotSame(a.Token, b.Token); Assert.NotSame(a.Token, c.Token);
        var consumed = queue.Request(1, "sunbeam", 2, combat, () => null, () => new());
        Assert.NotSame(a.Token, consumed.Token);
        var otherCombat = queue.Request(1, "sunbeam", 0, new object(), () => null, () => new());
        Assert.NotSame(consumed.Token, otherCombat.Token);
        Assert.Equal(3, queue.Entries.Count); // one current slot per recipient/kind, not a growing list of past phases
        queue.Clear(); Assert.Empty(queue.Entries);
    }

    [Fact]
    public void Invalid_or_throwing_request_never_erases_the_existing_pending_card()
    {
        var queue = new EncounterGrantQueue<int, object, object>(); var combat = new object();
        var first = queue.Request(1, "step", 2, combat, () => null, () => new());
        Assert.Throws<InvalidOperationException>(() => queue.Request(1, "step", 1, combat, () => null, () => new()));
        Assert.Throws<InvalidOperationException>(() => queue.Request(1, "step", 3, combat, () => null, () => throw new InvalidOperationException()));
        Assert.Same(first, Assert.Single(queue.Entries));
        Assert.Throws<ArgumentException>(() => queue.Request(1, "", 0, combat, () => null, () => new()));
        Assert.Throws<ArgumentOutOfRangeException>(() => queue.Request(1, "step", -1, combat, () => null, () => new()));
    }
}

public class BossCounterplayBoundaryTests
{
    [Theory]
    [InlineData(false, 1, 70, 100, 70, true)]
    [InlineData(false, 1, 71, 100, 70, false)]
    [InlineData(false, 60, 10, 100, 70, true)]
    [InlineData(true, 1, 10, 100, 40, false)]
    [InlineData(false, 0, 10, 100, 40, false)]
    [InlineData(false, -1, 10, 100, 40, false)]
    [InlineData(false, 1, 0, 100, 20, false)]
    [InlineData(false, 1, 10, 0, 20, false)]
    public void A_zero_packet_or_current_immunity_never_burns_the_next_cocoon(bool immune, int packet, int hp, int max, int threshold, bool expected) =>
        Assert.Equal(expected, BossCounterplayRules.CanStartCocoon(immune, packet, hp, max, threshold));

    [Theory]
    [InlineData(-2, 0)]
    [InlineData(0, 0)]
    [InlineData(1, -1)]
    [InlineData(2, -2)]
    [InlineData(3, -3)]
    [InlineData(99, -3)]
    public void Word_is_at_most_minus_three_magic_tiers_not_a_second_damage_multiplier(int stacks, int penalty)
    {
        Assert.Equal(penalty, BossCounterplayRules.BindingPenalty(true, DamageType.Magic, stacks));
        Assert.Equal(0, BossCounterplayRules.BindingPenalty(false, DamageType.Magic, stacks));
        foreach (var type in new[] { DamageType.Shock, DamageType.Fire, DamageType.Physical, DamageType.Light, DamageType.True })
            Assert.Equal(0, BossCounterplayRules.BindingPenalty(true, type, stacks));
    }

    [Fact]
    public void Word_cannot_accumulate_invisible_stacks_above_the_cap()
    {
        Assert.Equal(1m, BossCounterplayRules.BindingStacksToApply(0, 1m));
        Assert.Equal(1m, BossCounterplayRules.BindingStacksToApply(2, 6m));
        Assert.Equal(0m, BossCounterplayRules.BindingStacksToApply(3, 1m));
        Assert.Equal(0m, BossCounterplayRules.BindingStacksToApply(99, 1m));
        Assert.Equal(0m, BossCounterplayRules.BindingStacksToApply(0, -1m));
        var permanent = 3;
        var queried = permanent + BossCounterplayRules.AuraPenalty(true, true, DamageType.Magic)
            + BossCounterplayRules.BindingPenalty(true, DamageType.Magic, 3);
        Assert.Equal(-1, queried);
        Assert.Equal(3, permanent); // neither marker subtracts from the saved/base resistance power
        Assert.Equal(1.5m, CombatMath.ResistanceMultiplier(queried));
    }

    [Theory]
    [InlineData(true, false, false, DamageType.Physical, 0)]
    [InlineData(true, false, true, DamageType.Physical, 0)]
    [InlineData(true, true, false, DamageType.Fire, 0)]
    [InlineData(true, true, true, DamageType.Fire, 1)]
    [InlineData(true, false, false, DamageType.Light, 1)]
    [InlineData(true, true, false, DamageType.Light, 1)]
    [InlineData(false, false, false, DamageType.Fire, 1)]
    [InlineData(true, false, false, DamageType.True, 0)]
    public void Mist_exempts_light_and_area_spells_not_every_single_target_spell(bool move, bool spell, bool area, DamageType type, int expected) =>
        Assert.Equal((decimal)expected, BossCounterplayRules.MistMultiplier(move, spell, area, type));

    [Fact]
    public void Shelf_overflow_phase_reissue_and_replays_cannot_shorten_a_shout_cooldown()
    {
        var cooldowns = new EncounterShoutCooldowns<ulong>();
        Assert.True(cooldowns.Ready(11, "skies", 1));
        cooldowns.Played(11, "skies", 1, 3);
        for (var replay = 0; replay < 5; replay++) cooldowns.Played(11, "skies", 1, 3);
        cooldowns.Played(11, "skies", 1, 1); // a newly acquired bonus cannot retroactively shorten this cast
        foreach (var ownerTurn in new[] { 1, 2, 3 }) Assert.False(cooldowns.Ready(11, "skies", ownerTurn));
        Assert.True(cooldowns.Ready(11, "skies", 4)); // counted from play, not every phase/retry
        cooldowns.Played(11, "skies", 4, 3);
        Assert.False(cooldowns.Ready(11, "skies", 6));
        Assert.True(cooldowns.Ready(11, "skies", 7));
    }

    [Fact]
    public void Shout_delay_is_per_recipient_kind_and_own_turn_and_clears_with_the_encounter()
    {
        var cooldowns = new EncounterShoutCooldowns<ulong>();
        cooldowns.Played(11, "wing", 5, 3);
        Assert.False(cooldowns.Ready(11, "wing", 7));
        Assert.True(cooldowns.Ready(12, "wing", 2)); // co-op peer's clock is independent
        Assert.True(cooldowns.Ready(11, "skies", 5));
        Assert.True(cooldowns.Ready(11, "wing", 8)); // extra owner turns count like the normal shelf
        cooldowns.Clear(); Assert.True(cooldowns.Ready(11, "wing", 1));
        cooldowns.Played(11, "wing", 1, 0);
        Assert.False(cooldowns.Ready(11, "wing", 1));
        Assert.True(cooldowns.Ready(11, "wing", 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => cooldowns.Played(11, "wing", 0, 3));
        Assert.Throws<ArgumentException>(() => cooldowns.Played(11, "", 1, 3));
    }

    [Fact]
    public void A_null_factory_is_not_a_successful_entitlement_or_permission_to_forget_the_old_one()
    {
        var queue = new EncounterGrantQueue<int, object, object>(); var combat = new object();
        var first = queue.Request(1, "step", 1, combat, () => null, () => new());
        Assert.Throws<InvalidOperationException>(() => queue.Request(1, "step", 2, combat, () => null, () => null!));
        Assert.Same(first, Assert.Single(queue.Entries));
        Assert.Throws<ArgumentNullException>(() => queue.Request(1, "step", 2, null!, () => null, () => new()));
        Assert.Same(first, Assert.Single(queue.Entries));
    }
}


public class BossPhaseCounterplayTests
{
    [Theory]
    [InlineData(-1, 3)]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(3, 0)]
    [InlineData(4, 0)]
    [InlineData(99, 0)]
    public void Next_cocoon_replenishes_three_guards_not_three_more_after_a_sunbeam(int living, int missing) =>
        Assert.Equal(missing, BossCounterplayRules.MissingCocoonGuards(living));

    [Theory]
    [InlineData(0, 3, 0, BoilerMove.Bite)]
    [InlineData(2, 3, 0, BoilerMove.Bite)]
    [InlineData(2, 3, 1, BoilerMove.Coil)]
    [InlineData(3, 3, 0, BoilerMove.Release)]
    [InlineData(3, 3, 1, BoilerMove.Release)]
    [InlineData(99, 3, 1, BoilerMove.Release)]
    [InlineData(2, 2, 0, BoilerMove.Release)]
    [InlineData(1, 2, 1, BoilerMove.Coil)]
    [InlineData(-1, 3, 0, BoilerMove.Bite)]
    public void Pressure_cold_and_phase_threshold_select_the_same_telegraphed_action(int pressure, int threshold, int alternate, BoilerMove expected) =>
        Assert.Equal(expected, BossCounterplayRules.BoilerMoveAt(pressure, threshold, alternate));

    [Fact]
    public void Requerying_pressure_never_advances_the_bite_coil_alternation()
    {
        for (var preview = 0; preview < 20; preview++)
        {
            Assert.Equal(BoilerMove.Release, BossCounterplayRules.BoilerMoveAt(3, 3, 1));
            Assert.Equal(BoilerMove.Coil, BossCounterplayRules.BoilerMoveAt(2, 3, 1)); // one actual Frost packet
            Assert.Equal(BoilerMove.Release, BossCounterplayRules.BoilerMoveAt(2, 2, 1)); // phase-two threshold
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => BossCounterplayRules.BoilerMoveAt(1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BossCounterplayRules.BoilerMoveAt(1, 3, -1));
    }
}
