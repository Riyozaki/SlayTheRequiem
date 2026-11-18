using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Acts;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Net;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Свой вид карты (M6 п. 1, 13 §4, art spec §3.8): значки 20 подтипов узлов на ванильных узлах, Тьма курганов,
/// окраска особых рёбер (порталы, трубы вентилей, Тайная и Опасная тропа, карманы, Запертые врата) и фильтр легенды.
/// Узлы и рёбра — ванильные (<c>NNormalMapPoint</c>, точки пути <c>NMapScreen._paths</c>), мод меняет текстуры и цвета.
/// </summary>
public static class MapVisuals
{
    private static readonly AccessTools.FieldRef<NMapScreen, Dictionary<(MapCoord, MapCoord), IReadOnlyList<TextureRect>>> Paths =
        AccessTools.FieldRefAccess<NMapScreen, Dictionary<(MapCoord, MapCoord), IReadOnlyList<TextureRect>>>("_paths");

    private static readonly AccessTools.FieldRef<NMapScreen, Dictionary<MapCoord, NMapPoint>> Points =
        AccessTools.FieldRefAccess<NMapScreen, Dictionary<MapCoord, NMapPoint>>("_mapPointDictionary");

    private static readonly AccessTools.FieldRef<NNormalMapPoint, TextureRect> Icon = AccessTools.FieldRefAccess<NNormalMapPoint, TextureRect>("_icon");
    private static readonly AccessTools.FieldRef<NNormalMapPoint, TextureRect> Outline = AccessTools.FieldRefAccess<NNormalMapPoint, TextureRect>("_outline");
    private static readonly AccessTools.FieldRef<NNormalMapPoint, Control> IconContainer = AccessTools.FieldRefAccess<NNormalMapPoint, Control>("_iconContainer");

    // ───── цвета рёбер (art spec §1.1: синие — магия, красные — опасность, медь — Глубины) ─────
    public static readonly Color PortalColor = new("B080FF");
    public static readonly Color SecretColor = new("9FE8FF");
    public static readonly Color DangerColor = new("E05040");
    public static readonly Color PocketColor = new("E8C060");
    public static readonly Color PipeColor = new("D08A40");
    public static readonly Color PipeClosedColor = new(0.45f, 0.18f, 0.12f, 0.45f);
    public static readonly Color LockedColor = new(0.55f, 0.62f, 0.72f, 0.5f);
    public static readonly Color LastRestColor = new("C9A8FF");

    /// <summary>Фильтр легенды: показывать только эту группу (null — все).</summary>
    public static NodeGroup? Filter { get; private set; }

    public static void SetFilter(NodeGroup? group)
    {
        Filter = Filter == group ? null : group;
        RefreshSoon();
    }

    public static string IconPath(NodeSubtype subtype) => $"{MainFile.ResPath}/images/map/icons/{NodeRules.IconName(subtype)}.png";
    public static string OutlinePath(NodeSubtype subtype) => $"{MainFile.ResPath}/images/map/icons/{NodeRules.IconName(subtype)}_outline.png";

    // Keep the native image underneath the overlay. Darkness, legend changes and Continue can restyle a
    // point WITHOUT another UpdateIcon call; leaving our previous texture/hidden outline behind is stale UI.
    private sealed record NativeIcon(Texture2D? Icon, Texture2D? Outline, bool OutlineVisible);
    private static readonly ConditionalWeakTable<NNormalMapPoint, NativeIcon> NativeIcons = new();

    internal static void RestoreNative(NNormalMapPoint node)
    {
        if (!NativeIcons.TryGetValue(node, out var original)) return;
        if (Icon(node) is { } icon) icon.Texture = original.Icon;
        if (Outline(node) is { } outline) { outline.Texture = original.Outline; outline.Visible = original.OutlineVisible; }
    }

    internal static void RememberNativeAndApply(NNormalMapPoint node)
    {
        if (Icon(node) is { } icon && Outline(node) is { } outline)
            NativeIcons.AddOrUpdate(node, new NativeIcon(icon.Texture, outline.Texture, outline.Visible));
        ApplyIcon(node);
    }

    private static bool _queued;

    /// <summary>Сброс при новом забеге: отложенная перерисовка могла не выполниться (ревью F-06).</summary>
    internal static void ResetForNewRun() => _queued = false;

    /// <summary>Перерисовать карту в следующем кадре (состояние вентилей, ключей, зрения изменилось).</summary>
    public static void RefreshSoon()
    {
        if (_queued || NMapScreen.Instance is not { } screen || !GodotObject.IsInstanceValid(screen)) return;
        _queued = true;
        Callable.From(() =>
        {
            _queued = false;
            if (NMapScreen.Instance is { } s && GodotObject.IsInstanceValid(s)) Restyle(s);
        }).CallDeferred();
    }

    /// <summary>Окраска рёбер и значки всех узлов.</summary>
    public static void Restyle(NMapScreen screen)
    {
        var run = RequiemNet.State;
        if (run?.Map == null || !RunSettings.EnableRequiemSystems) return;
        try
        {
            StylePaths(screen, run);
            foreach (var node in Points(screen).Values.OfType<NNormalMapPoint>()) ApplyIcon(node, run);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Карта: оформление не удалось ({e.Message}).");
        }
    }

