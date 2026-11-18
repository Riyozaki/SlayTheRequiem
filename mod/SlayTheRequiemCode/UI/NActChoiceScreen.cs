using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Acts;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Net;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Экран «две двери» (docs/design/13 §2, art spec §4.10, M5 п. 2): после босса актов 1 и 3 две арки с видом на варианты
/// следующего акта — картина, название, фракция и правила акта (только то, что уже работает; числа — из
/// <see cref="ActRules"/>). Кнопка «Довериться судьбе» — воздержаться (<see cref="ActDoorRules.Abstain"/>).
/// <para>
/// Одиночная игра: выбор сразу закрывает экран. Кооп: после своего голоса экран остаётся открытым, двери показывают
/// голоса спутников (<see cref="ActDoors.Changed"/>), а когда хост объявит итог — выбранная арка вспыхивает и экран
/// закрывается. Узлы строятся кодом, без .tscn; модальное окно — через ванильный <see cref="NModalContainer"/>.
/// </para>
/// </summary>
public partial class NActChoiceScreen : Control, IScreenContext
{
    /// <summary>Сколько ждать, пока закроется чужое модальное окно, прежде чем «довериться судьбе».</summary>
    private const double BusyModalWaitSeconds = 8;

    /// <summary>Страховка: в коопе экран закрывается сам, даже если итог не пришёл (выпавший хост и т. п.).</summary>
    private const double CoopSafetySeconds = ActDoors.HostWaitSeconds + 20;

    private static readonly Color Gold = new("D4B45A");
    private static readonly Color Dim = new(0.55f, 0.55f, 0.6f);

    private readonly int _index;
    private readonly ActKind[] _options;
    private readonly TaskCompletionSource<int> _result = new();
    private readonly DoorCard[] _cards = new DoorCard[ActDoorRules.Doors];
    private Button _fate = null!;
    private Label _status = null!;
    private int _myVote = int.MinValue;
    private bool _closing;

    public Control? DefaultFocusedControl => _cards[0]?.Button;

    private NActChoiceScreen(int index)
    {
        _index = index;
        _options = ActDoorRules.Options(index);
        Name = "RequiemActChoice";
    }

    /// <summary>Показать двери перед актом <paramref name="index"/>; результат — дверь 0/1 или <see cref="ActDoorRules.Abstain"/>.</summary>
    public static async Task<int> Show(RunState run, int index)
    {
        if (ActDoorRules.Options(index).Length != ActDoorRules.Doors) return ActDoorRules.Abstain;
        var container = NModalContainer.Instance;
        if (container == null) return ActDoorRules.Abstain;

        // Ванильный Add молча выходит, если уже открыто другое окно (подсказка, попап) — дождёмся, пока его закроют.
        var waited = 0.0;
        while (container.OpenModal != null && waited < BusyModalWaitSeconds && container.GetTree() is { } tree)
        {
            await container.ToSignal(tree.CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            waited += 0.25;
        }
        if (container.OpenModal != null)
        {
            MainFile.Logger.Warn("[SlayTheRequiem] Две двери: открыто другое модальное окно — голос «судьба».");
            return ActDoorRules.Abstain;
        }

        var screen = new NActChoiceScreen(index);
        screen.Build(run);
        container.Add(screen);
        return await screen._result.Task;
    }

    // ───── построение ─────

    private void Build(RunState run)
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var root = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 18);
        AddChild(root);

        var title = RequiemUi.MakeLabel(52, RequiemUi.Outline);
        title.Text = RequiemUi.TipText("DOORS.heading");
        SetLabelColors(title, Gold);
        root.AddChild(title);

