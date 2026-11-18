using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>Нарисованный кодом виджет с подсказкой при наведении (как счётчик ресурса BaseLib).</summary>
public abstract partial class NRequiemHoverWidget : Control
{
    protected NRequiemHoverWidget()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Connect(Control.SignalName.MouseEntered, Callable.From(OnHovered));
        Connect(Control.SignalName.MouseExited, Callable.From(OnUnhovered));
    }

    protected abstract IEnumerable<IHoverTip> Tips();

    private void OnHovered()
    {
        var tips = Tips().ToList();
        if (tips.Count > 0) NHoverTipSet.CreateAndShow(this, tips, HoverTip.GetHoverTipAlignment(this));
    }

    private void OnUnhovered() => NHoverTipSet.Remove(this);

    public override void _ExitTree()
    {
        NHoverTipSet.Remove(this);
        base._ExitTree();
    }

    protected static HoverTip MakeTip(string key, params (string, decimal)[] vars) =>
        new(RequiemUi.TipLoc($"{key}.title", vars), RequiemUi.TipLoc($"{key}.description", vars).GetFormattedText());

    /// <summary>Текст с обводкой (DrawString + DrawStringOutline).</summary>
    protected void DrawOutlined(string text, Vector2 baseline, float width, int size, Color color,
        HorizontalAlignment align = HorizontalAlignment.Center)
    {
        var font = RequiemUi.Font;
        if (!GodotObject.IsInstanceValid(font)) font = ThemeDB.FallbackFont;
        DrawStringOutline(font, baseline, text, align, width, size, 4, new Color(0, 0, 0, 0.9f));
        DrawString(font, baseline, text, align, width, size, color);
    }
}
