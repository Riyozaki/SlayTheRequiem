using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Progression;

/// <summary>Сводные чары надетого снаряжения.</summary>
public readonly record struct EnchantTotals(
    int MaxHp, int StaminaPool, int ManaPool, int Burden, int WeaponMight, int FireRes, int FrostRes, int ShockRes);

/// <summary>
/// Зачарование снаряжения (docs/design/07 §6.1): чары хранятся на предмете (<see cref="EquipmentRelic.Enchants"/>)
/// и действуют, пока он надет. Числовые чары складываются в набор Летописи героя; оружейные — срабатывают на
/// «Оружейных» атаках и тратят заряды (восстанавливаются новым камнем душ того же вида чар).
/// </summary>
public static class Enchanting
{
    public static EnchantTotals Totals(Player? player)
    {
        if (player == null) return default;
        int hp = 0, st = 0, mana = 0, burden = 0, might = 0, fire = 0, frost = 0, shock = 0;
        foreach (var item in Loadout.Equipped(player))
        foreach (var slot in item.EnchantSlots)
        {
            var v = EnchantRules.Value(slot.Enchant, slot.Power);
            switch (slot.Enchant)
            {
                case Enchant.Health: hp += v; break;
                case Enchant.Stamina: st += v; break;
                case Enchant.Magic: mana += v; break;
                case Enchant.Burden: burden += v; break;
                case Enchant.WeaponMight: might += v; break;
                case Enchant.FireResistance: fire = Math.Max(fire, v); break;
                case Enchant.FrostResistance: frost = Math.Max(frost, v); break;
                case Enchant.ShockResistance: shock = Math.Max(shock, v); break;
            }
        }
        return new EnchantTotals(hp, st, mana, burden, might, fire, frost, shock);
    }

    /// <summary>Предметы, на которые можно наложить хоть одни чары.</summary>
    public static List<EquipmentRelic> Enchantable(Player player) =>
        Loadout.Equipped(player).Where(i => EnchantRules.All.Any(e => EnchantRules.Allowed(e, i.Slot))).ToList();

    public static List<Enchant> AllowedFor(EquipmentRelic item) =>
        EnchantRules.All.Where(e => EnchantRules.Allowed(e, item.Slot)).ToList();

    public static int SlotsFor(Player player) =>
        EnchantRules.SlotsPerItem(HeroRecord.Of(player)?.Has(Skill.CraftEnchanting) == true);

    public static void Apply(Player player, EquipmentRelic item, Enchant enchant, int power)
    {
        item.SetEnchants(EnchantRules.Apply(item.EnchantSlots, enchant, power, SlotsFor(player)));
        item.Flash();
    }

    /// <summary>Оружейные чары: Горение / Озноб / выжечь ✦ по каждой цели, −1 заряд за атаку.</summary>
    public static async Task OnWeaponHit(Player player, PlayerChoiceContext ctx, IReadOnlyList<Creature> targets, CardModel card)
    {
        var weapon = Loadout.InSlot(player, EquipSlot.MainHand);
        if (weapon == null || targets.Count == 0) return;
        var slots = weapon.EnchantSlots;
        var changed = false;
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (!EnchantRules.IsWeaponBrand(slot.Enchant) || slot.Charges <= 0) continue;
            var value = EnchantRules.Value(slot.Enchant, slot.Power);
            foreach (var target in targets)
            {
                if (!target.IsAlive) continue;
                switch (slot.Enchant)
                {
                    case Enchant.FireBrand:
                        await PowerCmd.Apply<BurningPower>(ctx, target, value, player.Creature, card);
                        break;
                    case Enchant.FrostBrand:
                        await PowerCmd.Apply<ChillPower>(ctx, target, value, player.Creature, card);
                        break;
                    default:
                        if (target.GetPower<EnemyManaPower>() is { } mana) await mana.Drain(value);
                        break;
                }
            }
            slots[i] = slot with { Charges = slot.Charges - 1 };
            changed = true;
        }
        if (changed) weapon.SetEnchants(slots);
    }
}
