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
/// ◆/✦ врага (art spec 4.4, 02 §6): точки под полоской ♥ (до 10), больше — цифрой. Пустые точки — потраченное.
/// </summary>
public partial class NEnemyResourcePips : NRequiemHoverWidget
{
    public const string NodeName = "StrEnemyPips";
    public const float RowHeight = 12f;
    private const int MaxPips = 10;

    private Creature? _creature;
    private Control? _anchor;

    public NEnemyResourcePips() => Name = NodeName;

    public void Bind(Creature creature, Control anchor)
    {
        _creature = creature;
        _anchor = anchor;
    }

    private EnemyStaminaPower? Stamina => _creature?.GetPower<EnemyStaminaPower>();
    private EnemyManaPower? ManaPower => _creature?.GetPower<EnemyManaPower>();

    public override void _Process(double delta)
    {
        var show = RequiemConfig.EnableRequiemSystems && _creature is { IsAlive: true } && _anchor != null
                   && IsInstanceValid(_anchor) && (Stamina != null || ManaPower != null);
        Visible = show;
        if (!show) return;
        var rows = (Stamina != null ? 1 : 0) + (ManaPower != null ? 1 : 0);
        var size = new Vector2(_anchor!.Size.X, rows * RowHeight);
        if (Size != size) Size = size;
        var pos = new Vector2(0, _anchor.Size.Y + 3);
        if (Position != pos) Position = pos;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var y = 0f;
        if (Stamina is { } s)
        {
            DrawRow(y, s.DisplayAmount, s.Pool, RequiemUi.StaminaColor, "◆");
            y += RowHeight;
        }
        if (ManaPower is { } m) DrawRow(y, m.DisplayAmount, m.Pool, RequiemUi.ManaColor, "✦");
    }

    private void DrawRow(float y, int current, int pool, Color color, string glyph)
    {
        if (pool > MaxPips)
        {
            DrawOutlined($"{glyph} {current}/{pool}", new Vector2(0, y + RowHeight - 1), Size.X, 11, color,
                HorizontalAlignment.Left);
            return;
        }
        const float r = 4.2f;
        for (var i = 0; i < pool; i++)
        {
            var center = new Vector2(r + 1 + i * (r * 2 + 3), y + RowHeight * 0.5f);
            DrawCircle(center, r + 1, new Color(0, 0, 0, 0.85f));
            DrawCircle(center, r, i < current ? color : new Color(color, 0.18f));
        }
    }

    protected override IEnumerable<IHoverTip> Tips()
    {
        if (Stamina is { } s)
            yield return MakeTip("ENEMY_STAMINA", ("Amount", s.DisplayAmount), ("Pool", s.Pool), ("Regen", EnemyRules.StaminaRegen),
                ("Block", EnemyRules.BreatherBlock));
        if (ManaPower is { } m)
            yield return MakeTip("ENEMY_MANA", ("Amount", m.DisplayAmount), ("Pool", m.Pool), ("Regen", EnemyRules.CasterManaRegen));
    }
}
