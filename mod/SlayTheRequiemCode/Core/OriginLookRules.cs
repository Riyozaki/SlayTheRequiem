namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Чей герб у происхождения: форма щита говорит о герое (docs/design/04 — кланы, племена, звёздные династии).</summary>
public enum OriginCrest
{
    /// <summary>Кланы Латоносца — геральдический щит с металлической каймой.</summary>
    Clan,

    /// <summary>Племена Безмолвной — круглый кожаный щит с перьями.</summary>
    Tribe,

    /// <summary>Звёздные династии Регента — золотой медальон с короной.</summary>
    Dynasty,
}

/// <summary>
/// Облик выбора происхождения и предыстории (M7 п. 3): свои значки <c>images/relics/{key}.png</c> (рисует
/// tools/art/relics/draw_origins.py) и заголовок ванильного экрана «выбери реликвию», когда на нём только происхождения
/// или только предыстории (Relics/OriginLooks.cs). Портреты героев — ванильные: герои мода — это ванильные Латоносец,
/// Безмолвная и Регент. Зеркало — tools/sim/requiem_rules.py (M7OriginLooks).
/// </summary>
public static class OriginLookRules
{
    public static readonly IReadOnlyList<string> ClanOrigins = ["FrostbornOrigin", "ForgebloodOrigin", "DunebladeOrigin", "CitadelOrigin"];
    public static readonly IReadOnlyList<string> TribeOrigins = ["MoonbornOrigin", "ThicketbornOrigin", "MarshbornOrigin"];
    public static readonly IReadOnlyList<string> DynastyOrigins = ["HighbornOrigin", "MistbornOrigin", "AshbornOrigin"];

    public static OriginCrest? CrestOf(string originType) =>
        ClanOrigins.Contains(originType) ? OriginCrest.Clan
        : TribeOrigins.Contains(originType) ? OriginCrest.Tribe
        : DynastyOrigins.Contains(originType) ? OriginCrest.Dynasty
        : null;

    /// <summary>
    /// Ключ заголовка экрана выбора (static_hover_tips, без префикса мода) или null — экран обычный, заголовок ванильный.
    /// Только происхождения одного героя → «Выберите клан / племя / династию»; только предыстории → «Выберите предысторию».
    /// </summary>
    public static string? HeadingKey(IReadOnlyList<string> relicTypes)
    {
        if (relicTypes.Count == 0) return null;
        if (relicTypes.All(t => t.EndsWith("Background"))) return "ORIGIN_PICK.background";
        var crests = relicTypes.Select(CrestOf).Distinct().ToList();
        return crests.Count == 1 && crests[0] is { } crest ? crest switch
        {
            OriginCrest.Clan => "ORIGIN_PICK.clan",
            OriginCrest.Tribe => "ORIGIN_PICK.tribe",
            _ => "ORIGIN_PICK.dynasty",
        } : null;
    }
}
