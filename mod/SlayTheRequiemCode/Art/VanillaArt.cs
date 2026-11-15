using Godot;

namespace SlayTheRequiem.SlayTheRequiemCode.Art;

/// <summary>
/// Откуда модель мода берёт картинку (docs/plan/art-ui-animation-spec.md §0, «сначала ванильное»):
/// <list type="number">
/// <item>свой файл в <c>res://SlayTheRequiem/images/…</c> — если художник его положил;</item>
/// <item>ванильный портрет/значок по таблице <see cref="CardTable"/>… (генерируется из <c>tools/art/vanilla_art.csv</c>);</item>
/// <item>заглушка шаблона.</item>
/// </list>
/// Проверки <see cref="ResourceLoader.Exists(string, string)"/> кэшируются, «нет картинки» пишется в лог один раз на путь
/// (раньше — при каждом обращении к пути, ревью R-21).
/// </summary>
public static partial class VanillaArt
{
    private static readonly Dictionary<string, bool> ExistsCache = new();
    private static readonly HashSet<string> Reported = new();
    private static readonly object Gate = new();

    /// <summary>Есть ли ресурс (с кэшем: пути не меняются за время работы игры).</summary>
    public static bool Exists(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        lock (Gate)
        {
            if (ExistsCache.TryGetValue(path, out var known)) return known;
            var exists = ResourceLoader.Exists(path);
            ExistsCache[path] = exists;
            return exists;
        }
    }

    /// <summary>Первый существующий путь из <paramref name="candidates"/>, иначе <paramref name="placeholder"/> (с одной строкой в лог).</summary>
    public static string FirstExisting(string placeholder, params string?[] candidates)
    {
        foreach (var path in candidates)
            if (Exists(path)) return path!;
        var wanted = candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c)) ?? placeholder;
        lock (Gate)
        {
            if (Reported.Add(wanted)) MainFile.Logger.Info($"Art: нет картинки {wanted} — заглушка {placeholder}");
        }
        return placeholder;
    }

    private static string? Lookup(Dictionary<string, string> table, string modClass) =>
        table.TryGetValue(modClass, out var id) ? id : null;

    /// <summary>Портрет ванильной карты (спрайт атласа, как <c>CardModel.PortraitPath</c>).</summary>
    public static string? CardPortrait(string modClass) =>
        Lookup(CardTable, modClass) is { } id ? $"res://images/atlases/card_atlas.sprites/{id}.tres" : null;

    /// <summary>Малый значок ванильной силы (как <c>PowerModel.PackedIconPath</c>).</summary>
    public static string? PowerIcon(string modClass) =>
        Lookup(PowerTable, modClass) is { } id ? $"res://images/atlases/power_atlas.sprites/{id}.tres" : null;

    /// <summary>Большой значок ванильной силы (как <c>PowerModel.BigIconPath</c>).</summary>
    public static string? PowerBigIcon(string modClass) =>
        Lookup(PowerTable, modClass) is { } id ? $"res://images/powers/{id}.png" : null;

    /// <summary>Значок ванильной реликвии (как <c>RelicModel.PackedIconPath</c>).</summary>
    public static string? RelicIcon(string modClass) =>
        Lookup(RelicTable, modClass) is { } id ? $"res://images/atlases/relic_atlas.sprites/{id}.tres" : null;

    /// <summary>Контур ванильной реликвии (как <c>RelicModel.PackedIconOutlinePath</c>).</summary>
    public static string? RelicOutline(string modClass) =>
        Lookup(RelicTable, modClass) is { } id ? $"res://images/atlases/relic_outline_atlas.sprites/{id}.tres" : null;

    /// <summary>Большая картинка ванильной реликвии (как <c>RelicModel.BigIconPath</c>).</summary>
    public static string? RelicBigIcon(string modClass) =>
        Lookup(RelicTable, modClass) is { } id ? $"res://images/relics/{id}.png" : null;

    /// <summary>Портрет ванильного события (как <c>EventModel.InitialPortraitPath</c>).</summary>
    public static string? EventPortrait(string modClass) =>
        Lookup(EventTable, modClass) is { } id ? $"res://images/events/{id}.png" : null;

    /// <summary>Значок ванильного зачарования (как <c>EnchantmentModel.IntendedIconPath</c>).</summary>
    public static string? EnchantmentIcon(string modClass) =>
        Lookup(EnchantmentTable, modClass) is { } id ? $"res://images/enchantments/{id}.png" : null;

    /// <summary>Заглушка значка зачарования из игры.</summary>
    public const string MissingEnchantmentIcon = "res://images/enchantments/missing_enchantment.png";
}
