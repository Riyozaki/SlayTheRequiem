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
        if (Equipment.EquipmentCombatRules.IsWeaponAttack(card))
            cost = EquipmentRules.WeaponAttackCost(cost, Equipment.Loadout.Bonuses(card.Owner));
        var owner = card.Owner?.Creature;
        if (owner != null && owner.HasPower<GripPower>()) cost = 0;
        if (card.Owner != null)
            foreach (var modifier in card.Owner.Relics.OfType<IAttackStaminaModifier>())
                cost = Math.Max(0, modifier.ModifyAttackStamina(card, cost));
        return cost;
    }

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
