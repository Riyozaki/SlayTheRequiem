using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Permanent enemy traits below HP. PowerApplied/Removed + DisplayAmountChanged invalidate a cached, ordered
/// view; a clean visible frame does not enumerate/sort/hash powers or request another canvas redraw.
/// Bind/re-enter/load invalidates explicitly. All model subscriptions are released on exit/rebind.
/// Temporary combat effects stay in the native power row. This is presentation, never a combat authority.
/// </summary>
public partial class NEnemyTraitChips : NRequiemHoverWidget
{
    public const string NodeName = "StrEnemyTraits";
    public const float RowHeight = 22f;
    private const float Icon = 20f;
    private const float Gap = 3f;
    private static readonly Dictionary<Type, Texture2D?> IconCache = new();

    private Creature? _creature;
    private Control? _anchor;
    private float _top;
    private readonly InvalidatedListView<PowerModel> _view;
    private readonly HashSet<PowerModel> _observed = new(ReferenceEqualityComparer.Instance);
    private bool _listening, _paintDirty = true;
    private float _appear, _reportedHeight = -1;
    public event Action<float>? HeightChanged;
    public float ReportedHeight => Math.Max(0, _reportedHeight);

    public NEnemyTraitChips() { Name = NodeName; _view = new(BuildTraits); }

    public void Bind(Creature creature, Control anchor, float top)
    {
        Unsubscribe();
        _creature = creature; _anchor = anchor; _top = top;
        _view.Reset(); _paintDirty = true; ReportHeight(0);
        _appear = RequiemMotion.Enabled ? 0f : 1f;
        Subscribe();
    }

    public override void _EnterTree() { base._EnterTree(); Subscribe(); }
    public override void _ExitTree() { Unsubscribe(); _view.Reset(); base._ExitTree(); }

    private void Subscribe()
    {
        if (_listening || _creature == null || !IsInsideTree()) return;
        _listening = true;
        _creature.PowerApplied += Observe;
        _creature.PowerRemoved += Removed;
        foreach (var power in _creature.Powers) Observe(power); // includes zero-amount traits that can grow later
        Dirty();
    }

    private void Unsubscribe()
    {
        if (_listening && _creature != null)
        {
            _creature.PowerApplied -= Observe;
            _creature.PowerRemoved -= Removed;
        }
        foreach (var power in _observed) power.DisplayAmountChanged -= Dirty;
        _observed.Clear(); _listening = false;
    }

    private void Observe(PowerModel power)
    {
        if (!EnemyTraits.IsTrait(power)) return;
        if (_observed.Add(power)) power.DisplayAmountChanged += Dirty;
        Dirty();
    }

    private void Removed(PowerModel power)
    {
        if (!_observed.Remove(power)) return;
        power.DisplayAmountChanged -= Dirty;
        Dirty();
    }

    private void Dirty() { _view.Invalidate(); _paintDirty = true; }

    private void BuildTraits(List<PowerModel> traits)
    {
        var started = Debugging.TechnicalMetrics.Start();
        if (_creature != null)
            foreach (var power in _creature.Powers)
                if (EnemyTraits.IsTrait(power) && power.Amount != 0) traits.Add(power);
        // A deterministic tie-break, rather than an unstable sort of equal-ranked resistances/affixes.
        traits.Sort(static (a, b) =>
        {
            var rank = Rank(a).CompareTo(Rank(b));
            return rank != 0 ? rank : string.Compare(a.Id.Entry, b.Id.Entry, StringComparison.Ordinal);
        });
        Debugging.TechnicalMetrics.TraitRebuilt(started);
    }

    public override void _Process(double delta)
    {
        var show = RunSettings.EnableRequiemSystems && RequiemConfig.CompactEnemyStatuses && _creature is { IsAlive: true }
                   && _anchor != null && IsInstanceValid(_anchor);
        if (show) _view.Refresh();
        show = show && _view.Value.Count > 0;
        if (Visible != show) { Visible = show; _paintDirty = true; }
        if (!show) { ReportHeight(0); return; }

        var width = _anchor!.Size.X;
        var fit = Math.Max(1, (int)((width + Gap) / (Icon + Gap)));
        var size = new Vector2(width, RowHeight * Math.Max(1, (int)Math.Ceiling(_view.Value.Count / (double)fit)));
        if (Size != size) { Size = size; _paintDirty = true; }
        ReportHeight(size.Y);
        var pos = new Vector2(0, _anchor.Size.Y + _top);
        if (Position != pos) Position = pos; // moving the canvas does not change its drawing commands
        var appear = RequiemMotion.Enabled ? Math.Min(1f, _appear + (float)delta * 4f) : 1f;
        if (appear != _appear) { _appear = appear; _paintDirty = true; }
        if (!_paintDirty) return;
        _paintDirty = false;
        Debugging.TechnicalMetrics.TraitRedraw();
        QueueRedraw();
    }

    private void ReportHeight(float height)
    {
        if (_reportedHeight == height) return;
        _reportedHeight = height; HeightChanged?.Invoke(height);
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
        var traits = _view.Value;
        var fit = Math.Max(1, (int)((Size.X + Gap) / (Icon + Gap)));
        var alpha = Mathf.Ease(_appear, 0.4f);
        for (var i = 0; i < traits.Count; i++)
        {
            var p = traits[i];
            var x = i % fit * (Icon + Gap); var y = i / fit * RowHeight;
            var rect = new Rect2(x, y + 1, Icon, Icon);
            DrawRect(rect.Grow(1.5f), new Color(0, 0, 0, 0.72f * alpha));
            DrawRect(rect.Grow(0.5f), BorderOf(p) with { A = alpha }, filled: false, width: 1.5f);
            if (IconOf(p) is { } tex) DrawTextureRect(tex, rect, false, new Color(1, 1, 1, alpha));
            if (ShowsAmount(p))
                DrawOutlined(p.DisplayAmount.ToString(), new Vector2(x, y + Icon + 1), Icon + 4, 11,
                    StsCream with { A = alpha }, HorizontalAlignment.Right);
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
            tex = p is ResistancePower r && RequiemUi.DamageTypeIcon(r.Element) is { } path
                ? RequiemUi.Texture(path) : p.Icon;
        }
        catch (Exception e) { MainFile.Logger.Warn($"UI: значок черты {type.Name}: {e.Message}"); tex = null; }
        IconCache[type] = tex;
        return tex;
    }

    protected override IEnumerable<IHoverTip> Tips()
    {
        _view.Refresh(); // a hover between model mutation and _Process must not show the old amount/set
        var tips = new List<IHoverTip>();
        var reveal = EnemyTraits.Reveal;
        EnemyTraits.Reveal = true;
        try { foreach (var p in _view.Value) tips.AddRange(p.HoverTips); }
        finally { EnemyTraits.Reveal = reveal; }
        return tips;
    }
}
