using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

/// <summary>
/// Латы N: каждый удар атакой по носителю слабее на N (не ниже 1). Постоянны в бою.
/// Применяются последним шагом расчёта урона (<see cref="PlatingFinalStep"/>): после Силы, Уязвимости и сопротивлений.
/// </summary>
public sealed class PlatingPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    /// <summary>Латы могут уйти в минус (Осколок Ветра) — тогда каждый удар по носителю сильнее.</summary>
    public override bool AllowNegative => true;

    // Сам расчёт — в PlatingFinalStep: последним шагом после всех множителей (Уязвимость, сопротивления),
    // чтобы результат не зависел от порядка слушателей хука.

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

    // Сам расчёт — в PlatingFinalStep (вместе с Латами, последним шагом).

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner)) await PowerCmd.Decrement(this);
    }
}

/// <summary>
/// Латы и Раскол — последний шаг расчёта урона (02 §4.1: «каждый входящий удар уменьшается на значение Лат»).
/// Раньше они были множителями в общей мультипликативной фазе, и итог зависел от порядка слушателей: 10 урона,
/// Латы 3, Уязвимость — 10,5 или 12. Теперь сначала все множители игры и мода, потом Латы и Раскол, всегда одинаково.
/// Постфикс на внутренний расчёт игры, поэтому одинаково работает и для удара, и для превью на карте и в намерении.
/// </summary>
[HarmonyPatch(typeof(Hook), "ModifyDamageInternal")]
public static class PlatingFinalStep
{
    [HarmonyPostfix]
    public static void Postfix(Creature? target, Creature? dealer, ValueProp props, CardModel? cardSource,
        ModifyDamageHookType modifyDamageHookType, ref decimal __result, ref List<AbstractModel> modifiers)
    {
        try
        {
            if (!modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative)) return;
            if (target == null || __result <= 0 || !props.IsPoweredAttack()) return;
            var plating = target.GetPower<PlatingPower>();
            var sunder = target.GetPower<SunderPower>();
            if (plating == null && sunder == null) return;
            var trueDamage = DamageTyping.Resolve(props, dealer, cardSource) == DamageType.True;
            var platingAmount = trueDamage ? 0 : plating?.Amount ?? 0;
            var sunderAmount = sunder?.Amount ?? 0;
            var damage = (int)__result;
            var reduced = CombatMath.ApplyPlating(damage, platingAmount, PlatingPower.PierceOf(dealer), sunderAmount);
            if (reduced == damage) return;
            // Потолок урона (Бесплотность и т. п.) Раскол не пробивает: прибавку не даём, если удар уже срезан потолком.
            // A ref Harmony parameter cannot be captured by a lambda (CS1628). Read it once; the formula and
            // the cap comparison are deliberately unchanged by this compilation-only repair.
            var incoming = __result;
            if (reduced > damage && modifiers.Any(m => m.ModifyDamageCap(target, props, dealer, cardSource) <= incoming)) return;
            __result = reduced;
            if (plating != null && platingAmount != 0 && !modifiers.Contains(plating)) modifiers.Add(plating);
            if (sunder != null && sunderAmount != 0 && !modifiers.Contains(sunder)) modifiers.Add(sunder);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"Латы/Раскол: расчёт пропущен ({e.Message})");
        }
    }
}
