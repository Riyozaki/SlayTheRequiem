using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Debugging;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Shops;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>Responsive, native-modal passport/recovery view. No 1040px minimum that overflows 720p/scaled UI;
/// controller/keyboard focus and the native backstop keep inputs out of the combat underneath. Captures once,
/// not every frame: inspecting a passport must not continuously contaminate a performance capture.</summary>
public partial class NRunPassportScreen : Control, IScreenContext
{
    private RunState _run = null!;
    private ScrollContainer _scroll = null!;
    private RichTextLabel _text = null!;
    private Button _retry = null!, _restore = null!, _close = null!;
    private bool _busy, _closing;
    private IReadOnlyList<Control> Controls => [_text, _retry, _restore, _close];
    public Control DefaultFocusedControl => _close;

    public static bool Show(RunState run)
    {
        if (!ReferenceEquals(RequiemNet.State, run) || NModalContainer.Instance is not { OpenModal: null } container) return false;
        var screen = new NRunPassportScreen { Name = "RequiemRunPassport", _run = run };
        screen.Build(); container.Add(screen);
        if (ReferenceEquals(container.OpenModal, screen)) return true;
        RequiemNodes.Retire(screen); return false;
    }

    private void Build()
    {
        SetAnchorsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop;
        var panel = RequiemKit.Panel(); AddChild(panel);
        panel.AnchorLeft = .05f; panel.AnchorRight = .95f; panel.AnchorTop = .05f; panel.AnchorBottom = .95f;
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 10); panel.AddChild(box);
        box.AddChild(NHeroCreationScreen.MakeText(RequiemUi.TipText("RUN_PASSPORT.title"), 27));
        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        box.AddChild(_scroll);
        _text = new RichTextLabel { BbcodeEnabled = false, FitContent = true, SelectionEnabled = true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, FocusMode = FocusModeEnum.All,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _text.AddThemeFontOverride("normal_font", RequiemUi.Font);
        _text.AddThemeFontSizeOverride("normal_font_size", 17); _scroll.AddChild(_text);
        _retry = RequiemKit.MakeButton(RequiemUi.TipText("RECOVERY_RETRY.title"), 20, new Vector2(0, 44));
        _retry.Pressed += () => TaskHelper.RunSafely(Retry()); box.AddChild(_retry);
        _restore = RequiemKit.MakeButton(RequiemUi.TipText("RECOVERY_RESTORE.title"), 20, new Vector2(0, 44), RequiemKit.Danger);
        _restore.Pressed += () =>
        {
            if (_busy || _closing || !ReferenceEquals(_run, RequiemNet.State)) return;
            Dismiss(); // release native OpenModal BEFORE OfferRestore creates its confirmation popup
            TaskHelper.RunSafely(OperationRecovery.OfferRestore());
        };
        box.AddChild(_restore);
        box.AddChild(NHeroCreationScreen.MakeText(RequiemUi.TipText("UI.controls.title"), 17));
        _close = RequiemKit.MakeButton(RequiemUi.TipText("RUN_PASSPORT_CLOSE.title"), 21, new Vector2(0, 44));
        _close.Pressed += Close; box.AddChild(_close);
        foreach (var button in new[] { _retry, _restore, _close })
        {
            button.ClipText = true; button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            button.TooltipText = button.Text;
        }
    }

    public override void _Ready()
    {
        Refresh(); RequiemFocus.RestoreLater([_close], stillActive: OwnsModal);
    }
    private void Refresh()
    {
        try { _text.Text = OperationRecovery.Describe(_run) + "\n\n" + RunDiagnostics.Describe(_run); }
        catch (Exception error)
        {
            _text.Text = RequiemUi.TipText("UI.journal.actionFailed.title");
            MainFile.Logger.Error($"[RunPassport] {error}");
        }
        _retry.Disabled = _busy || LocalContext.GetMe(_run) == null;
        _restore.Disabled = _busy || CombatManager.Instance.IsInProgress;
        _restore.TooltipText = _restore.Disabled ? RequiemUi.TipText("RECOVERY_BUSY.title") : _restore.Text;
        _close.Disabled = _busy;
        RequiemFocus.Wire(Controls);
    }
    private async Task Retry()
    {
        if (_busy || _closing || !ReferenceEquals(_run, RequiemNet.State) || LocalContext.GetMe(_run) is not { } owner) return;
        _busy = true;
        _retry.Disabled = _restore.Disabled = _close.Disabled = true;
        try { await ShopOperations.RetryReceipts(owner); }
        finally
        {
            _busy = false;
            if (IsInstanceValid(this) && IsInsideTree() && !_closing && ReferenceEquals(_run, RequiemNet.State))
            { Refresh(); RequiemFocus.RestoreLater(Controls, 1, OwnsModal); }
        }
    }
    public override void _Process(double delta)
    {
        if (!_closing && (!ReferenceEquals(_run, RequiemNet.State) || !ReferenceEquals(NModalContainer.Instance?.OpenModal, this))) Dismiss();
    }
    public override void _Input(InputEvent input)
    {
        if (_closing || !ReferenceEquals(NModalContainer.Instance?.OpenModal, this)) return;
        if (RequiemFocus.Scroll(this, input, _scroll)) return;
        RequiemFocus.Handle(this, input, _busy ? [_text] : Controls, _busy ? null : Close);
    }
    private bool OwnsModal() => IsInstanceValid(this) && !_closing && ReferenceEquals(NModalContainer.Instance?.OpenModal, this);
    private void Close() { if (!_busy) Dismiss(); }
    private void Dismiss()
    {
        if (_closing) return; _closing = true;
        if (ReferenceEquals(NModalContainer.Instance?.OpenModal, this)) NModalContainer.Instance!.Clear();
        else RequiemNodes.Retire(this);
    }
    public override void _ExitTree() { _closing = true; base._ExitTree(); }
}
