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
    private PanelContainer? _panel;

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

        _panel = panel;
    }

    public override void _Ready()
    {
        if (_panel is not { } panel) return;
        // Build runs BEFORE AddChildSafely: creating tweens there was invalid when the node was not in a tree.
        RequiemKit.PopIn(panel, dy: -24f);
        Tween? fade = null;
        RequiemMotion.Watch(this, () =>
        {
            if (RequiemMotion.Enabled) return;
            RequiemMotion.Kill(fade); fade = null;
            panel.Modulate = Colors.White;
        });
        // Lifetime is independent from optional motion, so disabling effects cannot leak or erase a refusal early.
        var life = CreateTween();
        life.TweenInterval(Seconds);
        life.TweenCallback(Callable.From(() =>
        {
            if (!RequiemMotion.Enabled) return;
            fade = panel.CreateTween();
            fade.TweenProperty(panel, "modulate:a", 0f, 0.6);
        }));
        life.TweenInterval(0.6);
        life.TweenCallback(Callable.From(QueueFree));
    }
}
