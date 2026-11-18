using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Debugging;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>Always visible, including degraded/disabled sessions. Click for actual rules and artifact identities.</summary>
public partial class NRunStatusBadge : CanvasLayer
{
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
        var panel = RequiemKit.Panel();
        root.AddChild(panel);
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.OffsetLeft = -520; panel.OffsetRight = 520; panel.OffsetTop = -340; panel.OffsetBottom = 340;
        panel.Visible = false;
        var box = new VBoxContainer(); panel.AddChild(box);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(960, 560) };
        box.AddChild(scroll);
        var text = new RichTextLabel { BbcodeEnabled = false, FitContent = true, SelectionEnabled = true,
            CustomMinimumSize = new Vector2(930, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        text.AddThemeFontSizeOverride("normal_font_size", 17);
        scroll.AddChild(text);
        var close = RequiemKit.MakeButton(RequiemUi.TipText("RUN_PASSPORT_CLOSE.title"), 21, new Vector2(240, 44));
        box.AddChild(close);
        close.Pressed += () => panel.Visible = false;
        badge.Pressed += () =>
        {
            panel.Visible = !panel.Visible;
            if (panel.Visible)
            {
                try { text.Text = RequiemUi.TipText("RUN_PASSPORT.title") + "\n" + RunDiagnostics.Describe(RequiemNet.State); }
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
