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
    private int _previewDelta;
    // Анимация: число и полоса «доезжают» до нового веса, при росте ступени перегруза шкала вздрагивает и
    // вспыхивает, в перегрузе — мягко пульсирует (плейтест 2026-11: «перегруз не заметен, пока не заблокирует»).
    private float _shownWeight = -1f;
    private float _flash;
    private float _pulse;
    private int _tier = -1;

    private static int TierOf(BurdenRules.Overload o) => o.Excess <= 0 ? 0 : o.Excess <= 5 ? 1 : o.Excess <= 10 ? 2 : 3;

    /// <summary>
    /// Превью покупки (+вес) или продажи (−вес) в лавке (M6 п. 5): призрачный отрезок до веса «после», подпись
    /// «было → станет» и ступень перегруза после сделки. 0 — без превью.
    /// </summary>
    public int PreviewDelta
    {
        get => _previewDelta;
        set
        {
            if (_previewDelta == value) return;
            _previewDelta = value;
            QueueRedraw();
        }
    }

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
        Animate((float)delta);
        _sinceRefresh += delta;
        if (_sinceRefresh < 0.25) return;
        _sinceRefresh = 0;
        Visible = _player != null && RequiemMode.IsInSystem(_player);
        if (!Visible) return;
        _weight = Loadout.TotalWeight(_player);
        _capacity = Loadout.Capacity(_player);
        _overload = BurdenRules.Evaluate(_weight, _capacity);
        var tier = TierOf(_overload);
        if (_tier >= 0 && tier > _tier)
        {
            _flash = 1f;
            RequiemKit.Shake(this, 4f + 2f * tier);
        }
        _tier = tier;
        if (_shownWeight < 0) _shownWeight = _weight;
        QueueRedraw();
    }

    private void Animate(float dt)
    {
        if (!Visible || _shownWeight < 0) return;
        var moving = Math.Abs(_shownWeight - _weight) > 0.01f;
        if (moving) _shownWeight = Mathf.Lerp(_shownWeight, _weight, 1f - Mathf.Exp(-dt * 8f));
        if (!moving) _shownWeight = _weight;
        if (_flash > 0) _flash = Math.Max(0, _flash - dt * 1.6f);
        if (_overload.Excess > 0) _pulse = (_pulse + dt * (1.2f + 0.6f * _tier)) % Mathf.Tau;
        if (moving || _flash > 0 || _overload.Excess > 0) QueueRedraw();
    }

    /// <summary>Цвет ступени с пульсом и вспышкой.</summary>
    private Color Glow(Color tint)
    {
        var pulse = _overload.Excess > 0 ? 0.18f * (0.5f + 0.5f * Mathf.Sin(_pulse)) : 0f;
        return tint.Lerp(Colors.White, Math.Max(pulse, _flash * 0.7f));
    }

    public override void _Draw()
    {
        if (_player == null) return;
        var h = Size.Y;
        DrawRect(new Rect2(Vector2.Zero, Size), RequiemUi.BarBack);
        var icon = RequiemUi.Texture(RequiemUi.UiImage("burden"));
        if (icon != null) DrawTextureRect(icon, new Rect2(2, 2, h - 4, h - 4), false);
        var tint = Glow(RequiemUi.BurdenTint(_overload));
        if (_flash > 0) DrawRect(new Rect2(Vector2.Zero, Size), new Color(RequiemUi.BurdenTint(_overload), 0.35f * _flash), false, 3f);
        var left = h + 2;
        var shown = _shownWeight < 0 ? _weight : (int)Math.Round(_shownWeight);
        var text = $"{shown}/{_capacity}";
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
        var fillTo = barX + barW * Math.Clamp(_shownWeight < 0 ? _weight : _shownWeight, 0, max) / max;
        DrawRect(new Rect2(barX, y, fillTo - barX, 10), RequiemUi.BurdenColor.Lerp(tint, _overload.Excess > 0 ? 0.6f : 0f));
        if (_previewDelta != 0)
        {
            // превью сделки: покупка — отрезок вправо цветом ступени «после», продажа — светлая «выемка» влево
            var preview = ShopUiRules.Preview(_weight, _capacity, _previewDelta);
            var after = BurdenRules.Evaluate(preview.After, _capacity);
            var ghost = _previewDelta > 0 ? RequiemUi.BurdenTint(after) : new Color(0.55f, 0.95f, 0.6f);
            var from = X(Math.Min(preview.Before, preview.After));
            var to = X(Math.Max(preview.Before, preview.After));
            DrawRect(new Rect2(from, y, Math.Max(2f, to - from), 10), new Color(ghost, 0.6f));
            DrawRect(new Rect2(from, y, Math.Max(2f, to - from), 10), ghost, false, 1.5f);
            DrawLine(new Vector2(X(_capacity), y - 3), new Vector2(X(_capacity), y + 13), StsColors.cream, 2f);
            var arrow = $"⚖ {_weight} → {preview.After}/{_capacity}  " +
                        RequiemUi.TipText($"{RequiemUi.BurdenEffectKey(after)}.title", ("Excess", after.Excess));
            var color = preview.Worsens ? RequiemUi.BurdenTint(after) : preview.Improves ? ghost : tint;
            DrawOutlined(arrow, new Vector2(barX, y - 4), barW, 16, color, HorizontalAlignment.Left);
            return;
        }
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
