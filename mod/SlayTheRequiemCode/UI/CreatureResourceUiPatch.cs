using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Встраивание шкал под полоску ♥ (NCreatureStateDisplay → NHealthBar.HpBarContainer). Ряд статусов
/// (NPowerContainer) сдвигается вниз на высоту шкал, чтобы ничего не перекрывалось.
/// </summary>
[HarmonyPatch(typeof(NCreatureStateDisplay), nameof(NCreatureStateDisplay.SetCreature))]
public static class CreatureResourceUiPatch
{
    private static readonly StringName ShiftedMeta = "str_power_shift";

    [HarmonyPostfix]
    public static void Postfix(NCreatureStateDisplay __instance, Creature creature)
    {
        try
        {
            if (!RunSettings.EnableRequiemSystems) return;
            var health = __instance.GetNodeOrNull<NHealthBar>("%HealthBar");
            var anchor = health?.HpBarContainer;
            if (anchor == null) return;

            float shift;
            NEnemyTraitChips? traitWidget = null;
            if (creature.IsPlayer)
            {
                if (!RequiemMode.IsInSystem(creature)) return;
                if (anchor.GetNodeOrNull(NRequiemResourceBars.NodeName) != null) return;
                var bars = new NRequiemResourceBars();
                bars.Bind(creature, anchor);
                anchor.AddChild(bars);
                shift = NRequiemResourceBars.TotalHeight + NRequiemResourceBars.Gap;
            }
            else if (creature.Monster != null)
            {
                if (anchor.GetNodeOrNull(NEnemyResourcePips.NodeName) != null) return;
                var pips = new NEnemyResourcePips();
                pips.Bind(creature, anchor);
                anchor.AddChild(pips);
                shift = NEnemyResourcePips.RowHeight * 2 + 3;
                // Компактные черты врага (Латы, Устойчивость, сопротивления, аффиксы) — строкой под точками ◆/✦.
                if (RequiemConfig.CompactEnemyStatuses)
                {
                    var traits = traitWidget = new NEnemyTraitChips();
                    traits.Bind(creature, anchor, shift + 1);
                    anchor.AddChild(traits);
                    shift += NEnemyTraitChips.RowHeight + 2;
                }
            }
            else return;

            var powers = __instance.GetNodeOrNull<Control>("%PowerContainer");
            if (powers != null && !powers.HasMeta(ShiftedMeta))
            {
                powers.SetMeta(ShiftedMeta, true);
                var baseY = powers.Position.Y;
                powers.Position += new Vector2(0, shift);
                if (traitWidget != null)
                {
                    var beforeTraits = shift - NEnemyTraitChips.RowHeight;
                    traitWidget.HeightChanged += height =>
                    {
                        if (Godot.GodotObject.IsInstanceValid(powers)) powers.Position = new Vector2(powers.Position.X, baseY + beforeTraits + height);
                    };
                }
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"UI: шкалы ресурсов: {e.Message}");
        }
    }
}
