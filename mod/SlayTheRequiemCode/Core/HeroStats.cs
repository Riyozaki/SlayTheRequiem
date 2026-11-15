namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Базовые статы героя (docs/design/04, таблица «Базовые статы героев»).</summary>
public sealed record HeroStats(
    int StaminaPool,
    int StaminaRegen,
    int ManaPool,
    int ManaRegen,
    int BurdenCapacity,
    int Poise)
{
    public static readonly HeroStats Platebearer = new(8, 3, 3, 1, 32, 3);
    public static readonly HeroStats Silent = new(7, 3, 5, 1, 26, 2);
    public static readonly HeroStats Regent = new(5, 2, 10, 2, 24, 2);
    /// <summary>Для незнакомых (модовых/ванильных вне системы) персонажей — нейтральные значения.</summary>
    public static readonly HeroStats Fallback = new(7, 3, 5, 1, 26, 2);

    /// <summary>
    /// Сопоставление по имени класса персонажа игры. Латоносец живёт на модели Ironclad.
    /// </summary>
    public static HeroStats ForCharacterTypeName(string? typeName) => typeName switch
    {
        "Ironclad" => Platebearer,
        "Silent" => Silent,
        "Regent" => Regent,
        _ => Fallback,
    };
}
