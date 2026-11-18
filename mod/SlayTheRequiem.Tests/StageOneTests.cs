using System.Text.Json;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

public class CooldownShelfTests
{
    [Fact]
    public void Due_entry_is_not_consumed_by_failed_return()
    {
        var shelf = new CooldownShelf<object, object>();
        var card = new object(); var combat = new object();
        Assert.True(shelf.Put(card, combat, 3));
        Assert.Empty(shelf.Tick(combat));
        Assert.Equal(2, shelf.NextTurns);
        Assert.Empty(shelf.Tick(combat));
        Assert.Same(card, Assert.Single(shelf.Tick(combat)).Card);
        Assert.Equal(0, shelf.NextTurns);
        Assert.Equal(1, shelf.Count);
        Assert.Same(card, Assert.Single(shelf.Tick(combat)).Card); // failed Add stays due, no new countdown
        Assert.True(shelf.Complete(card)); // adapter calls only on observed Hand OR full-hand Discard
        Assert.Empty(shelf.Tick(combat));
        Assert.Equal(0, shelf.Count);
    }

    [Fact]
    public void Repeated_play_does_not_create_a_second_physical_card()
    {
        var shelf = new CooldownShelf<object, object>();
        var card = new object(); var combat = new object();
        Assert.True(shelf.Put(card, combat, 1));
        Assert.False(shelf.Put(card, combat, 9));
        Assert.Equal(1, shelf.Count);
        Assert.Single(shelf.Tick(combat));
        Assert.True(shelf.Complete(card));
        Assert.False(shelf.Complete(card));
        Assert.True(shelf.Put(card, combat, 2)); // the returned card may be played again
        Assert.Empty(shelf.Tick(combat));
        Assert.Single(shelf.Tick(combat));
    }

    [Fact]
    public void Distinct_copies_owners_and_combats_do_not_share_shelves()
    {
        var a = new CooldownShelf<object, object>(); var b = new CooldownShelf<object, object>();
        var combat = new object(); var nextCombat = new object(); var card = new object();
        a.Put(card, combat, 1); a.Put(new object(), combat, 1);
        Assert.Equal(2, a.Count);
        Assert.Equal(0, b.Count);
        Assert.Empty(a.Tick(nextCombat));
        Assert.Equal(0, a.Count); // never return a card from a previous combat
        b.Put(card, nextCombat, 1); b.Clear();
        Assert.Empty(b.Tick(nextCombat));
    }
}

public class RecoveryContractTests
{
    [Fact]
    public void Checkpoint_round_trips_full_opaque_run_and_rejects_wrong_identity()
    {
        const string opaque = "{\"unknown_mod\":{\"charges\":[1,2]},\"gold\":130}";
        var checkpoint = RunCheckpoint.Create("seed", 1234, "timeline", 2, 32, "shop:7", opaque);
        var restored = RunCheckpoint.Decode(checkpoint.Encode());
        Assert.Equal(checkpoint, restored);
        Assert.Equal(opaque, restored!.SaveJson);
        Assert.True(restored.Matches("seed", 1234, "timeline"));
        Assert.False(restored.Matches("seed", 1235, "timeline"));
        Assert.False(restored.Matches("seed", 1234, "other-attempt"));
        Assert.False(restored.Matches("other-seed", 1234, "timeline"));
        Assert.Null(RunCheckpoint.Decode((checkpoint with { SaveJson = opaque + " " }).Encode()));
        Assert.Null(RunCheckpoint.Decode((checkpoint with { Version = 99 }).Encode()));
        Assert.Null(RunCheckpoint.Decode((checkpoint with { Floor = 99 }).Encode()));
        Assert.Null(RunCheckpoint.Decode((checkpoint with { Operation = "different" }).Encode()));
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("{}")] [InlineData("null")] [InlineData("{garbage}")]
    public void Unknown_or_damaged_checkpoint_is_not_a_recovery(string? payload) => Assert.Null(RunCheckpoint.Decode(payload));

