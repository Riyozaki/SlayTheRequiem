namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Множитель цвета (Modulate) и цвет фона комнаты сундука акта.</summary>
public readonly record struct ActTint(float R, float G, float B, string Backdrop);

/// <summary>
/// Свои фоны актов Реквиема (M7 п. 2, docs/plan/art-ui-animation-spec.md §3.10): <c>images/acts/{ArtKey}/…</c> — бой (задник и
/// передний план), лагерь, три куска карты; сундук — перекраска ванильной комнаты. Ванильные акты (Подножие, Улей, Слава) не трогаем.
/// Рисует tools/art/acts/build_acts.py, подключает Acts/ActLooks.cs. Зеркало — tools/sim/requiem_rules.py (M7ActLooks).
/// </summary>
public static class ActLookRules
{
    public const string CombatBack = "combat.jpg";
    public const string CombatFore = "combat_fg.png";
    public const string Camp = "camp.jpg";
    public const string MapTop = "map_top.png";
    public const string MapMiddle = "map_middle.png";
    public const string MapBottom = "map_bottom.png";

    public static readonly IReadOnlyList<string> Pieces = [CombatBack, CombatFore, Camp, MapTop, MapMiddle, MapBottom];

    /// <summary>Половина размера ванильного слоя боя (TextureRect вокруг центра NCombatBackground, текстура 2048×960).</summary>
    public const float CombatHalfWidth = 1440.77f;
    public const float CombatHalfHeight = 675.36f;

    /// <summary>Прямоугольник фона ванильного лагеря (RestSiteBG у glory/hive/overgrowth, RestSiteBG2 у underdocks), текстура 3072×1440.</summary>
    public static readonly float[] CampRect = [-444f, -139f, 2320.8f, 1157f];

    public static string? ArtKey(ActKind kind) => kind switch
    {
        ActKind.FrozenBarrows => "frozen_barrows",
        ActKind.ClockworkDepths => "clockwork_depths",
        ActKind.BloodCourt => "blood_court",
        ActKind.StarTower => "star_tower",
        ActKind.NamelessLimit => "nameless_limit",
        _ => null,
    };

    /// <summary>Путь картинки внутри images/ мода (без корня) или null — у ванильного акта своих фонов нет.</summary>
    public static string? PiecePath(ActKind kind, string piece) => ArtKey(kind) is { } key ? $"acts/{key}/{piece}" : null;

    /// <summary>
    /// Узел сцены лагеря, на который кладётся наш фон: у underdocks основной RestSiteBG — маленький прямоугольник с шейдером воды,
    /// полноразмерный там RestSiteBG2.
    /// </summary>
    public static string CampTarget(string lookId) => lookId == "underdocks" ? "RestSiteBG2" : "RestSiteBG";

    /// <summary>
    /// Остаётся ли видимым прямой потомок сцены лагеря: костёр (брёвна, огонь, свет — RestSiteLighting обязателен для NRestSiteRoom)
    /// и наш фон; ванильные «тематические» узлы (передние планы, листва, вода, звёзды, жуки, пепел) прячем.
    /// </summary>
    public static bool CampKeeps(string nodeName, string target) =>
        nodeName == target || nodeName == "RestSiteLighting" || nodeName.Contains("Log") || nodeName.StartsWith("light_small");

    /// <summary>Сундук: ванильная Spine-комната акта (act2/act3), подкрашенная в цвет нашего акта, и тёмный фон вокруг неё.</summary>
    public static ActTint? ChestTint(ActKind kind) => kind switch
    {
        ActKind.FrozenBarrows => new(0.80f, 0.92f, 1.00f, "0E1620"),
        ActKind.ClockworkDepths => new(1.00f, 0.88f, 0.70f, "1E1408"),
        ActKind.BloodCourt => new(1.00f, 0.72f, 0.74f, "1A0608"),
        ActKind.StarTower => new(0.82f, 0.84f, 1.00f, "0E0E24"),
        ActKind.NamelessLimit => new(0.78f, 0.70f, 0.92f, "0E0A16"),
        _ => null,
    };
}
