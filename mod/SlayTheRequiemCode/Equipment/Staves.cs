using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Equipment;

/// <summary>
/// Посох-фокус Чародейской лавки (docs/design/07 §3.4, 14 §2.3): левая рука, N зарядов заклинания. В начале каждого боя,
/// пока есть заряды, кладёт в руку своё заклинание за 0 ✦ (владение не проверяется, Истощение) и тратит заряд.
/// У костра (вход в лагерь) восстанавливает 2 заряда. Материал задаёт заклинание и число зарядов.
/// Это не «Посох стихии» Повелителя стихии (<see cref="Transformations.ElementStaff"/>, правая рука).
/// </summary>
public abstract class MageStaff : EquipmentRelic
{
    public const int CampRecharge = 2;

    public override EquipSlot Slot => EquipSlot.OffHand;
    public override WeaponKind Weapon => WeaponKind.Staff;
    public override int Weight => 2;
    public override RelicRarity Rarity => Tier switch { <= 1 => RelicRarity.Common, <= 3 => RelicRarity.Uncommon, _ => RelicRarity.Rare };

    /// <summary>Полный запас зарядов.</summary>
    public abstract int MaxCharges { get; }

    /// <summary>Каноническое заклинание посоха.</summary>
    public abstract SpellCard Spell { get; }

    /// <summary>Сколько карт заклинания за заряд (Бездонный посох — две: «×2 урона»).</summary>
    protected virtual int CopiesPerCharge => 1;

    private int _chargesUsed;

    /// <summary>Потрачено зарядов (сохраняется; 0 — посох полон).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ChargesUsed
    {
        get => _chargesUsed;
        set
        {
            AssertMutable();
            _chargesUsed = Math.Clamp(value, 0, MaxCharges);
            InvokeDisplayAmountChanged();
        }
    }

    public int Charges => Math.Max(0, MaxCharges - _chargesUsed);

    /// <summary>На иконке посоха — оставшиеся заряды (снятый — 0).</summary>
    public override int DisplayAmount => IsStowed ? 0 : Charges;

    /// <summary>Цена посоха — по таблице Чародейской лавки (70–320), а не щита.</summary>
    public override int MerchantCost => ShopRules.StaffPrice(Tier);

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || !IsEquipped || Charges <= 0) return;
        if (player.Creature.CombatState is not { RoundNumber: 1 } combat || CombatManager.Instance.IsOverOrEnding) return;
        ChargesUsed = _chargesUsed + 1;
        for (var i = 0; i < CopiesPerCharge; i++)
        {
            var card = combat.CreateCard(Spell, Owner);
            if (card is SpellCard spell) spell.MakeFreeCast(null);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
        }
        Flash();
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is RestSiteRoom && _chargesUsed > 0)
        {
            ChargesUsed = Math.Max(0, _chargesUsed - CampRecharge);
            Flash();
        }
        return Task.CompletedTask;
    }
}

/// <summary>Дубовый посох (I): Искры, 6 зарядов.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class OakStaff : MageStaff
{
    public override int Tier => 1;
    public override int MaxCharges => 6;
    public override SpellCard Spell => ModelDb.Card<Sparks>();
}

/// <summary>Серебряный посох (II): «Изгнание нежити» по всем, 5 зарядов.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class SilverStaff : MageStaff
{
    public override int Tier => 2;
    public override int MaxCharges => 5;
    public override SpellCard Spell => ModelDb.Card<TurnUndead>();
}

/// <summary>Обсидиановый посох (III): Огненный шар, 4 заряда.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ObsidianStaff : MageStaff
{
    public override int Tier => 3;
    public override int MaxCharges => 4;
    public override SpellCard Spell => ModelDb.Card<Fireball>();
}

/// <summary>Посох вечного льда (IV): Буран, 4 заряда.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class EverfrostStaff : MageStaff
{
    public override int Tier => 4;
    public override int MaxCharges => 4;
    public override SpellCard Spell => ModelDb.Card<Blizzard>();
}

/// <summary>Бездонный посох (V): Цепная молния ×2 за заряд, 3 заряда; Бездонная вещь (праведность не выше «Запятнанного»).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class AbyssalStaff : MageStaff, IAbyssalItem
{
    public override int Tier => 5;
    public override int MaxCharges => 3;
    protected override int CopiesPerCharge => 2;
    public override SpellCard Spell => ModelDb.Card<ChainLightning>();
}
