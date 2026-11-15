namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// Пулы карт Реквиема (docs/design/19-card-pools.md, data/requiem_pools.csv — генерирует tools/pools/build_pools.py):
/// <list type="bullet">
/// <item><c>cut</c> — ванильная карта не выпадает героям Реквиема (награды, лавка, случайные карты);</item>
/// <item><c>gated</c> — карта чужого пула, которую открывает трансформация (gate = <c>Pact</c>: Тёмный договор → Лич).</item>
/// </list>
/// Ключ пула — имя класса пула без «CardPool» (<c>Ironclad</c>, <c>Silent</c>, <c>Regent</c>, <c>Colorless</c>, <c>Necrobinder</c>).
/// </summary>
public sealed class PoolTable
{
    private readonly HashSet<(string Pool, string Key)> _cuts;
    private readonly Dictionary<string, List<(string Pool, string Key)>> _gated;

    private PoolTable(HashSet<(string, string)> cuts, Dictionary<string, List<(string, string)>> gated)
    {
        _cuts = cuts;
        _gated = gated;
    }

    public int CutCount => _cuts.Count;
    public int GatedCount => _gated.Values.Sum(v => v.Count);

    public bool IsCut(string pool, string key) => _cuts.Contains((pool, key));

    /// <summary>Карты, которые открывает трансформация <paramref name="gate"/>: (пул-источник, класс).</summary>
    public IReadOnlyList<(string Pool, string Key)> Gated(string gate) =>
        _gated.TryGetValue(gate, out var list) ? list : Array.Empty<(string, string)>();

    /// <summary>«IroncladCardPool» → «Ironclad».</summary>
    public static string PoolKey(string poolTypeName) =>
        poolTypeName.EndsWith("CardPool", StringComparison.Ordinal) ? poolTypeName[..^"CardPool".Length] : poolTypeName;

    public static PoolTable Parse(string csv)
    {
        var cuts = new HashSet<(string, string)>();
        var gated = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);
        foreach (var row in CsvRows.Read(csv))
        {
            var pool = row.Get("pool");
            var key = row.Get("key");
            if (pool.Length == 0 || key.Length == 0) continue;
            switch (row.Get("decision"))
            {
                case "cut":
                    cuts.Add((pool, key));
                    break;
                case "gated":
                    var gate = row.Get("gate");
                    if (!gated.TryGetValue(gate, out var list)) gated[gate] = list = new List<(string, string)>();
                    list.Add((pool, key));
                    break;
                default:
                    throw new FormatException($"requiem_pools.csv: неизвестное решение «{row.Get("decision")}» у {key}");
            }
        }
        return new PoolTable(cuts, gated);
    }
}

/// <summary>Числа политики пулов (19 §3).</summary>
public static class PoolRules
{
    /// <summary>Целевая доля своих карт героя среди карт его пула в наградах за бой, %.</summary>
    public const int OwnCardSharePercent = 30;

    /// <summary>Шанс, что карта награды из пула героя после Тёмного договора станет картой обречённости (19 §4), %.</summary>
    public const int PactCardSharePercent = 25;

    /// <summary>
    /// Шанс заменить ванильную карту награды своей картой героя той же редкости, чтобы доля своих в наградах дошла до
    /// <paramref name="targetPercent"/>. Если своих карт <paramref name="own"/>, а ванильных <paramref name="vanilla"/>, то
    /// естественная доля f = own / (own + vanilla); замена с шансом p = (t − f) / (1 − f) даёт ровно t. Своих нет — 0;
    /// своих уже не меньше цели — 0.
    /// </summary>
    public static double OwnReplaceChance(int own, int vanilla, int targetPercent = OwnCardSharePercent)
    {
        if (own <= 0 || vanilla <= 0) return 0;
        var t = Math.Clamp(targetPercent, 0, 100) / 100.0;
        var f = own / (double)(own + vanilla);
        return f >= t ? 0 : (t - f) / (1 - f);
    }
}
