namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Корзина переработки ванильной реликвии (docs/design/07 §7).</summary>
public enum RelicBucket
{
    /// <summary>Остаётся реликвией (рюкзак): правила без изменений, вес 0–3.</summary>
    Keep,
    /// <summary>Уходит в бижутерию: не выпадает героям Реквиема, вместо неё в пуле — кольцо или амулет мода.</summary>
    Jewelry,
    /// <summary>Перерабатывается: ломает новые ресурсы и получает правку (энергия → −1 прирост ◆/✦).</summary>
    Rework,
}

/// <summary>Правка переработанной реликвии.</summary>
public enum RelicChange
{
    None,
    /// <summary>+1 энергия остаётся, но прирост основного ресурса героя −1 (◆ у Латоносца и Безмолвной, ✦ у Регента).</summary>
    RegenMinusOne,
}

/// <summary>
/// Таблица переработки ванильных реликвий (docs/design/07 §7, <c>data/vanilla_relics.csv</c>; проверяет и пишет сводку
/// <c>tools/relics/build_relics.py</c>). Ключ — имя класса ванильной реликвии. Каждая реликвия игры — ровно одна строка,
/// новая реликвия без строки — ошибка CI.
/// </summary>
public sealed class VanillaRelicTable
{
    public sealed record Entry(string Key, string Pool, RelicBucket Bucket, int Weight, RelicChange Change, string Replacement);

    private readonly Dictionary<string, Entry> _rows;

    private VanillaRelicTable(Dictionary<string, Entry> rows) => _rows = rows;

    public int Count => _rows.Count;
    public int CountOf(RelicBucket bucket) => _rows.Values.Count(e => e.Bucket == bucket);

    public bool TryGet(string key, out Entry entry) => _rows.TryGetValue(key, out entry!);

    /// <summary>Вес реликвии в Грузе или null (нет в таблице — правило по умолчанию).</summary>
    public int? Weight(string key) => _rows.TryGetValue(key, out var e) ? e.Weight : null;

    /// <summary>Не выпадает героям Реквиема (ушла в бижутерию).</summary>
    public bool IsCut(string key) => _rows.TryGetValue(key, out var e) && e.Bucket == RelicBucket.Jewelry;

    public RelicChange Change(string key) => _rows.TryGetValue(key, out var e) ? e.Change : RelicChange.None;

    public IEnumerable<Entry> Cut => _rows.Values.Where(e => e.Bucket == RelicBucket.Jewelry);

    public const int MaxWeight = 3;

    /// <summary>Чей ресурс режет «−1 прирост»: у Регента — ✦, у остальных героев Реквиема — ◆ (07 §7 «по типу»).</summary>
    public static bool PenaltyHitsMana(string? heroKey) => heroKey == "Regent";

    /// <summary>Штраф к приросту (◆, ✦) от переработанных реликвий героя.</summary>
    public (int Stamina, int Mana) RegenPenalty(string? heroKey, IEnumerable<string> relicKeys)
    {
        var n = relicKeys.Count(k => Change(k) == RelicChange.RegenMinusOne);
        return PenaltyHitsMana(heroKey) ? (0, n) : (n, 0);
    }

    public static VanillaRelicTable Parse(string csv)
    {
        var rows = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var row in CsvRows.Read(csv))
        {
            var key = row.Get("key");
            if (key.Length == 0) continue;
            var bucket = row.Get("bucket") switch
            {
                "keep" => RelicBucket.Keep,
                "jewelry" => RelicBucket.Jewelry,
                "rework" => RelicBucket.Rework,
                var b => throw new FormatException($"vanilla_relics.csv: неизвестная корзина «{b}» у {key}"),
            };
            var change = row.Get("change") switch
            {
                "" => RelicChange.None,
                "regen-1" => RelicChange.RegenMinusOne,
                var c => throw new FormatException($"vanilla_relics.csv: неизвестная правка «{c}» у {key}"),
            };
            var weight = Math.Clamp(row.GetInt("weight", 1), 0, MaxWeight);
            rows[key] = new Entry(key, row.Get("pool"), bucket, weight, change, row.Get("replacement"));
        }
        return new VanillaRelicTable(rows);
    }
}

/// <summary>Числа бижутерии, заменяющей ванильные реликвии (07 §5).</summary>
public static class JewelryRules
{
    /// <summary>Кольцо Ярого Удара (вместо Vajra): Оружейные атаки +1 урона.</summary>
    public const int FuryStrikeDamage = 1;

    /// <summary>Кольцо Железной Кожи (вместо Anchor и Orichalcum): +3 Блока в начале хода, пока Латы ≥ 1.</summary>
    public const int IronSkinBlock = 3;
    public const int IronSkinPlatingNeeded = 1;

    /// <summary>Ожерелье Колючих Шипов (вместо Bronze Scales): Шипы 2.</summary>
    public const int ThornsNecklace = 2;

    public static bool IronSkinActive(int plating) => plating >= IronSkinPlatingNeeded;

