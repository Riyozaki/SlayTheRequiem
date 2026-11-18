using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Equipment;

/// <summary>
/// Боевые эффекты снаряжения, общие для всех предметов: Латы/Уворот/Покров/Стойкость брони в начале боя,
/// урон оружия на «Оружейных» атаках, штраф Груза к стартовой руке.
/// Уникальные свойства предметов реализуют сами предметы (свои хуки реликвий).
/// </summary>
public sealed class EquipmentCombatRules() : CustomSingletonModel(CustomSingletonModel.HookType.Combat)
{
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        // Первый ход героев = начало боя (у BeforeCombatStart нет доступа к состоянию боя).
        if (side != CombatSide.Player || combatState.RoundNumber != 1) return;
        foreach (var player in combatState.Players)
        {
            if (!RequiemMode.IsInSystem(player)) continue;
            var bonuses = Loadout.Bonuses(player);
            var ctx = new ThrowingPlayerChoiceContext();
            var hero = player.Creature;
            if (bonuses.Plating > 0) await PowerCmd.Apply<PlatingPower>(ctx, hero, bonuses.Plating, hero, null, silent: true);
            if (bonuses.DodgeCharges > 0) await PowerCmd.Apply<DodgePower>(ctx, hero, bonuses.DodgeCharges, hero, null, silent: true);
            if (bonuses.Ward > 0) await PowerCmd.Apply<WardPower>(ctx, hero, bonuses.Ward, hero, null, silent: true);
        }
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (cardSource == null || !props.IsPoweredAttack() || !IsWeaponAttack(cardSource)) return 0m;
        var player = cardSource.Owner;
        if (!RequiemMode.IsInSystem(player) || dealer != player.Creature) return 0m;
        return Loadout.Bonuses(player).WeaponDamage;
    }

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player.Creature.CombatState?.RoundNumber != 1) return count;
        return Math.Max(1, count - BurdenPenalty.OpeningHandPenalty(player));
    }

    /// <summary>«Оружейная» атака: Удары (тег Strike), ванильные с флагом weapon в разметке, карты мода с IWeaponCard.</summary>
    public static bool IsWeaponAttack(CardModel card)
    {
        if (card.Type != CardType.Attack || card is Spells.SpellCard) return false;
        if (card is IWeaponCard) return true;
        if (card.Tags.Contains(CardTag.Strike)) return true;
        // Разметка CSV — только для ванильных карт: у карт мода есть тёзки в игре (Backstab, Pounce, Whirlwind, Rampage),
        // и без этой проверки они получали бы флаг оружия ванильной карты (аудит M2).
        if (card is IRequiemCard) return false;
        return RequiemData.Attacks != null && RequiemData.Attacks.TryGet(card.GetType().Name, out var e) && e.Weapon;
    }
}

/// <summary>Маркер «Оружейной» карты мода.</summary>
public interface IWeaponCard { }
