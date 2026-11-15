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
}
