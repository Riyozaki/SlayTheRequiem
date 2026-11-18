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

public class SavedSnapshotContractTests
{
    [Fact]
    public void No_grimoire_means_disabled_or_vanilla_not_local_menu_rules() => Assert.Null(SavedRulesSnapshot.Read([]).Rules);

    [Fact]
    public void Missing_peer_copy_can_only_use_the_single_consistent_saved_snapshot()
    {
        var code = new RunSettingsData { EnemyHpPercent = 95, RunId = "1234567890abcdef1234567890abcdef" }.Encode();
        var result = SavedRulesSnapshot.Read([code, null, ""]);
        Assert.True(result.MissingCopies);
        Assert.Equal(code, result.Rules!.Encode());
        Assert.False(SavedRulesSnapshot.Read([code, code]).MissingCopies);
        Assert.Throws<FormatException>(() => SavedRulesSnapshot.Read([null, ""]));
        Assert.Throws<FormatException>(() => SavedRulesSnapshot.Read([code, "v=2"]));
        Assert.Throws<FormatException>(() => SavedRulesSnapshot.Read([code, new RunSettingsData { EnemyHpPercent = 120 }.Encode()]));
    }

    [Fact]
    public void Unknown_current_schema_keys_and_personal_snapshots_survive_migration_and_clone()
    {
        var saved = RunSettingsData.Decode("v=1;hp=95;extension=opaque=payload;p=7:1,2,3,4")!;
        var clone = saved.Clone();
        Assert.Equal("opaque=payload", clone.Extensions["extension"]);
        clone.Extensions["extension"] = "changed";
        clone.Personal[7] = default;
        Assert.Equal("opaque=payload", saved.Extensions["extension"]);
        Assert.Equal(new PersonalChoices(1, 2, 3, 4), saved.Personal[7]);
        Assert.Throws<FormatException>(() => new RunSettingsData { RunId = "broken" }.Clone());
        Assert.Throws<FormatException>(() => new RunSettingsData { EnemyHpPercent = 9999 }.Clone());
    }

    [Fact]
    public void Legacy_declared_startup_is_not_replayed_and_continue_does_not_add_fatigue()
    {
        Assert.Equal(4, StartupState.LegacyPhase(true));
        Assert.Equal(0, StartupState.LegacyPhase(false));
        Assert.True(StartupState.IsComplete(4, true));
        Assert.False(StartupState.IsComplete(3, true));
        Assert.False(StartupState.ShouldCountVisit("1:30", "1:30", true));
        Assert.True(StartupState.ShouldCountVisit("1:30", "1:31", true));
        Assert.False(StartupState.ShouldCountVisit("1:30", "1:31", false));
    }
}

public class MandatoryGrantSourceTests
{
    [Fact]
    public void Sources_not_types_own_distinct_claims_and_completed_sources_survive_json()
    {
        var sources = new Dictionary<string, string> { ["r:1/bite"] = "q:3", ["r:2/bite"] = "q:4" };
        var saved = MandatoryGrantLedger.Write(sources);
        var back = MandatoryGrantLedger.Read(saved);
        Assert.Equal(2, back.Count);
        Assert.Equal("q:3", back["r:1/bite"]);
        back["r:1/bite"] = "q:5";
        Assert.Equal("q:3", MandatoryGrantLedger.Read(saved)["r:1/bite"]);
        Assert.Equal("q:3", sources["r:1/bite"]);
        Assert.Empty(MandatoryGrantLedger.Read(""));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"version\":2,\"sources\":{}}")]
    [InlineData("{\"version\":1,\"sources\":{},\"future\":42}")]
    [InlineData("{\"version\":1,\"sources\":{\"source\":\"q:1\",\"source\":\"q:2\"}}")]
    [InlineData("{\"version\":1,\"sources\":{\"a\":\"q:1\",\"b\":\"q:1\"}}")]
    [InlineData("{\"version\":1,\"sources\":{\"source\":\"q:01\"}}")]
    [InlineData("{\"version\":1,\"sources\":{\"source\":\"q:0\"}}")]
    public void Unknown_or_ambiguous_sources_are_rejected_without_a_lossy_rewrite(string text) =>
        Assert.Throws<FormatException>(() => MandatoryGrantLedger.Read(text));
}

