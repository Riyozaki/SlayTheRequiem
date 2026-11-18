using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Combat;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Alchemy;

/// <summary>
/// Походный мешок (docs/design/02 §3, 10) — служебная стартовая реликвия Груза:
/// счётчик = текущий вес (в описании — вес/вместимость/перегруз); при перегрузе &gt; +10 новые карты не берутся;
/// «Пёрышко» добавляет вместимость до следующего лагеря; хранит содержимое Сундука лагеря (карты и снаряжение).
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class TravelPack : RequiemRelic, IResourceSource
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => false;
    public override bool ShowCounter => true;
    public override int DisplayAmount => Loadout.TotalWeight(Owner);

    public static TravelPack? Of(Player? player) => player?.Relics.OfType<TravelPack>().FirstOrDefault();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Weight", 0), new DynamicVar("Capacity", 0), new DynamicVar("Excess", 0), new DynamicVar("Stored", 0)];

    // ───── «Пёрышко» ─────

    private int _featherBonus;

    /// <summary>Вместимость от зелья «Пёрышко» — до следующего лагеря.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FeatherBonus { get => _featherBonus; set { AssertMutable(); _featherBonus = value; } }

    public int BurdenBonus => FeatherBonus;

    // ───── Сундук ─────

    private List<SerializableCard> _storedCards = new();
    private string _storedItems = ""; // v1 compatibility: do NOT remove or reinterpret this saved field
    private string _storedItemData = "";
    private int _instanceSequence, _operationRevision, _dataSchema, _shopProtocol;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DataSchema
    {
        get => _dataSchema;
        set { AssertMutable(); if (value is < 0 or > Debugging.RunDiagnostics.SaveSchema) throw new FormatException("Unsupported Requiem save schema"); _dataSchema = value; }
    }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ShopProtocol
    {
        get => _shopProtocol;
        set { AssertMutable(); if (value is < 0 or > Shops.ShopOperations.Protocol) throw new FormatException("Unsupported Requiem operation protocol"); _shopProtocol = value; }
    }
    private ConditionalWeakTable<CardModel, SerializableCard> _cardPreviews = new();
    private ConditionalWeakTable<RelicModel, StoredItemRecord> _itemPreviews = new();

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        // AbstractModel uses MemberwiseClone: neither a canonical pack nor a second player may share a chest,
        // pending claims or preview mappings. The inventory lock is keyed by the actual run player.
        // Deep copy SavedProperties, not only the list shell.
        SerializableCard Copy(SerializableCard card) => JsonSerializer.Deserialize(
            JsonSerializer.Serialize(card, JsonSerializationUtility.GetTypeInfo<SerializableCard>()),
            JsonSerializationUtility.GetTypeInfo<SerializableCard>())!;
        _storedCards = _storedCards.Select(Copy).ToList();
        _pendingCards = _pendingCards.Select(Copy).ToList();
        _cardPreviews = new();
        _itemPreviews = new();
        _offeringPending = false;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public List<SerializableCard> StoredCards { get => _storedCards; set { AssertMutable(); _storedCards = value ?? new(); } }

    /// <summary>Legacy class-name list. Migrated atomically on the first successful chest mutation.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string StoredItems { get => _storedItems; set { AssertMutable(); _storedItems = value ?? ""; } }

    /// <summary>v2: opaque SerializableRelic JSON, saved properties and stable instance key for EVERY entry.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string StoredItemData { get => _storedItemData; set { AssertMutable(); _storedItemData = value ?? ""; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int InstanceSequence { get => _instanceSequence; set { AssertMutable(); _instanceSequence = Math.Max(0, value); } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int OperationRevision { get => _operationRevision; set { AssertMutable(); _operationRevision = Math.Max(0, value); } }

    private bool _operationBlocked;
    /// <summary>An unresolved partial result must not become safe merely because the same save was reloaded.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool OperationBlocked { get => _operationBlocked; set { AssertMutable(); _operationBlocked = value; } }

    private string _operationPending = "";
    /// <summary>A save inside an async effect/payment callback has an unknown outcome until its scope finishes.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string OperationPending { get => _operationPending; set { AssertMutable(); _operationPending = value ?? ""; } }

    private string _operationFailure = "";
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string OperationFailure { get => _operationFailure; set { AssertMutable(); _operationFailure = value ?? ""; } }

    private string _operationRunId = "";
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string OperationRunId { get => _operationRunId; set { AssertMutable(); _operationRunId = value ?? ""; } }

    public int StoredCount => _storedCards.Count + (TryItems(out var items, out _) ? items.Count : 0);

    private bool TryItems(out List<StoredItemRecord> items, out string error) =>
        StoredItemCodec.TryRead(_storedItemData, _storedItems, out items, out error);

    private void SaveItems(List<StoredItemRecord> items)
    {
        // Encode before changing either saved field. Unknown types/payloads survive every rewrite.
        var data = StoredItemCodec.Write(items);
        _storedItemData = data;
        _storedItems = "";
    }

    internal static string CardPayload(SerializableCard snapshot) =>
        JsonSerializer.Serialize(snapshot, JsonSerializationUtility.GetTypeInfo<SerializableCard>());

    private static SerializableCard CopyCardPayload(string payload) =>
        JsonSerializer.Deserialize(payload, JsonSerializationUtility.GetTypeInfo<SerializableCard>())
        ?? throw new FormatException("Empty stored card snapshot");

    private void RemoveStoredCard(SerializableCard snapshot, string expectedPayload)
    {
        var index = _storedCards.FindIndex(c => ReferenceEquals(c, snapshot));
        if (index < 0)
        {
            var key = InventoryIdentity.CardKey(CopyCardPayload(expectedPayload));
            if (key.Length > 0 && _storedCards.Any(s => InventoryIdentity.CardKey(s) == key))
                throw new InvalidOperationException("Saved chest card was replaced in a relocation callback; retain the replacement for reconciliation");
            return; // a callback already removed this source, not permission to remove an equal-looking entry
        }
        if (!SavedPayload.Equivalent(expectedPayload, CardPayload(_storedCards[index])))
            throw new InvalidOperationException("Saved chest card changed in a relocation callback; retain it for reconciliation");
        _storedCards.RemoveAt(index); // SerializableCard.Equals ignores some properties: never use Remove(value).
    }

    private void RemoveStoredItem(StoredItemRecord expected)
    {
        if (!TryItems(out var current, out var failure)) throw new InvalidOperationException(failure);
        var index = current.FindIndex(s => s.Key == expected.Key);
        if (index < 0) return;
        if (current[index] != expected) throw new InvalidOperationException("Saved chest source changed in a relocation callback; retain it for reconciliation");
        current.RemoveAt(index);
        SaveItems(current); // remove only this source; never overwrite unrelated changes made by a callback
    }

    /// <summary>One outcome per requested card; an exception after removal is a committed transfer, not a lost card.</summary>
    public async Task<IReadOnlyList<OperationResult>> StoreCards(IReadOnlyList<CardModel> cards, OperationGate.Lease? held = null)
    {
        AssertMutable();
        var results = new List<OperationResult>();
        var requested = cards.Distinct().ToList();
        using var lease = await InventoryGate.For(Owner).Enter(held);
        if (lease == null) return Report(requested.Select(c => new OperationResult(OperationStatus.Busy, c.Id.Entry, "Inventory synchronization blocked")).ToList());
        try
        {
            InventoryIdentity.Ensure(Owner);
            OperationRecovery.Capture(Owner, "chest:store-cards");
            foreach (var card in requested)
            {
                if (!CanTransfer || !card.IsMutable || card.Owner != Owner || !Owner.Deck.Cards.Contains(card) || !card.IsRemovable)
                { results.Add(OperationResult.Reject(card.Id.Entry, "Not an owned removable deck card")); continue; }
                var key = InventoryIdentity.CardKey(card);
                SerializableCard snapshot;
                string stagedPayload;
                try
                {
                    snapshot = card.ToSerializable();
                    var prior = _storedCards.Where(s => InventoryIdentity.CardKey(s) == key).ToList();
                    if (prior.Count > 0)
                    {
                        if (prior.Count != 1 || !SavedPayload.Preserves(
                            JsonSerializer.Serialize(prior[0], JsonSerializationUtility.GetTypeInfo<SerializableCard>()),
                            JsonSerializer.Serialize(snapshot, JsonSerializationUtility.GetTypeInfo<SerializableCard>())))
                        {
                            var conflict = new OperationResult(OperationStatus.PartialCommit, key, "Store collides with a different saved chest instance; both sources retained");
                            results.Add(conflict); InventoryGate.Block(Owner, conflict.Detail); continue;
                        }
                        snapshot = prior[0]; // an interrupted prior store already wrote this exact source
                    }
                    else _storedCards.Add(snapshot);
                    stagedPayload = CardPayload(snapshot);
                    // Write BEFORE detaching: a save inside CardRemoved must never see neither side. If a save
                    // sees both, the saved instance key + full-payload reconciliation handles that duplicate view.
                }
                catch (Exception e) { results.Add(new(OperationStatus.Error, key, e.ToString())); continue; }
                var moved = await TransferProtocol.Move(key,
                    () =>
                    {
                        // Storage is not a permanent removal: no paid curse-removal surcharge, history or hooks.
                        card.RemoveFromCurrentPile();
                        return Task.FromResult(true);
                    },
                    () => !Owner.Deck.Cards.Contains(card),
                    () =>
                    {
                        // A before/after hook may have thrown between pile removal and scope removal.
                        if (Owner.RunState.ContainsCard(card)) Owner.RunState.RemoveCard(card);
                        card.HasBeenRemovedFromState = true;
                    },
                    () => RemoveStoredCard(snapshot, stagedPayload));
                results.Add(moved);
                if (moved.Status == OperationStatus.PartialCommit) InventoryGate.Block(Owner, $"Chest store {key}: {moved.Detail}");
            }
        }
        finally { Refresh(); }
        return Report(results);
    }

    /// <summary>Indices are captured BEFORE awaiting the lock, never reinterpreted after another transfer.</summary>
    public Task<IReadOnlyList<OperationResult>> TakeCards(IReadOnlyList<int> indices, OperationGate.Lease? held = null) =>
        TakeSnapshots(indices.Distinct().Select(i => i >= 0 && i < _storedCards.Count ? _storedCards[i] : null).ToList(), held);

    public Task<IReadOnlyList<OperationResult>> TakeCards(IReadOnlyList<CardModel> previews, OperationGate.Lease? held = null) =>
        TakeSnapshots(previews.Distinct().Select(c => _cardPreviews.TryGetValue(c, out var snapshot) ? snapshot : null).ToList(), held);

    private async Task<IReadOnlyList<OperationResult>> TakeSnapshots(List<SerializableCard?> requested, OperationGate.Lease? held)
    {
        AssertMutable();
        var results = new List<OperationResult>();
        using var lease = await InventoryGate.For(Owner).Enter(held);
        if (lease == null) return Report(requested.Select(c => new OperationResult(OperationStatus.Busy, c?.Id?.Entry ?? "card", "Inventory synchronization blocked")).ToList());
        try
        {
            OperationRecovery.Capture(Owner, "chest:take-cards");
            InventoryIdentity.Ensure(Owner); // migrate legacy SAVED card metadata before the first Add callback
            foreach (var snapshot in requested)
            {
                if (!CanTransfer || snapshot == null || !_storedCards.Any(s => ReferenceEquals(s, snapshot)))
                { results.Add(OperationResult.Reject("card", "Stale chest selection")); continue; }
                var key = "";
                string expectedPayload;
                CardModel card;
                try
                {
                    key = InventoryIdentity.CardKey(snapshot);
                    expectedPayload = CardPayload(snapshot);
                    if (key.Length == 0 || _storedCards.Count(s => InventoryIdentity.CardKey(s) == key) != 1)
                    {
                        var conflict = new OperationResult(OperationStatus.PartialCommit, key, "Ambiguous saved chest instance identity; all sources retained");
                        results.Add(conflict); InventoryGate.Block(Owner, conflict.Detail); continue;
                    }
                    if (InventoryIdentity.FindCard(Owner, key) is { } existing)
                    {
                        if (SavedPayload.Preserves(expectedPayload, CardPayload(existing.ToSerializable())))
                        {
                            RemoveStoredCard(snapshot, expectedPayload);
                            results.Add(new(OperationStatus.Committed, key, "Reconciled an already restored saved instance; no second Add"));
                        }
                        else
                        {
                            var conflict = new OperationResult(OperationStatus.PartialCommit, key, "Chest/live instance identity collides with different properties; source retained");
                            results.Add(conflict); InventoryGate.Block(Owner, conflict.Detail);
                        }
                        continue;
                    }
                    if (snapshot.Id == null || ModelDb.GetByIdOrNull<CardModel>(snapshot.Id) == null)
                        throw new InvalidOperationException("Stored card type unavailable; snapshot retained");
                    // Fill may hand a saved int/card array to the restored model. Never let that model or its
                    // callbacks mutate the durable source by aliasing SavedProperties before finalization.
                    card = CardModel.FromSerializable(CopyCardPayload(expectedPayload));
                }
                catch (Exception e) { results.Add(new(OperationStatus.Error, snapshot.Id?.Entry ?? "unknown", e.ToString())); continue; }
                var moved = await TransferProtocol.Move(key.Length > 0 ? key : card.Id.Entry,
                    async () =>
                    {
                        Owner.RunState.AddCard(card, Owner);
                        if (!SavedPayload.Preserves(expectedPayload, CardPayload(card.ToSerializable())))
                            throw new InvalidOperationException("Stored card cannot round-trip ALL saved properties/enchantment; original snapshot retained");
                        return (await CardAcquisition.RestoreFromChest(card)).success;
                    },
                    () => Owner.Deck.Cards.Contains(card),
                    () => RemoveStoredCard(snapshot, expectedPayload),
                    () => CardAcquisition.CleanUncommitted(card, Owner));
                results.Add(moved);
                if (moved.Status == OperationStatus.PartialCommit) InventoryGate.Block(Owner, $"Chest take {key}: {moved.Detail}");
            }
        }
        finally { Refresh(); }
        return Report(results);
    }

    /// <summary>Preview references identify exact saved entries even if several models have identical class/upgrade.</summary>
    public List<CardModel> PreviewStoredCards()
    {
        var models = new List<CardModel>();
        foreach (var snapshot in _storedCards)
        {
            try
            {
                if (snapshot.Id == null || ModelDb.GetByIdOrNull<CardModel>(snapshot.Id) == null) continue;
                var card = CardModel.FromSerializable(CopyCardPayload(CardPayload(snapshot)));
                _cardPreviews.Add(card, snapshot);
                models.Add(card);
            }
            catch (Exception e) { MainFile.Logger.Warn($"[Chest] Card preview unavailable; kept: {snapshot.Id}: {e.Message}"); }
        }
        return models;
    }

    private EquipmentRelic RestoreItem(StoredItemRecord record)
    {
        if (!string.IsNullOrEmpty(record.Payload))
        {
            var snapshot = JsonSerializer.Deserialize(record.Payload, JsonSerializationUtility.GetTypeInfo<SerializableRelic>())
                ?? throw new FormatException("Empty relic snapshot");
            if (snapshot.Id == null || ModelDb.GetByIdOrNull<RelicModel>(snapshot.Id) is not EquipmentRelic)
                throw new InvalidOperationException("Stored equipment type unavailable; payload retained");
            var item = (EquipmentRelic)RelicModel.FromSerializable(snapshot);
            item.Owner = Owner; // getters may depend on the owner, but no inventory registration/obtain notification
            if (!SavedPayload.Preserves(record.Payload, JsonSerializer.Serialize(item.ToSerializable(), JsonSerializationUtility.GetTypeInfo<SerializableRelic>())))
                throw new InvalidOperationException("Stored equipment cannot round-trip ALL saved properties; opaque payload retained");
            return item;
        }
        // Old saves never recorded upgrades/charges: recover the original base type, without inventing those values.
        var canonical = ModelDb.AllRelics.OfType<EquipmentRelic>().FirstOrDefault(r => r.GetType().Name == record.LegacyType)
            ?? throw new InvalidOperationException($"Legacy equipment unavailable; name retained: {record.LegacyType}");
        return (EquipmentRelic)canonical.ToMutable();
    }

    public List<RelicModel> StoredItemRelics()
    {
        var models = new List<RelicModel>();
        if (!TryItems(out var items, out var error)) { MainFile.Logger.Error($"[Chest] {error}"); return models; }
        foreach (var record in items)
        {
            try
            {
                var item = RestoreItem(record);
                item.IsStowed = true;
                _itemPreviews.Add(item, record);
                models.Add(item);
            }
            catch (Exception e) { MainFile.Logger.Warn($"[Chest] Item {record.Key} unavailable; kept: {e.Message}"); }
        }
        return models;
    }

    public async Task<OperationResult> StoreItem(EquipmentRelic item, OperationGate.Lease? held = null)
    {
        AssertMutable();
        using var lease = await InventoryGate.For(Owner).Enter(held);
        if (lease == null) return new(OperationStatus.Busy, item.Id.Entry, "Inventory synchronization blocked");
        try
        {
            if (!CanTransfer || !item.IsStowed || item.Owner != Owner || !Owner.Relics.Contains(item))
                return OperationResult.Reject(item.Id.Entry, "Not an owned stowed item");
            if (!TryItems(out var items, out var error)) return OperationResult.Reject(item.Id.Entry, error);
            var key = InventoryIdentity.RelicKey(item);
            var payload = JsonSerializer.Serialize(item.ToSerializable(), JsonSerializationUtility.GetTypeInfo<SerializableRelic>());
            var prior = items.FirstOrDefault(s => s.Key == key);
            if (prior != null && (prior.Payload.Length == 0 || !SavedPayload.Preserves(prior.Payload, payload)))
            {
                var conflict = new OperationResult(OperationStatus.PartialCommit, key, "Store collides with a different saved chest relic; both sources retained");
                Report([conflict]); return conflict;
            }
            OperationRecovery.Capture(Owner, "chest:store-item");
            var staged = prior ?? new StoredItemRecord(key, payload);
            if (prior == null) items.Add(staged);
            SaveItems(items); // Write-ahead source: a save inside RelicRemoved must contain the complete item.
            var result = await TransferProtocol.Move(key,
                () =>
                {
                    // Relocation, not a new removal/obtain: no reward history or AfterRemoved/AfterObtained bonuses.
                    Owner.RemoveRelicInternal(item);
                    return Task.FromResult(true);
                },
                () => !Owner.Relics.Contains(item),
                () => { },
                () => RemoveStoredItem(staged));
            Report([result]);
            return result;
        }
        catch (Exception e) { var result = new OperationResult(OperationStatus.Error, item.Id.Entry, e.ToString()); Report([result]); return result; }
        finally { Refresh(); }
    }

    public Task<OperationResult> TakeItem(RelicModel preview, OperationGate.Lease? held = null) =>
        TakeItem(_itemPreviews.TryGetValue(preview, out var entry) ? entry.Key : "", held);

    /// <summary>Exact entry key, never a class name. A veto/unknown type keeps the complete source record.</summary>
    public async Task<OperationResult> TakeItem(string key, OperationGate.Lease? held = null)
    {
        AssertMutable();
        using var lease = await InventoryGate.For(Owner).Enter(held);
        if (lease == null) return new(OperationStatus.Busy, key, "Inventory synchronization blocked");
        try
        {
            if (!CanTransfer) return OperationResult.Reject(key, "Chest is not available in combat or outside Requiem systems");
            if (!TryItems(out var items, out var error)) return OperationResult.Reject(key, error);
            var index = items.FindIndex(i => i.Key == key);
            if (index < 0) return OperationResult.Reject(key, "Stale chest instance");
            OperationRecovery.Capture(Owner, "chest:take-item");
            if (InventoryIdentity.FindRelic(Owner, key) is { } existing)
            {
                if (items[index].Payload.Length == 0 || !SavedPayload.Preserves(items[index].Payload,
                    JsonSerializer.Serialize(existing.ToSerializable(), JsonSerializationUtility.GetTypeInfo<SerializableRelic>())))
                {
                    var conflict = new OperationResult(OperationStatus.PartialCommit, key, "Chest/live relic identity collides with different properties; source retained");
                    Report([conflict]); return conflict;
                }
                items.RemoveAt(index); SaveItems(items);
                var reconciled = new OperationResult(OperationStatus.Committed, key, "Reconciled an already restored saved relic; no second obtain/equip");
                Report([reconciled]); return reconciled;
            }
            var record = items[index];
            var item = RestoreItem(record);
            item.IsStowed = true; // BEFORE notifying the inventory: never equip it for even one frame.
            if (record.Payload.Length == 0)
            {
                // Legacy names have neither saved identity nor properties. Migrate this one known instance
                // BEFORE AddRelicInternal; an insertion save must not leave a repeatable legacy name behind.
                item.Owner = Owner;
                key = InventoryIdentity.NewStoredRelicKey(Owner, item);
                record = new(key, JsonSerializer.Serialize(item.ToSerializable(), JsonSerializationUtility.GetTypeInfo<SerializableRelic>()));
                items[index] = record;
                SaveItems(items);
            }
            else if (InventoryIdentity.RelicKey(item.ToSerializable()) != key)
                return OperationResult.Reject(key, "Saved relic payload/instance key disagree; opaque source retained");
            var result = await TransferProtocol.Move(key,
                () => { Owner.AddRelicInternal(item); return Task.FromResult(true); },
                () => Owner.Relics.Contains(item),
                () => RemoveStoredItem(record));
            // Identity was assigned/validated BEFORE attach. Do not run a fallible migration after an observed
            // commit outside TransferProtocol: it could downgrade a finalized transfer to an ordinary Error.
            Report([result]);
            return result;
        }
        catch (Exception e) { var result = new OperationResult(OperationStatus.Error, key, e.ToString()); Report([result]); return result; }
        finally { Refresh(); }
    }

    private IReadOnlyList<OperationResult> Report(IReadOnlyList<OperationResult> results)
    {
        foreach (var result in results)
        {
            MainFile.Logger.Info($"[Chest] {result.Status} target={result.Target} {result.Detail}");
            if (result.Status == OperationStatus.PartialCommit) InventoryGate.Block(Owner, $"Chest {result.Target}: {result.Detail}");
        }
        return results;
    }

    /// <summary>Explicit event loss, including opaque/legacy entries. Corrupt/future schemas are never rewritten.</summary>
    public int LoseHalf(Func<int, int> roll)
    {
        AssertMutable();
        using var lease = InventoryGate.For(Owner).TryEnter();
        if (lease == null) return 0;
        try
        {
            if (!TryItems(out var items, out var error)) { MainFile.Logger.Error($"[Chest] {error}"); return 0; }
            OperationRecovery.Capture(Owner, "chest:event-loss");
            var lose = CampRules.ChestLoss(_storedCards.Count + items.Count);
            for (var n = 0; n < lose; n++)
            {
                var total = _storedCards.Count + items.Count;
                var i = Math.Clamp(roll(total), 0, total - 1);
                if (i < _storedCards.Count) _storedCards.RemoveAt(i);
                else items.RemoveAt(i - _storedCards.Count);
            }
            SaveItems(items);
            Refresh();
            return lose;
        }
        finally { Refresh(); }
    }

    private bool CanTransfer => RequiemMode.IsInSystem(Owner) && Owner.Creature.IsAlive && !CombatManager.Instance.IsInProgress
        && !InventoryGate.For(Owner).Blocked;

    private string _mandatoryGrantSources = "";
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string MandatoryGrantSources { get => _mandatoryGrantSources; set { AssertMutable(); _mandatoryGrantSources = value ?? ""; } }

    internal bool HasMandatorySource(string source)
    {
        MandatoryGrantLedger.ValidateSource(source);
        return MandatoryGrantLedger.Read(_mandatoryGrantSources).ContainsKey(source);
    }

    internal void PrepareMandatorySource(string source, CardModel card)
    {
        var sources = MandatoryGrantLedger.Read(_mandatoryGrantSources);
        MandatoryGrantLedger.ValidateSource(source);
        if (sources.ContainsKey(source)) throw new InvalidOperationException("Mandatory source already prepared");
        if (sources.Count >= MandatoryGrantLedger.MaxEntries) throw new InvalidOperationException("Mandatory source journal full");
        if (!CardAcquisition.IsDeferredClaim(card)) CardAcquisition.MarkClaim(card, InventoryIdentity.NewClaimKey(Owner));
        sources.Add(source, CardAcquisition.ClaimKey(card));
        var journal = MandatoryGrantLedger.Write(sources); // validate/encode BEFORE changing either durable source
        PrepareDeferredCard(card);
        _mandatoryGrantSources = journal; // synchronous write-ahead pair, before any await
    }

    internal void CancelMandatorySources(string prefix)
    {
        var claims = MandatoryGrantLedger.Read(_mandatoryGrantSources).Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(kv => kv.Value).ToHashSet(StringComparer.Ordinal);
        _pendingCards.RemoveAll(s => claims.Contains(CardAcquisition.ClaimKey(s))); // deliberate source revocation, e.g. shedding a form
    }

    private List<SerializableCard> _pendingCards = new();
    private bool _offeringPending;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public List<SerializableCard> PendingCards { get => _pendingCards; set { AssertMutable(); _pendingCards = value ?? new(); } }

    public void DeferCard(CardModel card) { PrepareDeferredCard(card); }

    /// <summary>Write-ahead grant source. Keep a private payload copy before any obtain/selection callback.</summary>
    internal SerializableCard PrepareDeferredCard(CardModel card)
    {
        AssertMutable();
        if (card.Owner != Owner || Owner.Deck.Cards.Contains(card)) throw new InvalidOperationException("Invalid deferred card grant");
        if (!CardAcquisition.IsDeferredClaim(card)) CardAcquisition.MarkClaim(card, InventoryIdentity.NewClaimKey(Owner));
        var key = CardAcquisition.ClaimKey(card);
        var payload = CardPayload(card.ToSerializable());
        var prior = _pendingCards.Where(s => CardAcquisition.ClaimKey(s) == key).ToList();
        if (prior.Count > 0)
        {
            if (prior.Any(s => !SavedPayload.Equivalent(payload, CardPayload(s))))
            {
                InventoryGate.Block(Owner, $"Deferred claim {key} collides with a different saved payload; source retained");
                throw new InvalidOperationException("Conflicting deferred card entitlement");
            }
            return prior[0]; // repeated preparation, not another entitlement
        }
        var snapshot = CopyCardPayload(payload);
        _pendingCards.Add(snapshot);
        return snapshot;
    }

    /// <summary>The caller must clean the returned run-scoped preview after its reward task, never during VFX.</summary>
    internal CardModel RestorePendingCard(SerializableCard snapshot)
    {
        if (snapshot.Id == null || ModelDb.GetByIdOrNull<CardModel>(snapshot.Id) == null)
            throw new InvalidOperationException("Pending card type unavailable; snapshot retained");
        var payload = CardPayload(snapshot);
        var card = CardModel.FromSerializable(CopyCardPayload(payload));
        try
        {
            Owner.RunState.AddCard(card, Owner);
            if (!SavedPayload.Preserves(payload, CardPayload(card.ToSerializable())))
                throw new InvalidOperationException("Pending card cannot round-trip ALL saved properties; snapshot retained");
            return card;
        }
        catch { CardAcquisition.CleanUncommitted(card, Owner); throw; }
    }

    internal bool PendingMatches(SerializableCard snapshot, string payload)
    {
        if (!_pendingCards.Any(s => ReferenceEquals(s, snapshot)) || !SavedPayload.Equivalent(payload, CardPayload(snapshot))) return false;
        var key = CardAcquisition.ClaimKey(snapshot);
        return key.Length > 0 && _pendingCards.Where(s => CardAcquisition.ClaimKey(s) == key)
            .All(s => SavedPayload.Equivalent(payload, CardPayload(s)));
    }

    /// <summary>Retry one-time claims outside combat; skipping postpones them, it does not erase the saved claim.</summary>
    public async Task OfferPendingCards()
    {
        if (_offeringPending || _pendingCards.Count == 0 || CombatManager.Instance.IsInProgress) return;
        _offeringPending = true; // set before awaiting a lease; two room callbacks must not offer the same entitlement
        var room = Owner.RunState.CurrentRoom;
        try
        {
            foreach (var saved in _pendingCards.ToList())
            {
                var snapshot = saved;
                var expectedPayload = "";
                CardModel? card = null;
                CardReward? reward = null;
                DeferredRewardScope? scope = null;
                try
                {
                    using (var lease = await InventoryGate.For(Owner).Enter())
                    {
                        if (lease == null || !CanTransfer || !ReferenceEquals(room, Owner.RunState.CurrentRoom)) return;
                        if (!_pendingCards.Any(s => ReferenceEquals(s, snapshot))) continue;
                        card = RestorePendingCard(snapshot); // private Props copy, full round-trip before offering anything
                        if (!CardAcquisition.IsDeferredClaim(card))
                        {
                            // A pre-claim legacy snapshot must be made durable BEFORE the first obtain hook.
                            CardAcquisition.MarkClaim(card, InventoryIdentity.NewClaimKey(Owner));
                            var index = _pendingCards.FindIndex(s => ReferenceEquals(s, snapshot));
                            snapshot = CopyCardPayload(CardPayload(card.ToSerializable()));
                            _pendingCards[index] = snapshot;
                        }
                        expectedPayload = CardPayload(snapshot);
                        if (!PendingMatches(snapshot, expectedPayload))
                            throw new InvalidOperationException("Ambiguous deferred claim payloads; all sources retained");
                        if (CardAcquisition.CommittedCard(card, Owner) == null)
                        {
                            reward = new CardReward([card], CardCreationSource.Other, Owner,
                                CardCreationOptions.ForNonCombatWithDefaultOdds([Owner.Character.CardPool]));
                            scope = new DeferredRewardScope(reward, this, snapshot);
                        }
                    }
                    if (reward != null) await RewardsCmd.OfferCustom(Owner, [reward]);
                }
                catch (Exception e) { MainFile.Logger.Error($"[CardReward] phase=deferred-offer card={snapshot.Id}: {e}"); }
                finally
                {
                    scope?.Dispose();
                    if (card != null)
                    {
                        // A known claim can finish even if an obtain callback blocked new operations. No new Add.
                        using var lease = await InventoryGate.For(Owner).Enter(allowBlocked: true);
                        try
                        {
                            var committed = CardAcquisition.CommittedCard(card, Owner);
                            if (committed != null)
                            {
                                if (await CardAcquisition.CompleteReplacement(committed))
                                {
                                    ConsumePending(snapshot, expectedPayload); // exact saved entitlement, including duplicate views
                                    CardAcquisition.MarkClaim(committed, "");
                                }
                            }
                            else if (reward?.SuccessfullySelected == true)
                            {
                                // An explicitly chosen alternative may consume the reward, but must NOT remove a starter.
                                ConsumePending(snapshot, expectedPayload);
                            }
                        }
                        catch (Exception e) { InventoryGate.Block(Owner, $"Deferred reward finalization {snapshot.Id}: {e}"); }
                        finally { CardAcquisition.CleanUncommitted(card, Owner); }
                    }
                }
            }
        }
        finally { _offeringPending = false; }
    }

    internal void ConsumePending(SerializableCard snapshot, string expectedPayload)
    {
        var key = CardAcquisition.ClaimKey(CopyCardPayload(expectedPayload)); // never trust a callback-mutated source key
        bool Matches(SerializableCard s) => ReferenceEquals(s, snapshot) || key.Length > 0 && CardAcquisition.ClaimKey(s) == key;
        if (_pendingCards.Where(Matches).Any(s => !SavedPayload.Equivalent(expectedPayload, CardPayload(s))))
            throw new InvalidOperationException("Deferred source changed during acquisition; retain it for reconciliation");
        _pendingCards.RemoveAll(s => Matches(s)); // unrelated grants added by callbacks stay untouched
    }

    // ───── лагерь ─────

    /// <summary>Действия лагеря Реквиема (очки лагеря — <see cref="Camp"/>).</summary>
    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner || !RequiemMode.IsInSystem(Owner)) return false;
        Camp.AddOptions(player, options);
        return true;
    }

    // ───── Груз ─────

    /// <summary>Перегруз &gt; +10: новые карты не подбираются, пока не выбросите лишнее (docs/design/02 §3).</summary>
    public override bool ShouldAddToDeck(CardModel card)
    {
        if (card.Owner != Owner || !RequiemMode.IsInSystem(Owner)) return true;
        if (card.Type is CardType.Curse or CardType.Status) return true; // explicit forced-grant policy, not a voluntary sale
        return !InventoryGate.For(Owner).Blocked && Loadout.Overload(Owner).CanPickUp;
    }

    /// <summary>
    /// Карта не взята из-за перегруза: раньше игра молча оставляла её на экране награды («карты то добавляются, то нет»,
    /// плейтест 2026-11). Теперь реликвия мигает и игрок видит, почему и что делать.
    /// </summary>
    public override Task AfterAddToDeckPrevented(CardModel card)
    {
        if (card.Owner != Owner) return Task.CompletedTask;
        if (InventoryGate.For(Owner).Blocked)
        {
            Debugging.CardRewardTrace.Card("veto:operation-blocked", card, "Unresolved or interrupted inventory operation", force: true);
            if (MegaCrit.Sts2.Core.Context.LocalContext.IsMe(Owner)) UI.NRequiemToast.Show(UI.RequiemUi.TipText("OPERATION_BLOCKED.title"));
            return Task.CompletedTask;
        }
        Flash();
        Debugging.CardRewardTrace.Card("veto:burden", card, $"weight={Loadout.TotalWeight(Owner)} capacity={Loadout.Capacity(Owner)}", force: true);
        if (MegaCrit.Sts2.Core.Context.LocalContext.IsMe(Owner))
        {
            var overload = Loadout.Overload(Owner);
            var loc = UI.RequiemUi.Tip("BURDEN_BLOCKED.title");
            loc.Add("Card", card.Title);
            loc.Add("Weight", Loadout.TotalWeight(Owner));
            loc.Add("Capacity", Loadout.Capacity(Owner));
            loc.Add("Excess", overload.Excess);
            UI.NRequiemToast.Show(loc.GetFormattedText());
        }
        return Task.CompletedTask;
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room.RoomType == RoomType.RestSite && FeatherBonus > 0) FeatherBonus = 0;
        Refresh();
        if (room is not CombatRoom)
        {
            await CardAcquisition.CompletePendingReplacements(Owner);
            await OfferPendingCards();
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        Refresh();
        return Task.CompletedTask;
    }

    public void Refresh()
    {
        if (Owner == null) return;
        var weight = Loadout.TotalWeight(Owner);
        var capacity = Loadout.Capacity(Owner);
        Set("Weight", weight);
        Set("Capacity", capacity);
        Set("Excess", Math.Max(0, weight - capacity));
        Set("Stored", StoredCount);
        InvokeDisplayAmountChanged();
    }

    private void Set(string name, int value)
    {
        if (DynamicVars.TryGetValue(name, out var v)) v.BaseValue = value;
    }
}
