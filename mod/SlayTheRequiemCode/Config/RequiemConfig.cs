using BaseLib.Config;

namespace SlayTheRequiem.SlayTheRequiemCode.Config;

/// <summary>Настройки мода (меню модов BaseLib). Полный список режимов — docs/design/13, 15.</summary>
[ConfigHoverTipsByDefault]
internal class RequiemConfig : SimpleModConfig
{
    /// <summary>Главный выключатель: ◆/✦ и статусы Реквиема для Латоносца, Безмолвной и Регента.</summary>
    [ConfigSection("CoreSection")]
    public static bool EnableRequiemSystems { get; set; } = true;

    /// <summary>Надрыв: атака без ◆ всё равно играется, но стоит ♥. Если выключено — карта просто бьёт вполсилы.</summary>
    public static bool EnableOverstrain { get; set; } = true;

    /// <summary>♥ ванильных врагов в процентах (docs/design/11 §1: +10–15 %).</summary>
    [ConfigSlider(80, 150, 5)]
    public static int EnemyHpPercent { get; set; } = 110;

    /// <summary>
    /// Происхождение Регента (docs/design/04). Пока выбор происхождений в начале забега не сделан (этап 6, экран
    /// выбора), берётся из настроек. «Случайно» — по сиду забега.
    /// </summary>
    [ConfigSection("HeroSection")]
    public static RegentOriginChoice RegentOrigin { get; set; } = RegentOriginChoice.Random;

    /// <summary>«Короткий забег»: акты 1 → 3 → 5 (docs/design/13 §1). Пока не используется кодом.</summary>
    [ConfigSection("RunSection")]
    public static bool ShortRun { get; set; } = false;
}

public enum RegentOriginChoice
{
    Random,
    Highborn,
    Mistborn,
    Ashborn,
    None,
}
