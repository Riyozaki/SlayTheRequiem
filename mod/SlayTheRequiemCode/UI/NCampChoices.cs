using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SlayTheRequiem.SlayTheRequiemCode.Config;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Новое меню лагеря (плейтест 2026-11: «элементы лагеря одного размера и не влезают в экран»). Лагерь Реквиема
/// добавляет к ванильным «Отдыху» и «Ковке» до 12 действий, а ванильный ряд <c>%ChoicesContainer</c> рассчитан на 3–4.
/// <para>
/// Этот контейнер подменяет ванильный (то же поле <c>_choicesContainer</c>, поэтому вся логика игры — выбор, синхронизация
/// в коопе, подсветка, блокировка — работает как была): ванильные кнопки <see cref="NRestSiteButton"/> раскладываются
/// рядами по категориям — Отдых, Ремесло, Магия, Снаряжение, Особое, — с подписью над рядом. Если рядов много, кнопки
/// уменьшаются, чтобы всё влезло. Появление — рядами снизу вверх.
/// </para>
/// </summary>
public partial class NCampChoices : Container
{
    public enum Category { Rest, Craft, Magic, Gear, Special }

    private const float LabelHeight = 30f;
    private const float RowGap = 10f;
    private const float ColGap = 14f;
    private static readonly Vector2 FallbackButton = new(190, 210);

    private readonly List<(Category cat, float y, float x0, float x1)> _rows = new();
    private bool _animated;

    public static Category CategoryOf(RestSiteOption option)
    {
        var id = option.OptionId.ToUpperInvariant().Replace("SLAYTHEREQUIEM_", "");
        return id switch
        {
            "HEAL" or "SHORT_REST" or "MEDITATE" or "PRAY" or "WARM" => Category.Rest,
            "SMITH" or "FORGE" or "BREW" or "COOK" or "DIG" or "MEND" => Category.Craft,
            "ENCHANT" or "CARD_ENCHANT" => Category.Magic,
            "CHEST" or "TRAIN" or "LIFT" => Category.Gear,
            _ => Category.Special,
        };
    }

    public override void _Notification(int what)
    {
        if (what == NotificationSortChildren) Arrange();
    }

    private void Arrange()
    {
        var buttons = GetChildren().OfType<NRestSiteButton>().Where(b => b.Option != null).ToList();
        _rows.Clear();
        if (buttons.Count == 0) return;

        var baseSize = buttons.Select(b => Max(b.GetCombinedMinimumSize(), b.Size)).Aggregate(Vector2.Zero, Max);
        if (baseSize.X < 40 || baseSize.Y < 40) baseSize = FallbackButton;

        var groups = buttons.GroupBy(b => CategoryOf(b.Option)).OrderBy(g => g.Key).Select(g => (g.Key, g.ToList())).ToList();
        // Мало кнопок — один ряд без подписей, как в ванили.
        var flat = buttons.Count <= 5;
        if (flat) groups = [(Category.Rest, buttons)];
        var labelH = flat ? 0f : LabelHeight;

        var widest = groups.Max(g => g.Item2.Count);
        var scaleW = (Size.X - (widest - 1) * ColGap) / (widest * baseSize.X);
        var scaleH = (Size.Y - groups.Count * (labelH + RowGap)) / (groups.Count * baseSize.Y);
        var scale = Math.Clamp(Math.Min(1f, Math.Min(scaleW, scaleH)), 0.42f, 1f);
        var cell = baseSize * scale;

        var totalH = groups.Count * (cell.Y + labelH) + (groups.Count - 1) * RowGap;
        var y = Math.Max(0, Size.Y - totalH); // ряды прижаты к низу, как ванильные кнопки
        foreach (var (cat, row) in groups)
        {
            var rowW = row.Count * cell.X + (row.Count - 1) * ColGap;
            var x = (Size.X - rowW) / 2;
            _rows.Add((cat, y, x, x + rowW));
            y += labelH;
            foreach (var b in row)
            {
                b.Size = baseSize;
                b.Scale = Vector2.One * scale;
                b.Position = new Vector2(x, y);
                x += cell.X + ColGap;
            }
            y += cell.Y + RowGap;
        }
        QueueRedraw();
        if (!_animated) CallDeferred(MethodName.AnimateRows);
    }

