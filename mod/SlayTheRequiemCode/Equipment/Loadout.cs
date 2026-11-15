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
        var displaced = EquipmentRules.Displaced(others.Select(r => r.Describe()).ToList(), item.Describe());
        foreach (var old in others.Where(r => displaced.Any(d => d.Key == r.GetType().Name && d.Slot == r.Slot)))
            old.IsStowed = true;
        item.IsStowed = false;
    }

    /// <summary>Снять в рюкзак.</summary>
    public static void Stow(EquipmentRelic item) => item.IsStowed = true;

    /// <summary>Вес колоды и всех реликвий (снаряжение — по весу предмета, обычные реликвии — 1, стартовые — 0).</summary>
    public static int TotalWeight(Player? player)
    {
        if (player == null) return 0;
        var cards = player.Deck.Cards.Sum(CardWeight);
        var relics = player.Relics.Sum(r => r switch
        {
            EquipmentRelic e => e.Weight,
            IWeightless => 0,
            _ when r.Rarity is MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.Starter => 0,
            _ => 1,
        });
        return cards + relics;
    }

    public static int CardWeight(CardModel card) =>
        BurdenRules.DefaultCardWeight(card.Rarity.ToString(), card.Type == CardType.Curse, card.Type == CardType.Status);

    public static int Capacity(Player? player) => HeroStatRules.BurdenCapacity(player);

    public static BurdenRules.Overload Overload(Player? player) => BurdenRules.Evaluate(TotalWeight(player), Capacity(player));
}

/// <summary>Реликвия без веса Груза: дары и благословения (не вещи).</summary>
public interface IWeightless { }

/// <summary>Штрафы Груза для ресурсов и руки.</summary>
public static class BurdenPenalty
{
    public static int StaminaRegenPenalty(Player? player) =>
        player == null || !RequiemMode.IsInSystem(player) ? 0 : Loadout.Overload(player).StaminaRegenPenalty;

    public static int OpeningHandPenalty(Player? player) =>
        player == null || !RequiemMode.IsInSystem(player) ? 0 : Loadout.Overload(player).OpeningHandPenalty;
}
