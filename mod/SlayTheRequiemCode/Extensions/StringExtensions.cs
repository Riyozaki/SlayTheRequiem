using SlayTheRequiem.SlayTheRequiemCode.Art;

namespace SlayTheRequiem.SlayTheRequiemCode.Extensions;

/// <summary>
/// Пути к картинкам моделей. Порядок поиска везде один: свой файл мода → ванильный арт по таблице
/// (<paramref name="borrowed"/>, см. <see cref="VanillaArt"/>) → заглушка шаблона.
/// </summary>
public static class StringExtensions
{
    private static string Res(params string[] parts)
    {
        var all = new string[parts.Length + 2];
        all[0] = MainFile.ResPath;
        all[1] = "images";
        parts.CopyTo(all, 2);
        return Path.Join(all);
    }

    public static string ImagePath(this string path) => Res(path);

    /// <summary>Малый портрет карты: свой малый → свой большой → ванильный → заглушка.</summary>
    public static string CardImagePath(this string path, string? borrowed = null) =>
        VanillaArt.FirstExisting(Res("card_portraits", "card.png"),
            Res("card_portraits", path), Res("card_portraits", "big", path), borrowed);

    /// <summary>Основной арт карты: свой большой → свой малый → ванильный → заглушка.</summary>
    public static string BigCardImagePath(this string path, string? borrowed = null) =>
        VanillaArt.FirstExisting(Res("card_portraits", "big", "card.png"),
            Res("card_portraits", "big", path), Res("card_portraits", path), borrowed);

    public static string PowerImagePath(this string path, string? borrowed = null) =>
        VanillaArt.FirstExisting(Res("powers", "power.png"), Res("powers", path), borrowed);

    /// <summary>Большой значок силы: свой большой → ванильный большой → ванильный малый → заглушка.</summary>
    public static string BigPowerImagePath(this string path, string? borrowed = null, string? borrowedSmall = null) =>
        VanillaArt.FirstExisting(Res("powers", "big", "power.png"), Res("powers", "big", path), borrowed, borrowedSmall);

    public static string RelicImagePath(this string path, string? borrowed = null) =>
        VanillaArt.FirstExisting(Res("relics", "relic.png"), Res("relics", path), borrowed);

    /// <summary>Большая картинка реликвии: своя большая → ванильная большая → ванильный значок → заглушка.</summary>
    public static string BigRelicImagePath(this string path, string? borrowed = null, string? borrowedSmall = null) =>
        VanillaArt.FirstExisting(Res("relics", "big", "relic.png"), Res("relics", "big", path), borrowed, borrowedSmall);

    /// <summary>Портрет события: свой → ванильный → большая заглушка реликвии.</summary>
    public static string EventImagePath(this string path, string? borrowed = null) =>
        VanillaArt.FirstExisting(Res("relics", "big", "relic.png"), Res("events", path), borrowed);

    public static string CharacterUiPath(this string path) => Res("charui", path);

    /// <summary>Картинка зелья мода; если её нет — ванильная из атласа (<paramref name="vanillaFallback"/>, напр. "blood_potion").</summary>
    public static string PotionImagePath(this string path, string vanillaFallback, bool outline = false)
    {
        var own = outline ? Res("potions", "outline", path) : Res("potions", path);
        return VanillaArt.Exists(own)
            ? own
            : $"res://images/atlases/{(outline ? "potion_outline_atlas" : "potion_atlas")}.sprites/{vanillaFallback}.tres";
    }
}