    // ───── кольца §5.2 ─────
    /// <summary>Кольцо Скорого Дыхания: +1 прирост ◆.</summary>
    public const int QuickBreathRegen = 1;
    /// <summary>Кольцо Ясного Ума: +1 прирост ✦.</summary>
    public const int ClearMindRegen = 1;
    /// <summary>Кольцо Змеиной Улыбки: +1 карта в первой руке.</summary>
    public const int SerpentSmileDraw = 1;
    /// <summary>Кольцо Пиромана: Горение +2 при наложении.</summary>
    public const int PyromancerBurning = 2;
    /// <summary>Кольцо Изморози: Озноб +1 при наложении.</summary>
    public const int FrostbiteChill = 1;
    /// <summary>Кольцо Громоотвода: сопротивление разряду II, половина урона разрядом — в ✦.</summary>
    public const int LightningRodResistance = 2;
    public const int LightningRodAbsorbPercent = 50;
    /// <summary>Кольцо Охотника на Ведьм: атаки по врагу с намерением-заклинанием +4.</summary>
    public const int WitchHunterDamage = 4;
    /// <summary>Кольцо Регенерации: Регенерация 1, пока ♥ ниже половины.</summary>
    public const int RegenerationHeal = 1;
    /// <summary>Кольцо Пьяницы: зелья +50 % эффекта; −1 пул ◆.</summary>
    public const int DrunkardPotionPercent = 50;
    public const int DrunkardStaminaPool = -1;
    /// <summary>Кольцо Безликого Вора: кража в лавках и +1 попытка за визит.</summary>
    public const int FacelessExtraTheft = 1;
    /// <summary>Кольцо Призывателя: +1 слот призыва.</summary>
    public const int SummonerSlots = 1;

    // ───── амулеты §5.1 ─────
    /// <summary>Амулет Ловчего: атаки по зверям +3; Обращение оборотня +1 ход.</summary>
    public const int HunterBeastDamage = 3;
    public const int HunterFormTurns = 1;
    /// <summary>Амулет Матери Очага: после боя лечит 4 ♥.</summary>
    public const int HearthHeal = 4;
    /// <summary>Амулет Говорящего с Мёртвыми: призывы-нежить +3 ♥.</summary>
    public const int NecromancerUndeadHp = 3;
    /// <summary>Амулет Ткачихи: Подлые атаки накладывают Яд 2.</summary>
    public const int WeaverPoison = 2;
    /// <summary>Ожерелье Пустоты: Поглощение заклинаний 1; −1 пул ✦.</summary>
    public const int VoidAbsorption = 1;
    public const int VoidManaPool = -1;
    /// <summary>Ожерелье Звёздного Камня: раз в бой при ✦ = 0 — восстановить 4 ✦.</summary>
    public const int StarstoneMana = 4;

    // ───── сочетания §5.3 ─────
    /// <summary>Скорое Дыхание + Ярый Удар: атаки за 0 ◆ +2 урона.</summary>
    public const int QuickFuryDamage = 2;

    /// <summary>Сила зелья с Кольцом Пьяницы: +50 %, с округлением вверх (отрицательное и 0 не трогаем).</summary>
    public static int DrunkardPotency(int potency) =>
        potency <= 0 ? potency : (potency * (100 + DrunkardPotionPercent) + 99) / 100;

    /// <summary>Сколько ✦ даёт Громоотвод за удар разрядом: половина полученного урона (вниз).</summary>
    public static int LightningRodMana(int shockDamage) => Math.Max(0, shockDamage) * LightningRodAbsorbPercent / 100;

    /// <summary>Кольцо Регенерации работает, пока ♥ строго ниже половины максимума.</summary>
    public static bool RegenerationActive(int currentHp, int maxHp) => maxHp > 0 && currentHp * 2 < maxHp;

    /// <summary>Ожерелье Звёздного Камня: срабатывает при ✦ = 0, если ещё не срабатывало в этом бою.</summary>
    public static bool StarstoneTriggers(int mana, bool usedThisCombat) => mana <= 0 && !usedThisCombat;

    /// <summary>Пар (Горение + Озноб на цели): оба снимаются, урон = сумма стаков без сопротивлений (03).</summary>
    public static int SteamDamage(int burning, int chill) => burning > 0 && chill > 0 ? burning + chill : 0;

    /// <summary>Повторов Кольца Наместника за бой: 1, с Кольцом Ясного Ума — 2 (§5.3).</summary>
    public static int ViceroyReplays(bool clearMindCombo) => clearMindCombo ? 2 : 1;
}

/// <summary>Скрытые сочетания колец (07 §5.3): бонус работает, пока надеты оба кольца пары.</summary>
public enum RingCombo
{
    /// <summary>Пироман + Изморозь → реакция «Пар» без модуля реакций.</summary>
    Steam = 1,
    /// <summary>Скорое Дыхание + Ярый Удар → атаки за 0 ◆ +2 урона.</summary>
    QuickFury = 2,
    /// <summary>Ясный Ум + Наместник → повтор срабатывает дважды за бой.</summary>
    ClearViceroy = 4,
}

public static class RingComboRules
{
    public static readonly IReadOnlyDictionary<RingCombo, (string A, string B)> Pairs = new Dictionary<RingCombo, (string, string)>
    {
        [RingCombo.Steam] = ("RingOfThePyromancer", "RingOfHoarfrost"),
        [RingCombo.QuickFury] = ("RingOfQuickBreath", "RingOfFuryStrike"),
        [RingCombo.ClearViceroy] = ("RingOfClearMind", "RingOfTheViceroy"),
    };

    /// <summary>Сочетания, открытые надетыми кольцами (имена классов).</summary>
    public static IReadOnlyList<RingCombo> Active(IEnumerable<string> equippedRings)
    {
        var set = equippedRings.ToHashSet();
        return Pairs.Where(p => set.Contains(p.Value.A) && set.Contains(p.Value.B)).Select(p => p.Key).ToList();
    }

    /// <summary>Сочетания, в которых участвует кольцо.</summary>
    public static IReadOnlyList<RingCombo> Involving(string ring) =>
        Pairs.Where(p => p.Value.A == ring || p.Value.B == ring).Select(p => p.Key).ToList();

    /// <summary>Маска открытых сочетаний (сохраняется на кольце — подсказка видна после первого совпадения).</summary>
    public static int Mark(int mask, IEnumerable<RingCombo> combos) => combos.Aggregate(mask, (m, c) => m | (int)c);

    public static bool Known(int mask, RingCombo combo) => (mask & (int)combo) != 0;
}
