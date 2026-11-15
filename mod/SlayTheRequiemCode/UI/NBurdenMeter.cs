using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Шкала груза (art spec 4.8, 02 §3): «⚖ вес/вместимость», пороги перегруза 1–5 / 6–10 / больше 10 и подпись
/// эффекта в подсказке. Компактный вид — в бою над стопкой добора (ExtraCombatUi Left); полный — на экране колоды.
/// </summary>
public partial class NBurdenMeter : NRequiemHoverWidget
{
    public const string NodeName = "StrBurdenMeter";

    private readonly Player? _player;
    private readonly bool _compact;
    private double _sinceRefresh = 1;
    private int _weight, _capacity;
    private BurdenRules.Overload _overload;

    public NBurdenMeter() : this(null, true) { }

    public NBurdenMeter(Player? player, bool compact)
    {
        Name = NodeName;
        _player = player;
        _compact = compact;
        var size = compact ? new Vector2(112, 40) : new Vector2(300, 46);
        Size = size;
        CustomMinimumSize = size;
    }

    public override void _Process(double delta)
    {
        _sinceRefresh += delta;
        if (_sinceRefresh < 0.25) return;
        _sinceRefresh = 0;
        Visible = _player != null && RequiemMode.IsInSystem(_player);
        if (!Visible) return;
        _weight = Loadout.TotalWeight(_player);
        _capacity = Loadout.Capacity(_player);
        _overload = BurdenRules.Evaluate(_weight, _capacity);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_player == null) return;
        var h = Size.Y;
        DrawRect(new Rect2(Vector2.Zero, Size), RequiemUi.BarBack);
        var icon = RequiemUi.Texture(RequiemUi.UiImage("burden"));
        if (icon != null) DrawTextureRect(icon, new Rect2(2, 2, h - 4, h - 4), false);
        var tint = RequiemUi.BurdenTint(_overload);
        var left = h + 2;
        var text = $"{_weight}/{_capacity}";
        if (_compact)
        {
            DrawOutlined(text, new Vector2(left, h * 0.5f + 8), Size.X - left - 4, 20, tint, HorizontalAlignment.Left);
            return;
        }
        // Полный вид: полоса до вместимости и три зоны перегруза (+5, +10, дальше).
        var barX = left;
        var barW = Size.X - left - 6;
        var max = Math.Max(1, _capacity + 12);
        float X(int w) => barX + barW * Math.Clamp(w, 0, max) / (float)max;
        var y = h - 16;
        DrawRect(new Rect2(barX, y, barW, 10), new Color(0, 0, 0, 0.7f));
        DrawRect(new Rect2(X(_capacity), y, X(_capacity + 5) - X(_capacity), 10), new Color(RequiemUi.TiredColor, 0.25f));
        DrawRect(new Rect2(X(_capacity + 5), y, X(_capacity + 10) - X(_capacity + 5), 10), new Color(RequiemUi.ExhaustedColor, 0.25f));
        DrawRect(new Rect2(X(_capacity + 10), y, X(max) - X(_capacity + 10), 10), new Color(RequiemUi.OverstrainColor, 0.25f));
        DrawRect(new Rect2(barX, y, X(_weight) - barX, 10), RequiemUi.BurdenColor.Lerp(tint, _overload.Excess > 0 ? 0.6f : 0f));
        DrawLine(new Vector2(X(_capacity), y - 3), new Vector2(X(_capacity), y + 13), StsColors.cream, 2f);
        var label = $"⚖ {text}  " + RequiemUi.TipText($"{RequiemUi.BurdenEffectKey(_overload)}.title", ("Excess", _overload.Excess));
        DrawOutlined(label, new Vector2(barX, y - 4), barW, 16, tint, HorizontalAlignment.Left);
    }

    protected override IEnumerable<IHoverTip> Tips()
    {
        yield return MakeTip("BURDEN", ("Weight", _weight), ("Capacity", _capacity), ("Excess", _overload.Excess));
        yield return MakeTip(RequiemUi.BurdenEffectKey(_overload), ("Excess", _overload.Excess),
            ("StaminaPenalty", _overload.StaminaRegenPenalty), ("HandPenalty", _overload.OpeningHandPenalty));
    }
}

/// <summary>Шкала груза на экране колоды (art spec 4.8): справа вверху, над сеткой карт.</summary>
[HarmonyPatch(typeof(NDeckViewScreen), nameof(NDeckViewScreen._Ready))]
public static class DeckViewBurdenPatch
{
    private static readonly AccessTools.FieldRef<NDeckViewScreen, Player> PlayerRef = AccessTools.FieldRefAccess<NDeckViewScreen, Player>("_player");

    [HarmonyPostfix]
    public static void Postfix(NDeckViewScreen __instance)
    {
        try
        {
            var player = PlayerRef(__instance);
            if (player == null || !RequiemMode.IsInSystem(player) || __instance.GetNodeOrNull(NBurdenMeter.NodeName) != null) return;
            var meter = new NBurdenMeter(player, compact: false);
            __instance.AddChild(meter);
            meter.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            meter.Position = new Vector2(__instance.Size.X - meter.Size.X - 48, 120);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"UI: груз на экране колоды: {e.Message}");
        }
    }
}