        var subtitle = RequiemUi.MakeLabel(26, RequiemUi.Outline);
        subtitle.Text = RequiemUi.TipText("DOORS.subtitle", ("Act", _index + 1));
        root.AddChild(subtitle);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 72);
        root.AddChild(row);
        for (var door = 0; door < ActDoorRules.Doors; door++)
        {
            var card = new DoorCard(this, door, _options[door], door == ActDoors.RolledDoor(run, _index));
            _cards[door] = card;
            row.AddChild(card.Button);
        }
        _cards[0].Button.FocusNeighborRight = _cards[1].Button.GetPath();
        _cards[1].Button.FocusNeighborLeft = _cards[0].Button.GetPath();

        _fate = MakeTextButton(RequiemUi.TipText("DOORS.fate"), RequiemUi.TipText("DOORS.fateHint"));
        _fate.Pressed += () => Pick(ActDoorRules.Abstain);
        var fateRow = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        fateRow.AddChild(_fate);
        root.AddChild(fateRow);

        _status = RequiemUi.MakeLabel(22, RequiemUi.Outline);
        _status.CustomMinimumSize = new Vector2(0, 30);
        root.AddChild(_status);

        Refresh();
    }

    public override void _Ready()
    {
        ActDoors.Changed += OnChanged;
        RequiemKit.PopIn(this, dy: 0);
        _cards[0].Button.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _ExitTree()
    {
        ActDoors.Changed -= OnChanged;
        _result.TrySetResult(ActDoorRules.Abstain);
        base._ExitTree();
    }

    // ───── выбор и голоса ─────

    private void Pick(int door)
    {
        if (_myVote != int.MinValue || _closing) return;
        _myVote = door;
        foreach (var card in _cards) card.Button.Disabled = true;
        _fate.Disabled = true;
        if (door >= 0) _cards[door].Pulse();

        if (RequiemNet.IsSingleplayer)
        {
            _result.TrySetResult(door);
            Close(door >= 0 ? door : null);
            return;
        }
        // Кооп: голос уходит сразу (ActDoors рассылает его и ставит готовность), экран ждёт итога хоста.
        _result.TrySetResult(door);
        Refresh();
        if (GetTree() is { } tree) tree.CreateTimer(CoopSafetySeconds).Timeout += () => Close(null);
    }

    private void OnChanged(int index)
    {
        if (index != _index) return;
        Callable.From(Refresh).CallDeferred();
    }

    private void Refresh()
    {
        if (!IsInstanceValid(this) || _closing) return;
        var votes = ActDoors.VotesFor(_index);
        var local = RequiemNet.LocalNetId;
        for (var door = 0; door < ActDoorRules.Doors; door++)
        {
            var count = votes.Count(v => v.Value == door);
            var mine = votes.TryGetValue(local, out var my) && my == door;
            _cards[door].ShowVotes(count, mine, coop: !RequiemNet.IsSingleplayer);
        }

        if (ActDoors.DecisionFor(_index) is { } decided)
        {
            // Итог объявлен; если свой голос ещё не отдан (решили без нас) — воздерживаемся.
            _result.TrySetResult(ActDoorRules.Abstain);
            _status.Text = RequiemUi.TipText("DOORS.decided");
            Close(decided);
            return;
        }
        var total = RequiemNet.State?.Players.Count ?? 1;
        _status.Text = _myVote != int.MinValue && !RequiemNet.IsSingleplayer
            ? RequiemUi.TipText("DOORS.waiting", ("Voted", votes.Count), ("Total", total))
            : "";
    }

    private void Close(int? highlight)
    {
        if (_closing || !IsInstanceValid(this)) return;
        _closing = true;
        if (highlight is int door && door >= 0) _cards[door].Pulse();
        var tween = CreateTween();
        tween.TweenInterval(RequiemMotion.Enabled && highlight != null ? 0.55 : 0.1);
        tween.TweenProperty(this, "modulate:a", 0f, RequiemMotion.Enabled ? 0.3 : 0);
        tween.TweenCallback(Callable.From(() =>
        {
            if (NModalContainer.Instance is { } container && ReferenceEquals(container.OpenModal, this)) container.Clear();
            else this.QueueFreeSafely();
        }));
    }

    // ───── элементы ─────

    private static void SetLabelColors(Label label, Color color) => RequiemUi.SetLabelColors(label, color, RequiemUi.Outline);

    private static StyleBoxFlat Box(Color bg, Color border, int width, int radius = 10)
    {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        box.SetBorderWidthAll(width);
        box.SetCornerRadiusAll(radius);
        box.SetContentMarginAll(10);
        return box;
    }

    private static void Style(Button button, Color accent)
    {
        button.AddThemeStyleboxOverride("normal", Box(new Color(0.06f, 0.05f, 0.07f, 0.92f), accent.Darkened(0.45f), 3));
        button.AddThemeStyleboxOverride("hover", Box(new Color(0.1f, 0.08f, 0.1f, 0.95f), accent, 4));
        button.AddThemeStyleboxOverride("pressed", Box(new Color(0.12f, 0.1f, 0.12f, 0.95f), accent.Lightened(0.3f), 5));
        button.AddThemeStyleboxOverride("focus", Box(new Color(0, 0, 0, 0), accent.Lightened(0.2f), 4));
        button.AddThemeStyleboxOverride("disabled", Box(new Color(0.05f, 0.05f, 0.06f, 0.92f), accent.Darkened(0.6f), 3));
    }

    private static Button MakeTextButton(string text, string tooltip)
    {
        var button = new Button { Text = text, TooltipText = tooltip, CustomMinimumSize = new Vector2(360, 56), FocusMode = FocusModeEnum.All };
        button.AddThemeFontOverride("font", RequiemUi.Font);
        button.AddThemeFontSizeOverride("font_size", 26);
        button.AddThemeColorOverride("font_color", StsColors.cream);
        button.AddThemeColorOverride("font_hover_color", Gold);
        button.AddThemeColorOverride("font_disabled_color", Dim);
        button.AddThemeColorOverride("font_outline_color", RequiemUi.Outline);
        button.AddThemeConstantOverride("outline_size", 8);
        Style(button, Gold);
        return button;
    }

    /// <summary>Арка-вариант: картина, название акта, фракция, правила, отметки голосов.</summary>
    private sealed class DoorCard
    {
        private const float ArtWidth = 400, ArtHeight = 573; // картины 600×860 (tools/art/scenes)

        public readonly Button Button;
        private readonly HBoxContainer _pips;
        private readonly Color _accent;
        private Tween? _animation;

        public DoorCard(NActChoiceScreen screen, int door, ActKind kind, bool rolled)
        {
            _accent = AccentOf(kind);
            Button = new Button { CustomMinimumSize = new Vector2(ArtWidth + 28, 0), FocusMode = FocusModeEnum.All, ClipContents = true };
            Style(Button, _accent);
            Button.Pressed += () => screen.Pick(door);
            Button.MouseEntered += () => Hover(true);
            Button.MouseExited += () => Hover(false);
            Button.FocusEntered += () => Hover(true);
            Button.FocusExited += () => Hover(false);
            Button.Resized += () => Button.PivotOffset = Button.Size / 2;
            RequiemMotion.Watch(Button, () =>
            {
                if (RequiemMotion.Enabled) return;
                RequiemMotion.Kill(_animation); _animation = null; Button.Scale = Vector2.One;
            });

            var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            column.SetAnchorsPreset(LayoutPreset.FullRect);
            column.OffsetLeft = 14;
            column.OffsetRight = -14;
            column.OffsetTop = 14;
            column.OffsetBottom = -14;
            column.AddThemeConstantOverride("separation", 8);
            Button.AddChild(column);

            var art = RequiemUi.MakeIcon(ArtPath(kind), new Vector2(ArtWidth, ArtHeight));
            art.CustomMinimumSize = new Vector2(ArtWidth, ArtHeight);
            art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            column.AddChild(art);

            var name = RequiemUi.MakeLabel(34, RequiemUi.Outline);
            name.Text = ActDoors.ModelOf(kind).Title.GetFormattedText();
            RequiemUi.SetLabelColors(name, _accent.Lightened(0.35f), RequiemUi.Outline);
            column.AddChild(name);

            var foes = RequiemUi.MakeLabel(19, RequiemUi.Outline);
            foes.Text = RequiemUi.TipText($"DOORS.{Key(kind)}.foes");
            RequiemUi.SetLabelColors(foes, Dim.Lightened(0.25f), RequiemUi.Outline);
            column.AddChild(foes);

            var rules = RequiemUi.MakeLabel(19, RequiemUi.Outline);
            rules.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            rules.HorizontalAlignment = HorizontalAlignment.Left;
            rules.VerticalAlignment = VerticalAlignment.Top;
            rules.CustomMinimumSize = new Vector2(ArtWidth, 150);
            rules.Text = RulesText(kind);
            column.AddChild(rules);

            _pips = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            _pips.CustomMinimumSize = new Vector2(0, 28);
            column.AddChild(_pips);

            if (rolled)
                Button.TooltipText = RequiemUi.TipText("DOORS.rolledHint");
            Button.CustomMinimumSize = new Vector2(ArtWidth + 28, ArtHeight + 330);
        }

        public void ShowVotes(int count, bool mine, bool coop)
        {
            foreach (var child in _pips.GetChildren()) child.QueueFree();
            if (!coop && !mine) return;
            for (var i = 0; i < count; i++)
            {
                var pip = RequiemUi.MakeLabel(24, RequiemUi.Outline);
                pip.Text = "●";
                RequiemUi.SetLabelColors(pip, i == 0 && mine ? Gold : _accent.Lightened(0.4f), RequiemUi.Outline);
                _pips.AddChild(pip);
            }
            if (mine)
            {
                var you = RequiemUi.MakeLabel(18, RequiemUi.Outline);
                you.Text = " " + RequiemUi.TipText("DOORS.you");
                RequiemUi.SetLabelColors(you, Gold, RequiemUi.Outline);
                _pips.AddChild(you);
            }
        }

        public void Pulse()
        {
            // The choice/vote remains distinguishable with all optional motion disabled.
            Button.AddThemeStyleboxOverride("disabled", Box(new Color(0.12f, 0.1f, 0.1f, 0.95f), Gold, 6));
            RequiemMotion.Kill(_animation); _animation = null;
            if (!RequiemMotion.Enabled) { Button.Scale = Vector2.One; return; }
            var tween = _animation = Button.CreateTween();
            tween.TweenProperty(Button, "scale", new Vector2(1.06f, 1.06f), 0.12);
            tween.TweenProperty(Button, "scale", new Vector2(1.03f, 1.03f), 0.18);
        }

        private void Hover(bool on)
        {
            if (Button.Disabled) return;
            RequiemMotion.Kill(_animation); _animation = null;
            if (!RequiemMotion.Enabled) { Button.Scale = Vector2.One; return; }
            _animation = Button.CreateTween();
            _animation.TweenProperty(Button, "scale", on ? new Vector2(1.03f, 1.03f) : Vector2.One, 0.12);
        }
    }

    // ───── данные вариантов ─────

    /// <summary>Часть ключа текстов (DOORS.{Key}.foes/rules) и имя картины — как Id акта в acts.json.</summary>
    private static readonly Dictionary<ActKind, string> Keys = new()
    {
        [ActKind.FrozenBarrows] = "FROZEN_BARROWS",
        [ActKind.ClockworkDepths] = "CLOCKWORK_DEPTHS",
        [ActKind.BloodCourt] = "BLOOD_COURT",
        [ActKind.StarTower] = "STAR_TOWER",
    };

    private static string Key(ActKind kind) => Keys.TryGetValue(kind, out var key) ? key : Keys[ActKind.StarTower];

    private static string ArtPath(ActKind kind) => RequiemUi.UiImage($"doors/{Key(kind).ToLowerInvariant()}").Replace(".png", ".jpg");

    /// <summary>Цвет контрового света акта (палитра doc 13 / art spec §3.4).</summary>
    private static Color AccentOf(ActKind kind) => kind switch
    {
        ActKind.FrozenBarrows => new Color("9DB0C4"),
        ActKind.ClockworkDepths => new Color("B89A63"),
        ActKind.BloodCourt => new Color("C0505A"),
        _ => new Color("8C9AC8"),
    };

    /// <summary>Правила варианта — только работающие (карта — M6 п. 1: Тьма, вентили, порталы).</summary>
    public static string RulesText(ActKind kind) => kind switch
    {
        ActKind.FrozenBarrows => RequiemUi.TipText("DOORS.FROZEN_BARROWS.rules",
            ("Chill", ActRules.FrostChill), ("Warmth", ActRules.WarmthFights),
            ("Keys", ActRules.MaxClawKeysPerAct), ("Gold", ActRules.VaultGold),
            ("Dark", ActRules.DarknessVisibleRows(false)), ("Sight", ActRules.DarknessVisibleRows(true))),
        ActKind.ClockworkDepths => RequiemUi.TipText("DOORS.CLOCKWORK_DEPTHS.rules",
            ("Period", ActRules.OverheatPeriod), ("Burning", ActRules.OverheatBurning), ("Relief", ActRules.ValveReliefFights)),
        ActKind.BloodCourt => RequiemUi.TipText("DOORS.BLOOD_COURT.rules",
            ("Block", ActRules.DayNightBlock), ("Hp", ActRules.NightHpBonusPercent), ("Gold", ActRules.NightGoldBonus)),
        _ => RequiemUi.TipText("DOORS.STAR_TOWER.rules", ("Mana", ActRules.LeyLineMana)),
    };
}