    [Fact]
    public void Atomic_backup_keeps_previous_complete_generation_and_no_temporary_files()
    {
        var directory = Path.Combine(Path.GetTempPath(), "str-atomic-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "checkpoint.save");
        try
        {
            AtomicSaveFile.Write(path, "first complete run");
            Assert.Equal("first complete run", File.ReadAllText(path));
            AtomicSaveFile.Write(path, "second complete run");
            Assert.Equal("first complete run", File.ReadAllText(path + ".previous"));
            Assert.Equal("second complete run", File.ReadAllText(path));
            Assert.Equal(2, Directory.GetFiles(directory).Length);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Late_matching_receipts_can_release_only_a_receipt_timeout_without_replaying_effects()
    {
        var proof = new OperationReceiptProof(OperationStatus.Committed, 60, OperationIdentity.Hash("inventory"));
        Assert.True(ReceiptRecoveryPolicy.CanResume(true, 7, 8, proof.Digest, proof, 2, [proof, proof]));
        Assert.False(ReceiptRecoveryPolicy.CanResume(false, 7, 8, proof.Digest, proof, 2, [proof, proof]));
        Assert.False(ReceiptRecoveryPolicy.CanResume(true, 7, 9, proof.Digest, proof, 2, [proof, proof]));
        Assert.False(ReceiptRecoveryPolicy.CanResume(true, 7, 8, proof.Digest, proof, 2, [proof]));
        Assert.False(ReceiptRecoveryPolicy.CanResume(true, 7, 8, OperationIdentity.Hash("changed"), proof, 2, [proof, proof]));
        Assert.False(ReceiptRecoveryPolicy.CanResume(true, 7, 8, proof.Digest, proof, 2, [proof, proof with { Gold = 0 }]));
        Assert.False(ReceiptRecoveryPolicy.CanResume(true, int.MaxValue, int.MaxValue, proof.Digest, proof, 1, [proof]));
    }

    [Theory]
    [InlineData(OperationStatus.PartialCommit)] [InlineData(OperationStatus.Stale)] [InlineData(OperationStatus.Busy)]
    public void Partial_or_uncertain_effects_are_not_unblocked_by_receipts(OperationStatus status)
    {
        var proof = new OperationReceiptProof(status, 60, OperationIdentity.Hash("inventory"));
        Assert.False(ReceiptRecoveryPolicy.CanResume(true, 7, 8, proof.Digest, proof, 1, [proof]));
    }

    [Theory]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, true, false, true)]
    public void Imprint_requires_confirmed_ancient_startup_and_no_mid_effect_save(bool unfinishedAncient, bool choicesReady, bool mutation, bool expected) =>
        Assert.Equal(expected, MemoryStoneRules.CanCaptureImprint(unfinishedAncient, choicesReady, mutation));
}

public class SavedRulesValidationTests
{
    [Theory]
    [InlineData("v=9;hp=110")]
    [InlineData("v=1;v=1")]
    [InlineData("v=1;sys=1;sys=0")]
    [InlineData("v=1;sys=2")]
    [InlineData("v=1;hp=oops")]
    [InlineData("v=1;hp=9999")]
    [InlineData("v=1;run=not-a-guid")]
    [InlineData("v=1;level=11")]
    [InlineData("v=1;variant=2")]
    [InlineData("v=1;boss=3")]
    [InlineData("v=1;p=7:1,2,3,4|7:2,2,2,2")]
    [InlineData("v=1;p=7:1,2")]
    [InlineData("v=1;p=7:1,2,6,4")]
    [InlineData("v=1;p=7:1,2,3,4,5")]
    public void Invalid_ambiguous_or_future_saved_rules_never_fall_back_to_a_local_menu(string code) =>
        Assert.Null(RunSettingsData.Decode(code));

    [Fact]
    public void Legacy_current_schema_is_readable_and_personal_choices_do_not_alias_a_snapshot_clone()
    {
        var legacy = RunSettingsData.Decode("v=1;hp=95;p=7:1,2,3,4")!;
        Assert.Empty(legacy.RunId);
        var clone = legacy.Clone();
        clone.Personal[7] = default;
        Assert.Equal(new PersonalChoices(1, 2, 3, 4), legacy.ChoicesFor(7));
        Assert.NotEqual(legacy.Encode(), clone.Encode());
    }
}


public class MetricWindowTests
{
    [Fact]
    public void Samples_are_bounded_and_invalid_values_do_not_pollute_the_baseline()
    {
        var samples = new MetricWindow(3);
        samples.Add(1); samples.Add(2); samples.Add(3); samples.Add(9);
        samples.Add(double.NaN); samples.Add(-1); samples.Add(double.PositiveInfinity);
        var summary = samples.Snapshot();
        Assert.Equal(3, summary.Samples);
        Assert.Equal(3, summary.MedianMs);
        Assert.Equal(9, summary.P95Ms);
        samples.Clear();
        Assert.Equal(0, samples.Snapshot().Samples);
    }
}


public class CampVisitStateTests
{
    [Fact]
    public void Continue_preserves_spent_points_artisan_and_removed_options()
    {
        var saved = new CampVisitState("1:30", 1, true, "SMITH");
        var resumed = saved.Enter("1:30", 4, ["SMITH"]);
        Assert.Equal(saved, resumed);
        Assert.Contains("SMITH", resumed.CompletedOptions);
        Assert.Equal(1, resumed.Points);
        Assert.True(resumed.ArtisanUsed);
    }
    [Fact]
    public void A_new_camp_refills_once_and_drops_the_previous_camps_choices()
    {
        var next = new CampVisitState("1:30", 0, true, "SMITH").Enter("1:33", 4, []);
        Assert.Equal(4, next.Points);
        Assert.False(next.ArtisanUsed);
        Assert.Empty(next.CompletedOptions);
        Assert.Equal(next, next.Enter("1:33", 4, []));
        Assert.Equal("SMITH", next.Complete("SMITH").Complete("SMITH").Completed);
    }
    [Fact]
    public void Legacy_camp_history_does_not_mint_a_new_budget_on_load()
    {
        var saved = new CampVisitState("", 0, true, "");
        var resumed = saved.Enter("1:30", 4, ["HEAL"]);
        Assert.Equal(0, resumed.Points);
        Assert.True(resumed.ArtisanUsed);
        Assert.Contains("HEAL", resumed.CompletedOptions);
    }
}
