using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using SlayTheRequiem.SlayTheRequiemCode.Shops;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Debugging;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>Always visible, including degraded/disabled sessions. Click for actual rules and artifact identities.</summary>
public partial class NRunStatusBadge : CanvasLayer
{
    public override void _Process(double delta) => TechnicalMetrics.SampleFrame(delta);

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.H }
            && input is not InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.LeftStick }) return;
        if (RequiemNet.State is { } run && LocalContext.GetMe(run) is { } owner && RequiemMode.IsInSystem(owner)
            && HeroJournal.Show(owner)) GetViewport().SetInputAsHandled();
    }

    public override void _Ready()
    {
        Layer = 95;
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var badge = RequiemKit.MakeButton("", 17, new Vector2(176, 34), PatchReport.Degraded ? RequiemKit.Danger : RequiemKit.Gold);
        badge.Position = new Vector2(20, 82);
        badge.Text = RequiemUi.TipText(PatchReport.Degraded ? "RUN_DEGRADED.title" : RunSettings.EnableRequiemSystems ? "RUN_ENABLED.title" : "RUN_DISABLED.title");
        badge.TooltipText = RequiemUi.TipText("RUN_PASSPORT.title") + "\n" + RunSettings.SourceName;
        root.AddChild(badge);
        if (RequiemNet.State is { } run && LocalContext.GetMe(run) is { } owner && RequiemMode.IsInSystem(owner))
        {
            var journal = RequiemKit.MakeButton(RequiemUi.TipText("UI.journal.button.title"), 18, new Vector2(176, 38));
            journal.Position = new Vector2(20, 124);
            journal.Pressed += () => HeroJournal.Show(owner);
            root.AddChild(journal);
        }
        var panel = RequiemKit.Panel();
        root.AddChild(panel);
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.OffsetLeft = -520; panel.OffsetRight = 520; panel.OffsetTop = -340; panel.OffsetBottom = 340;
        panel.Visible = false;
        var box = new VBoxContainer(); panel.AddChild(box);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(960, 460) };
        box.AddChild(scroll);
        var text = new RichTextLabel { BbcodeEnabled = false, FitContent = true, SelectionEnabled = true,
            CustomMinimumSize = new Vector2(930, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        text.AddThemeFontSizeOverride("normal_font_size", 17);
        scroll.AddChild(text);
        var retry = RequiemKit.MakeButton(RequiemUi.TipText("RECOVERY_RETRY.title"), 20, new Vector2(360, 42));
        box.AddChild(retry);
        retry.Pressed += () =>
        {
            if (RequiemNet.State is { } state && LocalContext.GetMe(state) is { } me)
                TaskHelper.RunSafely(ShopOperations.RetryReceipts(me));
        };
        var restore = RequiemKit.MakeButton(RequiemUi.TipText("RECOVERY_RESTORE.title"), 20, new Vector2(360, 42), RequiemKit.Danger);
        box.AddChild(restore);
        restore.Pressed += () => TaskHelper.RunSafely(OperationRecovery.OfferRestore());
        var close = RequiemKit.MakeButton(RequiemUi.TipText("RUN_PASSPORT_CLOSE.title"), 21, new Vector2(240, 44));
        box.AddChild(close);
        close.Pressed += () => panel.Visible = false;
        badge.Pressed += () =>
        {
            panel.Visible = !panel.Visible;
            if (panel.Visible)
            {
                try { text.Text = RequiemUi.TipText("RUN_PASSPORT.title") + "\n" + OperationRecovery.Describe(RequiemNet.State)
                    + "\n\n" + RunDiagnostics.Describe(RequiemNet.State); }
                catch (Exception e) { text.Text = "Passport unavailable: " + e.Message; MainFile.Logger.Error($"[RunPassport] {e}"); }
            }
        };
    }
}

[HarmonyPatch(typeof(NRun), nameof(NRun._Ready))]
public static class RunStatusBadgePatch
{
    [HarmonyPostfix]
    public static void Postfix(NRun __instance)
    {
        try
        {
            if (__instance.GetNodeOrNull("RequiemRunStatus") == null)
                __instance.AddChild(new NRunStatusBadge { Name = "RequiemRunStatus" });
        }
        catch (Exception e) { MainFile.Logger.Error($"[RunPassport] Status badge unavailable: {e}"); }
    }
}
