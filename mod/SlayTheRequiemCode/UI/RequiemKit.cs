using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Общий набор оформления своих окон (план UI, docs/review/playtest-review-2026-11.md §5): тёмная панель с золотой
/// рамкой, кнопка, заголовок, анимации появления. Все свои экраны строятся кодом — так их проще патчить и не нужен
/// импорт сцен в проект мода.
/// </summary>
public static class RequiemKit
{
    public static readonly Color Gold = StsColors.gold;
    public static readonly Color Dim = new(0.55f, 0.52f, 0.48f);
    public static readonly Color PanelBg = new(0.055f, 0.045f, 0.06f, 0.94f);
    public static readonly Color Danger = new("E0533A");

    public static StyleBoxFlat Box(Color bg, Color border, int width, int radius = 10, int margin = 12)
    {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        box.SetBorderWidthAll(width);
        box.SetCornerRadiusAll(radius);
        box.SetContentMarginAll(margin);
        box.ShadowColor = new Color(0, 0, 0, 0.45f);
        box.ShadowSize = 6;
        return box;
    }

    /// <summary>Панель-пергамент для своих окон.</summary>
    public static PanelContainer Panel(int margin = 16, Color? border = null)
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", Box(PanelBg, border ?? Gold.Darkened(0.25f), 3, 14, margin));
        return panel;
    }

    /// <summary>Кнопка в стиле мода. <paramref name="accent"/> — цвет рамки при наведении (по умолчанию золото).</summary>
    public static Button MakeButton(string text, int fontSize, Vector2 size, Color? accent = null)
    {
        var a = accent ?? Gold;
        var button = new Button { Text = text, CustomMinimumSize = size, FocusMode = Control.FocusModeEnum.All };
        button.AddThemeFontOverride("font", RequiemUi.Font);
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeColorOverride("font_color", StsColors.cream);
        button.AddThemeColorOverride("font_hover_color", a.Lightened(0.15f));
        button.AddThemeColorOverride("font_pressed_color", a);
        button.AddThemeColorOverride("font_hover_pressed_color", a);
        button.AddThemeColorOverride("font_disabled_color", Dim);
        button.AddThemeColorOverride("font_outline_color", RequiemUi.Outline);
        button.AddThemeConstantOverride("outline_size", Math.Max(4, fontSize / 3));
        button.AddThemeStyleboxOverride("normal", Box(new Color(0.06f, 0.05f, 0.07f, 0.92f), a.Darkened(0.45f), 3));
        button.AddThemeStyleboxOverride("hover", Box(new Color(0.1f, 0.08f, 0.1f, 0.95f), a, 4));
        button.AddThemeStyleboxOverride("pressed", Box(new Color(0.16f, 0.12f, 0.08f, 0.96f), a.Lightened(0.3f), 5));
        button.AddThemeStyleboxOverride("hover_pressed", Box(new Color(0.18f, 0.14f, 0.09f, 0.96f), a.Lightened(0.4f), 5));
        button.AddThemeStyleboxOverride("focus", Box(new Color(0, 0, 0, 0), a.Lightened(0.2f), 4));
        button.AddThemeStyleboxOverride("disabled", Box(new Color(0.05f, 0.05f, 0.06f, 0.85f), a.Darkened(0.65f), 3));
        // Лёгкая «пружина» при наведении, как у ванильных кнопок.
        button.MouseEntered += () => Pulse(button, 1.04f);
        button.MouseExited += () => Pulse(button, 1f);
        button.Resized += () => button.PivotOffset = button.Size / 2;
        return button;
    }

    private sealed class MotionState
    {
        internal Tween? Hover, Entry, Denied;
        internal Vector2 EntryTarget, DeniedOrigin;
        internal bool EntryMoves;
        internal Color EntryTint;
        internal void FinishEntry(Control control)
        {
            if (Entry == null) return;
            RequiemMotion.Kill(Entry); Entry = null;
            if (EntryMoves) control.Position = EntryTarget;
            control.Modulate = EntryTint;
        }
        internal void FinishDenied(Control control)
        {
            if (Denied == null) return;
            RequiemMotion.Kill(Denied); Denied = null; control.Position = DeniedOrigin;
        }
    }
    private static readonly ConditionalWeakTable<Control, MotionState> Motions = new();
    private static MotionState Motion(Control control) => Motions.GetValue(control, owner =>
    {
        var state = new MotionState();
        RequiemMotion.Watch(owner, () =>
        {
            if (RequiemMotion.Enabled || !GodotObject.IsInstanceValid(owner)) return;
            RequiemMotion.Kill(state.Hover); state.Hover = null; owner.Scale = Vector2.One;
            state.FinishEntry(owner); state.FinishDenied(owner);
        });
        return state;
    });

    private static void Pulse(Control control, float scale)
    {
        if (!GodotObject.IsInstanceValid(control) || !control.IsInsideTree()) return;
        var state = Motion(control);
        RequiemMotion.Kill(state.Hover); state.Hover = null;
        if (!RequiemMotion.Enabled) { control.Scale = Vector2.One; return; }
        state.Hover = control.CreateTween();
        state.Hover.TweenProperty(control, "scale", Vector2.One * scale, 0.12)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
    }

    /// <summary>Optional entry only. Reduced motion reveals the final state immediately, without a hidden delay.</summary>
    public static void PopIn(Control control, float delay = 0f, float dy = 24f)
    {
        if (!GodotObject.IsInstanceValid(control) || !control.IsInsideTree()) return;
        var state = Motion(control);
        state.FinishEntry(control); state.FinishDenied(control);
        if (!RequiemMotion.Enabled) return;
        state.EntryTint = control.Modulate; state.EntryTarget = control.Position;
        state.EntryMoves = dy != 0 && control.GetParent() is not Container;
        control.Modulate = state.EntryTint with { A = 0 };
        if (state.EntryMoves) control.Position = state.EntryTarget + new Vector2(0, dy);
        var tween = state.Entry = control.CreateTween().SetParallel();
        tween.TweenProperty(control, "modulate:a", state.EntryTint.A, 0.22).SetDelay(delay);
        if (state.EntryMoves)
            tween.TweenProperty(control, "position", state.EntryTarget, 0.3).SetDelay(delay)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        tween.Chain().TweenCallback(Callable.From(() => state.Entry = null));
    }

    /// <summary>Optional denial motion; a repeated request returns to the ORIGINAL origin, never accumulates drift.</summary>
    public static void Shake(Control control, float strength = 8f)
    {
        if (!GodotObject.IsInstanceValid(control) || !control.IsInsideTree()) return;
        var state = Motion(control);
        state.FinishEntry(control); state.FinishDenied(control);
        if (!RequiemMotion.Enabled) return; // persistent text/disabled/focus styles remain; no gameplay veto lives here
        var origin = state.DeniedOrigin = control.Position;
        var tween = state.Denied = control.CreateTween();
        for (var i = 0; i < 4; i++)
        {
            var dx = (i % 2 == 0 ? 1 : -1) * strength * (1f - i / 4f);
            tween.TweenProperty(control, "position", origin + new Vector2(dx, 0), 0.04);
        }
        tween.TweenProperty(control, "position", origin, 0.04);
        tween.TweenCallback(Callable.From(() => state.Denied = null));
    }
}
