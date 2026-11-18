using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Context;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Debugging;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>Always visible, including degraded/disabled sessions. The actual passport owns a native modal,
/// not a fixed-size floating panel that leaks keyboard/gamepad input to the combat underneath.</summary>
public partial class NRunStatusBadge : CanvasLayer
{
    public override void _Process(double delta) => TechnicalMetrics.SampleFrame(delta);

    public override void _UnhandledInput(InputEvent input)
    {
        if (RequiemNet.State is not { } run) return;
        // Shop/reward right-stick actions consume their own events first. Never replace an existing modal.
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F8 }
            || input is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.RightStick })
        {
            if (NRunPassportScreen.Show(run)) GetViewport().SetInputAsHandled();
            return;
        }
        if (input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.H }
            && input is not InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.LeftStick }) return;
        if (LocalContext.GetMe(run) is { } owner && RequiemMode.IsInSystem(owner)
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
        badge.TooltipText = RequiemUi.TipText("RUN_PASSPORT.title") + "\n" + RequiemUi.TipText("RUN_PASSPORT_SHORTCUT.title") + "\n" + RunSettings.SourceName;
        badge.Pressed += () => { if (RequiemNet.State is { } state) NRunPassportScreen.Show(state); };
        root.AddChild(badge);
        if (RequiemNet.State is { } run && LocalContext.GetMe(run) is { } owner && RequiemMode.IsInSystem(owner))
        {
            var journal = RequiemKit.MakeButton(RequiemUi.TipText("UI.journal.button.title"), 18, new Vector2(176, 38));
            journal.Position = new Vector2(20, 124);
            journal.Pressed += () => HeroJournal.Show(owner);
            root.AddChild(journal);
        }
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
