using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>Actual public Godot 4.5.1 scene-tree/focus smoke using production helpers, WITHOUT the game.
/// No art approval, rendered-layout test, native creature hook, window-cycle performance or mod build is claimed.</summary>
public partial class PresentationSmoke : Node
{
    private readonly List<string> _passed = [];
    private readonly List<Node> _owned = [];
    private const int Cycles = 100;
    private static void Require(bool value, string why)
    {
        if (!value) throw new InvalidOperationException(why);
    }
    private async Task Frame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private T Own<T>(T node) where T : Node
    {
        _owned.Add(node); AddChild(node); return node;
    }
    private async Task Case(string id, Func<Task> action)
    {
        await action();
        foreach (var node in _owned) RequiemNodes.Retire(node);
        _owned.Clear();
        await Frame(); await Frame();
        _passed.Add(id); GD.Print("PASS " + id);
    }
    private Control Screen() => Own(new Control { Size = new Vector2(1200, 650) });
    private static Button Button(Control screen, string name, bool disabled = false, bool hidden = false)
    {
        var button = new Button { Name = name, Text = name, Disabled = disabled, Visible = !hidden,
            FocusMode = Control.FocusModeEnum.All, Position = new Vector2(20, 30 + screen.GetChildCount() * 50), Size = new Vector2(200, 40) };
        screen.AddChild(button); return button;
    }

