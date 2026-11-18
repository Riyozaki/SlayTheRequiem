using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Net;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>Scrollable category grid WITHOUT reparenting native buttons: native disable/selection/synchronizer and
/// DefaultFocusedControl still see their direct children. No 0.42-scale unreadable fit-to-all fallback.</summary>
public partial class NCampChoices : Container
{
    public enum Category { Rest, Craft, Magic, Gear, Special }
    private const float LabelHeight = 36, Gap = 18, CostHeight = 32;
    private readonly Dictionary<NRestSiteButton, Vector2> _positions = new();
    private readonly Dictionary<Category, float> _categories = new();
    private readonly HashSet<ulong> _wired = new();
    private readonly List<(NRestSiteButton Button, int Cost)> _costs = new();
    private float _contentHeight, _offset;
    internal VScrollBar ScrollBar { get; set; } = null!;

    public static Category CategoryOf(RestSiteOption option) => option.OptionId.ToUpperInvariant().Replace("SLAYTHEREQUIEM_", "") switch
    {
        "HEAL" or "SHORT_REST" or "MEDITATE" or "PRAY" or "WARM" => Category.Rest,
        "SMITH" or "FORGE" or "BREW" or "COOK" or "DIG" or "MEND" => Category.Craft,
        "ENCHANT" or "CARD_ENCHANT" => Category.Magic,
        "CHEST" or "TRAIN" or "LIFT" => Category.Gear,
        _ => Category.Special,
    };
    internal static string CategoryTitle(Category category) => category switch
    {
        Category.Rest => RequiemUi.TipText("CAMP_CAT_REST.title"),
        Category.Craft => RequiemUi.TipText("CAMP_CAT_CRAFT.title"),
        Category.Magic => RequiemUi.TipText("CAMP_CAT_MAGIC.title"),
        Category.Gear => RequiemUi.TipText("CAMP_CAT_GEAR.title"),
        _ => RequiemUi.TipText("CAMP_CAT_SPECIAL.title"),
    };
    public override void _Notification(int what) { if (what == NotificationSortChildren) Arrange(); }
    public override void _Ready() { ClipContents = true; MouseFilter = MouseFilterEnum.Pass; }
    public override void _GuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true } wheel) return;
        if (wheel.ButtonIndex is not (MouseButton.WheelDown or MouseButton.WheelUp)) return;
        SetOffset(_offset + (wheel.ButtonIndex == MouseButton.WheelDown ? 100 : -100)); AcceptEvent();
    }
    internal void SetOffset(double value)
    {
        _offset = (float)Math.Clamp(value, 0, Math.Max(0, _contentHeight - Size.Y));
        foreach (var (button, position) in _positions)
            if (IsInstanceValid(button) && !button.IsQueuedForDeletion()) button.Position = position - new Vector2(0, _offset);
        if (ScrollBar != null && IsInstanceValid(ScrollBar) && ScrollBar.Value != _offset) ScrollBar.SetValueNoSignal(_offset);
        QueueRedraw();
    }
    internal void GoTo(Category category)
    {
        if (!_categories.TryGetValue(category, out var top)) return;
        SetOffset(top);
        _positions.Keys.FirstOrDefault(b => CategoryOf(b.Option) == category)?.GrabFocus();
    }
    private void Reveal(NRestSiteButton button)
    {
        if (_positions.TryGetValue(button, out var p))
            SetOffset(UiLayoutRules.ScrollIntoView(_offset, p.Y - LabelHeight, p.Y + button.Size.Y + CostHeight, Size.Y, _contentHeight));
    }
    private void Arrange()
    {
        var buttons = GetChildren().OfType<NRestSiteButton>().Where(b => !b.IsQueuedForDeletion() && b.Option != null).ToList();
        _positions.Clear(); _categories.Clear(); _costs.Clear();
        if (buttons.Count == 0) { _contentHeight = 0; SetOffset(0); return; }
        var baseSize = new Vector2(Math.Max(190, buttons.Max(b => Math.Max(b.Size.X, b.GetCombinedMinimumSize().X))),
            Math.Max(210, buttons.Max(b => Math.Max(b.Size.Y, b.GetCombinedMinimumSize().Y))));
        var columns = UiLayoutRules.Columns(Size.X, baseSize.X, Gap);
        var y = 0f;
        foreach (var group in buttons.GroupBy(b => CategoryOf(b.Option)).OrderBy(g => g.Key))
        {
            _categories[group.Key] = y; y += LabelHeight;
            var list = group.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                var button = list[i]; var col = i % columns; var row = i / columns;
                var rowCount = Math.Min(columns, list.Count - row * columns);
                var x = (Size.X - rowCount * baseSize.X - (rowCount - 1) * Gap) / 2 + col * (baseSize.X + Gap);
                var position = new Vector2(x, y + row * (baseSize.Y + CostHeight + Gap));
                _positions[button] = position; button.Size = baseSize; button.Scale = Vector2.One;
                var cost = button.Option switch { RequiemRestSiteOption own => own.Cost, ForeignRestSiteOption foreign => foreign.Cost, _ => 0 };
                _costs.Add((button, cost));
                button.TooltipText = RequiemText.Plain(button.Option.Description.GetFormattedText()) + "\n"
                    + RequiemUi.TipText("UI.campCost.title", ("Cost", cost));
                if (_wired.Add(button.GetInstanceId())) button.FocusEntered += () => Reveal(button);
            }
            y += (float)Math.Ceiling(list.Count / (double)columns) * (baseSize.Y + CostHeight + Gap);
        }
        _contentHeight = y;
        if (ScrollBar != null && IsInstanceValid(ScrollBar))
        { ScrollBar.MaxValue = Math.Max(Size.Y, y); ScrollBar.Page = Size.Y; ScrollBar.Visible = y > Size.Y; }
        SetOffset(_offset);
        // Native UpdateRestSiteOptions wires a horizontal row. Replace AFTER sort with spatial up/down neighbors.
        var ordered = _positions.Keys.OrderBy(b => _positions[b].Y).ThenBy(b => _positions[b].X).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var button = ordered[i]; var p = _positions[button];
            button.FocusNeighborLeft = ordered[(i + ordered.Count - 1) % ordered.Count].GetPath();
            button.FocusNeighborRight = ordered[(i + 1) % ordered.Count].GetPath();
            NRestSiteButton Neighbor(bool up) => ordered.Where(b => up ? _positions[b].Y < p.Y : _positions[b].Y > p.Y)
                .OrderBy(b => Math.Abs(_positions[b].Y - p.Y)).ThenBy(b => Math.Abs(_positions[b].X - p.X)).FirstOrDefault()
                ?? ordered[up ? ordered.Count - 1 : 0];
            button.FocusNeighborTop = Neighbor(true).GetPath(); button.FocusNeighborBottom = Neighbor(false).GetPath();
        }
    }
    public override void _Draw()
    {
        var font = RequiemUi.Font;
        foreach (var (category, top) in _categories)
        {
            var y = top - _offset + 25;
            DrawString(font, new Vector2(0, y), CategoryTitle(category), HorizontalAlignment.Center, Size.X, 23, StsColors.gold);
        }
        foreach (var (button, cost) in _costs)
        {
            if (!_positions.TryGetValue(button, out var p)) continue;
            var text = RequiemUi.TipText("UI.campCost.title", ("Cost", cost));
            DrawString(font, new Vector2(p.X, p.Y - _offset + button.Size.Y + 23), text,
                HorizontalAlignment.Center, button.Size.X, 20, button.Option.IsEnabled ? StsColors.gold : RequiemKit.Dim);
        }
    }
}

