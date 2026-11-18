using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// A mandatory, scrollable decision sheet. Uses the native choice-ID/result stream, including replay/remote
/// peers; a missing/closed UI sends cancellation, never a random choice. It does not mutate a hero or own an
/// inventory transaction. Native engine/layout/controller acceptance is separate from the pure plan tests.
/// </summary>
public partial class NHeroCreationScreen : Control, IScreenContext
{
    public sealed record Choice(string Id, string Title, string Description = "", bool Enabled = true, string Group = "");
    private readonly TaskCompletionSource<int> _result = new();
    private readonly List<Button> _buttons = new();
    private readonly List<Control> _focus = new();
    private ScrollContainer _scroll = null!;
    private IRunState? _run;
    private bool _closing, _cancelable;
    public Control? DefaultFocusedControl => _buttons.FirstOrDefault(b => !b.Disabled);

    public static async Task<string> Pick(Player owner, string heading, string body, IReadOnlyList<Choice> choices, bool cancelable = false)
    {
        if (choices.Count == 0 || !choices.Any(c => c.Enabled) || choices.Select(c => c.Id).Distinct().Count() != choices.Count)
            throw new ArgumentException("A hero decision requires distinct, available choices", nameof(choices));
        var run = owner.RunState;
        void RequireCurrentRun()
        {
            if (!ReferenceEquals(run, Net.RequiemNet.State)) throw new OperationCanceledException("Hero decision belongs to an exited run");
        }
        RequireCurrentRun();
        var manager = RunManager.Instance;
        var synchronizer = manager.PlayerChoiceSynchronizer;
        var choiceId = synchronizer.ReserveChoiceId(owner); // same deterministic order on every peer
        int index;
        if (LocalContext.IsMe(owner) && manager.NetService.Type != NetGameType.Replay)
        {
            index = await ChooseLocal(owner, heading, body, choices, cancelable);
            RequireCurrentRun(); // do not send an old choice through a new run's synchronizer after quit/load
            synchronizer.SyncLocalChoice(owner, choiceId, PlayerChoiceResult.FromIndex(index));
        }
        else index = (await synchronizer.WaitForRemoteChoice(owner, choiceId)).AsIndex();
        RequireCurrentRun();
        if (index == -1 && cancelable) return "";
        if (index == -1) throw new OperationCanceledException("Hero creation UI closed; the saved draft is retained");
        if (index < 0 || index >= choices.Count || !choices[index].Enabled)
            throw new InvalidOperationException("Invalid synchronized hero creation choice; no default applied");
        return choices[index].Id;
    }

    public static async Task<int> ChooseLocal(Player owner, string heading, string body, IReadOnlyList<Choice> choices, bool cancelable = true)
    {
        var container = NModalContainer.Instance;
        if (container == null) return -1;
        // NModalContainer.Add otherwise silently drops a second modal. No timeout or automatic choice.
        while (container.OpenModal != null)
        {
            if (!IsInstanceValid(container) || container.GetTree() is not { } tree
                || !ReferenceEquals(owner.RunState, Net.RequiemNet.State)) return -1;
            await container.ToSignal(tree.CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
        }
        if (!IsInstanceValid(container) || !ReferenceEquals(owner.RunState, Net.RequiemNet.State)) return -1;
        var screen = new NHeroCreationScreen { Name = "RequiemHeroCreation", _run = owner.RunState, _cancelable = cancelable };
        screen.Build(heading, body, choices);
        container.Add(screen);
        if (!ReferenceEquals(container.OpenModal, screen)) { screen.QueueFree(); return -1; }
        return await screen._result.Task;
    }

    private void Build(string heading, string body, IReadOnlyList<Choice> choices)
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var panel = RequiemKit.Panel(18);
        AddChild(panel);
        panel.AnchorLeft = 0.08f; panel.AnchorRight = 0.92f;
        panel.AnchorTop = 0.06f; panel.AnchorBottom = 0.94f;
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        panel.AddChild(root);
        var title = MakeText(heading, 30);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(title);
        _scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        root.AddChild(_scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 12);
        _scroll.AddChild(content);
        if (body.Length > 0) content.AddChild(MakeText(RequiemText.Plain(body), 21));
        var group = "";
        for (var i = 0; i < choices.Count; i++)
        {
            var index = i;
            var choice = choices[i];
            if (choice.Group != group && choice.Group.Length > 0)
            {
                group = choice.Group;
                content.AddChild(MakeText(group, 26));
            }
            // Description belongs to the same row as its button; disabled offers remain keyboard-readable.
            var choicePanel = RequiemKit.Panel(12); choicePanel.FocusMode = FocusModeEnum.All; content.AddChild(choicePanel);
            var row = new VBoxContainer(); row.AddThemeConstantOverride("separation", 8); choicePanel.AddChild(row);
            row.AddChild(MakeText(choice.Title, 23));
            if (choice.Description.Length > 0) row.AddChild(MakeText(RequiemText.Plain(choice.Description), 20));
            var button = RequiemKit.MakeButton(RequiemUi.TipText("UI.select.title"), 22, new Vector2(0, 50));
            button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            button.TooltipText = choice.Title + (choice.Description.Length > 0 ? "\n" + choice.Description : "");
            button.Disabled = !choice.Enabled;
            button.Pressed += () => Finish(index);
            button.FocusEntered += () => _scroll.EnsureControlVisible(choicePanel);
            _buttons.Add(button); _focus.Add(choice.Enabled ? button : choicePanel);
            row.AddChild(button);
        }
        if (_cancelable)
        {
            var back = RequiemKit.MakeButton(RequiemUi.TipText("UI.back.title"), 22, new Vector2(0, 48));
            back.Pressed += () => Finish(-1); _buttons.Add(back); _focus.Add(back); root.AddChild(back);
        }
        root.AddChild(MakeText(RequiemUi.TipText(_cancelable ? "UI.controls.title" : "CREATION.controls.title"), 17));
    }

    internal static Label MakeText(string text, int size)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontOverride("font", RequiemUi.Font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", MegaCrit.Sts2.Core.Helpers.StsColors.cream);
        return label;
    }

    public override void _Ready()
    {
        RequiemFocus.Wire(_focus);
        DefaultFocusedControl?.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _Process(double delta)
    {
        if (!_closing && (!ReferenceEquals(_run, Net.RequiemNet.State)
            || !ReferenceEquals(NModalContainer.Instance?.OpenModal, this))) Finish(-1);
    }

    public override void _Input(InputEvent @event)
    {
        if (_closing || !ReferenceEquals(NModalContainer.Instance?.OpenModal, this)) return;
        if (RequiemFocus.Scroll(this, @event, _scroll)) return;
        RequiemFocus.Handle(this, @event, _focus, _cancelable ? () => Finish(-1) : null);
    }

    private void Finish(int index)
    {
        if (_closing) return;
        _closing = true;
        if (ReferenceEquals(NModalContainer.Instance?.OpenModal, this)) NModalContainer.Instance!.Clear();
        _result.TrySetResult(index);
    }

    public override void _ExitTree()
    {
        if (!_closing) _result.TrySetResult(-1); // Clear may exit the tree synchronously; it cannot override a confirmed choice
        base._ExitTree();
    }
}
