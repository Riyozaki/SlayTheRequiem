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
    private const float Step = 96f;

    // Живые сообщения: новое встаёт под предыдущими, а не поверх них (несколько отказов подряд читаются).
    private static readonly List<NRequiemToast> Live = new();

    public static void Show(string text)
    {
        try
        {
            if (NGame.Instance == null || string.IsNullOrWhiteSpace(text)) return;
            Live.RemoveAll(t => !IsInstanceValid(t));
            if (Live.Count >= 3) Live[0].QueueFree();
            Live.RemoveAll(t => t.IsQueuedForDeletion());
            var toast = new NRequiemToast();
            toast.Build(text, Live.Count);
            Live.Add(toast);
            NGame.Instance.AddChildSafely(toast);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"UI: сообщение не показано: {e.Message}");
        }
    }

    public override void _ExitTree() => Live.Remove(this);

    private void Build(string text, int slot)
    {
        Layer = 120;
        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 0f,
            OffsetLeft = -460, OffsetRight = 460, OffsetTop = 96 + slot * Step, OffsetBottom = 180 + slot * Step,
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

        // Появление: выезжает сверху с лёгким перелётом, рамка вспыхивает золотом и остывает.
        panel.Modulate = new Color(1, 1, 1, 0);
        var top = panel.OffsetTop;
        var bottom = panel.OffsetBottom;
        panel.OffsetTop = top - 40;
        panel.OffsetBottom = bottom - 40;
        var slide = panel.CreateTween().SetParallel();
        slide.TweenProperty(panel, "offset_top", top, 0.35f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        slide.TweenProperty(panel, "offset_bottom", bottom, 0.35f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        style.BorderColor = StsColors.gold.Lightened(0.5f);
        slide.TweenProperty(style, "border_color", new Color("C9A45C"), 0.8f);
        var tween = panel.CreateTween();
        tween.TweenProperty(panel, "modulate:a", 1f, 0.2f);
        tween.TweenInterval(Seconds);
        tween.TweenProperty(panel, "modulate:a", 0f, 0.6f);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