public class CheckpointGenerationTests
{
    [Fact]
    public void Corrupt_or_foreign_current_generation_does_not_hide_the_matching_previous_copy()
    {
        var directory = Path.Combine(Path.GetTempPath(), "str-generation-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "checkpoint.save");
        var expected = RunCheckpoint.Create("seed", 123, "attempt", 1, 20, "test", "{\"opaque\":42}");
        try
        {
            AtomicSaveFile.Write(path, expected.Encode());
            AtomicSaveFile.Write(path, "corrupt-json");
            RunCheckpoint? Decode(string text)
            {
                var cp = RunCheckpoint.Decode(text);
                return cp?.Matches("seed", 123, "attempt") == true ? cp : null;
            }
            Assert.Equal(expected, AtomicSaveFile.ReadValidated(path, Decode));
            File.WriteAllText(path, RunCheckpoint.Create("other", 123, "attempt", 1, 20, "test", "{}").Encode());
            Assert.Equal(expected, AtomicSaveFile.ReadValidated(path, Decode));
            File.Delete(path + ".previous");
            Assert.Null(AtomicSaveFile.ReadValidated(path, Decode));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}

public class NativeBoundaryPolicyTests
{
    [Theory]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, false, false, false, true)]
    [InlineData(false, true, true, false, true)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(false, true, true, true, false)]
    public void Shelf_uses_captured_ownership_not_a_null_computed_combat_property(bool matches, bool missing, bool removed, bool piled, bool expected) =>
        Assert.Equal(expected, ShoutRules.CanRestoreCaptured(matches, missing, removed, piled));

    [Fact]
    public void Receipt_unblock_cannot_overtake_mutation_disposal_or_a_new_failure()
    {
        Assert.False(ReceiptRecoveryPolicy.CanClear(7, 7, 1, "shop:42"));
        Assert.False(ReceiptRecoveryPolicy.CanClear(7, 7, 0, "shop:42"));
        Assert.False(ReceiptRecoveryPolicy.CanClear(7, 8, 0, ""));
        Assert.True(ReceiptRecoveryPolicy.CanClear(7, 7, 0, ""));
    }

    private static readonly HashSet<string> Fields = ["schema_version", "players", "start_time"];

    [Theory]
    [InlineData("{\"schema_version\":18,\"players\":[],\"future_mod\":{\"charges\":[1,2]}}")]
    [InlineData("{\"schema_version\":19,\"players\":[]}")]
    [InlineData("{\"schema_version\":18,\"schema_version\":17,\"players\":[]}")]
    [InlineData("{\"schema_version\":18,\"players\":[{\"gold\":1,\"gold\":2}]}")]
    [InlineData("{\"schema_version\":\"18\",\"players\":[]}")]
    [InlineData("{\"players\":[]}")]
    public void Unknown_future_or_duplicate_native_fields_refuse_before_native_reserialization(string original) =>
        Assert.Throws<FormatException>(() => RawRunDocument.Validate(System.Text.Encoding.UTF8.GetBytes(original), Fields, 18));

    [Fact]
    public void Original_save_backup_is_byte_identical_including_BOM_whitespace_and_newlines()
    {
        var body = System.Text.Encoding.UTF8.GetBytes("{\r\n  \"schema_version\": 18, \"players\": []\r\n}\r\n");
        var original = new byte[] { 0xef, 0xbb, 0xbf }.Concat(body).ToArray();
        RawRunDocument.Validate(original, Fields, 18);
        var directory = Path.Combine(Path.GetTempPath(), "str-raw-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "backup.save");
        try
        {
            AtomicSaveFile.WriteBytes(path, original);
            Assert.Equal(original, File.ReadAllBytes(path));
            AtomicSaveFile.WriteBytes(path, body);
            Assert.Equal(original, File.ReadAllBytes(path + ".previous"));
            Assert.Equal(body, File.ReadAllBytes(path));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}

public class AsyncBoundaryTests
{
    [Fact]
    public async Task Cleanup_registered_after_the_notification_must_complete_before_the_boundary()
    {
        var late = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        IReadOnlyList<Task> Snapshot()
        {
            if (++calls == 1) return [Task.CompletedTask];
            registered.TrySetResult();
            return [Task.CompletedTask, late.Task];
        }
        var boundary = AsyncTaskBoundary.Settle(Snapshot, () => true);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(boundary.IsCompleted);
        late.SetResult();
        Assert.True(await boundary.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task Fault_cancel_or_changed_room_cannot_be_a_confirmed_imprint()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => AsyncTaskBoundary.Settle(
            () => [Task.FromException(new InvalidOperationException("cleanup failed"))], () => true));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AsyncTaskBoundary.Settle(
            () => [Task.FromCanceled(new CancellationToken(canceled: true))], () => true));
        var called = false;
        Assert.False(await AsyncTaskBoundary.Settle(() => { called = true; return []; }, () => false));
        Assert.False(called);
    }

    [Fact]
    public async Task A_task_set_that_never_stabilizes_is_not_silently_accepted()
    {
        var count = 0;
        Assert.False(await AsyncTaskBoundary.Settle(() => [Task.FromResult(++count)], () => true));
    }
}

public class SavedOperationVersionTests
{
    [Theory]
    [InlineData(-1, 1, false, "", false)]
    [InlineData(4, 1, false, "", false)]
    [InlineData(3, 2, false, "", false)]
    [InlineData(0, 0, true, "", false)]
    [InlineData(0, 0, false, "old-partial", false)]
    [InlineData(3, 0, true, "", false)]
    [InlineData(0, 0, false, "", true)]
    [InlineData(3, 1, true, "known-partial", true)]
    public void Legacy_uncertainty_is_a_read_only_refusal_not_an_unexplained_imported_block(int schema, int protocol, bool blocked, string pending, bool allowed)
    {
        if (allowed) SavedOperationState.Validate(schema, protocol, blocked, pending, 3, 1);
        else Assert.Throws<FormatException>(() => SavedOperationState.Validate(schema, protocol, blocked, pending, 3, 1));
    }
}

public class RawReadBoundaryTests
{
    public sealed class Document { public List<Entry> Players { get; set; } = []; }
    public sealed class Entry { public int Gold { get; set; } }
    private static byte[] Bytes(string value) => System.Text.Encoding.UTF8.GetBytes(value);

    [Theory]
    [InlineData("{\"id\":\"SlayTheRequiem-GRIMOIRE\"}", true)]
    [InlineData("{\"id\":\"\\u0053layTheRequiem-GRIMOIRE\"}", true)]
    [InlineData("{\"id\":\"SLAYTHEREQUIEM-GRIMOIRE\"}", true)]
    [InlineData("{\"players\":[{\"relics\":[\"slaytherequiem-PACK\"]}]}", true)]
    [InlineData("{\"id\":\"SlayTheRequiem-GRIMOIRE\" broken", true)]
    [InlineData("{\"id\":\"BURNING_BLOOD\"}", false)]
    public void Guard_identifies_primary_or_backup_even_with_escaped_model_ids(string raw, bool expected) =>
        Assert.Equal(expected, RawRunDocument.Mentions(Bytes(raw), "SlayTheRequiem"));

    [Fact]
    public void Typed_preflight_rejects_nested_unknown_fields_and_wrong_types_without_invoking_a_native_reader()
    {
        var options = new JsonSerializerOptions();
        Assert.Throws<JsonException>(() => RawRunDocument.DeserializeStrict<Document>(Bytes("{\"Players\":[{\"Gold\":7,\"UnknownCharges\":[2]}]}"), options));
        Assert.Throws<JsonException>(() => RawRunDocument.DeserializeStrict<Document>(Bytes("{\"Players\":[{\"Gold\":\"broken\"}]}"), options));
        var bom = new byte[] { 0xef, 0xbb, 0xbf }.Concat(Bytes("{\"Players\":[{\"Gold\":7}]}\r\n")).ToArray();
        Assert.Equal(7, RawRunDocument.DeserializeStrict<Document>(bom, options).Players[0].Gold);
        // Options belong to the game and must not be made strict or frozen by a load attempt.
        Assert.Equal(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip, options.UnmappedMemberHandling);
        Assert.False(options.IsReadOnly);
    }
}

public class RestoreOwnershipTests
{
    [Fact]
    public void Restore_lease_excludes_other_transitions_and_stale_disposal_cannot_release_its_successor()
    {
        var gate = new OperationGate();
        Assert.False(gate.IsHeld);
        var old = gate.TryEnter()!;
        Assert.True(gate.IsHeld);
        Assert.Null(gate.TryEnter());
        old.Dispose();
        using var next = gate.TryEnter();
        Assert.NotNull(next);
        old.Dispose(); // e.g. finally from a completed generation
        Assert.True(gate.IsHeld);
        Assert.Null(gate.TryEnter());
    }
}

public class RelicEntitlementTests
{
    [Fact]
    public void Companion_cannot_finish_before_issuer_hooks_but_can_resume_without_replaying_them()
    {
        var prepared = RelicGrantState.Prepare("r:41", "{\"id\":\"SIGN\",\"props\":{\"strings\":[{\"name\":\"Opaque\",\"value\":\"kept\"}]}}");
        var saved = RelicGrantState.Read(prepared.Encode())!;
        Assert.Equal(prepared, saved);
        Assert.Throws<InvalidOperationException>(() => saved.Complete());
        var ready = RelicGrantState.Read(saved.IssuerCompleted().Encode())!;
        Assert.Equal(RelicGrantState.Ready, ready.Phase);
        var complete = ready.Complete();
        Assert.Equal(complete, RelicGrantState.Read(complete.Encode())!.Complete());
        Assert.Equal(prepared.Key, complete.Key);
        Assert.Equal(prepared.Payload, complete.Payload); // completion remains proof even after item loss/storage
    }

    [Fact]
    public void Legacy_absence_does_not_prove_non_delivery_and_no_sign_paths_complete_explicitly()
    {
        Assert.Null(RelicGrantState.Read(""));
        var noSign = RelicGrantState.Prepare("", "").IssuerCompleted().Complete();
        Assert.Equal(noSign, RelicGrantState.Read(noSign.Encode()));
        Assert.Throws<FormatException>(() => RelicGrantState.Prepare("r:7", ""));
    }

    [Theory]
    [InlineData("{\"Version\":2,\"Phase\":1,\"Key\":\"r:1\",\"Payload\":\"{}\"}")]
    [InlineData("{\"Version\":1,\"Phase\":3,\"Key\":\"r:1\",\"Payload\":\"{}\"}")]
    [InlineData("{\"Version\":1,\"Phase\":1,\"Key\":\"r:01\",\"Payload\":\"{}\"}")]
    [InlineData("{\"Version\":1,\"Phase\":1,\"Key\":\"r:0\",\"Payload\":\"{}\"}")]
    [InlineData("{\"Version\":1,\"Phase\":1,\"Key\":\"r:1\",\"Payload\":\"[]\"}")]
    [InlineData("{\"Version\":1,\"Phase\":1,\"Phase\":2,\"Key\":\"r:1\",\"Payload\":\"{}\"}")]
    [InlineData("{\"Version\":1,\"Phase\":1,\"Key\":\"r:1\",\"Payload\":\"{}\",\"FutureEffect\":7}")]
    public void Unknown_ambiguous_or_invalid_rights_are_retained_not_guessed(string text) =>
        Assert.Throws<FormatException>(() => RelicGrantState.Read(text));

    [Theory]
    [InlineData(-1)] [InlineData(999)]
    public void Undefined_form_never_falls_through_to_Clockwork(int kind)
    {
        Assert.False(TransformationRules.IsWorldview((FormKind)kind));
        Assert.False(TransformationRules.CanTakeWorldview(FormKind.None, (FormKind)kind, true, false, false));
    }
}
