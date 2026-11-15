using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

/// <summary>
/// Латы N: каждый удар атакой по носителю слабее на N (не ниже 1). Постоянны в бою.
/// Применяются в мультипликативной фазе хуков урона — порядок относительно Уязвимости проверить в игре.
/// </summary>
public sealed class PlatingPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    /// <summary>Латы могут уйти в минус (Осколок Ветра) — тогда каждый удар по носителю сильнее.</summary>
    public override bool AllowNegative => true;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0 || !props.IsPoweredAttack()) return 1m;
        if (DamageTyping.Resolve(props, dealer, cardSource) == DamageType.True) return 1m;
        var sunder = Owner.GetPowerAmount<SunderPower>();
        var reduced = CombatMath.ApplyPlating((int)amount, Amount, pierce: PierceOf(dealer), sunder: sunder);
        return reduced / amount;
    }

    /// <summary>Пробитие атакующего героя (Осколок Зверя, дар Князя Измены).</summary>
    public static int PierceOf(Creature? dealer) =>
        dealer?.Player == null ? 0 : Math.Max(0, dealer.Player.Relics.OfType<IPierceSource>().Sum(p => p.Pierce));
}

/// <summary>Источник Пробития N у героя: его атаки игнорируют N Лат цели.</summary>
public interface IPierceSource
{
    int Pierce { get; }
}

/// <summary>
/// Раскол N: Латы цели ниже на N; если Лат нет или они ушли в минус — каждый удар атакой по цели сильнее.
/// −1 в начале хода носителя.
/// </summary>
public sealed class SunderPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // Если есть Латы — Раскол учитывает PlatingPower, иначе работает сам.
        if (target != Owner || amount <= 0 || !props.IsPoweredAttack() || Owner.HasPower<PlatingPower>()) return 1m;
        return CombatMath.ApplyPlating((int)amount, 0, sunder: Amount) / amount;
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner)) await PowerCmd.Decrement(this);
    }
}