    public override async void _Ready()
    {
        try
        {
            await Run();
            var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "../../.."));
            var files = new[] { "mod/SlayTheRequiemCode/UI/RequiemNodes.cs", "mod/SlayTheRequiemCode/UI/RequiemFocus.cs",
                "tools/ci/Presentation/PresentationSmoke.cs", "tools/ci/Presentation/Presentation.csproj",
                "tools/ci/Presentation/project.godot", "tools/ci/Presentation/PresentationSmoke.tscn",
                "tools/ci/presentation_smoke.py", ".github/workflows/ci.yml" };
            var hashes = files.ToDictionary(file => file, file => Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(System.IO.Path.Combine(root, file)))).ToLowerInvariant());
            var result = new
            {
                schema = 1, producer = "actual-godot-presentation-helper-smoke", source_commit = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "working-tree",
                scope = "public-Godot-NOT-MegaDot-NOT-full-mod-build-NOT-native-game-NOT-FPS-NOT-art-acceptance",
                engine = Engine.GetVersionInfo()["string"].AsString(), godot_executable_sha256 = Environment.GetEnvironmentVariable("STR_ENGINE_SHA256"),
                source_sha256 = hashes, passed = _passed,
                same_frame_replacement_cycles = Cycles, child_clear_cycles = Cycles, actual_native_runs = 0,
            };
            var json = JsonSerializer.Serialize(result);
            var output = Environment.GetEnvironmentVariable("STR_PRESENTATION_REPORT");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set STR_PRESENTATION_REPORT to a fresh private/ignored output file");
            if (System.IO.File.Exists(output)) throw new InvalidOperationException("Smoke output must be fresh");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(output))!);
            System.IO.File.WriteAllText(output, json + "\n");
            Require(json.Length <= 3500, "Smoke notice exceeds bounded annotation budget");
            GD.Print(json);
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError("Presentation helper smoke failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task Run()
    {
        await Case("NODE-01-immediate-named-replacement-100", async () =>
        {
            var parent = Own(new Node2D());
            for (var cycle = 0; cycle < Cycles; cycle++)
            {
                var old = RequiemNodes.Find<Node2D>(parent, "Overlay");
                RequiemNodes.Retire(old);
                var next = new Node2D { Name = "Overlay" }; parent.AddChild(next);
                Require(next.Name.ToString() == "Overlay" && parent.GetChildCount() == 1
                    && ReferenceEquals(parent.GetNode<Node2D>("Overlay"), next), "Replacement was renamed or old overlay still attached");
                if (old != null) Require(old.GetParent() == null && old.IsQueuedForDeletion(), "Old overlay was not detached/retired immediately");
            }
            await Frame();
            Require(parent.GetChildCount() == 1, "Deferred deletion removed the replacement instead");
        });
        await Case("NODE-02-never-reuse-queued-child", async () =>
        {
            var parent = Own(new Node2D());
            var old = new Node2D { Name = "Aura" }; parent.AddChild(old); old.QueueFree();
            Require(RequiemNodes.Find<Node2D>(parent, "Aura") == null && parent.GetChildCount() == 0, "Queued node reused");
            var next = new Node2D { Name = "Aura" }; parent.AddChild(next);
            await Frame(); await Frame();
            Require(!GodotObject.IsInstanceValid(old) && next.Name.ToString() == "Aura", "Queued generation still alive or name collision");
        });
        await Case("NODE-03-clear-children-100-and-exit-signals", () =>
        {
            var parent = Own(new Node2D()); var exits = 0;
            for (var cycle = 0; cycle < Cycles; cycle++)
            {
                for (var index = 0; index < 3; index++)
                {
                    var child = new Node2D { Name = "Orb" + index };
                    child.TreeExiting += () => exits++;
                    parent.AddChild(child);
                }
                Require(parent.GetChildCount() == 3, "Old orb generation remained attached");
                RequiemNodes.Clear(parent);
                Require(parent.GetChildCount() == 0 && exits == (cycle + 1) * 3, "Clear/TreeExiting was deferred");
            }
            return Task.CompletedTask;
        });
        await Case("NODE-04-idempotent-null-queued-and-freed-retirement", async () =>
        {
            RequiemNodes.Retire(null);
            var child = Own(new Node2D());
            RequiemNodes.Retire(child); RequiemNodes.Retire(child);
            await Frame(); await Frame();
            Require(!GodotObject.IsInstanceValid(child), "Retired node was not freed");
            RequiemNodes.Retire(child);
        });
        await Case("FOCUS-01-wire-only-live-enabled-visible-controls", () =>
        {
            var screen = Screen(); var a = Button(screen, "A"); var disabled = Button(screen, "Disabled", disabled: true);
            var b = Button(screen, "B"); var hidden = Button(screen, "Hidden", hidden: true);
            RequiemFocus.Wire([a, disabled, hidden, b]);
            Require(a.FocusNeighborBottom == b.GetPath() && b.FocusNeighborBottom == a.GetPath(), "Disabled/hidden target in focus ring");
            return Task.CompletedTask;
        });
        await Case("FOCUS-02-restore-skips-disabled-hidden-rows", async () =>
        {
            var screen = Screen(); var disabled = Button(screen, "Disabled", disabled: true);
            var hidden = Button(screen, "Hidden", hidden: true); var target = Button(screen, "Target");
            RequiemFocus.RestoreLater([disabled, hidden, target]);
            await Frame(); await Frame();
            Require(target.HasFocus(), "Focus lost after selected offer became disabled");
        });
        await Case("FOCUS-03-ignore-retired-deferred-target", async () =>
        {
            var screen = Screen(); var old = Button(screen, "Old"); var target = Button(screen, "Target");
            RequiemFocus.RestoreLater([old, target]); RequiemNodes.Retire(old);
            await Frame(); await Frame();
            Require(target.HasFocus() && !GodotObject.IsInstanceValid(old), "Deferred focus targeted retired row");
        });
        await Case("FOCUS-04-empty-and-ineligible-ring-does-not-steal-focus", async () =>
        {
            var screen = Screen(); var keep = Button(screen, "Keep"); var disabled = Button(screen, "Disabled", disabled: true);
            keep.GrabFocus(); RequiemFocus.RestoreLater([]); RequiemFocus.RestoreLater([disabled], 99);
            await Frame(); await Frame();
            Require(keep.HasFocus(), "Ineligible refresh stole unrelated focus");
        });
        await Case("FOCUS-05-ignore-children-of-queued-modal", async () =>
        {
            var oldScreen = Screen(); var old = Button(oldScreen, "Old");
            var activeScreen = Screen(); var active = Button(activeScreen, "Active");
            active.GrabFocus();
            RequiemFocus.RestoreLater([old]); oldScreen.QueueFree();
            Require(!old.IsQueuedForDeletion(), "Fixture no longer exercises the queued-ancestor distinction");
            await Frame(); await Frame();
            Require(active.HasFocus(), "Child of queued modal stole focus");
        });
        await Case("FOCUS-06-owner-change-cancels-deferred-restore", async () =>
        {
            var screen = Screen(); var old = Button(screen, "Old"); var active = Button(screen, "Active");
            var stillActive = true;
            RequiemFocus.RestoreLater([old], stillActive: () => stillActive);
            stillActive = false; active.GrabFocus();
            await Frame(); await Frame();
            Require(active.HasFocus(), "Old modal ownership was ignored");
        });
        await Case("INPUT-01-gamepad-accept-never-invokes-disabled-offer", () =>
        {
            var screen = Screen(); var disabled = Button(screen, "Disabled", disabled: true); var active = Button(screen, "Active");
            var wrong = 0; var right = 0; disabled.Pressed += () => wrong++; active.Pressed += () => right++;
            active.GrabFocus();
            RequiemFocus.Handle(screen, new InputEventJoypadButton { Pressed = true, ButtonIndex = JoyButton.A }, [disabled, active]);
            Require(wrong == 0 && right == 1, "Gamepad accept bypassed eligibility");
            return Task.CompletedTask;
        });
        await Case("INPUT-02-keyboard-and-gamepad-wrap-enabled-ring", () =>
        {
            var screen = Screen(); var a = Button(screen, "A"); var disabled = Button(screen, "Disabled", disabled: true); var b = Button(screen, "B");
            a.GrabFocus();
            RequiemFocus.Handle(screen, new InputEventJoypadButton { Pressed = true, ButtonIndex = JoyButton.DpadDown }, [a, disabled, b]);
            Require(b.HasFocus(), "Dpad did not skip disabled row");
            RequiemFocus.Handle(screen, new InputEventKey { Pressed = true, Keycode = Key.Down }, [a, disabled, b]);
            Require(a.HasFocus(), "Keyboard did not wrap enabled ring");
            return Task.CompletedTask;
        });
    }
}
