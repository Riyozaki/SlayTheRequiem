using BaseLib.Patches.UI;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Resources;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Интерфейс систем мода (docs/plan/art-ui-animation-spec.md §4, completion-plan M1). Все узлы строятся кодом,
/// без .tscn: значки ◆ и типа урона на карте (<see cref="NStaminaCostBadge"/>, <see cref="NDamageTypeBadge"/>),
/// вес карты (<see cref="NWeightBadge"/>), шкалы ◆/✦ героя (<see cref="NRequiemResourceBars"/>), точки ◆/✦ врага
/// (<see cref="NEnemyResourcePips"/>), цена на намерении (<see cref="IntentCostPatch"/>) и шкала груза
/// (<see cref="NBurdenMeter"/>). Картинки — свои минимальные PNG из images/ui (art spec §0: остальное — ваниль).
/// </summary>
public static class RequiemUi
{
    // ───── палитра (art spec §1.1) ─────
    public static readonly Color StaminaColor = new("E8A33A");
    public static readonly Color ManaColor = new("5EC8F2");
    public static readonly Color BurdenColor = new("9C7A4A");
    public static readonly Color TiredColor = new("F2D06B");
    public static readonly Color ExhaustedColor = new("FF8A3D");
    public static readonly Color OverstrainColor = new("FF5555");
    public static readonly Color Outline = new("2A1A08");
    public static readonly Color BarBack = new(0f, 0f, 0f, 0.62f);

    /// <summary>Цвет типа урона — общий с боевыми эффектами (<see cref="VfxRules.DamageTint"/>); физический — кремовый.</summary>
    public static Color DamageTypeColor(DamageType type) =>
        type == DamageType.Physical ? StsColors.cream : new Color(VfxRules.DamageTint(type));

    // ───── картинки ─────
    public static string UiImage(string name) => $"{MainFile.ResPath}/images/ui/{name}.png";

    public static string? DamageTypeIcon(DamageType type) =>
        type == DamageType.Physical ? null : UiImage($"damage_types/{type.ToString().ToLowerInvariant()}");

    private static readonly Dictionary<string, Texture2D?> Textures = new();

