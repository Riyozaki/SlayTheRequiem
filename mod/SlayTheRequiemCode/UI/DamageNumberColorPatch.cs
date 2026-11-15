using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Число урона окрашено по типу (art spec 4.6): огонь — оранжевым, холод — голубым и т. д. Тип определяется тем же
/// <see cref="DamageTyping.Resolve"/>, что и сопротивления. Игра записывает удар в историю боя прямо перед созданием
/// всплывающего числа (CreatureCmd.Damage) — там тип и запоминаем, по самому объекту DamageResult.
/// </summary>
public static class DamageNumberColors
{
    private static readonly ConditionalWeakTable<DamageResult, StrongBox<DamageType>> ByResult = new();
    private static readonly ConditionalWeakTable<NDamageNumVfx, StrongBox<Color>> ByVfx = new();

    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.DamageReceived))]
    public static class RememberType
    {
        [HarmonyPostfix]
        public static void Postfix(Creature? dealer, DamageResult result, CardModel? cardSource)
        {
            if (!RequiemConfig.EnableRequiemSystems || result == null) return;
            var type = DamageTyping.Resolve(result.Props, dealer, cardSource);
            if (type != DamageType.Physical) ByResult.AddOrUpdate(result, new StrongBox<DamageType>(type));
        }
    }

    [HarmonyPatch(typeof(NDamageNumVfx), nameof(NDamageNumVfx.Create), typeof(Creature), typeof(DamageResult))]
    public static class TagVfx
    {
        [HarmonyPostfix]
        public static void Postfix(DamageResult result, NDamageNumVfx? __result)
        {
            if (__result == null || !ByResult.TryGetValue(result, out var type)) return;
            ByVfx.AddOrUpdate(__result, new StrongBox<Color>(RequiemUi.DamageTypeColor(type.Value)));
        }
    }

    [HarmonyPatch(typeof(NDamageNumVfx), nameof(NDamageNumVfx._Ready))]
    public static class PaintVfx
    {
        [HarmonyPostfix]
        public static void Postfix(NDamageNumVfx __instance)
        {
            if (!ByVfx.TryGetValue(__instance, out var color)) return;
            var label = __instance.GetNodeOrNull<Label>("Label");
            label?.AddThemeColorOverride(ThemeConstants.Label.FontColor, color.Value);
        }
    }
}
