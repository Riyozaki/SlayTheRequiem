using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

/// <summary>Production transfer/codec/journal contracts, NOT a mirror of the implementation or a game adapter test.</summary>
public sealed class IntegrityTests
{
    [Fact]
    public async Task Veto_keeps_the_exact_source_and_properties()
    {
        var source = new Item("staff:2", 2, "fire:3:7", 4);
        Item? destination = null;
        var finalized = false;
        var result = await TransferProtocol.Move(source.Id, () => Task.FromResult(false),
            () => destination != null, () => finalized = true);
        Assert.Equal(OperationStatus.Rejected, result.Status);
        Assert.False(finalized);
        Assert.Null(destination);
        Assert.Equal(new Item("staff:2", 2, "fire:3:7", 4), source);
    }

    [Theory]
    [InlineData(false, OperationStatus.Error)]
    [InlineData(true, OperationStatus.PartialCommit)]
    public async Task Exceptions_are_classified_by_observed_commit(bool throwAfterCommit, OperationStatus expected)
    {
        var source = true;
        var destination = false;
        var cleaned = false;
        var result = await TransferProtocol.Move("c:1", () =>
        {
            destination = throwAfterCommit;
            throw new InvalidOperationException("hook failure");
        }, () => destination, () => source = false, () => cleaned = true);
        Assert.Equal(expected, result.Status);
        Assert.Equal(1, (source ? 1 : 0) + (destination ? 1 : 0));
        Assert.Equal(!throwAfterCommit, cleaned);
        Assert.Contains("hook failure", result.Detail);
    }

    [Fact]
    public async Task Reported_success_without_destination_cannot_consume_a_source()
    {
        var removed = false;
        var result = await TransferProtocol.Move("c:1", () => Task.FromResult(true), () => false, () => removed = true);
        Assert.Equal(OperationStatus.Rejected, result.Status);
        Assert.False(removed);
    }

    [Fact]
    public async Task Exception_after_destination_does_not_duplicate_on_retry()
    {
        var source = new List<int> { 42 };
        var destination = new List<int>();
        var result = await TransferProtocol.Move("c:42", () =>
        {
            destination.Add(source[0]);
            throw new InvalidOperationException("after-add animation");
        }, () => destination.Contains(42), () => source.Clear());
        Assert.Equal(OperationStatus.PartialCommit, result.Status);
        Assert.Empty(source);
        Assert.Equal(new[] { 42 }, destination);
    }

    [Fact]
    public async Task Batch_keeps_second_veto_and_continues_with_third_with_individual_results()
    {
        var source = new List<int> { 1, 2, 3 };
        var destination = new List<int>();
        var results = new List<OperationResult>();
        foreach (var item in source.ToArray())
            results.Add(await TransferProtocol.Move($"c:{item}", () =>
            {
                if (item == 2) return Task.FromResult(false);
                destination.Add(item); return Task.FromResult(true);
            }, () => destination.Contains(item), () => source.Remove(item)));
        Assert.Equal(new[] { OperationStatus.Committed, OperationStatus.Rejected, OperationStatus.Committed }, results.Select(r => r.Status));
        Assert.Equal(new[] { 2 }, source);
        Assert.Equal(new[] { 1, 3 }, destination);
    }

