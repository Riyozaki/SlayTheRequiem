using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using VanillaPoison = MegaCrit.Sts2.Core.Models.Powers.PoisonPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

// Аффиксы врагов (docs/design/11 §2). Значок аффикса — сила-маркер без счётчика; сами числа — в EnemyRules.
// Выдаются в EnemySetup.Ensure: элиты — 1 аффикс (на «Реквиеме» 7+ — 2–3, в Славе пяти актов — ещё +1),
// обычные враги с акта 2 — с шансом 25 %.

public abstract class AffixPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>Попадание атакой носителя по герою (не по Блоку).</summary>
    protected bool IsMyHitOnHero(Creature? dealer, DamageResult result, ValueProp props, Creature target) =>
        dealer == Owner && target.IsPlayer && result.UnblockedDamage > 0 && props.IsPoweredAttack();
}

/// <summary>Закалённый: Латы +2 (выдаются вместе с аффиксом).</summary>
public sealed class HardenedAffixPower : AffixPower { }

/// <summary>Огненный / Ледяной / Грозовой: постоянный Плащ стихии 3 и сопротивление II (выдаются вместе с аффиксом).</summary>
public sealed class ElementalAffixPower : AffixPower { }

/// <summary>Выпивающий силы: атаки, задевшие ♥, отнимают 2 ◆.</summary>
public sealed class DrainingAffixPower : AffixPower
{
    public override Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (!IsMyHitOnHero(dealer, result, props, target)) return Task.CompletedTask;
        if (RequiemResource.Of<StaminaResource>(target.Player) is { } stamina)
        {
            stamina.Amount = Math.Max(0, stamina.Amount - EnemyRules.AffixDrain);
            Flash();
        }
        return Task.CompletedTask;
    }
}

/// <summary>Пожиратель маны: атаки, задевшие ♥, выжигают 2 ✦.</summary>
public sealed class ManaEaterAffixPower : AffixPower
{
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (!IsMyHitOnHero(dealer, result, props, target)) return;
        Flash();
        await Mana.Lose(target.Player, EnemyRules.AffixDrain);
    }
}

/// <summary>Вампирический: лечит 50 % урона, нанесённого по ♥.</summary>
public sealed class VampiricAffixPower : AffixPower
{
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner || result.UnblockedDamage <= 0 || !Owner.IsAlive || !props.IsPoweredAttack()) return;
        var heal = result.UnblockedDamage * EnemyRules.VampiricPercent / 100;
        if (heal <= 0) return;
        Flash();
        await CreatureCmd.Heal(Owner, heal);
    }
}

/// <summary>Отравленный клинок: атаки, задевшие ♥, накладывают Яд 2.</summary>
public sealed class PoisonedBladeAffixPower : AffixPower
{
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (!IsMyHitOnHero(dealer, result, props, target) || !target.IsAlive) return;
        Flash();
        await PowerCmd.Apply<VanillaPoison>(choiceContext, target, EnemyRules.AffixPoison, Owner, null);
    }
}

/// <summary>Знаменосец: остальные враги наносят атаками +2 урона, пока носитель жив.</summary>
public sealed class BannermanAffixPower : AffixPower
{
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        Owner.IsAlive && dealer != null && dealer != Owner && dealer.Monster != null && dealer.PetOwner == null && props.IsPoweredAttack() ? EnemyRules.BannerDamage : 0m;
}

/// <summary>Поглотитель: Поглощение заклинаний 1, восстанавливается в начале каждого хода героев.</summary>
public sealed class AbsorberAffixPower : AffixPower
{
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || !Owner.IsAlive) return;
        var missing = EnemyRules.AbsorberStacks - Owner.GetPowerAmount<SpellAbsorptionPower>();
        if (missing > 0)
            await PowerCmd.Apply<SpellAbsorptionPower>(new ThrowingPlayerChoiceContext(), Owner, missing, Owner, null, silent: true);
    }
}

/// <summary>Неистовый: при ♥ &lt; 50 % каждая атака носителя бьёт на 1 раз больше.</summary>
public sealed class FrenziedAffixPower : AffixPower
{
    public override int ModifyAttackHitCount(AttackCommand attack, int hitCount) =>
        attack.Attacker == Owner && Owner.IsAlive && AffixRules.IsFrenzied(Owner.CurrentHp, Owner.MaxHp) ? hitCount + 1 : hitCount;
}

/// <summary>
/// Призрачный (аффикс): физический урон не от заклинаний по носителю −50 % (полная Призрачность у духов — −75 %,
/// docs/design/03). Серебряное оружие героя Призрачность не замечает.
/// </summary>
public sealed class GhostlyAffixPower : AffixPower
{
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner || dealer == null || dealer == Owner) return 1m;
        if (MagicDamage.IsSpell(props, dealer, cardSource)) return 1m;
        if (DamageTyping.Resolve(props, dealer, cardSource) != DamageType.Physical) return 1m;
        if (dealer.Player?.Relics.OfType<SlayTheRequiem.SlayTheRequiemCode.Equipment.SilverSaber>().Any() == true) return 1m;
        return (100 - EnemyRules.GhostlyAffixReduction) / 100m;
    }
}

/// <summary>Мститель: при смерти носителя — 6 урона каждому живому герою.</summary>
public sealed class AvengerAffixPower : AffixPower
{
    private bool _fired;

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature != Owner || wasRemovalPrevented || _fired) return;
        _fired = true;
        var heroes = Owner.CombatState?.Players.Select(p => p.Creature).Where(c => c is { IsAlive: true }).ToList() ?? [];
        if (heroes.Count == 0) return;
        Flash();
        foreach (var hero in heroes)
            await CreatureCmd.Damage(choiceContext, hero, EnemyRules.AvengerDamage, ValueProp.Unpowered, Owner, null, null);
    }

    public override bool ShouldPowerBeRemovedOnDeath(PowerModel power) => power != this && base.ShouldPowerBeRemovedOnDeath(power);
}

// ═════════════ Атаманша Багряных Ножей ═════════════

/// <summary>
/// Шайка: разбойник пассивен, пока герой не ударит его или атаманшу (или атаманша не позовёт) — тогда «Шайка»
/// снимается со всех, и со следующего хода они нападают.
/// </summary>
public sealed class GangPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public static async Task WakeAll(ICombatState? combatState)
    {
        if (combatState == null) return;
        foreach (var power in combatState.Enemies.Select(e => e.GetPower<GangPower>()).OfType<GangPower>().ToList())
            await PowerCmd.Remove(power);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && dealer is { IsPlayer: true }) await WakeAll(Owner.CombatState);
    }
}

/// <summary>Паника N (шайка Атаманши): следующая атака носителя не наносит урона. −1 в конце хода врагов.</summary>
public sealed class PanicPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        dealer == Owner && props.IsPoweredAttack() ? 0m : 1m;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy) await PowerCmd.Decrement(this);
    }
}

/// <summary>Адреналин N: физический урон по носителю −50 %. −1 в конце хода героев.</summary>
public sealed class AdrenalinePower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        target == Owner && DamageTyping.Resolve(props, dealer, cardSource) == DamageType.Physical ? 0.5m : 1m;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player) await PowerCmd.Decrement(this);
    }
}
