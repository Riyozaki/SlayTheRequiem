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

/// <summary>
/// Шкалы ◆/✦ под полоской ♥ героя (art spec 4.3, 02 §6): «текущее/пул (+прирост)», плавное убывание, вспышка
/// при надрыве, пометка «Устал»/«Изнурён» (4.7). В коопе — у каждого героя. Крупная цифра ◆ у энергии
/// (счётчик BaseLib <c>NAdditionalResourceDisplay</c>) остаётся — шкалы его дополняют.
/// </summary>
public partial class NRequiemResourceBars : NRequiemHoverWidget
{
    public const string NodeName = "StrResourceBars";
    public const float BarHeight = 13f;
    public const float Gap = 3f;
    public const float TotalHeight = BarHeight * 2 + Gap * 2;

    private Creature? _creature;
    private Control? _anchor;
    private float _shownStamina = -1, _shownMana = -1;
    private float _flash;
    private int _stamina, _staminaPool, _staminaRegen, _mana, _manaPool, _manaRegen;

    public NRequiemResourceBars() => Name = NodeName;

    public void Bind(Creature creature, Control anchor)
    {
        _creature = creature;
        _anchor = anchor;
        RequiemUiEvents.Overstrained += OnOverstrained;
    }

    public override void _ExitTree()
    {
        RequiemUiEvents.Overstrained -= OnOverstrained;
        base._ExitTree();
    }

    private void OnOverstrained(Player player)
    {
        if (player == _creature?.Player) _flash = 1f;
    }

    private void Sample()
    {
        var player = _creature?.Player;
        var stamina = RequiemResource.Of<StaminaResource>(player);
        _stamina = stamina?.Amount ?? 0;
        _staminaPool = stamina?.Pool ?? 0;
        _staminaRegen = stamina?.Regen ?? 0;
        _mana = Mana.Amount(player);
        _manaPool = Mana.Pool(player);
        _manaRegen = Mana.Regen(player);
    }

    public override void _Process(double delta)
    {
        var player = _creature?.Player;
        var show = player != null && RequiemMode.IsInSystem(player) && _creature!.IsAlive && _anchor != null
                   && IsInstanceValid(_anchor);
        Visible = show;
        if (!show) return;
        Sample();
        var t = 1f - MathF.Exp(-10f * (float)delta);
        _shownStamina = _shownStamina < 0 ? _stamina : Mathf.Lerp(_shownStamina, _stamina, t);
        _shownMana = _shownMana < 0 ? _mana : Mathf.Lerp(_shownMana, _mana, t);
        _flash = Math.Max(0f, _flash - (float)delta * 1.6f);
        var size = new Vector2(_anchor!.Size.X, TotalHeight);
        if (Size != size) Size = size;
        var pos = new Vector2(0, _anchor.Size.Y + Gap);
        if (Position != pos) Position = pos;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_creature == null) return;
        var staminaLabel = $"◆ {_stamina}/{_staminaPool} (+{_staminaRegen})";
        var state = StateKey();
        if (state != null) staminaLabel += " · " + RequiemUi.Tip($"{state}.title").GetFormattedText();
        var staminaColor = _stamina == 0 ? RequiemUi.ExhaustedColor
            : state != null ? RequiemUi.TiredColor : RequiemUi.StaminaColor;
        DrawBar(0, _shownStamina, _staminaPool, staminaColor, staminaLabel);
        if (_manaPool > 0)
            DrawBar(BarHeight + Gap, _shownMana, _manaPool, RequiemUi.ManaColor, $"✦ {_mana}/{_manaPool} (+{_manaRegen})");
        if (_flash > 0f)
            DrawRect(new Rect2(0, 0, Size.X, BarHeight), new Color(RequiemUi.OverstrainColor, 0.7f * _flash));
    }

    private string? StateKey()
    {
        if (_staminaPool <= 0) return null;
        if (_stamina == 0) return "STATE_EXHAUSTED";
        if (_stamina * 2 < _staminaPool && !RequiemCombatRules.IsTireless(_creature?.Player)) return "STATE_TIRED";
        return null;
    }

    private void DrawBar(float y, float value, int pool, Color color, string text)
    {
        var rect = new Rect2(0, y, Size.X, BarHeight);
        DrawRect(rect, RequiemUi.BarBack);
        if (pool > 0)
        {
            var fill = Math.Clamp(value / pool, 0f, 1f);
            DrawRect(new Rect2(1, y + 1, (Size.X - 2) * fill, BarHeight - 2), color);
            // Деления пула: по риске на единицу, если влезают.
            if (pool <= 20)
                for (var i = 1; i < pool; i++)
                {
                    var x = Size.X * i / pool;
                    DrawLine(new Vector2(x, y + 2), new Vector2(x, y + BarHeight - 2), new Color(0, 0, 0, 0.35f));
                }
        }
        DrawRect(rect, new Color(0, 0, 0, 0.9f), false, 1f);
        DrawOutlined(text, new Vector2(0, y + BarHeight - 2), Size.X, 11, StsColors.cream);
    }

    protected override IEnumerable<IHoverTip> Tips()
    {
        yield return MakeTip("STAMINA_BAR", ("Amount", _stamina), ("Pool", _staminaPool), ("Regen", _staminaRegen));
        if (_manaPool > 0) yield return MakeTip("MANA_BAR", ("Amount", _mana), ("Pool", _manaPool), ("Regen", _manaRegen));
        if (StateKey() is { } state) yield return MakeTip(state);
    }
}
