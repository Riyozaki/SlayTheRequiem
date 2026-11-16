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
    /// <summary>Оплата атаки: множитель урона и состояние ◆ до оплаты (живут, пока карта разыгрывается).</summary>
    private sealed record PaidAttack(decimal Multiplier, StaminaState State);

    /// <summary>Множитель и состояние, зафиксированные при оплате атаки.</summary>
    private static readonly ConditionalWeakTable<CardModel, PaidAttack> PendingMultiplier = new();

    // ───────────── ◆ за атаки ─────────────

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        // Заклинания и магические атаки платят только ✦ (08), Кличи — перезарядкой (04): ни ◆, ни множителя Устал/Изнурён.
        if (card.Type != CardType.Attack || StaminaCosts.IsStaminaExempt(card)) return;
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

        var multiplier = payment.State == StaminaState.Tired && IsTireless(player, card) ? 1m : payment.DamageMultiplier;
        PendingMultiplier.AddOrUpdate(card, new PaidAttack(multiplier, payment.State));

        if (payment.HpLoss > 0 && RunSettings.EnableOverstrain)
        {
            UI.RequiemUiEvents.RaiseOverstrained(player);
            UI.RequiemVfx.Overstrain(player.Creature);
            using var _ = DamageTyping.As(DamageType.True);
            using var quiet = UI.RequiemVfx.Quiet();
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, payment.HpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered, null, null);
        }
    }

    /// <summary>
    /// Был ли владелец Устал или Изнурён при оплате атаки <paramref name="card"/> (состояние до списания ◆). Карта, которая
    /// не платила ◆ (навык или вызов вне розыгрыша), смотрит на текущее ◆: «Устал» — ◆ меньше половины пула.
    /// </summary>
    public static bool IsOwnerWeary(MegaCrit.Sts2.Core.Entities.Players.Player player, CardModel? card = null)
    {
        if (card != null && PendingMultiplier.TryGetValue(card, out var paid)) return paid.State != StaminaState.Rested;
        var stamina = RequiemResource.Of<StaminaResource>(player);
        return stamina != null && StaminaRules.StateFor(stamina.Amount, stamina.Pool, 0) != StaminaState.Rested;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.IsLastInSeries) PendingMultiplier.Remove(cardPlay.Card);
        return Task.CompletedTask;
    }

    // ───────────── урон ─────────────

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!RunSettings.EnableRequiemSystems) return 1m;
        return StaminaMultiplier(props, cardSource) * ResistanceMultiplier(target, props, dealer, cardSource)
            * LevelMultiplier(target, dealer);
    }

    /// <summary>
    /// Уровень «Реквием» (11 §7): «Странник» — герои получают −25 % урона, «Легенда»+ — +25 %; «Проклятый» — герои наносят
    /// врагам −20 %. Урон от себя/своих и без источника не трогаем.
    /// </summary>
    private static decimal LevelMultiplier(Creature? target, Creature? dealer)
    {
        if (target == null || dealer == null || target.IsPlayer == dealer.IsPlayer) return 1m;
        var level = Acts.RequiemLevel.Of(Net.RequiemNet.State);
        return target.IsPlayer
            ? RequiemLevelRules.DamageTakenMultiplier(level)
            : RequiemLevelRules.DamageDealtMultiplier(level);
    }

    private static decimal StaminaMultiplier(ValueProp props, CardModel? cardSource)
    {
        if (cardSource == null || StaminaCosts.IsStaminaExempt(cardSource) || !props.IsPoweredAttack()) return 1m;
        if (PendingMultiplier.TryGetValue(cardSource, out var paid)) return paid.Multiplier;

        // Предпросмотр урона на карте в руке: показываем, сколько она нанесёт при текущей ◆.
        var player = cardSource.Owner;
        if (cardSource.Type != CardType.Attack || !RequiemMode.IsInSystem(player)) return 1m;
        var stamina = RequiemResource.Of<StaminaResource>(player);
        if (stamina == null) return 1m;
        var state = StaminaRules.StateFor(stamina.Amount, stamina.Pool, StaminaCosts.CostFor(cardSource));
        if (state == StaminaState.Tired && IsTireless(player, cardSource)) return 1m;
        return StaminaRules.DamageMultiplier(state);
    }

    /// <summary>
    /// «Устал» не снижает урон: источник у героя — реликвия или сила (<see cref="ITirelessSource"/>, сейчас — Знамение
    /// Клинка), либо сама карта награждает усталость (<see cref="IWearyPayoff"/>: «Вопреки всему», «Танец дюн» —
    /// ревью F-04, 02 §1). Изнурение (◆ не хватает) по-прежнему режет урон вдвое.
    /// </summary>
    public static bool IsTireless(MegaCrit.Sts2.Core.Entities.Players.Player? player, CardModel? card = null)
    {
        if (card is IWearyPayoff) return true;
        if (player == null) return false;
        return player.Relics.OfType<ITirelessSource>().Any(t => t.IgnoresWeariness)
               || player.Creature.Powers.OfType<ITirelessSource>().Any(t => t.IgnoresWeariness);
    }

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
        if (!RunSettings.EnableRequiemSystems || result.UnblockedDamage <= 0 || !target.IsAlive) return;
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
        if (!RunSettings.EnableRequiemSystems || amount <= 0) return false;

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
        if (!RunSettings.EnableRequiemSystems || side != CombatSide.Player) return;
        await EnemySetup.EnsureAll(combatState);
        // Намерения уже выбраны — враги без ◆ показывают «Отдышаться» вместо атаки.
        foreach (var enemy in combatState.Enemies.ToList()) await EnemyResponses.TryBreather(enemy);
    }

    public override async Task AfterCreatureAddedToCombat(Creature creature)
    {
        // Призывы героя — тоже монстры, но им не нужны ◆/Стойкость/аффиксы врагов.
        if (creature.Monster != null && creature.IsEnemy) await EnemySetup.Ensure(creature, creature.CombatState);
    }

    /// <summary>Павший враг — кандидат для «Поднять мертвеца».</summary>
    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (!wasRemovalPrevented) Graveyard.Note(creature.CombatState, creature);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Источник «Устал не снижает урон» — реликвия или сила героя (Знамение Клинка). Изнурение (◆ не хватает) по-прежнему
/// режет урон вдвое.
/// </summary>
public interface ITirelessSource
{
    bool IgnoresWeariness { get; }
}

/// <summary>
/// Карта, которая награждает усталость («если вы Устали — …»): штраф «Устал» −25 % на неё не действует, иначе бонус
/// карты съедался бы её же штрафом (ревью F-04, docs/design/02 §1).
/// </summary>
public interface IWearyPayoff { }
