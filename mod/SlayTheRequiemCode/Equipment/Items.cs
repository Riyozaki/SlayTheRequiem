using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Resources;

namespace SlayTheRequiem.SlayTheRequiemCode.Equipment;

// Первый набор снаряжения (docs/design/07 §3.2, §4.2). Ступень I → Common, II → Uncommon, III+ → Rare.
// В пул магазинов/сундуков они попадают через SharedRelicPool; магазины Реквиема фильтруют по типу (см. 14).

// ───────────── оружие ─────────────

[Pool(typeof(SharedRelicPool))]
public sealed class RustyCleaver : WeaponRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public override WeaponKind Weapon => WeaponKind.Sword;
}

[Pool(typeof(SharedRelicPool))]
public sealed class SmugglerDagger : WeaponRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public override WeaponKind Weapon => WeaponKind.Dagger;

    /// <summary>Подлый удар возвращает 1 ◆.</summary>
    protected override Task OnWeaponHit(PlayerChoiceContext ctx, Creature target, CardModel card)
    {
        if (Rules.SneakRules.IsSneak(Owner.Creature, target))
            RequiemResource.Of<StaminaResource>(Owner)?.ModifyAmount(1);
        return Task.CompletedTask;
    }
}

[Pool(typeof(SharedRelicPool))]
public sealed class WoodcutterAxe : WeaponRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public override WeaponKind Weapon => WeaponKind.Axe;
    /// <summary>Инструмент: продаётся дороже.</summary>
    public override int MerchantCost => base.MerchantCost + 30;
}

[Pool(typeof(SharedRelicPool))]
public sealed class IronMace : WeaponRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public override WeaponKind Weapon => WeaponKind.Mace;
}

[Pool(typeof(SharedRelicPool))]
public sealed class SteelWarhammer : WeaponRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override int Tier => 2;
    public override WeaponKind Weapon => WeaponKind.TwoHanded;
    public override WeaponKind PropertyKind => WeaponKind.Mace;
}

[Pool(typeof(SharedRelicPool))]
public sealed class SilverSaber : WeaponRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override int Tier => 3;
    public override WeaponKind Weapon => WeaponKind.Sword;

    /// <summary>По нежити и Призрачным: +2.</summary>
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, MegaCrit.Sts2.Core.ValueProps.ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        var bonus = base.ModifyDamageAdditive(target, amount, props, dealer, cardSource);
        if (target?.Monster != null && IsMyWeaponAttack(dealer, props, cardSource)
            && Rules.RequiemData.MonsterEntry(target.Monster.GetType().Name).Faction is "undead" or "spirit")
            bonus += 2;
        return bonus;
    }
}

[Pool(typeof(SharedRelicPool))]
public sealed class EverfrostHammer : WeaponRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override int Tier => 5;
    public override WeaponKind Weapon => WeaponKind.Mace;

    protected override Task OnWeaponHit(PlayerChoiceContext ctx, Creature target, CardModel card) =>
        PowerCmd.Apply<ChillPower>(ctx, target, 2, Owner.Creature, card);
}

// ───────────── левая рука ─────────────

/// <summary>Щит: Блок +1 от карт; Стойкость +1.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class OakShield : EquipmentRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public override EquipSlot Slot => EquipSlot.OffHand;
    public override WeaponKind Weapon => WeaponKind.Shield;
    public override int Weight => 3;

    public override decimal ModifyBlockAdditive(Creature target, decimal block, MegaCrit.Sts2.Core.ValueProps.ValueProp props, CardModel? cardSource, MegaCrit.Sts2.Core.Entities.Cards.CardPlay? cardPlay) =>
        IsEquipped && target == Owner.Creature && cardSource != null ? 1m : 0m;
}

// ───────────── броня: «Железо ополченца» (тяж. I) ─────────────

public abstract class MilitiaIronPiece : ArmorRelic
{
    public const string SetName = "MilitiaIron";
    public override RelicRarity Rarity => RelicRarity.Common;
    public override ArmorClass Armor => ArmorClass.Heavy;
    public override string? Set => SetName;

    /// <summary>Сет-бонус считает только «ведущая» часть (первая надетая), чтобы не дублировать.</summary>
    protected bool IsSetLeader => Loadout.Equipped(Owner).OfType<MilitiaIronPiece>().FirstOrDefault() == this;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!IsEquipped || !IsSetLeader || side != CombatSide.Player || combatState.RoundNumber != 1) return;
        var pieces = Loadout.SetPieces(Owner, SetName);
        var ctx = new ThrowingPlayerChoiceContext();
        if (pieces >= 2) await PowerCmd.Apply<PlatingPower>(ctx, Owner.Creature, 1, Owner.Creature, null, silent: true);
        if (pieces >= 4) await CreatureCmd.GainBlock(Owner.Creature, 5, MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered, null);
    }
}

