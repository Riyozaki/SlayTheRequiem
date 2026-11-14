using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Relics;

using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Equipment;

/// <summary>
/// Снаряжение = реликвия со слотом, весом и ступенью (docs/design/07). Новый предмет в занятом слоте
/// надевается, а прежний уходит в рюкзак (<see cref="IsStowed"/>): эффект пропадает, вес остаётся.
/// </summary>
public abstract class EquipmentRelic : RequiemRelic
{
    public abstract EquipSlot Slot { get; }
    public virtual int Weight => 1;
    public virtual int Tier => 1;
    public virtual ArmorClass Armor => ArmorClass.None;
    public virtual WeaponKind Weapon => WeaponKind.None;
    public virtual string? Set => null;
    /// <summary>Покров N (одеяния).</summary>
    public virtual int Ward => 0;

    private bool _isStowed;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IsStowed
    {
        get => _isStowed;
        set
        {
            AssertMutable();
            _isStowed = value;
        }
    }

    public bool IsEquipped => !IsStowed;

    public override bool ShowCounter => true;
    /// <summary>На иконке показывается вес; 0 — снято (в рюкзаке).</summary>
    public override int DisplayAmount => IsStowed ? 0 : Weight;

    public EquipItem Describe() => new(GetType().Name, Slot, Weight, Tier, Armor, Weapon, Set, Ward);

    public override Task AfterObtained()
    {
        Loadout.Equip(Owner, this);
        return Task.CompletedTask;
    }

    public override int MerchantCost => base.MerchantCost + 15 * (Tier - 1);
}

/// <summary>
/// Оружие правой руки. Свойство типа (docs/design/07 §3.1) срабатывает на «Оружейных» атаках:
/// Подлый (кинжал), Острый (меч), Рубящий (топор), Дробящий (булава); двуручное — ×2 свойства + Шаткость 2.
/// </summary>
public abstract class WeaponRelic : EquipmentRelic
{
    public override EquipSlot Slot => EquipSlot.MainHand;
    public override int Weight => Weapon switch
    {
        WeaponKind.Dagger => 1,
        WeaponKind.Mace or WeaponKind.Spear or WeaponKind.Crossbow => 3,
        WeaponKind.TwoHanded => 4,
        _ => 2,
    };

    /// <summary>Для двуручного — какой одноручный тип он повторяет (молот = булава, секира = топор...).</summary>
    public virtual WeaponKind PropertyKind => Weapon;
    public int PropertyScale => Weapon == WeaponKind.TwoHanded ? 2 : 1;

    private int _weaponAttacksThisCombat;

    public override Task BeforeCombatStart()
    {
        _weaponAttacksThisCombat = 0;
        return Task.CompletedTask;
    }

    protected bool IsMyWeaponAttack(Creature? dealer, ValueProp props, CardModel? cardSource) =>
        IsEquipped && cardSource != null && dealer == Owner.Creature && props.IsPoweredAttack()
        && EquipmentCombatRules.IsWeaponAttack(cardSource);

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == null || !IsMyWeaponAttack(dealer, props, cardSource)) return 0m;
        // Рубящий: +2 (+4 на IV+) по цели с ♥ < 50 %.
        if (PropertyKind == WeaponKind.Axe && target.CurrentHp * 2 < target.MaxHp)
            return (Tier >= 4 ? 4 : 2) * PropertyScale;
        return 0m;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == null || !IsMyWeaponAttack(dealer, props, cardSource)) return 1m;
        switch (PropertyKind)
        {
            // Подлый: ×1.5 по цели без атакующего намерения.
            case WeaponKind.Dagger when SneakRules.IsSneak(Owner.Creature, target):
                return SneakMultiplier;
            // Острый: каждая 3-я Оружейная атака за бой — крит ×1.5 (счётчик растёт после атаки).
            case WeaponKind.Sword when (_weaponAttacksThisCombat + 1) % 3 == 0:
                return 1.5m;
            default:
                return 1m;
        }
    }

    protected virtual decimal SneakMultiplier => SneakRules.DaggerMultiplier;

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (!IsEquipped || command.Attacker != Owner.Creature || command.ModelSource is not CardModel card
            || !EquipmentCombatRules.IsWeaponAttack(card)) return;
        _weaponAttacksThisCombat++;

        foreach (var target in command.Results.SelectMany(h => h).Select(r => r.Receiver).Distinct())
        {
            if (target == null || !target.IsAlive) continue;
            switch (PropertyKind)
            {
                case WeaponKind.Dagger:
                    await PowerCmd.Apply<BleedPower>(choiceContext, target, 1, Owner.Creature, card);
                    break;
                case WeaponKind.Mace:
                    await PowerCmd.Apply<SunderPower>(choiceContext, target, (Tier >= 4 ? 2 : 1) * PropertyScale, Owner.Creature, card);
                    break;
            }
            if (Weapon == WeaponKind.TwoHanded)
                await PowerCmd.Apply<StaggerPower>(choiceContext, target, 2, Owner.Creature, card);
            await OnWeaponHit(choiceContext, target, card);
        }
    }

    /// <summary>Доп. свойство конкретного предмета (Озноб молота из вечного льда и т.п.).</summary>
    protected virtual Task OnWeaponHit(PlayerChoiceContext ctx, Creature target, CardModel card) => Task.CompletedTask;
}

/// <summary>Часть брони (голова/тело/руки/ноги).</summary>
public abstract class ArmorRelic : EquipmentRelic
{
    public override int Weight => Armor switch
    {
        ArmorClass.Heavy => Tier >= 4 ? 4 : 3,
        ArmorClass.Light => Tier >= 3 ? 1 : 2,
        _ => 0,
    };
}
