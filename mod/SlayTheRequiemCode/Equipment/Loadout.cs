using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Equipment;

/// <summary>Надетое снаряжение героя и его сводные бонусы.</summary>
public static class Loadout
{
    public static IEnumerable<EquipmentRelic> All(Player? player) =>
        player?.Relics.OfType<EquipmentRelic>() ?? Enumerable.Empty<EquipmentRelic>();

    public static IReadOnlyList<EquipmentRelic> Equipped(Player? player) => All(player).Where(r => r.IsEquipped).ToList();

    public static LoadoutBonuses Bonuses(Player? player) =>
        EquipmentRules.Evaluate(Equipped(player).Select(r => r.Describe()).ToList());

    public static int SetPieces(Player? player, string set) => Equipped(player).Count(r => r.Set == set);

    public static EquipmentRelic? InSlot(Player? player, EquipSlot slot) => Equipped(player).FirstOrDefault(r => r.Slot == slot);

    /// <summary>Надеть предмет, сняв то, что ему мешает.</summary>
    public static void Equip(Player player, EquipmentRelic item)
    {
        var others = Equipped(player).Where(r => r != item).ToList();
        var displaced = EquipmentRules.DisplacedIndices(others.Select(r => r.Describe()).ToArray(), item.Describe());
        foreach (var index in displaced) others[index].IsStowed = true;
        item.IsStowed = false;
        PaperDoll.Refresh(player); // в бою («Сменить хват») — сразу на модели
    }

    /// <summary>Снять в рюкзак.</summary>
    public static void Stow(EquipmentRelic item)
    {
        item.IsStowed = true;
        if (item.IsMutable) PaperDoll.Refresh(item.Owner);
    }

    /// <summary>
    /// Вес колоды и всех реликвий: снаряжение — по весу предмета, ванильные реликвии — по таблице переработки
    /// (07 §7, <c>data/vanilla_relics.csv</c>, 0–3), прочие реликвии — 1, стартовые — 0.
    /// </summary>
    public static int TotalWeight(Player? player)
    {
        if (player == null) return 0;
        var cards = player.Deck.Cards.Sum(CardWeight);
        var waivers = player.Relics.OfType<IEquipmentWeightWaiver>().ToList();
        var relics = player.Relics.Sum(r => RelicWeight(r, waivers));
        return cards + relics;
    }

    /// <summary>Вес реликвии для игрока — и своей, и ещё не купленной (превью в лавке, M6 п. 5).</summary>
    public static int RelicWeight(Player? player, RelicModel relic) =>
        RelicWeight(relic, player?.Relics.OfType<IEquipmentWeightWaiver>().ToList() ?? new List<IEquipmentWeightWaiver>());

    private static int RelicWeight(RelicModel r, IReadOnlyList<IEquipmentWeightWaiver> waivers) => r switch
    {
        EquipmentRelic e => waivers.Any(w => w.Waives(e)) ? 0 : e.Weight,
        IWeighted w => w.Weight,
        IWeightless => 0,
        _ when VanillaRelicRework.EntryOf(r) is { } vanilla => vanilla.Weight,
        _ when r.Rarity is MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.Starter => 0,
        _ => 1,
    };

    public static int CardWeight(CardModel card) => card is Cards.EncounterCounterCard ? 0
        : TransformationRules.CardWeightAfter(
            BurdenRules.DefaultCardWeight(card.Rarity.ToString(), card.Type == CardType.Curse, card.Type == CardType.Status),
            Progression.RequiemCardEnchantment.Has(card, CardEnchant.Lightened));

    public static int Capacity(Player? player) => HeroStatRules.BurdenCapacity(player);

    public static BurdenRules.Overload Overload(Player? player) => BurdenRules.Evaluate(TotalWeight(player), Capacity(player));
}

/// <summary>Реликвия без веса Груза: дары и благословения (не вещи).</summary>
public interface IWeightless { }

/// <summary>Реликвия, снимающая вес с части снаряжения («Браконьер»: луки и арбалеты не весят).</summary>
public interface IEquipmentWeightWaiver
{
    bool Waives(EquipmentRelic item);
}

/// <summary>Не-снаряжение со своим весом Груза (Филактерия — 3).</summary>
public interface IWeighted
{
    int Weight { get; }
}

/// <summary>Штрафы Груза для ресурсов и руки.</summary>
public static class BurdenPenalty
{
    public static int StaminaRegenPenalty(Player? player) =>
        player == null || !RequiemMode.IsInSystem(player) ? 0 : Loadout.Overload(player).StaminaRegenPenalty;

    public static int OpeningHandPenalty(Player? player) =>
        player == null || !RequiemMode.IsInSystem(player) ? 0 : Loadout.Overload(player).OpeningHandPenalty;
}
