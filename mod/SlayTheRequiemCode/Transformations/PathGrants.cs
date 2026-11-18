using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Transformations;

/// <summary>Resumable companion delivery, never a second Path/Element/Reborn AfterObtained callback.</summary>
internal static class PathGrants
{
    private static string Json(SerializableRelic snapshot) => JsonSerializer.Serialize(snapshot, JsonSerializationUtility.GetTypeInfo<SerializableRelic>());

    internal static void Prepare(PathEssence essence, Player owner)
    {
        if (essence.SignGrant.Length > 0 || owner.Relics.Contains(essence)) throw new InvalidOperationException("Path issuer already prepared/attached");
        var key = "";
        var payload = "";
        if (Paths.SignType(essence.Path) is { } type)
        {
            var sign = Forms.Create(type);
            sign.Owner = owner;
            sign.FloorAddedToDeck = owner.RunState.TotalFloor; // native Obtain writes this after insertion
            key = InventoryIdentity.NewUnattachedRelicKey(owner, sign);
            payload = Json(sign.ToSerializable());
        }
        essence.SignGrant = RelicGrantState.Prepare(key, payload).Encode();
    }

    internal static async Task<OperationResult> CompleteSign(PathEssence essence, OperationGate.Lease? held = null)
    {
        var owner = essence.Owner;
        using var lease = await InventoryGate.For(owner).Enter(held);
        if (lease == null) return new(OperationStatus.Busy, "path-sign", "Inventory recovery required");
        if (!owner.Relics.Contains(essence)) return OperationResult.Reject("path-sign", "Issuer no longer belongs to this player");
        var source = essence.SignGrant;
        RelicGrantState? grant;
        try { grant = RelicGrantState.Read(source); }
        catch (Exception e)
        {
            InventoryGate.Block(owner, "Invalid saved Path companion entitlement; original retained: " + e);
            return new(OperationStatus.PartialCommit, "path-sign", e.Message);
        }
        if (grant == null)
        {
            // A legacy sign might have been deliberately lost/stored. Absence of a class is not proof that its
            // original grant never committed, and cannot authorize an extra reward on every Continue.
            MainFile.Logger.Warn("[PathGrant] Legacy issuer has no companion journal; no item guessed/reissued. Keep its original backup/matching build.");
            return OperationResult.Reject("path-sign", "Legacy issuer has no proof of an undelivered sign");
        }
        if (grant.Phase == RelicGrantState.Completed) return new(OperationStatus.Duplicate, grant.Key, "Companion already delivered; no replay after loss/storage");
        if (grant.Phase != RelicGrantState.Ready)
        {
            InventoryGate.Block(owner, "Path issuer callback was interrupted. Restore its pre-transition checkpoint; do not replay Element/Reborn/start effects.");
            return new(OperationStatus.PartialCommit, grant.Key, "Issuer callback is not confirmed");
        }
        using var mutation = InventoryGate.Track(owner, lease, "path-sign:" + grant.Key);
        try
        {
            void Finish()
            {
                if (essence.SignGrant != source) throw new InvalidOperationException("Path companion source changed in its obtain callback; retained for recovery");
                essence.SignGrant = grant.Complete().Encode();
            }
            if (Paths.SignType(essence.Path) is not { } signType)
            {
                if (grant.Key.Length > 0 || grant.Payload.Length > 0) throw new FormatException("No-sign path has an unexpected saved companion");
                Finish(); mutation.Complete(); return new(OperationStatus.Committed, "path-sign:none");
            }
            var snapshot = JsonSerializer.Deserialize(grant.Payload, JsonSerializationUtility.GetTypeInfo<SerializableRelic>())
                ?? throw new FormatException("Missing saved Path sign payload");
            if (snapshot.Id != ModelDb.GetId(signType) || InventoryIdentity.RelicKey(snapshot) != grant.Key)
                throw new FormatException("Path companion type/instance does not match its issuer; opaque source retained");
            var pack = TravelPack.Of(owner) ?? throw new InvalidOperationException("Path companion has no durable inventory store");
            if (!StoredItemCodec.TryRead(pack.StoredItemData, pack.StoredItems, out var stored, out var failure)) throw new FormatException(failure);
            var live = InventoryIdentity.FindRelic(owner, grant.Key);
            var inChest = stored.SingleOrDefault(i => i.Key == grant.Key);
            if (live != null || inChest != null)
            {
                if (live != null && inChest != null || !SavedPayload.Preserves(grant.Payload, live != null ? Json(live.ToSerializable()) : inChest!.Payload))
                    throw new InvalidOperationException("Path companion identity has conflicting live/chest payloads; source retained");
                Finish(); mutation.Complete(); return new(OperationStatus.Committed, grant.Key, "Reconciled exact companion; no second Obtain");
            }
            if (owner.Relics.OfType<PathSign>().Any(s => s.Path == essence.Path))
                throw new InvalidOperationException("A different companion instance already exists; do not guess which grant it represents");
            var sign = RelicModel.FromSerializable(snapshot);
            sign.Owner = owner;
            if (!SavedPayload.Preserves(grant.Payload, Json(sign.ToSerializable()))) throw new FormatException("Path companion cannot round-trip all saved properties");
            var result = await TransferProtocol.Move(grant.Key,
                async () => { await RelicCmd.Obtain(sign, owner); return true; },
                () => owner.Relics.Contains(sign), Finish);
            if (result.Status == OperationStatus.PartialCommit) InventoryGate.Block(owner, "Path companion committed with an incomplete callback; restore checkpoint: " + result.Detail);
            // A pre-insertion failure retains the exact ready source for a later room/retry. A known commit
            // permanently consumes it, including a later callback error. Never rerun the essence's one-shot code.
            MainFile.Logger.Info($"[PathGrant] {result.Status} key={grant.Key} {result.Detail}");
            mutation.Complete();
            return result;
        }
        catch (Exception e)
        {
            InventoryGate.Block(owner, "Path companion delivery cannot be reconciled; source retained, restore checkpoint: " + e);
            mutation.Complete();
            return new(OperationStatus.PartialCommit, grant.Key, e.Message);
        }
    }
}