[Pool(typeof(SharedRelicPool))] public sealed class MilitiaHelm : MilitiaIronPiece { public override EquipSlot Slot => EquipSlot.Head; }
[Pool(typeof(SharedRelicPool))] public sealed class MilitiaCuirass : MilitiaIronPiece { public override EquipSlot Slot => EquipSlot.Body; }
[Pool(typeof(SharedRelicPool))] public sealed class MilitiaGauntlets : MilitiaIronPiece { public override EquipSlot Slot => EquipSlot.Hands; }
[Pool(typeof(SharedRelicPool))] public sealed class MilitiaGreaves : MilitiaIronPiece { public override EquipSlot Slot => EquipSlot.Feet; }

// ───────────── броня: «Шкуры загонщика» (лёг. I) ─────────────

public abstract class StalkerHidePiece : ArmorRelic
{
    public const string SetName = "StalkerHides";
    public override RelicRarity Rarity => RelicRarity.Common;
    public override ArmorClass Armor => ArmorClass.Light;
    public override string? Set => SetName;

    private bool _firstAttackDone;

    public override Task BeforeCombatStart()
    {
        _firstAttackDone = false;
        return Task.CompletedTask;
    }

    private bool IsSetLeader => Loadout.Equipped(Owner).OfType<StalkerHidePiece>().FirstOrDefault() == this;

    /// <summary>4 части: первая атака боя +3.</summary>
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, MegaCrit.Sts2.Core.ValueProps.ValueProp props, Creature? dealer, CardModel? cardSource) =>
        IsEquipped && IsSetLeader && !_firstAttackDone && dealer == Owner.Creature && props.IsPoweredAttack()
        && Loadout.SetPieces(Owner, SetName) >= 4 ? 3m : 0m;

    public override Task AfterAttack(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Commands.Builders.AttackCommand command)
    {
        if (command.Attacker == Owner.Creature) _firstAttackDone = true;
        return Task.CompletedTask;
    }
}

[Pool(typeof(SharedRelicPool))] public sealed class StalkerHood : StalkerHidePiece { public override EquipSlot Slot => EquipSlot.Head; }
[Pool(typeof(SharedRelicPool))] public sealed class StalkerJerkin : StalkerHidePiece { public override EquipSlot Slot => EquipSlot.Body; }
[Pool(typeof(SharedRelicPool))] public sealed class StalkerGloves : StalkerHidePiece { public override EquipSlot Slot => EquipSlot.Hands; }
[Pool(typeof(SharedRelicPool))] public sealed class StalkerBoots : StalkerHidePiece { public override EquipSlot Slot => EquipSlot.Feet; }

// ───────────── одеяния ─────────────

/// <summary>Одеяние Звёздной Академии (тело): Покров 4.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class StarAcademyRobe : ArmorRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override int Tier => 2;
    public override EquipSlot Slot => EquipSlot.Body;
    public override ArmorClass Armor => ArmorClass.Robe;
    public override int Ward => 4;
}

// ───────────── плащи, амулеты, кольца ─────────────

/// <summary>Плащ Первой Метели: сопротивление холоду I.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class FirstBlizzardCloak : EquipmentRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override EquipSlot Slot => EquipSlot.Cloak;
    public override int Tier => 3;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!IsEquipped || side != CombatSide.Player || combatState.RoundNumber != 1) return;
        await PowerCmd.Apply<FrostResistancePower>(new ThrowingPlayerChoiceContext(), Owner.Creature, 1, Owner.Creature, null, silent: true);
    }
}

/// <summary>Кольцо выносливости: +2 ◆ в начале боя (сверх пула не поднимает).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RingOfStamina : EquipmentRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public override EquipSlot Slot => EquipSlot.Ring;
    public override int Weight => 0;

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (IsEquipped && side == CombatSide.Player && combatState.RoundNumber == 1)
            RequiemResource.Of<StaminaResource>(Owner)?.ModifyAmount(2);
        return Task.CompletedTask;
    }
}

