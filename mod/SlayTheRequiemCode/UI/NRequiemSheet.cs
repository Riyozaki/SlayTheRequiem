using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using SlayTheRequiem.SlayTheRequiemCode.Net;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>Local journal only. Optional actions MUST use an authoritative synchronized executor, never a UI mutation.
/// Read-only rows are focusable; refresh preserves focus/scroll. Closing/switching while an action awaits is disallowed;
/// run teardown may dismiss the view but does not pretend to cancel/roll back the authoritative operation.</summary>
public partial class NRequiemSheet : Control, IScreenContext
{
    public sealed record Entry(string Title, string Body, Texture2D? Icon = null, string Action = "", Func<Task>? Execute = null, bool Enabled = true, string Reason = "");
    public sealed record Page(string Title, IReadOnlyList<Entry> Entries);
    private Player _owner = null!;
    private Func<IReadOnlyList<Page>> _pages = null!;
    private readonly List<Button> _tabs = new(), _actions = new();
    private readonly List<Control> _rows = new();
    private Button _close = null!;
    private VBoxContainer _content = null!;
    private ScrollContainer _scroll = null!;
    private int _page;
    private bool _busy, _closing;
    private double _elapsed;
    private string _signature = "", _notice = "";
    private List<Control> FocusControls() => _tabs.Cast<Control>().Concat(_rows).Append(_close).ToList();
    public Control? DefaultFocusedControl => _busy ? _rows.FirstOrDefault() : _tabs.FirstOrDefault() ?? _close;

    public static bool Show(Player owner, string heading, Func<IReadOnlyList<Page>> pages)
    {
        if (!LocalContext.IsMe(owner) || NModalContainer.Instance is not { OpenModal: null } container
            || !ReferenceEquals(owner.RunState, RequiemNet.State)) return false;
        var screen = new NRequiemSheet { Name = "RequiemJournal", _owner = owner, _pages = pages };
        screen.Build(heading); container.Add(screen);
        if (ReferenceEquals(container.OpenModal, screen)) return true;
        screen.QueueFree(); return false;
    }

