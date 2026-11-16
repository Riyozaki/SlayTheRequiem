using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using SlayTheRequiem.SlayTheRequiemCode.Acts;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Net;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Легенда карты мода (13 §4.2, M6 п. 1): пять групп значков — кнопка группы оставляет яркими только её узлы
/// (повторное нажатие снимает фильтр), — и подписи особых рёбер. Строится кодом и висит слева от ванильной легенды.
/// </summary>
public partial class NRequiemMapLegend : PanelContainer
{
    public const string NodeName = "StrMapLegend";

    private static readonly Dictionary<NodeGroup, NodeSubtype> GroupIcon = new()
    {
        [NodeGroup.Trade] = NodeSubtype.Armorer,
        [NodeGroup.Faith] = NodeSubtype.Shrine,
        [NodeGroup.Danger] = NodeSubtype.AbyssGate,
        [NodeGroup.Nature] = NodeSubtype.Herbalist,
        [NodeGroup.Magic] = NodeSubtype.Library,
    };

    private readonly Dictionary<NodeGroup, Button> _buttons = new();
    private readonly List<(Control Row, Func<bool> Shown)> _edgeRows = new();

    public static void Attach(NMapScreen screen)
    {
        if (!RunSettings.EnableRequiemSystems || screen.GetNodeOrNull(NodeName) != null) return;
        var legend = new NRequiemMapLegend();
        screen.AddChildSafely(legend);
        legend.AnchorLeft = legend.AnchorRight = 1f;
        legend.AnchorTop = legend.AnchorBottom = 1f;
        legend.OffsetLeft = -640;
        legend.OffsetRight = -380;
        legend.OffsetTop = -520;
        legend.OffsetBottom = -120;
    }

    public NRequiemMapLegend()
    {
        Name = NodeName;
        MouseFilter = MouseFilterEnum.Stop;
        var style = new StyleBoxFlat { BgColor = new Color(0.08f, 0.06f, 0.05f, 0.72f), CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 8, ContentMarginBottom = 8, BorderColor = RequiemUi.Outline, BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2 };
        AddThemeStyleboxOverride("panel", style);

        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 4);
        AddChild(box);

        var title = RequiemUi.MakeLabel(22, RequiemUi.Outline);
        title.Text = RequiemUi.TipText("MAP_LEGEND.heading");
        box.AddChild(title);

        foreach (var group in NodeRules.Groups)
        {
            var button = new Button
            {
                ToggleMode = true,
                Text = RequiemUi.TipText($"MAP_GROUP.{group.ToString().ToUpperInvariant()}.name"),
                Icon = RequiemUi.Texture(MapVisuals.IconPath(GroupIcon[group])),
                ExpandIcon = true,
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 40),
                TooltipText = string.Join(", ", NodeRules.All.Where(s => NodeRules.GroupOf(s) == group)
                    .Select(s => RequiemUi.TipText($"MAP_NODE.{NodeRules.IconName(s).ToUpperInvariant()}.name"))),
            };
            button.AddThemeFontOverride("font", RequiemUi.Font);
            button.AddThemeFontSizeOverride("font_size", 18);
            button.AddThemeConstantOverride("icon_max_width", 32);
            var g = group;
            button.Pressed += () => MapVisuals.SetFilter(g);
            _buttons[group] = button;
            box.AddChild(button);
        }

        box.AddChild(new HSeparator());
        AddEdgeRow(box, "PORTAL", MapVisuals.PortalColor, () => HasEdge(EdgeKind.Portal));
        AddEdgeRow(box, "PIPE", MapVisuals.PipeColor, () => HasEdge(EdgeKind.ValveOpen) || HasEdge(EdgeKind.ValveClose));
        AddEdgeRow(box, "PIPE_CLOSED", MapVisuals.PipeClosedColor, () => HasEdge(EdgeKind.ValveOpen) || HasEdge(EdgeKind.ValveClose));
        AddEdgeRow(box, "SECRET", MapVisuals.SecretColor, () => HasEdge(EdgeKind.Secret) && MapGraph.SecretSight(RequiemNet.State));
        AddEdgeRow(box, "DANGER", MapVisuals.DangerColor, () => HasEdge(EdgeKind.Danger));
        AddEdgeRow(box, "POCKET", MapVisuals.PocketColor, () => HasEdge(EdgeKind.PocketIn));
        AddEdgeRow(box, "LOCKED", MapVisuals.LockedColor, () => ActKinds.CurrentRule(RequiemNet.State) == ActRule.Frost);
        AddEdgeRow(box, "LAST_REST", MapVisuals.LastRestColor, () => MapGraph.Current(RequiemNet.State).LastRest != null);
    }

    private static bool HasEdge(EdgeKind kind) => MapGraph.Current(RequiemNet.State).Edges.Any(e => e.Kind == kind);

    private void AddEdgeRow(VBoxContainer box, string key, Color color, Func<bool> shown)
    {
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        var swatch = new ColorRect { Color = color, CustomMinimumSize = new Vector2(28, 6), SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore };
        var label = RequiemUi.MakeLabel(16, RequiemUi.Outline);
        label.Text = RequiemUi.TipText($"MAP_EDGE.{key}.name");
        label.HorizontalAlignment = HorizontalAlignment.Left;
        row.AddChild(swatch);
        row.AddChild(label);
        row.TooltipText = RequiemUi.TipText($"MAP_EDGE.{key}.text");
        row.MouseFilter = MouseFilterEnum.Pass;
        box.AddChild(row);
        _edgeRows.Add((row, shown));
    }

    private double _sinceRefresh = 1;

    public override void _Process(double delta)
    {
        _sinceRefresh += delta;
        if (_sinceRefresh < 0.5) return;
        _sinceRefresh = 0;
        Visible = RunSettings.EnableRequiemSystems && RequiemNet.State?.Map != null;
        if (!Visible) return;
        foreach (var (group, button) in _buttons) button.SetPressedNoSignal(MapVisuals.Filter == group);
        foreach (var (row, shown) in _edgeRows) row.Visible = shown();
    }
}
