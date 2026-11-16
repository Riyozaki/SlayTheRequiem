namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Исход зелья «Похмелье?» (Бутыль Хмельного).</summary>
public enum Hangover { Heal, Stamina, Mana, Strength, Headache }

/// <summary>Числа новых реликвий рюкзака (docs/design/07 §7 «Прочее», M4 п. 5).</summary>
public static class BackpackRules
{
    /// <summary>Мешок-Перышко: +8 груза.</summary>
    public const int FeatherBurden = 8;
    /// <summary>Точильный камень: Оружейные атаки +1 на первом ходу.</summary>
    public const int SharpeningDamage = 1;
    /// <summary>Карта Курганов: сколько ближайших элит видно.</summary>
    public const int BarrowElitesShown = 3;

    // «Похмелье?»: случайный эффект — четыре хороших и один «похмельный».
    public const int HangoverHeal = 6;
    public const int HangoverStamina = 3;
    public const int HangoverMana = 3;
    public const int HangoverStrength = 1;
    /// <summary>Головная боль: Слабость 1 себе, зато добор 2.</summary>
    public const int HangoverWeak = 1;
    public const int HangoverDraw = 2;

    public static readonly Hangover[] Outcomes = Enum.GetValues<Hangover>();

    /// <summary>Исход по броску <paramref name="roll"/> ∈ [0, Outcomes.Length).</summary>
    public static Hangover Roll(int roll) => Outcomes[Math.Clamp(roll, 0, Outcomes.Length - 1)];

    /// <summary>Индексы следующих элит акта: игра выдаёт их по кругу с <paramref name="visited"/>.</summary>
    public static IReadOnlyList<int> UpcomingIndices(int visited, int count, int shown = BarrowElitesShown) =>
        count <= 0 ? [] : Enumerable.Range(0, Math.Min(shown, count)).Select(i => (Math.Max(0, visited) + i) % count).ToList();
}