    /// <summary>Текстура мода или null (нет файла — элемент рисуется без картинки, а не падает).</summary>
    public static Texture2D? Texture(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (Textures.TryGetValue(path, out var cached)) return cached;
        Texture2D? tex = null;
        try
        {
            if (ResourceLoader.Exists(path)) tex = ResourceLoader.Load<Texture2D>(path);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"UI: не удалось загрузить {path}: {e.Message}");
        }
        Textures[path] = tex;
        return tex;
    }

    // ───── шрифт и подписи ─────
    private const string FontPath = "res://themes/kreon_bold_shared.tres";
    private static Font? _font;

    public static Font Font
    {
        get
        {
            if (_font != null) return _font;
            try { _font = PreloadManager.Cache.GetAsset<Font>(FontPath); }
            catch { _font = null; }
            return _font ??= ThemeDB.FallbackFont;
        }
    }

    /// <summary>Подпись в стиле цены энергии на карте: кремовый текст, тёмная обводка, тень.</summary>
    public static Label MakeLabel(int fontSize, Color outline)
    {
        var label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.Off,
        };
        label.AddThemeFontOverride(ThemeConstants.Label.Font, Font);
        label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, fontSize);
        label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, Math.Max(4, fontSize / 2));
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        SetLabelColors(label, StsColors.cream, outline);
        label.AddThemeColorOverride(ThemeConstants.Label.FontShadowColor, new Color(0, 0, 0, 0.25f));
        return label;
    }

    public static void SetLabelColors(Label label, Color text, Color outline)
    {
        label.AddThemeColorOverride(ThemeConstants.Label.FontColor, text);
        label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, outline);
    }

    public static TextureRect MakeIcon(string? path, Vector2 size)
    {
        var icon = new TextureRect
        {
            Texture = Texture(path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = size,
        };
        return icon;
    }

    // ───── тексты (localization/*/static_hover_tips.json, генератор tools/loc/loc_ui.py) ─────
    public const string TipTable = "static_hover_tips";

    public static LocString Tip(string key) => new(TipTable, $"{MainFile.ModId.ToUpperInvariant()}-{key}");

    /// <summary>Строка с переменными SmartFormat ({Amount} и т. п.).</summary>
    public static LocString TipLoc(string key, params (string name, decimal value)[] vars)
    {
        var loc = Tip(key);
        foreach (var (name, value) in vars) loc.Add(name, value);
        return loc;
    }

    public static string TipText(string key, params (string name, decimal value)[] vars) => TipLoc(key, vars).GetFormattedText();

    // ───── данные ─────

    /// <summary>Платит ли карта ◆: атака персонажа в системе мода, кроме заклинаний, магических атак и Кличей.</summary>
    public static bool PaysStamina(CardModel card)
    {
        if (!RequiemConfig.EnableRequiemSystems || card.Type != CardType.Attack || StaminaCosts.IsStaminaExempt(card)) return false;
        var owner = StaminaCosts.OwnerOf(card);
        return owner != null ? RequiemMode.IsInSystem(owner) : !HiddenHeroes.IsHiddenPool(card.Pool);
    }

    /// <summary>Наносит ли карта урон определённого типа (для значка стихии).</summary>
    public static DamageType? VisibleDamageType(CardModel card)
    {
        if (!RequiemConfig.EnableRequiemSystems) return null;
        if (card.Type != CardType.Attack && card is not Spells.SpellCard) return null;
        var type = StaminaCosts.DamageTypeFor(card);
        return type == DamageType.Physical ? null : type;
    }

    /// <summary>
    /// Стихия карты для цвета названия: атаки и заклинания — по типу урона, карты мода любого типа — по объявленной
    /// стихии (навыки Горения, Озноба…), ванильные навыки Яда — по <c>PowerVar&lt;PoisonPower&gt;</c>.
    /// </summary>
    public static DamageType? TitleDamageType(CardModel card)
    {
        if (!RequiemConfig.EnableRequiemSystems || !RequiemConfig.ColorElementalTitles) return null;
        if (VisibleDamageType(card) is { } shown) return shown;
        if (card is IRequiemCard { DamageType: not DamageType.Physical } custom) return custom.DamageType;
        if (card.Type is CardType.Curse or CardType.Status) return null;
        return card.DynamicVars.ContainsKey(nameof(MegaCrit.Sts2.Core.Models.Powers.PoisonPower)) ? DamageType.Poison : null;
    }

    /// <summary>Цвет названия: те же оттенки, что у значков стихии, но светлее — тёмные (Хворь, Магия) иначе не читаются.</summary>
    public static Color TitleColor(DamageType type) => type switch
    {
        DamageType.Blight => new Color("B7C27A"),
        DamageType.Magic => new Color("D9A6FF"),
        _ => DamageTypeColor(type),
    };

    /// <summary>Обводка названия: тёмный оттенок стихии (у улучшенных — ванильная зелёная, «+» в названии остаётся).</summary>
    public static Color TitleOutline(DamageType type) => DamageTypeColor(type).Darkened(0.72f);

    /// <summary>Состояние ◆ при оплате атаки ценой <paramref name="cost"/>; null вне боя.</summary>
    public static (StaminaState state, bool overstrain)? StaminaStateFor(Player? player, int cost)
    {
        var stamina = RequiemResource.Of<StaminaResource>(player);
        if (stamina == null) return null;
        var state = StaminaRules.StateFor(stamina.Amount, stamina.Pool, cost);
        if (state == StaminaState.Tired && RequiemCombatRules.IsTireless(player)) state = StaminaState.Rested;
        return (state, cost > stamina.Amount && RequiemConfig.EnableOverstrain);
    }

    /// <summary>Цвет ступени перегруза: норма / 1–5 / 6–10 / больше 10 (02 §3).</summary>
    public static Color BurdenTint(BurdenRules.Overload overload) => overload.Excess switch
    {
        0 => StsColors.cream,
        <= 5 => TiredColor,
        <= 10 => ExhaustedColor,
        _ => OverstrainColor,
    };

    public static string BurdenEffectKey(BurdenRules.Overload overload) => overload.Excess switch
    {
        0 => "BURDEN_OK",
        <= 5 => "BURDEN_LIGHT",
        <= 10 => "BURDEN_HEAVY",
        _ => "BURDEN_CRUSHING",
    };

    // ───── регистрация ─────
    private static bool _registered;

    /// <summary>Вызывается из <see cref="MainFile.Initialize"/>; Harmony-патчи UI подключает PatchAll.</summary>
    public static void Register()
    {
        if (_registered) return;
        _registered = true;
        ExtraCardUi.RegisterCreateCardUiElement(ExtraCardUi.CardUiPositioning.Left,
            _ => new NStaminaCostBadge(), (card, model, badge) => badge.Refresh(card, model));
        ExtraCardUi.RegisterCreateCardUiElement(ExtraCardUi.CardUiPositioning.Left,
            _ => new NDamageTypeBadge(), (card, model, badge) => badge.Refresh(card, model));
        ExtraCombatUi.RegisterCombatUiElement((ui, player, _) =>
        {
            if (!RequiemMode.IsInSystem(player)) return null;
            var meter = new NBurdenMeter(player, compact: true);
            ui.AddChild(meter);
            return meter;
        }, ExtraCombatUi.CombatUiPositioning.Left);
    }
}

/// <summary>События игровой логики, на которые реагирует интерфейс (вспышка надрыва).</summary>
public static class RequiemUiEvents
{
    /// <summary>Игрок оплатил атаку здоровьем (надрыв).</summary>
    public static event Action<Player>? Overstrained;

    public static void RaiseOverstrained(Player player)
    {
        try { Overstrained?.Invoke(player); }
        catch (Exception e) { MainFile.Logger.Warn($"UI: Overstrained handler: {e.Message}"); }
    }
}
