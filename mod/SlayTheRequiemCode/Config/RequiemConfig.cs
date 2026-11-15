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

    /// <summary>Автоуворот (docs/design/02 §4.2): тратить заряд Уворота и 2 ◆ автоматически. Выкл — Уворот не срабатывает.</summary>
    public static bool AutoDodge { get; set; } = true;

    /// <summary>
    /// Происхождение Регента (docs/design/04): из настроек, «Случайно» — по сиду забега, «На выбор» — экран выбора
    /// реликвии при входе в первую комнату (этап 6).
    /// </summary>
    [ConfigSection("HeroSection")]
    public static RegentOriginChoice RegentOrigin { get; set; } = RegentOriginChoice.Random;

    /// <summary>Клан Латоносца (docs/design/04): Инеерождённые, Горнокровные, Дюнные клинки, Цитадельцы; «Случайно» — по сиду забега.</summary>
    public static PlatebearerOriginChoice PlatebearerOrigin { get; set; } = PlatebearerOriginChoice.Random;

    /// <summary>Племя Безмолвной (docs/design/04).</summary>
    public static SilentOriginChoice SilentOrigin { get; set; } = SilentOriginChoice.Random;

    /// <summary>
    /// Предыстория (для всех героев): 1-я/2-я/3-я предыстория выбранного происхождения или «Бродяга».
    /// Ещё не сделанные предыстории (дары Князей — этап 4) при выборе заменяются случайной готовой.
    /// </summary>
    public static BackgroundChoice Background { get; set; } = BackgroundChoice.Random;

    /// <summary>
    /// Некробиндер и Дефект (docs/design/06 §4): в моде это облики превращений, а не герои, поэтому на экране выбора
    /// скрыты, а их карты не попадают в награды и случайную генерацию. Вкл — вернуть их как ванильных героев
    /// (системы мода на них не действуют). В коопе должна совпадать у всех.
    /// </summary>
    public static bool ShowVanillaOnlyHeroes { get; set; } = false;

    /// <summary>Название стихийной карты окрашено в цвет стихии (огонь, холод, разряд, яд…). Только оформление.</summary>
    public static bool ColorElementalTitles { get; set; } = true;

    /// <summary>Кукла героя (docs/design/07 §8): в бою оружие и щит в руках цвета материала, аура брони. Только оформление.</summary>
    public static bool ShowPaperDoll { get; set; } = true;

    /// <summary>
    /// Пять актов (docs/design/13 §2): 1 → 2а/2б → Улей → 4а/4б → Слава. Выкл — ванильные три акта (с оверлеями).
    /// Меняется до начала забега; в коопе должна совпадать у всех.
    /// </summary>
    [ConfigSection("RunSection")]
    public static bool FiveActs { get; set; } = true;

    /// <summary>«Короткий забег»: акты 1 → 3 → 5 — Подножие, Улей, Слава с оверлеями мода (docs/design/13 §2).</summary>
    public static bool ShortRun { get; set; } = false;

    /// <summary>
    /// Вариант актов 2 и 4 (docs/design/13 §2): «Две двери» — после босса актов 1 и 3 экран выбора (в коопе — голосование),
    /// «Случайно» — вариант выпадает при создании забега, экрана нет (в коопе такой игрок воздерживается).
    /// </summary>
    public static ActVariantMode ActVariant { get; set; } = ActVariantMode.Doors;

    /// <summary>Уровень «Реквием» (docs/design/11 §7): −1 — по Возвышению (Возвышение N → уровень N + 1), 0–10 — свой.</summary>
    [ConfigSlider(-1, 10, 1)]
    public static int RequiemLevelSetting { get; set; } = -1;

    /// <summary>
    /// Супербоссы Багрового портала (docs/design/11 §6): «Выкл», «На выбор» (после босса акта — вопрос, войти ли в портал),
    /// «Всегда». На «Реквиеме 10» портал обязателен.
    /// </summary>
    public static SuperbossMode Superbosses { get; set; } = SuperbossMode.Optional;

    /// <summary>Ширина карты 9 в новых актах (docs/design/13 §4). Выкл — ванильные 7 колонок.</summary>
    public static bool WideMaps { get; set; } = true;

    /// <summary>Лавки Реквиема (docs/design/14): вид лавки на узле, свои витрины, доверие, продажа, золото ×1.3.</summary>
    public static bool ShopVariety { get; set; } = true;

    /// <summary>
    /// Кража в лавках (docs/design/14 §1) без источника. Источники — Знамение Ловкача, Кольцо Безликого Вора, Лунная
    /// порода-Плут — появятся на этапах 4–6; до тех пор кражу можно включить здесь. В коопе должна совпадать у всех.
    /// </summary>
    public static bool AllowTheft { get; set; } = false;

    /// <summary>
    /// Пантеон (docs/design/05): Знамение у Нео, Святилища, Тёмные святилища и Камни отлива вместо части «?» в актах 2+.
    /// В коопе должен совпадать у всех.
    /// </summary>
    public static bool EnablePantheon { get; set; } = true;

    /// <summary>
    /// Метапрогресс лагеря (docs/design/10 §5, открытый вопрос ROADMAP №2): победы в забегах по очереди открывают постройки —
    /// Котёл, Наковальня, Зачаровальный стол, Алтарь, Палатка травника; открытая стоит в каждом лагере. Только бонус: без
    /// него лагерь прежний. Действует в одиночной игре (в коопе настройки у игроков разные).
    /// </summary>
    public static bool EnableCampMeta { get; set; } = false;

    /// <summary>Сколько построек лагеря открыто победами (0–5). Не настройка — счётчик, переживает сброс по умолчанию.</summary>
    [ConfigHideInUI]
    [ConfigIgnoreRestoreDefaults]
    public static int CampBuildingsUnlocked { get; set; } = 0;

    /// <summary>Камень Памяти (docs/design/15): Отпечаток в начале акта и возврат с экрана поражения.</summary>
    [ConfigSection("DeathSection")]
    public static bool EnableMemoryStone { get; set; } = true;

    /// <summary>Свечи Реквиема за забег: −1 — по уровню «Реквием», 0 — классический рогалик.</summary>
    [ConfigSlider(-1, 9, 1)]
    public static int MemoryStoneCandles { get; set; } = -1;
}

public enum ActVariantMode
{
    Doors,
    Random,
}

public enum SuperbossMode
{
    Off,
    Optional,
    Always,
}

public enum PlatebearerOriginChoice
{
    Random,
    Frostborn,
    Forgeblood,
    Duneblade,
    Citadel,
    None,
    /// <summary>Выбор на экране в начале забега.</summary>
    Choose,
}

public enum SilentOriginChoice
{
    Random,
    Moonborn,
    Thicketborn,
    Marshborn,
    None,
    /// <summary>Выбор на экране в начале забега (этап 6).</summary>
    Choose,
}

public enum BackgroundChoice
{
    Random,
    First,
    Second,
    Third,
    Vagrant,
    None,
    /// <summary>Выбор на экране в начале забега (этап 6).</summary>
    Choose,
}

public enum RegentOriginChoice
{
    Random,
    Highborn,
    Mistborn,
    Ashborn,
    None,
    /// <summary>Выбор на экране в начале забега (этап 6).</summary>
    Choose,
}
