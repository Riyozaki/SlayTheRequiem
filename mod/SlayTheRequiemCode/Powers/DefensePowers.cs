using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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
/// Применяются после Силы, Уязвимости и сопротивлений, перед cap и Блоком (<see cref="PlatingFinalStep"/>).
/// </summary>
public sealed class PlatingPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    /// <summary>Латы могут уйти в минус (Осколок Ветра) — тогда каждый удар по носителю сильнее.</summary>
    public override bool AllowNegative => true;

    // Сам расчёт — в PlatingFinalStep: последним шагом после всех множителей (Уязвимость, сопротивления),
    // чтобы результат не зависел от порядка слушателей хука.

    /// <summary>Пробитие атакующего: явная сила врага/героя плюс реликвии героя; один запрос для preview/hit.</summary>
    public static int PierceOf(Creature? dealer) => dealer == null ? 0
        : Math.Max(0, dealer.GetPowerAmount<ArmorPiercingPower>() + (dealer.Player?.Relics.OfType<IPierceSource>().Sum(p => p.Pierce) ?? 0));
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
/// Insert armor AFTER the native multiplicative loop and BEFORE its cap loop. Both the hit and every target
/// preview call this same native method. Never re-enumerate or re-invoke ModifyDamageCap to guess whether a
/// cap had fired: listeners may be stateful and a second invocation is not the native contract.
/// </summary>
[CriticalPatch]
[HarmonyPatch(typeof(Hook), "ModifyDamageInternal")]
public static class PlatingFinalStep
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var code = instructions.ToList();
        var flag = AccessTools.Method(typeof(Enum), nameof(Enum.HasFlag), [typeof(Enum)]);
        var cap = (int)ModifyDamageHookType.Cap;
        // v0.111.1: CardPlay вставлен перед hookType, поэтому флаг cap лежит в аргументе 8.
        // Requiring this exact semantic anchor is intentional: IL drift is a critical startup failure, NOT
        // an excuse to quietly fall back to order-dependent armor or to patch a different decimal local.
        var anchors = Enumerable.Range(4, Math.Max(0, code.Count - 4)).Where(i => code[i].Calls(flag)
            && code[i - 1].opcode == OpCodes.Box && Equals(code[i - 1].operand, typeof(ModifyDamageHookType))
            && code[i - 2].LoadsConstant(cap)
            && code[i - 3].opcode == OpCodes.Box && Equals(code[i - 3].operand, typeof(ModifyDamageHookType))
            && ArgumentIndex(code[i - 4]) == 8).Select(i => i - 4).ToList();
        var locals = __originalMethod.GetMethodBody()?.LocalVariables;
        if (anchors.Count != 1 || locals == null || locals.Count < 2 || locals[0].LocalType != typeof(decimal)
            || locals[1].LocalType != typeof(List<AbstractModel>))
            throw new InvalidOperationException("Damage IL changed: expected one cap-flag boundary and decimal/list locals 0/1");
        var index = anchors[0];
        var first = new CodeInstruction(OpCodes.Ldloc_0);
        first.labels.AddRange(code[index].labels); code[index].labels.Clear();
        first.blocks.AddRange(code[index].blocks); code[index].blocks.Clear();
        code.InsertRange(index, new CodeInstruction[]
        {
            first, new(OpCodes.Ldarg_2), new(OpCodes.Ldarg_3), new(OpCodes.Ldarg_S, (byte)5),
            new(OpCodes.Ldarg_S, (byte)6), new(OpCodes.Ldarg_S, (byte)7), new(OpCodes.Ldarg_S, (byte)8), new(OpCodes.Ldloc_1),
            new(OpCodes.Call, AccessTools.Method(typeof(PlatingFinalStep), nameof(BeforeCap))), new(OpCodes.Stloc_0),
        });
        return code;
    }

    private static int ArgumentIndex(CodeInstruction code)
    {
        if (code.opcode != OpCodes.Ldarg && code.opcode != OpCodes.Ldarg_S) return -1;
        return code.operand is ParameterInfo p ? p.Position : Convert.ToInt32(code.operand);
    }

    internal static decimal BeforeCap(decimal damage, Creature? target, Creature? dealer, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay, ModifyDamageHookType hookType, List<AbstractModel> modifiers)
    {
        if (!Config.RunSettings.EnableRequiemSystems || !hookType.HasFlag(ModifyDamageHookType.Multiplicative)
            || target == null || damage <= 0m || !props.IsPoweredAttack()) return damage;
        var plating = target.GetPower<PlatingPower>();
        var sunder = target.GetPower<SunderPower>();
        var guard = target.GetPower<WandererGuardPower>();
        var curse = target.GetPower<CursedCryPower>();
        var temporary = target.GetPower<TemporaryPlatingPower>();
        var platingAmount = (plating?.Amount ?? 0) + (temporary?.Amount ?? 0) + (guard == null ? 0 : WandererGuardPower.PlatingBonus) - (curse?.Amount ?? 0);
        var reduced = CombatMath.ArmorStage(damage, true, DamageTyping.Resolve(props, dealer, cardSource),
            platingAmount, PlatingPower.PierceOf(dealer), sunder?.Amount ?? 0);
        if (reduced == damage) return damage;
        if (plating != null && plating.Amount != 0 && !modifiers.Contains(plating)) modifiers.Add(plating);
        if (sunder != null && sunder.Amount != 0 && !modifiers.Contains(sunder)) modifiers.Add(sunder);
        if (guard != null && !modifiers.Contains(guard)) modifiers.Add(guard);
        if (curse != null && !modifiers.Contains(curse)) modifiers.Add(curse);
        if (temporary != null && !modifiers.Contains(temporary)) modifiers.Add(temporary);
        return reduced;
    }
}
