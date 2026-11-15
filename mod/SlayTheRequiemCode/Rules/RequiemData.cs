using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Загрузка таблиц правил. Порядок: файл в .pck (<c>res://SlayTheRequiem/data</c> — его можно заменить, не пересобирая DLL)
/// → копия, встроенная в DLL (<c>EmbeddedResource</c> в SlayTheRequiem.csproj). Встроенная копия страхует от экспорта,
/// который теряет CSV или принимает его за таблицу переводов (ревью R-06).
/// </summary>
public static class RequiemData
{
    public static StaminaTable? Attacks { get; private set; }
    public static MonsterTable? Monsters { get; private set; }
    public static PoolTable? Pools { get; private set; }

    public static void Load()
    {
        Attacks = Read("vanilla_attacks_stamina.csv", StaminaTable.Parse);
        Monsters = Read("monsters_requiem.csv", MonsterTable.Parse);
        Pools = Read("requiem_pools.csv", PoolTable.Parse);
        MainFile.Logger.Info($"Requiem data: {Attacks?.Count ?? 0} attacks, {Monsters?.Count ?? 0} monsters, " +
                             $"pools: {Pools?.CutCount ?? 0} cut, {Pools?.GatedCount ?? 0} gated.");
    }

    private static T? Read<T>(string fileName, Func<string, T> parse) where T : class
    {
        var text = ReadPck(fileName) ?? ReadEmbedded(fileName);
        if (text == null)
        {
            MainFile.Logger.Error($"Requiem data: {fileName} не найден ни в .pck, ни в DLL — правила работают по умолчаниям.");
            return null;
        }
        try
        {
            return parse(text);
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"Requiem data: не удалось разобрать {fileName}: {e}");
            return null;
        }
    }

    private static string? ReadPck(string fileName)
    {
        var path = $"{MainFile.ResPath}/data/{fileName}";
        try
        {
            if (!Godot.FileAccess.FileExists(path)) return null;
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            if (file == null) return null;
            var text = file.GetAsText();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"Requiem data: {path} не читается ({e.Message}) — беру копию из DLL.");
            return null;
        }
    }

    private static string? ReadEmbedded(string fileName)
    {
        var assembly = typeof(RequiemData).Assembly;
        using var stream = assembly.GetManifestResourceStream($"{MainFile.ModId}.data.{fileName}");
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        MainFile.Logger.Info($"Requiem data: {fileName} — встроенная копия из DLL.");
        return reader.ReadToEnd();
    }

    public static MonsterTable.Entry MonsterEntry(string key) => Monsters?.Get(key) ?? MonsterTable.Default;
}