/// <summary>Header is a sibling: the vanilla engine may delete every child of _choicesContainer on refresh.</summary>
public partial class NCampHeader : VBoxContainer
{
    internal NCampChoices Menu = null!;
    private Label _points = null!;
    public override void _Ready()
    {
        _points = NHeroCreationScreen.MakeText("", 23); AddChild(_points);
        var row = new HFlowContainer(); AddChild(row);
        foreach (var cat in Enum.GetValues<NCampChoices.Category>())
        {
            var category = cat;
            var button = RequiemKit.MakeButton(NCampChoices.CategoryTitle(cat), 19, new Vector2(0, 40));
            button.Pressed += () => Menu.GoTo(category); row.AddChild(button);
        }
    }
    public override void _Process(double delta)
    {
        if (RequiemNet.State is { } run && LocalContext.GetMe(run) is { } owner)
            _points.Text = RequiemUi.TipText("UI.campPoints.title", ("Points", Camp.Points(owner)), ("Budget", Camp.Budget(owner)));
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), "UpdateRestSiteOptions")]
public static class CampChoicesPatch
{
    private static readonly AccessTools.FieldRef<NRestSiteRoom, Control> ContainerRef = AccessTools.FieldRefAccess<NRestSiteRoom, Control>("_choicesContainer");
    private static readonly AccessTools.FieldRef<NRestSiteRoom, Control> ScreenRef = AccessTools.FieldRefAccess<NRestSiteRoom, Control>("_choicesScreen");
    [HarmonyPrefix]
    public static void Prefix(NRestSiteRoom __instance)
    {
        try
        {
            if (!RunSettings.EnableRequiemSystems) return;
            var old = ContainerRef(__instance);
            if (old == null || old is NCampChoices || old.GetParent() is not Control parent) return;
            if (parent is Container) parent = ScreenRef(__instance) ?? parent;
            if (parent is Container) return;
            var menu = new NCampChoices { Name = "RequiemCampChoices" };
            parent.AddChild(menu);
            menu.AnchorLeft = .04f; menu.AnchorRight = .94f; menu.AnchorTop = .32f; menu.AnchorBottom = .79f;
            var bar = new VScrollBar { AnchorLeft = .95f, AnchorRight = .965f, AnchorTop = .32f, AnchorBottom = .79f };
            parent.AddChild(bar); menu.ScrollBar = bar; bar.ValueChanged += menu.SetOffset;
            var header = new NCampHeader { Name = "RequiemCampHeader", Menu = menu };
            parent.AddChild(header);
            header.AnchorLeft = .05f; header.AnchorRight = .95f; header.AnchorTop = .17f; header.AnchorBottom = .30f;
            foreach (var child in old.GetChildren()) child.QueueFree();
            old.Visible = false; ContainerRef(__instance) = menu;
        }
        catch (Exception e) { MainFile.Logger.Warn($"[CampUI] {e}"); }
    }
}