    [Fact]
    public async Task Transfer_conservation_under_repeated_failure_injection()
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var rng = new Random(seed);
            var source = Enumerable.Range(0, 12).Select(i => new Item($"c:{i}", i % 3, $"enchant:{i}", i)).ToList();
            var original = source.ToArray();
            var destination = new List<Item>();
            foreach (var item in original)
            {
                var mode = rng.Next(4); // accept, veto, throw before, throw after
                await TransferProtocol.Move(item.Id, () =>
                {
                    if (mode == 2) throw new InvalidOperationException("before");
                    if (mode == 1) return Task.FromResult(false);
                    destination.Add(item);
                    if (mode == 3) throw new InvalidOperationException("after");
                    return Task.FromResult(true);
                }, () => destination.Any(i => ReferenceEquals(i, item)), () => source.Remove(item));
            }
            Assert.Equal(original.OrderBy(i => i.Id), source.Concat(destination).OrderBy(i => i.Id));
            Assert.Equal(original.Length, source.Concat(destination).Select(i => i.Id).Distinct().Count());
        }
    }

    [Fact]
    public void Duplicate_types_remain_distinct_and_keep_opaque_saved_properties()
    {
        const string payload1 = "{\"id\":\"staff\",\"props\":{\"charges\":4,\"upgrade\":2,\"enchant\":\"fire\"}}";
        const string payload2 = "{\"id\":\"staff\",\"props\":{\"charges\":0,\"upgrade\":1,\"enchant\":\"ice\"}}";
        var entries = new[] { new StoredItemRecord("r:8", payload1), new StoredItemRecord("r:9", payload2) };
        Assert.True(StoredItemCodec.TryRead(StoredItemCodec.Write(entries), "", out var read, out _));
        Assert.Equal(entries, read);
        read.RemoveAll(r => r.Key == "r:9");
        Assert.Equal(payload1, Assert.Single(read).Payload);
    }

    [Fact]
    public void Legacy_names_migrate_without_collapsing_duplicates_or_unknown_types()
    {
        Assert.True(StoredItemCodec.TryRead("", "MageStaff,MageStaff,RemovedModRelic", out var old, out _));
        Assert.Equal(3, old.Count);
        Assert.Equal(3, old.Select(i => i.Key).Distinct().Count());
        Assert.All(old, e => Assert.Equal("", e.Payload));
        Assert.Equal("RemovedModRelic", old[2].LegacyType);
        var json = StoredItemCodec.Write(old);
        Assert.True(StoredItemCodec.TryRead(json, "", out var restored, out _));
        Assert.Equal(old, restored);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_chest_is_valid(string text)
    {
        Assert.True(StoredItemCodec.TryRead(text, "", out var entries, out var error));
        Assert.Empty(entries); Assert.Equal("", error);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"Version\":99,\"Items\":[]}")]
    [InlineData("{\"Version\":2,\"Items\":null}")]
    [InlineData("{\"Version\":2,\"Items\":[null]}")]
    [InlineData("{\"Version\":2,\"Items\":[{\"Key\":\"same\",\"Payload\":\"a\",\"LegacyType\":\"\"},{\"Key\":\"same\",\"Payload\":\"b\",\"LegacyType\":\"\"}]}")]
    public void Corrupt_or_future_schema_is_not_treated_as_an_empty_writable_chest(string json)
    {
        Assert.False(StoredItemCodec.TryRead(json, "MageStaff", out _, out var error));
        Assert.NotEmpty(error);
        // Adapter must retain its original saved string when TryRead returns false.
    }

    [Theory]
    [InlineData(0, 0, "run:0", "run:0", null)]
    [InlineData(4, 3, "run:3", "run:3", OperationStatus.Duplicate)]
    [InlineData(4, 5, "run:5", "run:5", OperationStatus.Stale)]
    [InlineData(4, 4, "other-run:4", "run:4", OperationStatus.Stale)]
    public void Persisted_revision_prevents_replays_and_out_of_order_requests(int current, int expected, string id, string correct, OperationStatus? status) =>
        Assert.Equal(status, OperationGate.Validate(current, expected, id, correct));

    [Fact]
    public async Task Conflicting_operations_are_serialized_and_double_input_is_rejected()
    {
        var gate = new OperationGate();
        var first = gate.TryEnter();
        Assert.NotNull(first);
        Assert.Null(gate.TryEnter());
        var queued = gate.Enter();
        Assert.False(queued.IsCompleted);
        first.Dispose();
        using var second = await queued;
        Assert.NotNull(second);
        Assert.Null(gate.TryEnter());
        second.Dispose();
        first.Dispose(); // accidental repeated cleanup must not release a second semaphore slot
        gate.Blocked = true;
        Assert.Null(gate.TryEnter());
        Assert.Null(await gate.Enter());
    }

    [Fact]
    public async Task Nested_mule_chest_borrows_its_shop_lease_but_does_not_release_the_outer_operation()
    {
        var gate = new OperationGate();
        using var outer = await gate.Enter();
        using (var nested = await gate.Enter(outer))
        {
            Assert.NotNull(nested);
            Assert.Null(gate.TryEnter());
        }
        Assert.Null(gate.TryEnter());
        outer!.Dispose();
        using var next = gate.TryEnter();
        Assert.NotNull(next);
    }

    [Fact]
    public async Task A_queued_operation_cannot_enter_after_a_peer_blocks_the_inventory()
    {
        var gate = new OperationGate();
        using var first = await gate.Enter();
        var queued = gate.Enter();
        gate.Blocked = true;
        first!.Dispose();
        Assert.Null(await queued);
        // Receipts may still be re-sent while blocked, but a new mutation is checked by the adapter.
        using var receiptOnly = await gate.Enter(allowBlocked: true);
        Assert.NotNull(receiptOnly);
    }

    [Fact]
    public async Task A_different_player_or_expired_lease_cannot_bypass_inventory_serialization()
    {
        var gate = new OperationGate();
        var other = new OperationGate();
        var lease = await gate.Enter();
        Assert.NotNull(lease);
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.Enter(lease));
        lease.Dispose();
        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.Enter(lease));
        using var independent = await other.Enter();
        Assert.NotNull(independent);
    }

    [Theory]
    [InlineData(false, true, true, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(true, true, true, true, true)]
    [InlineData(true, true, false, true, false)]
    public void Failed_add_never_completes_or_ends_reward_choice(bool success, bool complete, bool end, bool expectedComplete, bool expectedEnd)
    {
        AcquisitionPolicy.AfterAdd(success, ref complete, ref end);
        Assert.Equal(expectedComplete, complete); Assert.Equal(expectedEnd, end);
    }

    [Theory]
    [InlineData(true, false, true, true, 1, true)]
    [InlineData(false, false, true, true, 1, false)]
    [InlineData(true, true, true, true, 1, false)]
    [InlineData(true, false, false, true, 1, false)]
    [InlineData(true, false, true, false, 1, false)]
    [InlineData(true, false, true, true, 0, false)]
    [InlineData(true, false, true, true, -2, false)]
    public void Discard_executor_rechecks_every_permission(bool system, bool combat, bool own, bool kind, int excess, bool expected) =>
        Assert.Equal(expected, AcquisitionPolicy.CanDiscard(system, combat, own, kind, excess));

    [Fact]
    public async Task Finalizer_failure_is_partial_and_does_not_abort_the_rest_of_a_batch()
    {
        var cleaned = false;
        var result = await TransferProtocol.Move("r:1", () => Task.FromResult(true), () => true,
            () => throw new InvalidOperationException("save finalization"), () => cleaned = true);
        Assert.Equal(OperationStatus.PartialCommit, result.Status);
        Assert.False(cleaned);
        Assert.Contains("save finalization", result.Detail);
        var next = await TransferProtocol.Move("r:2", () => Task.FromResult(true), () => true, () => { });
        Assert.True(next.Succeeded);
    }

    [Fact]
    public async Task An_unobservable_commit_never_cleans_up_a_possibly_committed_destination()
    {
        var finalized = false; var cleaned = false;
        var result = await TransferProtocol.Move("r:1", () => Task.FromResult(true),
            () => throw new InvalidOperationException("observer failure"), () => finalized = true, () => cleaned = true);
        Assert.Equal(OperationStatus.PartialCommit, result.Status);
        Assert.False(finalized); Assert.False(cleaned);
    }

    [Fact]
    public async Task Cleanup_failure_is_an_explicit_error_and_preserves_the_source()
    {
        var source = true;
        var result = await TransferProtocol.Move("r:1", () => Task.FromResult(false), () => false,
            () => source = false, () => throw new InvalidOperationException("cleanup failure"));
        Assert.Equal(OperationStatus.Error, result.Status);
        Assert.True(source);
        Assert.Contains("cleanup failure", result.Detail);
    }

    [Fact]
    public void Host_nonce_survives_settings_save_clone_and_does_not_depend_on_a_peer_clock()
    {
        const string id = "1234567890abcdef1234567890abcdef";
        var host = new RunSettingsData { RunId = id };
        var peer = RunSettingsData.Decode(host.Encode())!;
        Assert.Equal(id, peer.RunId);
        Assert.Equal(id, peer.Clone().RunId);
        Assert.Empty(RunSettingsData.Decode("v=1;sys=1")!.RunId); // legacy migration belongs to the game adapter
        Assert.Empty(RunSettingsData.Decode("v=1;run=not-a-guid")!.RunId);
        Assert.NotEqual(host.Encode(), new RunSettingsData { RunId = "abcdef1234567890abcdef1234567890" }.Encode());
    }

    [Fact]
    public async Task Async_payment_and_source_finalization_only_run_after_observed_add()
    {
        foreach (var accept in new[] { false, true })
        {
            var price = 0; var scroll = true;
            var result = await TransferProtocol.MoveAsync("scroll:1", () => Task.FromResult(accept), () => accept,
                async () => { await Task.Yield(); price += 40; scroll = false; });
            Assert.Equal(accept ? OperationStatus.Committed : OperationStatus.Rejected, result.Status);
            Assert.Equal(accept ? 40 : 0, price);
            Assert.Equal(!accept, scroll);
        }
    }

    [Fact]
    public async Task A_finalizer_that_throws_after_await_is_partial_and_never_rolls_back_the_card()
    {
        var deckCard = true;
        var cleaned = false;
        var result = await TransferProtocol.MoveAsync("scroll:1", () => Task.FromResult(true), () => deckCard,
            async () => { await Task.Yield(); throw new InvalidOperationException("discard hook"); },
            () => { cleaned = true; deckCard = false; return Task.CompletedTask; });
        Assert.Equal(OperationStatus.PartialCommit, result.Status);
        Assert.True(deckCard);
        Assert.False(cleaned);
    }

    [Fact]
    public void Replay_returns_the_original_receipt_and_rejects_an_altered_body_without_overwriting_it()
    {
        var journal = new OperationJournal<JournalRequest, OperationResult>();
        var request = new JournalRequest("r:18", 90);
        var original = new OperationResult(OperationStatus.PartialCommit, "run:8", "hook failed after removal", -45);
        journal.Add("run:8", request, original);
        Assert.True(journal.TryGet("run:8", new("r:18", 90), out var replay, out var conflict));
        Assert.False(conflict);
        Assert.Same(original, replay); // Adapter re-sends this receipt instead of running the action.
        Assert.True(journal.TryGet("run:8", new("r:19", 90), out replay, out conflict));
        Assert.True(conflict);
        Assert.Same(original, replay);
        Assert.Throws<InvalidOperationException>(() => journal.Add("run:8", request, new(OperationStatus.Committed)));
    }

    [Fact]
    public void Receipt_eviction_does_not_erase_the_saved_replay_high_water_mark()
    {
        var journal = new OperationJournal<JournalRequest, OperationResult>(2);
        for (var revision = 0; revision < 3; revision++)
            journal.Add($"run:{revision}", new("r:1", revision), new(OperationStatus.Committed));
        Assert.Equal(2, journal.Count);
        Assert.False(journal.Contains("run:0"));
        Assert.Equal(OperationStatus.Duplicate, OperationGate.Validate(3, 0, "run:0", "run:0"));
        Assert.False(new OperationJournal<JournalRequest, OperationResult>().Contains("run:2")); // reload
        Assert.Equal(OperationStatus.Duplicate, OperationGate.Validate(3, 2, "run:2", "run:2"));
    }

    [Fact]
    public void Run_identity_is_shared_but_not_reused_by_new_runs_owners_or_rollback_timelines()
    {
        const string seed = "STR-G1-000";
        var host = OperationIdentity.RunKey(seed, "saved-host-nonce", 1);
        Assert.Equal(host, OperationIdentity.RunKey(seed, "saved-host-nonce", 1)); // Continue
        Assert.NotEqual(host, OperationIdentity.RunKey(seed, "another-new-run", 1));
        Assert.NotEqual(host, OperationIdentity.RunKey(seed, "saved-host-nonce", 2));
        Assert.NotEqual(host, OperationIdentity.RunKey(seed, "rollback-host-epoch", 1));
        Assert.Equal("", OperationIdentity.RunKey(seed, "", 1)); // cannot invent a local co-op nonce
        Assert.True(OperationIdentity.TryRevision(host, OperationIdentity.Id(host, 12), out var revision));
        Assert.Equal(12, revision);
        foreach (var tail in new[] { "-1", "+1", "01", " 1", "2147483648", "1:0", "" })
            Assert.False(OperationIdentity.TryRevision(host, host + ":" + tail, out _));
        Assert.False(OperationIdentity.TryRevision(host, OperationIdentity.Id(OperationIdentity.RunKey(seed, "old-attempt", 1), 12), out _));
    }

    [Fact]
    public void Legacy_nonce_comes_from_the_saved_start_time_and_is_culture_independent()
    {
        var before = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            var nonce = OperationIdentity.LegacyNonce("seed", 1794873600);
            var key = OperationIdentity.RunKey("seed", nonce, ulong.MaxValue);
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            Assert.Equal(nonce, OperationIdentity.LegacyNonce("seed", 1794873600));
            Assert.Equal(key, OperationIdentity.RunKey("seed", nonce, ulong.MaxValue));
            Assert.NotEqual(nonce, OperationIdentity.LegacyNonce("seed", 1794873601));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = before; }
    }

    [Theory]
    [InlineData(100, 60, 40)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 130, 0)]
    [InlineData(20, 0, 20)]
    public void Payment_trust_uses_actual_delta_even_for_a_partial_charge(int before, int after, int paid) =>
        Assert.Equal(paid, OperationAccounting.Paid(before, after));

    [Theory]
    [InlineData(90, 90, false, OperationStatus.Committed)]
    [InlineData(90, 50, false, OperationStatus.PartialCommit)]
    [InlineData(90, 0, false, OperationStatus.PartialCommit)]
    [InlineData(90, 90, true, OperationStatus.PartialCommit)]
    public void Sale_does_not_claim_full_success_if_payout_or_hooks_failed(int expected, int actual, bool error, OperationStatus status) =>
        Assert.Equal(status, OperationAccounting.SaleStatus(expected, actual, error));

    [Fact]
    public async Task Committed_finalization_attempts_every_step_and_reports_all_errors()
    {
        var calls = new List<int>();
        var error = await Assert.ThrowsAsync<AggregateException>(() => TransferProtocol.Finish(
            () => { calls.Add(1); throw new InvalidOperationException("materials"); },
            async () => { await Task.Yield(); calls.Add(2); throw new InvalidOperationException("payment"); },
            () => { calls.Add(3); return Task.CompletedTask; }));
        Assert.Equal(new[] { 1, 2, 3 }, calls);
        Assert.Equal(new[] { "materials", "payment" }, error.InnerExceptions.Select(e => e.Message));
    }

    [Fact]
    public async Task An_expired_borrow_cannot_enter_or_release_a_subsequent_operation()
    {
        var gate = new OperationGate();
        var root = await gate.Enter();
        var borrow = await gate.Enter(root);
        root!.Dispose();
        using var next = await gate.Enter();
        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.Enter(borrow));
        borrow!.Dispose();
        Assert.Null(gate.TryEnter());
    }

    [Fact]
    public async Task A_known_finalizer_may_borrow_its_live_lease_after_blocking_but_new_work_may_not()
    {
        var gate = new OperationGate();
        using var root = await gate.Enter();
        gate.Blocked = true;
        using var finish = await gate.Enter(root);
        Assert.NotNull(finish);
        Assert.Null(gate.TryEnter());
        finish!.Dispose(); root!.Dispose();
        Assert.Null(await gate.Enter());
    }

    [Theory]
    [InlineData("{\"Version\":2,\"Items\":[{\"Key\":\"r:1\"}]}")]
    [InlineData("{\"Version\":2,\"Items\":[{\"Key\":\" \",\"Payload\":\"opaque\"}]}")]
    [InlineData("{\"Version\":2,\"Items\":[{\"Key\":\"r:1\",\"Payload\":\"opaque\",\"LegacyType\":null}]}")]
    public void Incomplete_item_records_never_authorize_destructive_chest_rewrites(string json) =>
        Assert.False(StoredItemCodec.TryRead(json, "LegacyStaff", out _, out _));

    private sealed record JournalRequest(string Instance, int Price);
    private sealed record Item(string Id, int Upgrade, string Enchants, int ChargesUsed);
}