    private void AnimateRows()
    {
        _animated = true;
        var order = 0;
        foreach (var b in GetChildren().OfType<NRestSiteButton>().OrderByDescending(b => b.Position.Y).ThenBy(b => b.Position.X))
        {
            var target = b.Position;
            b.Position = target + new Vector2(0, 36);
            b.CreateTween().TweenProperty(b, "position", target, 0.35).SetDelay(0.04 * order++)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        }
    }

    public override void _Draw()
    {
        if (_rows.Count <= 1 && GetChildren().OfType<NRestSiteButton>().Count() <= 5) return;
        var font = RequiemUi.Font;
        foreach (var (cat, y, x0, x1) in _rows)
        {
            var title = RequiemUi.TipText(TitleKey(cat));
            var mid = (x0 + x1) / 2;
            var baseline = new Vector2(x0, y + LabelHeight - 8);
            DrawStringOutline(font, baseline, title, HorizontalAlignment.Center, x1 - x0, 20, 6, new Color(0, 0, 0, 0.85f));
            DrawString(font, baseline, title, HorizontalAlignment.Center, x1 - x0, 20, StsColors.gold);
            // Тонкие линии по бокам подписи — как разделители в ванильных меню.
            var half = Math.Min(160f, (x1 - x0) / 2 - 10);
            var textHalf = font.GetStringSize(title, HorizontalAlignment.Left, -1, 20).X / 2 + 12;
            if (half > textHalf + 8)
            {
                var ly = y + LabelHeight - 15;
                var line = new Color(StsColors.gold, 0.45f);
                DrawLine(new Vector2(mid - half, ly), new Vector2(mid - textHalf, ly), line, 2f);
                DrawLine(new Vector2(mid + textHalf, ly), new Vector2(mid + half, ly), line, 2f);
            }
        }
    }

    private static string TitleKey(Category cat) => cat switch
    {
        Category.Rest => "CAMP_CAT_REST.title",
        Category.Craft => "CAMP_CAT_CRAFT.title",
        Category.Magic => "CAMP_CAT_MAGIC.title",
        Category.Gear => "CAMP_CAT_GEAR.title",
        _ => "CAMP_CAT_SPECIAL.title",
    };

    private static Vector2 Max(Vector2 a, Vector2 b) => new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
}

/// <summary>Подмена ванильного ряда кнопок лагеря на <see cref="NCampChoices"/> — перед первой раскладкой кнопок.</summary>
[HarmonyPatch(typeof(NRestSiteRoom), "UpdateRestSiteOptions")]
public static class CampChoicesPatch
{
    private static readonly AccessTools.FieldRef<NRestSiteRoom, Control> ContainerRef =
        AccessTools.FieldRefAccess<NRestSiteRoom, Control>("_choicesContainer");

    private static readonly AccessTools.FieldRef<NRestSiteRoom, Control> ScreenRef =
        AccessTools.FieldRefAccess<NRestSiteRoom, Control>("_choicesScreen");

    [HarmonyPrefix]
    public static void Prefix(NRestSiteRoom __instance)
    {
        try
        {
            if (!RunSettings.EnableRequiemSystems) return;
            var old = ContainerRef(__instance);
            if (old == null || old is NCampChoices || old.GetParent() is not Control parent) return;
            // Родитель-Container перехватил бы размер и позицию — тогда вешаем меню прямо на экран выбора
            // (он плавно гаснет/появляется вместе с выбором, так что анимации ванили сохраняются).
            if (parent is Container) parent = ScreenRef(__instance) ?? parent;
            if (parent is Container) return;

            var menu = new NCampChoices { Name = "RequiemCampChoices", MouseFilter = Control.MouseFilterEnum.Ignore, TopLevel = false };
            parent.AddChild(menu);
            if (old.GetParent() == parent) parent.MoveChild(menu, old.GetIndex());
            // Шире и выше ванильного ряда: на всю ширину экрана, вверх — до 30 % высоты.
            var view = __instance.GetViewportRect().Size;
            var rect = old.GetGlobalRect();
            var top = Math.Min(rect.Position.Y, view.Y * 0.30f);
            var bottom = Math.Max(rect.End.Y, top + 260);
            menu.GlobalPosition = new Vector2(view.X * 0.04f, top);
            menu.Size = new Vector2(view.X * 0.92f, bottom - top);
            foreach (var child in old.GetChildren()) child.QueueFree();
            old.Visible = false;
            ContainerRef(__instance) = menu;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"UI: меню лагеря: {e.Message}");
        }
    }
}