    private void Build(string heading)
    {
        SetAnchorsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop;
        var panel = RequiemKit.Panel(); AddChild(panel);
        panel.AnchorLeft = .05f; panel.AnchorRight = .95f; panel.AnchorTop = .05f; panel.AnchorBottom = .95f;
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 10); panel.AddChild(root);
        root.AddChild(NHeroCreationScreen.MakeText(heading, 30));
        var tabs = new HFlowContainer(); root.AddChild(tabs);
        var pages = _pages();
        for (var i = 0; i < pages.Count; i++)
        {
            var index = i;
            var button = RequiemKit.MakeButton(pages[i].Title, 20, new Vector2(0, 44));
            button.Pressed += () => { if (_busy || _closing) return; _page = index; Refresh(true, resetScroll: true); };
            _tabs.Add(button); tabs.AddChild(button);
        }
        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        root.AddChild(_scroll);
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 12); _scroll.AddChild(_content);
        root.AddChild(NHeroCreationScreen.MakeText(RequiemUi.TipText("UI.controls.title"), 17));
        _close = RequiemKit.MakeButton(RequiemUi.TipText("UI.close.title"), 22, new Vector2(0, 48));
        _close.Pressed += Close; root.AddChild(_close);
    }
    public override void _Ready()
    {
        Refresh(true, resetScroll: true); RequiemFocus.RestoreLater(FocusControls(), stillActive: OwnsModal);
    }
    public override void _Process(double delta)
    {
        if (_closing) return;
        if (!ReferenceEquals(_owner.RunState, RequiemNet.State) || !ReferenceEquals(NModalContainer.Instance?.OpenModal, this))
        { Dismiss(); return; }
        _elapsed += delta;
        if (_elapsed < .5 || _busy) return;
        _elapsed = 0; Refresh(false);
    }
    private void Refresh(bool force, bool resetScroll = false)
    {
        if (_busy || _closing) return;
        var pages = _pages(); if (pages.Count == 0) { Close(); return; }
        _page = Math.Clamp(_page, 0, pages.Count - 1);
        var page = pages[_page];
        var signature = _notice + string.Join("\n", page.Entries.Select(e => $"{e.Title}|{e.Body}|{e.Enabled}|{e.Reason}|{e.Action}"));
        if (!force && signature == _signature) return;
        var oldFocus = FocusControls().FindIndex(c => IsInstanceValid(c) && c.HasFocus());
        var oldOffset = resetScroll ? 0 : _scroll.ScrollVertical;
        _signature = signature; _actions.Clear(); _rows.Clear();
        foreach (var child in _content.GetChildren()) { _content.RemoveChild(child); child.QueueFree(); }
        if (_notice.Length > 0) _content.AddChild(NHeroCreationScreen.MakeText(_notice, 20));
        foreach (var entry in page.Entries)
        {
            var panel = RequiemKit.Panel(12); panel.FocusMode = FocusModeEnum.All;
            panel.AddThemeStyleboxOverride("focus", RequiemKit.Box(Colors.Transparent, RequiemKit.Gold, 3));
            _content.AddChild(panel); _rows.Add(panel);
            var row = new HBoxContainer(); panel.AddChild(row); row.AddThemeConstantOverride("separation", 12);
            if (entry.Icon != null) row.AddChild(new TextureRect { Texture = entry.Icon, CustomMinimumSize = new Vector2(52, 52),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore });
            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(text);
            text.AddChild(NHeroCreationScreen.MakeText(entry.Title, 24));
            text.AddChild(NHeroCreationScreen.MakeText(RequiemText.Plain(entry.Body), 20));
            if (entry.Reason.Length > 0) text.AddChild(NHeroCreationScreen.MakeText(entry.Reason, 18));
            if (entry.Execute == null) continue;
            var button = RequiemKit.MakeButton(entry.Action, 21, new Vector2(0, 48));
            button.Disabled = !entry.Enabled; button.TooltipText = entry.Reason;
            button.Pressed += () => MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(Execute(entry.Execute));
            text.AddChild(button); _actions.Add(button); _rows.Add(button);
        }
        for (var i = 0; i < _tabs.Count; i++)
        { _tabs[i].Modulate = i == _page ? RequiemKit.Gold : Colors.White; _tabs[i].Disabled = false; }
        _close.Disabled = false;
        var focus = FocusControls(); RequiemFocus.Wire(focus);
        if (oldFocus >= 0 && IsInsideTree()) RequiemFocus.RestoreLater(focus, oldFocus, OwnsModal);
        _scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, oldOffset);
    }
    private async Task Execute(Func<Task> action)
    {
        if (_busy || _closing || !ReferenceEquals(_owner.RunState, RequiemNet.State)) return;
        _busy = true; _notice = "";
        foreach (var b in _tabs.Concat(_actions).Append(_close)) b.Disabled = true;
        try { await action(); }
        catch (Exception e)
        {
            _notice = RequiemUi.TipText("UI.journal.actionFailed.title");
            MainFile.Logger.Error($"[Journal] Action failed: {e}");
        }
        finally
        {
            _busy = false;
            if (IsInstanceValid(this) && !_closing && IsInsideTree() && ReferenceEquals(_owner.RunState, RequiemNet.State)) Refresh(true);
        }
    }
    public override void _Input(InputEvent input)
    {
        if (_closing || !ReferenceEquals(NModalContainer.Instance?.OpenModal, this)) return;
        if (RequiemFocus.Scroll(this, input, _scroll)) return;
        RequiemFocus.Handle(this, input, _busy ? [] : FocusControls(), _busy ? null : Close);
    }
    private bool OwnsModal() => IsInstanceValid(this) && !_closing && ReferenceEquals(NModalContainer.Instance?.OpenModal, this);
    private void Close() { if (!_busy) Dismiss(); }
    private void Dismiss()
    {
        if (_closing) return; _closing = true;
        if (ReferenceEquals(NModalContainer.Instance?.OpenModal, this)) NModalContainer.Instance!.Clear();
    }
    public override void _ExitTree() { _closing = true; base._ExitTree(); }
}
