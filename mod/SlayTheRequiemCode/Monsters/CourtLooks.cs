using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

/// <summary>
/// Перекраска свиты супербоссов (M7 п. 1): ванильная модель (Парафрайт, Потерянный, Шумобот…) получает цвет фракции хозяина —
/// сдвиг тона hsv-шейдера Spine (<see cref="NCreatureVisuals.SetScaleAndHue"/>) и множитель Modulate (<see cref="SuperbossLookRules.TintFor"/>).
/// Игра у одинаковых врагов в комнате выставляет случайный тон 0–0,05 (NCombatRoom.RandomizeEnemyScalesAndHues) — наш тон
/// складывается с ним (<see cref="SuperbossLookRules.CombinedHue"/>), чтобы три гаргульи остались кровавыми, но чуть разными.
/// </summary>
public static class CourtLooks
{
    public static CourtTint? TintOf(Creature? creature) =>
        creature?.Monster is RequiemMinion minion && SuperbossLookRules.CourtOf(minion.GetType().Name) is { } boss
            ? SuperbossLookRules.TintFor(boss)
            : null;

    public static void Apply(NCreature? node, float gameHue = 0f)
    {
        if (node == null || !GodotObject.IsInstanceValid(node) || TintOf(node.Entity) is not { } tint) return;
        var visuals = node.Visuals;
        if (visuals == null || OwnArtAnimator.IsOwnArt(visuals)) return;
        if (!visuals.IsNodeReady())
        {
            // SpineBody появляется в _Ready визуала — до него шейдер тона некуда ставить.
            visuals.Ready += () => Apply(node, gameHue);
            return;
        }
        try
        {
            visuals.SetScaleAndHue(visuals.DefaultScale, SuperbossLookRules.CombinedHue(tint.Hue, gameHue));
            visuals.Modulate = new Color(tint.R, tint.G, tint.B);
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"CourtLooks: {e.Message}");
        }
    }
}

/// <summary>Свита появилась (в начале боя или призвана) — красим в цвет хозяина.</summary>
[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature))]
public static class CourtTintAddPatch
{
    [HarmonyPostfix]
    public static void Postfix(NCombatRoom __instance, Creature creature)
    {
        if (CourtLooks.TintOf(creature) != null) CourtLooks.Apply(__instance.GetCreatureNode(creature));
    }
}

/// <summary>Разброс тона одинаковых врагов от игры перезаписал бы наш — возвращаем цвет фракции поверх него.</summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetScaleAndHue))]
public static class CourtTintHuePatch
{
    [HarmonyPostfix]
    public static void Postfix(NCreature __instance, float hue)
    {
        if (CourtLooks.TintOf(__instance.Entity) != null) CourtLooks.Apply(__instance, hue);
    }
}
