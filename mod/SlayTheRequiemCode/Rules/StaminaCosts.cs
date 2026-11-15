using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>Цена атак в ◆: ручная разметка ванильных атак + собственные карты мода.</summary>
public static class StaminaCosts
{
    public static int Count => RequiemData.Attacks?.Count ?? 0;

    /// <summary>
    /// Цена атаки в ◆ до модификаторов. X-атаки платят по 1 ◆ за каждую единицу X (= потраченная энергия).
    /// Неразмеченные атаки (другие моды) — 1 ◆.
    /// </summary>
    public static int BaseCostFor(CardModel card, CardPlay? play = null)
    {
        if (card is IRequiemCard custom) return Math.Max(0, custom.StaminaCost);
        if (RequiemData.Attacks != null && RequiemData.Attacks.TryGet(card.GetType().Name, out var entry))
            return entry.IsX ? Math.Max(0, play?.Resources.EnergyValue ?? 0) : entry.Stamina;
        return 1;
    }

    /// <summary>Итоговая цена с учётом сил владельца («Сменить хват» и т.п.).</summary>
    public static int CostFor(CardModel card, CardPlay? play = null)
    {
        var cost = BaseCostFor(card, play);
        // Чары «Дыхательная» (07 §6.2): −1 ◆.
        cost = TransformationRules.StaminaAfter(cost, Progression.RequiemCardEnchantment.Has(card, CardEnchant.Breathing));
        // Канонические карты (библиотека, превью наград) владельца не имеют: CardModel.Owner на них бросает исключение.
        var player = OwnerOf(card);
        if (Equipment.EquipmentCombatRules.IsWeaponAttack(card))
            cost = EquipmentRules.WeaponAttackCost(cost, Equipment.Loadout.Bonuses(player));
        var owner = player?.Creature;
        if (owner != null && owner.HasPower<GripPower>()) cost = 0;
        if (player != null)
            foreach (var modifier in player.Relics.OfType<IAttackStaminaModifier>())
                cost = Math.Max(0, modifier.ModifyAttackStamina(card, cost));
        return cost;
    }

    /// <summary>
    /// Атаки, которые не платят ◆ и не слабеют от Устал/Изнурён (card spec §3.4 п. 2, 04 «Кличи», 08):
    /// заклинания (<see cref="Spells.SpellCard"/>), магические атаки мода с ценой в ✦ и без объявленной ◆ (Кара, Освящение,
    /// Призменная стрела, Магическая буря с ценой X✦ — «атака платит либо мышцами, либо магией») и Кличи — их цена перезарядка, а не дыхание.
    /// Гибриды с обеими ценами (Клинок-заклинание Боевого мага: удар оружием + чары) платят и ◆, и ✦.
    /// Ванильные карты сюда не попадают: их цена — по разметке CSV.
    /// </summary>
    public static bool IsStaminaExempt(CardModel card) =>
        card is Spells.SpellCard || card is Cards.IShoutCard
        || (card is IRequiemCard custom && (card.CanonicalStarCost > 0 || card.HasStarCostX) && custom.StaminaCost == 0);

    /// <summary>Владелец карты или null для канонической модели (без исключения AssertMutable).</summary>
    public static MegaCrit.Sts2.Core.Entities.Players.Player? OwnerOf(CardModel card) => card.IsMutable ? card.Owner : null;

    /// <summary>X-атака: ◆ = потраченная энергия (на карте показываем «X»).</summary>
    public static bool IsX(CardModel card) =>
        card is not IRequiemCard && RequiemData.Attacks != null && RequiemData.Attacks.TryGet(card.GetType().Name, out var e) && e.IsX;

    public static DamageType DamageTypeFor(CardModel card)
    {
        if (card is IRequiemCard custom) return custom.DamageType;
        return RequiemData.Attacks != null && RequiemData.Attacks.TryGet(card.GetType().Name, out var entry)
            ? entry.DamageType
            : DamageType.Physical;
    }
}

/// <summary>Реликвия, меняющая цену атак в ◆ (предыстории, снаряжение).</summary>
public interface IAttackStaminaModifier
{
    int ModifyAttackStamina(CardModel card, int cost);
}

/// <summary>Карта мода: своя цена ◆ и тип урона.</summary>
public interface IRequiemCard
{
    int StaminaCost { get; }
    DamageType DamageType { get; }
}
