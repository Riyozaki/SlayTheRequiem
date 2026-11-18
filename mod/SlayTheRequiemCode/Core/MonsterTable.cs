namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Разметка ванильных врагов (data/monsters_requiem.csv, генерирует tools/markup/build_monsters.py).</summary>
public sealed class MonsterTable
{
    public sealed record Entry(
        string Key,
        string Faction,
        int Plating,
        int PoiseBonus,
        IReadOnlyDictionary<DamageType, int> Resistances,
        DamageType AttackType,
        EnemyRole Role = EnemyRole.Normal);

    public static readonly Entry Default = new("", "", 0, 0, new Dictionary<DamageType, int>(), DamageType.Physical);

    private readonly Dictionary<string, Entry> _entries;
    private MonsterTable(Dictionary<string, Entry> entries) => _entries = entries;

    public int Count => _entries.Count;
    public Entry Get(string key) => _entries.TryGetValue(key, out var e) ? e : Default;

    public static MonsterTable Parse(string csv)
    {
        var result = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var row in CsvRows.Read(csv))
        {
            var key = row.Get("key");
            if (key.Length == 0) continue;
            var res = new Dictionary<DamageType, int>
            {
                [DamageType.Fire] = row.GetInt("res_fire"),
                [DamageType.Frost] = row.GetInt("res_frost"),
                [DamageType.Shock] = row.GetInt("res_shock"),
                [DamageType.Poison] = row.GetInt("res_poison"),
                [DamageType.Magic] = row.GetInt("res_magic"),
            };
            result[key] = new Entry(key, row.Get("faction"), row.GetInt("plating"), row.GetInt("poise_bonus"), res,
                DamageTypes.Parse(row.Get("attack_type")), EnemyRoleRules.Parse(row.Get("role")));
        }
        return new MonsterTable(result);
    }
}

public static class DamageTypes
{
    public static DamageType Parse(string s) => s switch
    {
        "fire" => DamageType.Fire,
        "frost" => DamageType.Frost,
        "shock" => DamageType.Shock,
        "magic" => DamageType.Magic,
        "light" => DamageType.Light,
        "poison" => DamageType.Poison,
        "blight" => DamageType.Blight,
        "true" => DamageType.True,
        _ => DamageType.Physical,
    };
}

/// <summary>Строки CSV с заголовком; строки на '#' — комментарии.</summary>
public static class CsvRows
{
    public sealed class Row(string[] header, string[] cells)
    {
        public string Get(string name)
        {
            var i = Array.IndexOf(header, name);
            return i >= 0 && i < cells.Length ? cells[i] : "";
        }

        public int GetInt(string name, int fallback = 0) => int.TryParse(Get(name), out var v) ? v : fallback;
    }

    public static IEnumerable<Row> Read(string csv)
    {
        string[]? header = null;
        foreach (var raw in csv.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var cells = StaminaTable.SplitCsvLine(line);
            if (header == null) { header = cells; continue; }
            yield return new Row(header, cells);
        }
    }
}