/// <summary>Кольцо магики: +1 ✦ в начале каждого хода.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RingOfMagicka : EquipmentRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public override EquipSlot Slot => EquipSlot.Ring;
    public override int Weight => 0;

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState) =>
        IsEquipped && side == CombatSide.Player && combatState.RoundNumber > 1 ? Rules.Mana.Gain(Owner, 1) : Task.CompletedTask;
}

// ───────────── снаряжение Подлых ударов (этап 3, docs/design/07 §3.2–4.3) ─────────────

/// <summary>Чешуйчатый кинжал (III): Подлый; Подлые удары возвращают 1 ✦.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ScaledDagger : WeaponRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override int Tier => 3;
    public override WeaponKind Weapon => WeaponKind.Dagger;

    protected override Task OnWeaponHit(PlayerChoiceContext ctx, Creature target, CardModel card) =>
        Rules.SneakRules.IsSneak(Owner.Creature, target) ? Rules.Mana.Gain(Owner, 1) : Task.CompletedTask;
}

/// <summary>
/// Клинок Сумеречной Вдовы (артефакт Князя Бездны — праведность не выше «Запятнанного», кинжал +3): Подлый ×2 вместо ×1.5; убийство Оружейной атакой — Невидимость
/// до конца хода (переживает саму атаку — <see cref="InvisibilityPower.GrantUntilTurnEnd"/>).
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class TwilightWidowBlade : WeaponRelic, IAbyssalItem
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override int Tier => 5;
    public override WeaponKind Weapon => WeaponKind.Dagger;
    protected override decimal SneakMultiplier => 2m;

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Commands.Builders.AttackCommand command)
    {
        await base.AfterAttack(choiceContext, command);
        if (!IsEquipped || command.Attacker != Owner.Creature || command.ModelSource is not CardModel card
            || !EquipmentCombatRules.IsWeaponAttack(card)) return;
        if (command.Results.SelectMany(h => h).Any(r => r.WasTargetKilled))
            await InvisibilityPower.GrantUntilTurnEnd(choiceContext, Owner.Creature);
    }
}

// ───────────── броня: «Кольчуга контрабандиста» (лёг. II) ─────────────

/// <summary>2 части: +1 заряд Уворота; 4 части: после уворота следующая атака — Подлая.</summary>
public abstract class SmugglerMailPiece : ArmorRelic, IDodgeListener
{
    public const string SetName = "SmugglerMail";
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override int Tier => 2;
    public override ArmorClass Armor => ArmorClass.Light;
    public override string? Set => SetName;

    private bool IsSetLeader => Loadout.Equipped(Owner).OfType<SmugglerMailPiece>().FirstOrDefault() == this;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!IsEquipped || !IsSetLeader || side != CombatSide.Player || combatState.RoundNumber != 1) return;
        if (Loadout.SetPieces(Owner, SetName) >= 2)
            await PowerCmd.Apply<DodgePower>(new ThrowingPlayerChoiceContext(), Owner.Creature, 1, Owner.Creature, null, silent: true);
    }

    public async Task AfterDodged(PlayerChoiceContext ctx, Creature? attacker)
    {
        if (!IsEquipped || !IsSetLeader || Loadout.SetPieces(Owner, SetName) < 4) return;
        if (!Owner.Creature.HasPower<SneakNextPower>())
            await PowerCmd.Apply<SneakNextPower>(ctx, Owner.Creature, 1, Owner.Creature, null);
    }
}

[Pool(typeof(SharedRelicPool))] public sealed class SmugglerCoif : SmugglerMailPiece { public override EquipSlot Slot => EquipSlot.Head; }
[Pool(typeof(SharedRelicPool))] public sealed class SmugglerHauberk : SmugglerMailPiece { public override EquipSlot Slot => EquipSlot.Body; }
[Pool(typeof(SharedRelicPool))] public sealed class SmugglerBracers : SmugglerMailPiece { public override EquipSlot Slot => EquipSlot.Hands; }
[Pool(typeof(SharedRelicPool))] public sealed class SmugglerBoots : SmugglerMailPiece { public override EquipSlot Slot => EquipSlot.Feet; }

/// <summary>Сапоги Безмолвия (уник., ноги, лёгкие): атаки не снимают Невидимость (из Невидимости все атаки Подлые).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class BootsOfSilence : ArmorRelic, IInvisibilityKeeper
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override int Tier => 3;
    public override ArmorClass Armor => ArmorClass.Light;
    public override EquipSlot Slot => EquipSlot.Feet;
    public bool KeepsInvisibility => IsEquipped;
}
