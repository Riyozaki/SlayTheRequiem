using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Компактные черты врага (плейтест 2026-11: «на врагах висит слишком много статусов»). Постоянные силы из
/// <see cref="EnemyTraits"/> — Латы, Устойчивость, сопротивления, аффиксы, плащи — рисуются строкой мелких значков с
/// числом под полоской ♥ (и точками ◆/✦). Наведение — одна общая подсказка со всеми чертами. Временные эффекты боя
/// (Раскол, Шаткость, яды, Сила) остаются в обычном ряду статусов.
/// </summary>
public partial class NEnemyTraitChips : NRequiemHoverWidget
{
    public const string NodeName = "StrEnemyTraits";
    public const float RowHeight = 22f;
    private const float Icon = 20f;
    private const float Gap = 3f;

    // Ключ — тип силы (сопротивления разных стихий — разные типы).
    private static readonly Dictionary<Type, Texture2D?> IconCache = new();

    private Creature? _creature;
    private Control? _anchor;
    private float _top;
    private readonly List<PowerModel> _traits = new();
    private int _signature;
    private float _appear;

    public NEnemyTraitChips() => Name = NodeName;

    public void Bind(Creature creature, Control anchor, float top)
    {
        _creature = creature;
        _anchor = anchor;
        _top = top;
    }

    public override void _Process(double delta)
    {
        var show = RunSettings.EnableRequiemSystems && RequiemConfig.CompactEnemyStatuses && _creature is { IsAlive: true }
                   && _anchor != null && IsInstanceValid(_anchor);
        if (show) Collect();
        show = show && _traits.Count > 0;
        Visible = show;
        if (!show) return;

        var size = new Vector2(_anchor!.Size.X, RowHeight);
        if (Size != size) Size = size;
        var pos = new Vector2(0, _anchor.Size.Y + _top);
        if (Position != pos) Position = pos;
        // Мягкое появление при смене набора черт (аффикс снят, сопротивление выросло).
        if (_appear < 1f) _appear = Math.Min(1f, _appear + (float)delta * 4f);
        QueueRedraw();
    }

    private void Collect()
    {
        _traits.Clear();
        foreach (var power in _creature!.Powers)
            if (EnemyTraits.IsTrait(power) && power.Amount != 0) _traits.Add(power);
        // Порядок: защита (Латы, Устойчивость) → сопротивления → аффиксы и плащи.
        _traits.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
        var signature = _traits.Aggregate(17, (h, p) => h * 31 + p.GetType().GetHashCode() * 7 + p.Amount);
        if (signature != _signature)
        {
            _signature = signature;
            _appear = 0f;
        }
    }

    private static int Rank(PowerModel p) => p switch
    {
        PlatingPower => 0,
        UnshakeablePower or PoisePower => 1,
        ResistancePower => 2,
        CloakPower or SpellAbsorptionPower => 3,
        _ => 4,
    };

    public override void _Draw()
    {
        var fit = Math.Max(1, (int)((Size.X + Gap) / (Icon + Gap)));
        var shown = _traits.Count > fit ? fit - 1 : _traits.Count;
        var alpha = Mathf.Ease(_appear, 0.4f);
        for (var i = 0; i < shown; i++)
        {
            var p = _traits[i];
            var x = i * (Icon + Gap);
            var rect = new Rect2(x, 1, Icon, Icon);
            DrawRect(rect.Grow(1.5f), new Color(0, 0, 0, 0.72f * alpha));
            DrawRect(rect.Grow(0.5f), BorderOf(p) with { A = alpha }, filled: false, width: 1.5f);
            if (IconOf(p) is { } tex) DrawTextureRect(tex, rect, false, new Color(1, 1, 1, alpha));
            if (ShowsAmount(p))
                DrawOutlined(p.DisplayAmount.ToString(), new Vector2(x, Icon + 1), Icon + 4, 11,
                    StsCream with { A = alpha }, HorizontalAlignment.Right);
        }
        if (shown < _traits.Count)
        {
            var x = shown * (Icon + Gap);
            DrawRect(new Rect2(x, 1, Icon, Icon).Grow(1.5f), new Color(0, 0, 0, 0.72f * alpha));
            DrawOutlined($"+{_traits.Count - shown}", new Vector2(x, Icon - 4), Icon, 12, StsCream with { A = alpha });
        }
    }

    private static readonly Color StsCream = new("FFF6E2");

    private static bool ShowsAmount(PowerModel p) => p is not AffixPower && p.DisplayAmount != 0;

    private static Color BorderOf(PowerModel p) => p switch
    {
        PlatingPower => new Color("C9C2B0"),
        UnshakeablePower or PoisePower => RequiemUi.StaminaColor,
        ResistancePower r => RequiemUi.DamageTypeColor(r.Element),
        AffixPower => new Color("E0533A"),
        _ => RequiemUi.ManaColor,
    };

    private static Texture2D? IconOf(PowerModel p)
    {
        var type = p.GetType();
        if (IconCache.TryGetValue(type, out var tex)) return tex;
        try
        {
            // Сопротивления — значком стихии (огонь, мороз…): так ряд читается без наведения.
            tex = p is ResistancePower r && RequiemUi.DamageTypeIcon(r.Element) is { } path
                ? RequiemUi.Texture(path)
                : p.Icon;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"UI: значок черты {type.Name}: {e.Message}");
            tex = null;
        }
        IconCache[type] = tex;
        return tex;
    }

    protected override IEnumerable<IHoverTip> Tips()
    {
        var tips = new List<IHoverTip>();
        EnemyTraits.Reveal = true;
        try
        {
            foreach (var p in _traits.ToList()) tips.AddRange(p.HoverTips);
        }
        finally
        {
            EnemyTraits.Reveal = false;
        }
        return tips;
    }
}
