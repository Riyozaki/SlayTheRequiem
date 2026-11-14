using System.Runtime.CompilerServices;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Resources;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Глобальные правила боя Реквиема (получает боевые хуки всегда, без реликвии/силы-носителя):
/// оплата атак выносливостью, Устал/Изнурён, надрыв.
/// </summary>
public sealed class RequiemCombatRules() : CustomSingletonModel(CustomSingletonModel.HookType.Combat)
{
    /// <summary>Множитель урона, зафиксированный при оплате атаки (живёт, пока карта разыгрывается).</summary>
    private static readonly ConditionalWeakTable<CardModel, StrongBox<decimal>> PendingMultiplier = new();

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        if (card.Type != CardType.Attack) return;
        var player = card.Owner;
        if (!RequiemMode.IsInSystem(player)) return;
        // Повторы (Двойной удар, эхо) не платят второй раз и бьют с тем же множителем.
        if (!cardPlay.IsFirstInSeries) return;

        var stamina = RequiemResource.Of<StaminaResource>(player);
        if (stamina == null) return;

        var cost = StaminaCosts.CostFor(card, cardPlay);
        var payment = StaminaRules.Pay(stamina.Amount, stamina.Pool, cost);
        var combatState = player.Creature.CombatState;
        if (payment.Spent > 0)
        {
            if (combatState != null)
                await stamina.Spend<StaminaResource>(combatState, card, payment.Spent, optional: false);
            else
                stamina.ModifyAmount(-payment.Spent);
        }

        PendingMultiplier.AddOrUpdate(card, new StrongBox<decimal>(payment.DamageMultiplier));

        if (payment.HpLoss > 0 && RequiemConfig.EnableOverstrain)
        {
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, payment.HpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered, null, null);
        }
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (cardSource == null || !props.IsPoweredAttack()) return 1m;
        if (PendingMultiplier.TryGetValue(cardSource, out var box)) return box.Value;

        // Предпросмотр урона на карте в руке: показываем, сколько она нанесёт при текущей ◆.
        var player = cardSource.Owner;
        if (cardSource.Type != CardType.Attack || !RequiemMode.IsInSystem(player)) return 1m;
        var stamina = RequiemResource.Of<StaminaResource>(player);
        if (stamina == null) return 1m;
        var state = StaminaRules.StateFor(stamina.Amount, stamina.Pool, StaminaCosts.CostFor(cardSource));
        return StaminaRules.DamageMultiplier(state);
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.IsLastInSeries) PendingMultiplier.Remove(cardPlay.Card);
        return Task.CompletedTask;
    }
}
