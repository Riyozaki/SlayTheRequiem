using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>Attach once, but always rebind an existing widget. A loaded combat builds fresh subscriptions; a
/// repeated adapter call must neither add a second widget nor return before updating its model/anchor.</summary>
[HarmonyPatch(typeof(NCreatureStateDisplay), nameof(NCreatureStateDisplay.SetCreature))]
public static class CreatureResourceUiPatch
{
    [HarmonyPostfix]
    public static void Postfix(NCreatureStateDisplay __instance, Creature creature)
    {
        try
        {
            if (!RunSettings.EnableRequiemSystems) return;
            var anchor = __instance.GetNodeOrNull<NHealthBar>("%HealthBar")?.HpBarContainer;
            if (anchor == null) return;
            float reserved;
            NEnemyTraitChips? traits = null;
            if (creature.IsPlayer)
            {
                if (!RequiemMode.IsInSystem(creature)) return;
                var bars = anchor.GetNodeOrNull<NRequiemResourceBars>(NRequiemResourceBars.NodeName);
                if (bars == null) { bars = new NRequiemResourceBars(); anchor.AddChild(bars); }
                bars.Bind(creature, anchor);
                reserved = NRequiemResourceBars.TotalHeight + NRequiemResourceBars.Gap;
            }
            else if (creature.Monster != null)
            {
                var pips = anchor.GetNodeOrNull<NEnemyResourcePips>(NEnemyResourcePips.NodeName);
                if (pips == null) { pips = new NEnemyResourcePips(); anchor.AddChild(pips); }
                pips.Bind(creature, anchor);
                reserved = NEnemyResourcePips.RowHeight * 2 + 3;
                // Attach even when compact mode starts off; a later local toggle already has its replacement UI.
                traits = anchor.GetNodeOrNull<NEnemyTraitChips>(NEnemyTraitChips.NodeName);
                if (traits == null) { traits = new NEnemyTraitChips(); anchor.AddChild(traits); }
                traits.Bind(creature, anchor, reserved + 1);
            }
            else return;
            if (__instance.GetNodeOrNull<NPowerContainer>("%PowerContainer") is { } powers)
                CreaturePowerRowLayout.Bind(powers, anchor, reserved, traits);
        }
        catch (Exception e) { MainFile.Logger.Warn($"UI: resource/trait attachment: {e.Message}"); }
    }
}

/// <summary>Absolute HP-relative Y, not a cumulative shift. Native UpdatePositions runs again when powers are
/// added/removed and SetCreatureBounds changes; keep its X/hover/material/layout logic and apply our Y afterwards.
/// CWT holds no global strong list of combat nodes. A rebind detaches the old HeightChanged handler.</summary>
[HarmonyPatch(typeof(NPowerContainer), "UpdatePositions")]
public static class CreaturePowerRowLayout
{
    private sealed class Layout(NPowerContainer powers)
    {
        internal Control Anchor = null!;
        internal NEnemyTraitChips? Traits;
        internal float Reserved, TraitHeight;
        internal void HeightChanged(float height) { TraitHeight = height; Apply(); }
        internal void Apply()
        {
            if (!GodotObject.IsInstanceValid(powers) || !GodotObject.IsInstanceValid(Anchor)) return;
            powers.GlobalPosition = new Vector2(powers.GlobalPosition.X,
                Anchor.GlobalPosition.Y + Anchor.Size.Y + Reserved + (TraitHeight > 0 ? TraitHeight + 2 : 0));
        }
    }
    private static readonly ConditionalWeakTable<NPowerContainer, Layout> Layouts = new();
    public static void Bind(NPowerContainer powers, Control anchor, float reserved, NEnemyTraitChips? traits)
    {
        var layout = Layouts.GetValue(powers, key => new Layout(key));
        if (layout.Traits != null) layout.Traits.HeightChanged -= layout.HeightChanged;
        layout.Anchor = anchor; layout.Reserved = reserved; layout.Traits = traits;
        layout.TraitHeight = traits?.ReportedHeight ?? 0;
        if (traits != null) traits.HeightChanged += layout.HeightChanged;
        layout.Apply();
    }
    [HarmonyPostfix]
    public static void Postfix(NPowerContainer __instance)
    {
        if (Layouts.TryGetValue(__instance, out var layout)) layout.Apply();
    }
}

/// <summary>Native rows normally inspect IsVisible only on Add. Reconcile PRESENTATION on a local compact-mode
/// toggle; never remove/reapply a model power or alter its amount. Owner-scoped watcher is removed on exit.</summary>
[HarmonyPatch(typeof(NPowerContainer), nameof(NPowerContainer.SetCreature))]
public static class CompactPowerRowSettingsPatch
{
    private static readonly Action<NPowerContainer, PowerModel> Remove =
        AccessTools.MethodDelegate<Action<NPowerContainer, PowerModel>>(AccessTools.Method(typeof(NPowerContainer), "Remove"));
    private static readonly Action<NPowerContainer, PowerModel> Add =
        AccessTools.MethodDelegate<Action<NPowerContainer, PowerModel>>(AccessTools.Method(typeof(NPowerContainer), "Add"));

    [HarmonyPostfix]
    public static void Postfix(NPowerContainer __instance, Creature creature)
    {
        if (!creature.IsEnemy || !RunSettings.EnableRequiemSystems) return;
        var previous = RequiemConfig.CompactEnemyStatuses;
        RequiemMotion.Watch(__instance, () =>
        {
            var current = RequiemConfig.CompactEnemyStatuses;
            if (previous == current || !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsEnding) return;
            previous = current;
            foreach (var power in creature.Powers)
            {
                if (!EnemyTraits.IsTrait(power)) continue;
                Remove(__instance, power);
                Add(__instance, power); // native Add checks IsVisible under the NEW local preference
            }
        });
    }
}
