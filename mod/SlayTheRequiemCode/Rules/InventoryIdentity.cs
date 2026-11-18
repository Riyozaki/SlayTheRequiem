using System.Globalization;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Saved, per-owner instance numbers, NOT class IDs or mutable list indices. BaseLib writes them into SavedProperties
/// (including vanilla cards). No CopyOnClone: a newly generated duplicate must receive a new identity.
/// Assign missing legacy IDs in inventory order on every peer; never assign IDs to temporary menu/preview models.
/// </summary>
public static class InventoryIdentity
{
    private static readonly SavedSpireField<CardModel, int> Cards = new(() => 0, "SlayTheRequiem_Instance");
    private static readonly SavedSpireField<RelicModel, int> Relics = new(() => 0, "SlayTheRequiem_Instance");
    private static readonly SavedSpireField<PotionModel, int> Potions = new(() => 0, "SlayTheRequiem_Instance");

    // Must run before BaseLib builds its save-property/net-ID cache, even in a session without a run.
    public static void Initialize() { _ = Cards; _ = Relics; _ = Potions; }

    public static void Ensure(Player player)
    {
        if (TravelPack.Of(player) is not { } pack) return;
        var maximum = player.Deck.Cards.Select(c => Cards[c])
            .Concat(player.Relics.Select(r => Relics[r]))
            .Concat(player.PotionSlots.OfType<PotionModel>().Select(p => Potions[p])).DefaultIfEmpty(0).Max();
        maximum = Math.Max(maximum, pack.StoredCards.Concat(pack.PendingCards)
            .Select(s => s.Props?.ints?.FirstOrDefault(p => p.name == Cards.Name).value ?? 0).DefaultIfEmpty(0).Max());
        maximum = Math.Max(maximum, pack.StoredCards.Concat(pack.PendingCards).Select(s => Sequence(CardAcquisition.ClaimKey(s))).DefaultIfEmpty(0).Max());
        maximum = Math.Max(maximum, player.Deck.Cards.Select(c => Sequence(CardAcquisition.ClaimKey(c))).DefaultIfEmpty(0).Max());
        if (StoredItemCodec.TryRead(pack.StoredItemData, pack.StoredItems, out var stored, out _))
            maximum = Math.Max(maximum, stored.Select(s => Sequence(s.Key)).DefaultIfEmpty(0).Max());
        pack.InstanceSequence = Math.Max(pack.InstanceSequence, maximum);
        var seen = new HashSet<int>();
        int Unique(int id)
        {
            if (id > 0 && seen.Add(id)) return id;
            do { pack.InstanceSequence = checked(pack.InstanceSequence + 1); } while (!seen.Add(pack.InstanceSequence));
            return pack.InstanceSequence;
        }
        foreach (var c in player.Deck.Cards) Cards[c] = Unique(Cards[c]);
        foreach (var r in player.Relics) Relics[r] = Unique(Relics[r]);
        foreach (var p in player.PotionSlots.OfType<PotionModel>()) Potions[p] = Unique(Potions[p]);
        foreach (var snapshot in pack.StoredCards)
        {
            // Legacy chests did not store instance IDs. Stamp the SAVED source before the first restore/Add:
            // a save from inside CardAdded must link its live destination to this exact source. Do not recreate
            // the card to migrate metadata (that would discard unknown/unavailable-model SavedProperties).
            var matches = snapshot.Props?.ints?.Where(p => p.name == Cards.Name).ToList();
            if (matches is { Count: > 1 } || matches is { Count: 1 } && matches[0].value != 0) continue;
            snapshot.Props ??= new SavedProperties();
            snapshot.Props.ints ??= new();
            var id = checked(pack.InstanceSequence + 1);
            pack.InstanceSequence = id;
            if (matches is { Count: 1 }) snapshot.Props.ints.RemoveAll(p => p.name == Cards.Name);
            snapshot.Props.ints.Add(new(Cards.Name, id));
        }
    }

    /// <summary>Only a legacy class-name record may be migrated this way; persist its new payload before attach.</summary>
    internal static string NewStoredRelicKey(Player player, RelicModel relic)
    {
        Ensure(player);
        if (!relic.IsMutable || player.Relics.Contains(relic) || Relics[relic] != 0)
            throw new InvalidOperationException("Expected an unattached legacy chest relic without an instance ID");
        var pack = TravelPack.Of(player) ?? throw new InvalidOperationException("Missing durable chest store");
        pack.InstanceSequence = checked(pack.InstanceSequence + 1);
        Relics[relic] = pack.InstanceSequence;
        return Key("r:", pack.InstanceSequence);
    }

    internal static string NewClaimKey(Player player)
    {
        Ensure(player);
        var pack = TravelPack.Of(player) ?? throw new InvalidOperationException("Missing durable card claim store");
        pack.InstanceSequence = checked(pack.InstanceSequence + 1);
        return "q:" + pack.InstanceSequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int Sequence(string key) => key.Length > 2 && key[1] == ':' && key[0] is 'c' or 'r' or 'p' or 'q'
        && int.TryParse(key.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? Math.Max(0, number) : 0;

    private static string Key(string prefix, int id) => prefix + id.ToString(CultureInfo.InvariantCulture);
    internal static string CardKey(SerializableCard card) => SavedKey(card.Props, Cards.Name, "c:");

    internal static string RelicKey(SerializableRelic relic) => SavedKey(relic.Props, Relics.Name, "r:");

    private static string SavedKey(SavedProperties? props, string name, string prefix)
    {
        var entries = props?.ints?.Where(p => p.name == name).ToList();
        // FirstOrDefault would silently authorize one side of corrupt duplicate metadata. Keep those sources
        // opaque and let the caller refuse the transfer; never repair an ambiguous ID by picking a value.
        return entries is { Count: 1 } && entries[0].value > 0 ? Key(prefix, entries[0].value) : "";
    }

    public static string CardKey(CardModel card) { Ensure(card.Owner); return Key("c:", Cards[card]); }
    public static string RelicKey(RelicModel relic) { Ensure(relic.Owner); return Key("r:", Relics[relic]); }
    public static string PotionKey(PotionModel potion) { Ensure(potion.Owner); return Key("p:", Potions[potion]); }
    public static CardModel? FindCard(Player player, string key) { Ensure(player); return player.Deck.Cards.FirstOrDefault(c => Key("c:", Cards[c]) == key); }
    public static RelicModel? FindRelic(Player player, string key) { Ensure(player); return player.Relics.FirstOrDefault(r => Key("r:", Relics[r]) == key); }
    public static PotionModel? FindPotion(Player player, string key) { Ensure(player); return player.PotionSlots.OfType<PotionModel>().FirstOrDefault(p => Key("p:", Potions[p]) == key); }
}
