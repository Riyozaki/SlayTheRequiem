using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Короткое сообщение поверх всех экранов (плейтест 2026-11: правила мода молча отказывали — карта награды «не бралась»,
/// и игрок не понимал почему). Плашка вверху экрана, держится несколько секунд и гаснет. Только оформление: состояние
/// игры не меняет, показывается лишь на машине того игрока, которого касается.
/// </summary>
public partial class NRequiemToast : CanvasLayer
{
    private const float Seconds = 4.5f;

    public static void Show(string text)
    {
        try
        {
            if (NGame.Instance == null || string.IsNullOrWhiteSpace(text)) return;
            var toast = new NRequiemToast();
            toast.Build(text);
            NGame.Instance.AddChildSafely(toast);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"UI: сообщение не показано: {e.Message}");
        }
    }

    private void Build(string text)
    {
        Layer = 120;
        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 0f,
            OffsetLeft = -460, OffsetRight = 460, OffsetTop = 96, OffsetBottom = 180,
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.05f, 0.03f, 0.88f),
            BorderColor = new Color("C9A45C"),
            ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 12, ContentMarginBottom = 12,
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(6);
        panel.AddThemeStyleboxOverride("panel", style);

        var label = RequiemUi.MakeLabel(22, RequiemUi.Outline);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.Text = text;
        panel.AddChild(label);
        AddChild(panel);

        panel.Modulate = new Color(1, 1, 1, 0);
        var tween = panel.CreateTween();
        tween.TweenProperty(panel, "modulate:a", 1f, 0.2f);
        tween.TweenInterval(Seconds);
        tween.TweenProperty(panel, "modulate:a", 0f, 0.6f);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