    private static void StylePaths(NMapScreen screen, RunState run)
    {
        var plan = MapGraph.Current(run);
        var state = MapGraph.StateOf(run);
        var visited = run.VisitedMapCoords;
        var traveled = new HashSet<(MapCoord, MapCoord)>();
        for (var i = 0; i + 1 < visited.Count; i++) traveled.Add((visited[i], visited[i + 1]));
        foreach (var ((a, b), dots) in Paths(screen))
        {
            var from = run.Map.GetPoint(a);
            var to = run.Map.GetPoint(b);
            if (from == null || to == null || traveled.Contains((a, b))) continue;
            var edge = plan.EdgeOf(MapGraph.CellOf(from), MapGraph.CellOf(to));
            var visible = MapGraphRules.IsVisible(edge, state.SecretSight);
            var blocked = MapGraph.Blocked(run, from, to, state);
            Color? tint = edge?.Kind switch
            {
                EdgeKind.Portal => PortalColor,
                EdgeKind.Secret => SecretColor,
                EdgeKind.Danger => DangerColor,
                EdgeKind.PocketIn or EdgeKind.PocketOut => PocketColor,
                EdgeKind.ValveOpen or EdgeKind.ValveClose => blocked ? PipeClosedColor : PipeColor,
                _ => blocked ? LockedColor : null,
            };
            foreach (var dot in dots)
            {
                if (!GodotObject.IsInstanceValid(dot)) continue;
                dot.Visible = visible;
                if (tint is { } c) dot.Modulate = c;
            }
        }
    }

    /// <summary>
    /// Подтип, видимый на узле: отметка акта (Засада — только с Ночным зрением) или записанный при входе (лавка, Таверна, «?»).
    /// </summary>
    public static NodeSubtype? VisibleSubtype(IRunState run, MapPoint point)
    {
        if (ActMarks.SubtypeAt(run, point.coord) is { } marked)
            return NodeRules.RevealOf(marked) != NodeReveal.Hidden || MapGraph.NightSight(run) ? marked : null;
        var code = MapMarks.Encode(run.CurrentActIndex, point.coord.col, point.coord.row);
        foreach (var ledger in ActRunState.Records(run))
            if (ledger.VisitedSubtype(code) is { } visited) return visited;
        return null;
    }

    /// <summary>Значок узла: Тьма → силуэт; подтип → свой значок; фильтр легенды → приглушить чужие группы.</summary>
    public static void ApplyIcon(NNormalMapPoint node, IRunState? run = null)
    {
        if (!GodotObject.IsInstanceValid(node)) return;
        RestoreNative(node);
        run ??= RequiemNet.State;
        if (run?.Map == null || !RunSettings.EnableRequiemSystems) return;
        var icon = Icon(node);
        var container = IconContainer(node);
        if (icon == null || container == null) return;
        var point = node.Point;
        var currentRow = run.CurrentMapCoord?.row ?? 0;
        var dark = ActKinds.CurrentRule(run) == ActRule.Frost && node.State != MapPointState.Traveled
                   && ActRules.IsHiddenByDarkness(point.coord.row, currentRow, MapGraph.NightSight(run));
        NodeSubtype? subtype = dark ? null : VisibleSubtype(run, point);
        if (dark)
        {
            icon.Texture = RequiemUi.Texture(ImageHelper.GetImagePath("atlases/ui_atlas.sprites/map/icons/map_unknown.tres")) ?? icon.Texture;
            if (Outline(node) is { } hiddenOutline)
                hiddenOutline.Texture = RequiemUi.Texture(ImageHelper.GetImagePath("atlases/compressed.sprites/map/map_unknown_outline.tres")) ?? hiddenOutline.Texture;
            node.TooltipText = RequiemUi.TipText("MAP_DARK.text");
        }
        else if (subtype is { } sub && RequiemUi.Texture(IconPath(sub)) is { } tex
                 && RequiemUi.Texture(OutlinePath(sub)) is { } mask)
        {
            icon.Texture = tex;
            // Native hover/press/selection still tint this alpha mask; do not remove the only outline feedback.
            if (Outline(node) is { } outline) { outline.Texture = mask; outline.Visible = true; }
            node.TooltipText = RequiemUi.TipText($"MAP_NODE.{NodeRules.IconName(sub).ToUpperInvariant()}.name") + "\n"
                               + RequiemUi.TipText($"MAP_NODE.{NodeRules.IconName(sub).ToUpperInvariant()}.text");
        }
        else if (MapGraph.IsLastRest(run, point.coord))
            node.TooltipText = RequiemUi.TipText("MAP_LAST_REST.name") + "\n" + RequiemUi.TipText("MAP_LAST_REST.text");
        else if (MapGraph.IsPocket(run, point.coord))
            node.TooltipText = RequiemUi.TipText("MAP_POCKET.text");
        else
            node.TooltipText = string.Empty;

        var alpha = dark ? 0.35f : 1f;
        if (Filter is { } group && (subtype is not { } s2 || NodeRules.GroupOf(s2) != group)) alpha = Math.Min(alpha, 0.3f);
        var tint = MapGraph.IsLastRest(run, point.coord) && !dark ? LastRestColor : Colors.White;
        container.Modulate = new Color(tint, alpha);
    }
}

/// <summary>Своя карта: после построения карты — окраска рёбер и значки; кнопки легенды.</summary>
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetMap))]
public static class MapScreenSetMapPatch
{
    [HarmonyPostfix]
    public static void Postfix(NMapScreen __instance) => MapVisuals.Restyle(__instance);
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
public static class MapScreenOpenPatch
{
    [HarmonyPostfix]
    public static void Postfix(NMapScreen __instance)
    {
        NRequiemMapLegend.Attach(__instance);
        MapVisuals.Restyle(__instance);
    }
}

/// <summary>Значок узла после ванильного обновления (смена состояния, прохождение).</summary>
[HarmonyPatch(typeof(NNormalMapPoint), "UpdateIcon")]
public static class MapPointIconPatch
{
    [HarmonyPrefix]
    public static void Prefix(NNormalMapPoint __instance) => MapVisuals.RestoreNative(__instance);

    [HarmonyPostfix]
    public static void Postfix(NNormalMapPoint __instance) => MapVisuals.RememberNativeAndApply(__instance);
}
