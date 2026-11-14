namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// Таблица ручной разметки ванильных атак (data/vanilla_attacks_stamina.csv,
/// генерируется tools/markup/build_vanilla_attacks.py).
/// </summary>
public sealed class StaminaTable
{
    public readonly record struct Entry(string Key, int Stamina, bool IsX, StrikeClass StrikeClass, DamageType DamageType, bool Weapon);

    private readonly Dictionary<string, Entry> _entries;

    private StaminaTable(Dictionary<string, Entry> entries) => _entries = entries;

    public int Count => _entries.Count;
    public IEnumerable<Entry> Entries => _entries.Values;

    /// <summary>Ключ — имя класса карты в игре (например, "StrikeIronclad").</summary>
    public bool TryGet(string key, out Entry entry) => _entries.TryGetValue(key, out entry);

    public static StaminaTable Parse(string csv)
    {
        var result = new Dictionary<string, Entry>(StringComparer.Ordinal);
        string[]? header = null;
        foreach (var raw in csv.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var cells = SplitCsvLine(line);
            if (header == null) { header = cells; continue; }

            string Cell(string name)
            {
                var i = Array.IndexOf(header, name);
                return i >= 0 && i < cells.Length ? cells[i] : "";
            }

            var key = Cell("key");
            if (key.Length == 0) continue;
            var staminaText = Cell("stamina");
            var isX = staminaText == "X";
            var stamina = isX ? 0 : int.Parse(staminaText);
            result[key] = new Entry(key, stamina, isX, ParseClass(Cell("strike_class")), ParseType(Cell("damage_type")), Cell("weapon") == "Y");
        }
        return new StaminaTable(result);
    }

    private static StrikeClass ParseClass(string s) => s switch
    {
        "L" => StrikeClass.Light,
        "H" => StrikeClass.Heavy,
        _ => StrikeClass.Normal,
    };

    private static DamageType ParseType(string s) => s switch
    {
        "fire" => DamageType.Fire,
        "frost" => DamageType.Frost,
        "shock" => DamageType.Shock,
        "magic" => DamageType.Magic,
        "light" => DamageType.Light,
        "poison" => DamageType.Poison,
        _ => DamageType.Physical,
    };

    /// <summary>Минимальный CSV-разбор: запятые, поля в кавычках, удвоенные кавычки.</summary>
    internal static string[] SplitCsvLine(string line)
    {
        var cells = new List<string>();
        var sb = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else sb.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { cells.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        cells.Add(sb.ToString());
        return cells.ToArray();
    }
}
