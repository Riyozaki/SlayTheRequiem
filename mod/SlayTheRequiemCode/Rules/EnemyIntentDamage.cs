using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>Native AttackIntent always previews Move (powered). Our scripted spell explosions are explicitly
/// Move|Unpowered, just like their actual command: no phantom Strength/Weak/armor in the intent.</summary>
[CriticalPatch]
[HarmonyPatch(typeof(AttackIntent), nameof(AttackIntent.GetSingleDamage))]
public static class EnemyIntentDamage
{
    [HarmonyPrefix]
    public static bool Prefix(AttackIntent __instance, Creature owner, ref int __result)
    {
        if (!Config.RunSettings.EnableRequiemSystems || owner.Monster is not RequiemMonster own
            || !MonsterMoveRules.IsUnpowered(own.NextMove?.Id ?? string.Empty)) return true;
        var amount = __instance.DamageCalc?.Invoke() ?? 0m;
        if (LocalContext.GetMe(owner.CombatState) is { } me)
            amount = Hook.ModifyDamage(me.RunState, owner.CombatState, me.Creature, owner, amount,
                ValueProp.Move | ValueProp.Unpowered, null, null, ModifyDamageHookType.All, CardPreviewMode.None, out _);
        __result = (int)Math.Clamp(amount, 0m, int.MaxValue);
        return false;
    }
}
