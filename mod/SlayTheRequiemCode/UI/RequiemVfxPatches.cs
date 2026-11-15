using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Удар по типу урона (M7 п. 4): игра записывает удар в историю боя сразу после расчёта — тип выводится тем же
/// <see cref="DamageTyping.Resolve"/>, что и сопротивления и цвет числа урона, и поверх ванильного эффекта атаки
/// встаёт вспышка стихии (<see cref="RequiemVfx.Hit"/>).
/// </summary>
[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.DamageReceived))]
public static class HitFxPatch
{
    [HarmonyPostfix]
    public static void Postfix(Creature receiver, Creature? dealer, DamageResult result, CardModel? cardSource)
    {
        if (!RequiemConfig.EnableRequiemSystems || !RequiemVfx.Enabled || RequiemVfx.IsQuiet || result == null || receiver == null) return;
        var type = DamageTyping.Resolve(result.Props, dealer, cardSource);
        RequiemVfx.Hit(receiver, type, result.Props.IsPoweredAttack(), result.UnblockedDamage + result.BlockedDamage);
    }
}

/// <summary>«Уворот!» — ванильная табличка «Заблокировано», помеченная <see cref="DodgeText"/>: после её _Ready текст подменяется.</summary>
[HarmonyPatch(typeof(NDamageBlockedVfx), nameof(NDamageBlockedVfx._Ready))]
public static class DodgeTextPatch
{
    [HarmonyPostfix]
    public static void Postfix(NDamageBlockedVfx __instance)
    {
        if (!DodgeText.IsMarked(__instance)) return;
        try
        {
            __instance.GetNodeOrNull<MegaLabel>("Label")?.SetTextAutoSize(RequiemUi.TipText(VfxRules.DodgeTextKey));
            __instance.Modulate = new Color(VfxRules.ShoutTint(ShoutFx.Rally));
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"[SlayTheRequiem] VFX dodge text: {e.Message}");
        }
    }
}

/// <summary>Багровый портал на карте: узел второго босса акта, если там супербосс, пульсирует багровым.</summary>
[HarmonyPatch(typeof(NBossMapPoint), nameof(NBossMapPoint._Ready))]
public static class PortalPulsePatch
{
    private static readonly AccessTools.FieldRef<NMapPoint, IRunState> RunStateRef = AccessTools.FieldRefAccess<NMapPoint, IRunState>("_runState");

    [HarmonyPostfix]
    public static void Postfix(NBossMapPoint __instance)
    {
        try
        {
            var run = RunStateRef(__instance);
            if (run?.Map == null) return;
            var second = run.Map.SecondBossMapPoint != null && __instance.Point == run.Map.SecondBossMapPoint;
            if (!VfxRules.PortalPulse(second, run.Act.SecondBossEncounter is SuperbossEncounter)) return;
            if (__instance.GetNodeOrNull<Node2D>("%SpriteContainer") is { } container) RequiemVfx.PortalPulse(container);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"[SlayTheRequiem] VFX portal pulse: {e.Message}");
        }
    }
}

/// <summary>Вход к супербоссу: красная вспышка и багровая дымка при открытии боя.</summary>
[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom._Ready))]
public static class PortalEnterPatch
{
    private static readonly AccessTools.FieldRef<NCombatRoom, ICombatRoomVisuals> VisualsRef =
        AccessTools.FieldRefAccess<NCombatRoom, ICombatRoomVisuals>("_visuals");

    [HarmonyPostfix]
    public static void Postfix(NCombatRoom __instance)
    {
        try
        {
            if (__instance.Mode != CombatRoomMode.ActiveCombat || VisualsRef(__instance)?.Encounter is not SuperbossEncounter) return;
            RequiemVfx.PortalEnter(__instance);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"[SlayTheRequiem] VFX portal enter: {e.Message}");
        }
    }
}
