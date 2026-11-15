using System.Runtime.CompilerServices;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Resources;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Глобальные правила боя Реквиема (получает боевые хуки всегда, без реликвии/силы-носителя):
/// ◆ за атаки и надрыв, типы урона и сопротивления, холод/разряд по ресурсам, стаки статусов против сопротивлений,
/// подготовка врагов и «Отдышаться».
/// </summary>
public sealed class RequiemCombatRules() : CustomSingletonModel(CustomSingletonModel.HookType.Combat)
{
    /// <summary>Множитель урона, зафиксированный при оплате атаки (живёт, пока карта разыгрывается).</summary>
    private static readonly ConditionalWeakTable<CardModel, StrongBox<decimal>> PendingMultiplier = new();

    // ───────────── ◆ за атаки ─────────────

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        // Заклинания платят только ✦ (docs/design/08): ни ◆, ни множителя Устал/Изнурён.
        if (card.Type != CardType.Attack || card is Spells.SpellCard) return;
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

        var multiplier = payment.State == StaminaState.Tired && IsTireless(player) ? 1m : payment.DamageMultiplier;
        PendingMultiplier.AddOrUpdate(card, new StrongBox<decimal>(multiplier));

        if (payment.HpLoss > 0 && RequiemConfig.EnableOverstrain)
        {
            using var _ = DamageTyping.As(DamageType.True);
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, payment.HpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered, null, null);
        }
    }

    /// <summary>Был ли владелец Устал/Изнурён при оплате текущей атаки (или сейчас, если карта не оплачивалась).</summary>
    public static bool IsOwnerWeary(MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        var stamina = RequiemResource.Of<StaminaResource>(player);
        return stamina != null && stamina.Amount * 2 < stamina.Pool;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.IsLastInSeries) PendingMultiplier.Remove(cardPlay.Card);
        return Task.CompletedTask;
    }

    // ───────────── урон ─────────────

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!RequiemConfig.EnableRequiemSystems) return 1m;
        return StaminaMultiplier(props, cardSource) * ResistanceMultiplier(target, props, dealer, cardSource);
    }

    private static decimal StaminaMultiplier(ValueProp props, CardModel? cardSource)
    {
        if (cardSource == null || cardSource is Spells.SpellCard || !props.IsPoweredAttack()) return 1m;
        if (PendingMultiplier.TryGetValue(cardSource, out var box)) return box.Value;

        // Предпросмотр урона на карте в руке: показываем, сколько она нанесёт при текущей ◆.
        var player = cardSource.Owner;
        if (cardSource.Type != CardType.Attack || !RequiemMode.IsInSystem(player)) return 1m;
        var stamina = RequiemResource.Of<StaminaResource>(player);
        if (stamina == null) return 1m;
        var state = StaminaRules.StateFor(stamina.Amount, stamina.Pool, StaminaCosts.CostFor(cardSource));
        if (state == StaminaState.Tired && IsTireless(player)) return 1m;
        return StaminaRules.DamageMultiplier(state);
    }

    /// <summary>«Устал» не снижает урон (Знамение Клинка).</summary>
    public static bool IsTireless(MegaCrit.Sts2.Core.Entities.Players.Player? player) =>
        player != null && player.Relics.OfType<ITirelessSource>().Any(t => t.IgnoresWeariness);

    private static decimal ResistanceMultiplier(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == null) return 1m;
        var type = DamageTyping.Resolve(props, dealer, cardSource);
        if (type is DamageType.Physical or DamageType.True) return 1m;
        // Сияние (03): свет по нежити, духам и Бездне ×2.
        if (type == DamageType.Light)
            return EnemySetup.IsUndead(target) || (target.Monster != null && RequiemData.MonsterEntry(target.Monster.GetType().Name).Faction == "abyss") ? 2m : 1m;
        var tier = ResistancePower.TierOf(target, type);
        if (tier == 0) return 1m;
        // Уязвимость стихии и ванильная Уязвимость не складываются — берём одну.
        if (tier < 0 && props.IsPoweredAttack() && target.HasPower<VulnerablePower>()) return 1m;
        return CombatMath.ResistanceMultiplier(tier);
    }

    /// <summary>Холод бьёт и по ◆, разряд — по ✦ («урон по ХП и СТ», docs/design/03).</summary>
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!RequiemConfig.EnableRequiemSystems || result.UnblockedDamage <= 0 || !target.IsAlive) return;
        var type = DamageTyping.Resolve(props, dealer, cardSource);
        switch (type)
        {
            case DamageType.Frost:
                if (RequiemResource.Of<StaminaResource>(target.Player) is { } stamina)
                    stamina.Amount = Math.Max(0, stamina.Amount - result.UnblockedDamage);
                else if (target.GetPower<EnemyStaminaPower>() is { } enemyStamina)
                    await enemyStamina.Drain(result.UnblockedDamage);
                break;
            case DamageType.Shock:
                if (target.IsPlayer) await Mana.Lose(target.Player, result.UnblockedDamage);
                else if (target.GetPower<EnemyManaPower>() is { } enemyMana) await enemyMana.Drain(result.UnblockedDamage);
                break;
        }
    }

    // ───────────── стаки статусов против сопротивлений ─────────────

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (!RequiemConfig.EnableRequiemSystems || amount <= 0) return false;

        if ((canonicalPower is BleedPower && EnemySetup.IsBleedImmune(target))
            || (canonicalPower is BlightPower && EnemySetup.IsBlightImmune(target))
            || Transformations.Healing.IsImmune(target, canonicalPower))
        {
            modifiedAmount = 0;
            return true;
        }

        DamageType? element = canonicalPower switch
        {
            ChillPower => DamageType.Frost,
            BurningPower => DamageType.Fire,
            StaticPower => DamageType.Shock,
            PoisonPower => DamageType.Poison,
            BlightPower => DamageType.Blight,
            _ => null,
        };
        if (element == null) return false;

        var tier = ResistancePower.TierOf(target, element.Value);
        if (tier == 0) return false;
        modifiedAmount = CombatMath.ResistStacks((int)amount, tier);
        return modifiedAmount != amount;
    }

    // ───────────── враги ─────────────

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!RequiemConfig.EnableRequiemSystems || side != CombatSide.Player) return;
        await EnemySetup.EnsureAll(combatState);
        // Намерения уже выбраны — враги без ◆ показывают «Отдышаться» вместо атаки.
        foreach (var enemy in combatState.Enemies.ToList()) await EnemyResponses.TryBreather(enemy);
    }

    public override async Task AfterCreatureAddedToCombat(Creature creature)
    {
        if (creature.Monster != null) await EnemySetup.Ensure(creature, creature.CombatState);
    }
}

/// <summary>Источник «Устал не снижает урон» (Знамение Клинка). Изнурение (◆ не хватает) по-прежнему режет урон вдвое.</summary>
public interface ITirelessSource
{
    bool IgnoresWeariness { get; }
}
