namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Перекраска свиты супербосса: сдвиг тона (hsv-шейдер Spine, доля круга 0–1) и множитель цвета узла (Modulate).</summary>
public readonly record struct CourtTint(float Hue, float R, float G, float B);

/// <summary>
/// Облик супербоссов (M7 п. 1, docs/design/11 §6, docs/plan/completion-plan.md M7): свой спрайт <c>images/monsters/{ArtKey}.png</c>
/// у каждого из восьми и у Рыцаря-эха, картинка-вариант по состоянию боя (стойка, кокон, приземление, снятая неуязвимость).
/// Свита — ванильные модели с фракционной перекраской <see cref="TintFor"/>. Зеркало — tools/sim/requiem_rules.py (M7Looks).
/// </summary>
public static class SuperbossLookRules
{
    public const string GuardArt = "guard";
    public const string CocoonArt = "cocoon";
    public const string LandedArt = "landed";
    public const string ExposedArt = "exposed";

    /// <summary>Рыцарь-эхо (элита Безымянного предела) — призрачный отзвук Чёрного Странника.</summary>
    public const string EchoKnightArt = "echo_knight";

    /// <summary>Множитель цвета свиты сверх тона: чем ниже, тем темнее и насыщеннее фракционный цвет.</summary>
    public const float MinTintChannel = 0.4f;

    public static string ArtKey(Superboss boss) => boss switch
    {
        Superboss.BlackWanderer => "black_wanderer",
        Superboss.BarrowReaper => "barrow_reaper",
        Superboss.DeepMasterSmith => "deep_master_smith",
        Superboss.GatebreakerEnvoy => "gatebreaker_envoy",
        Superboss.BloodCourtLord => "blood_court_lord",
        Superboss.FirstSummoner => "first_summoner",
        Superboss.WorldEater => "world_eater",
        _ => "nameless_shadow",
    };

    /// <summary>Картинки-варианты (суффиксы файла), которые нужно подгрузить заранее.</summary>
    public static IReadOnlyList<string> Variants(Superboss boss) => boss switch
    {
        Superboss.BlackWanderer => [GuardArt],
        Superboss.BloodCourtLord => [CocoonArt],
        Superboss.WorldEater => [LandedArt],
        Superboss.NamelessShadow => [ExposedArt],
        _ => [],
    };

    /// <summary>
    /// Картинка по состоянию (null — основная): Странник в Стойке блока (герои не атаковали) — «guard»; Владыка под Неуязвимостью
    /// (кокон, пока живы гаргульи) — «cocoon»; Пожиратель без Полёта — «landed»; Тень без Неуязвимости (убита душа) — «exposed».
    /// </summary>
    public static string? Variant(Superboss boss, bool guarding, bool invulnerable, bool flying) => boss switch
    {
        Superboss.BlackWanderer => guarding ? GuardArt : null,
        Superboss.BloodCourtLord => invulnerable ? CocoonArt : null,
        Superboss.WorldEater => flying ? null : LandedArt,
        Superboss.NamelessShadow => invulnerable ? null : ExposedArt,
        _ => null,
    };

    /// <summary>Свита супербосса по имени класса врага (только своя: Врата Бездны общие с элитами Бездны — не красим).</summary>
    public static Superboss? CourtOf(string monsterType) => monsterType switch
    {
        "WraithThrall" => Superboss.BlackWanderer,
        "BarrowDead" => Superboss.BarrowReaper,
        "SteamValve" => Superboss.DeepMasterSmith,
        "GargoyleServant" => Superboss.BloodCourtLord,
        "DragonEgg" or "CultistThrall" => Superboss.FirstSummoner,
        "FacelessPriest" or "DevouredSoulGhost" => Superboss.WorldEater,
        "ShadowSoul" => Superboss.NamelessShadow,
        _ => null,
    };

    /// <summary>Цвет фракции: соли душ, иней, латунь, пламя Бездны, кровь, арканные молнии, магма, пустота (art spec §3.10).</summary>
    public static CourtTint TintFor(Superboss boss) => boss switch
    {
        Superboss.BlackWanderer => new(0.60f, 0.70f, 0.82f, 1.00f),
        Superboss.BarrowReaper => new(0.52f, 0.80f, 0.95f, 1.00f),
        Superboss.DeepMasterSmith => new(0.08f, 1.00f, 0.86f, 0.62f),
        Superboss.GatebreakerEnvoy => new(0.00f, 1.00f, 0.55f, 0.45f),
        Superboss.BloodCourtLord => new(0.96f, 1.00f, 0.50f, 0.56f),
        Superboss.FirstSummoner => new(0.70f, 0.80f, 0.74f, 1.00f),
        Superboss.WorldEater => new(0.04f, 1.00f, 0.66f, 0.44f),
        _ => new(0.78f, 0.55f, 0.45f, 0.75f),
    };

    /// <summary>Итоговый тон свиты поверх случайного разброса игры у одинаковых врагов (0–0,05), по кругу 0–1.</summary>
    public static float CombinedHue(float courtHue, float gameHue)
    {
        var h = (courtHue + gameHue) % 1f;
        return h < 0 ? h + 1f : h;
    }
}
