using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>Загрузка таблиц из res://SlayTheRequiem/data (попадают в .pck вместе с ресурсами мода).</summary>
public static class RequiemData
{
    public static StaminaTable? Attacks { get; private set; }
    public static MonsterTable? Monsters { get; private set; }

    public static void Load()
    {
        Attacks = Read("vanilla_attacks_stamina.csv", StaminaTable.Parse);
        Monsters = Read("monsters_requiem.csv", MonsterTable.Parse);
        MainFile.Logger.Info($"Requiem data: {Attacks?.Count ?? 0} attacks, {Monsters?.Count ?? 0} monsters.");
    }

    private static T? Read<T>(string fileName, Func<string, T> parse) where T : class
    {
        var path = $"{MainFile.ResPath}/data/{fileName}";
        try
        {
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                MainFile.Logger.Error($"Cannot open {path}: {Godot.FileAccess.GetOpenError()}");
                return null;
            }
            return parse(file.GetAsText());
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"Failed to load {path}: {e}");
            return null;
        }
    }

    public static MonsterTable.Entry MonsterEntry(string key) => Monsters?.Get(key) ?? MonsterTable.Default;
}
