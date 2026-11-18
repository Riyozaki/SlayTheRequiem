using System.Globalization;
using System.Text;

namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// Личный выбор игрока в настройках мода (docs/design/04): происхождение каждого героя и предыстория. Значения —
/// номера вариантов перечислений настроек (0 = «Случайно»). Это выбор самого игрока, поэтому в коопе у каждого свой.
/// </summary>
public readonly record struct PersonalChoices(int PlatebearerOrigin, int SilentOrigin, int RegentOrigin, int Background)
{
    /// <summary>Нет данных об игроке — всё «Случайно» (одинаково на всех машинах).</summary>
    public static PersonalChoices Random => default;
}

/// <summary>
/// Снимок игровых настроек мода на один забег (ревью F-03). Берётся у хоста (в одиночной игре — у игрока) в момент
/// старта, рассылается клиентам коопа и хранится в сохранении забега. Поэтому у всех игроков и после загрузки забег
/// идёт по одним правилам, а смена настроек посреди забега его не меняет. Оформление (эффекты, звуки, кукла, цвет
/// названий) в снимок не входит: оно не влияет на состояние игры.
///
/// Формат строки: <c>v=1;sys=1;…;p=&lt;netId&gt;:a,b,c,d|…</c>. Неизвестные ключи пропускаются, отсутствующие берут
/// значения по умолчанию в текущей схеме. Будущая версия, некорректные числа и неоднозначные повторы отвергаются:
/// неизвестные правила нельзя молча заменить меню локальной машины.
/// </summary>
public sealed class RunSettingsData
{
    public const int Version = 1;

    /// <summary>Host-created nonce shared before a NEW run, persisted with its settings. Empty in legacy saves.</summary>
    public string RunId { get; set; } = "";

    public bool EnableRequiemSystems { get; set; } = true;
    public bool EnableOverstrain { get; set; } = true;
    public int EnemyHpPercent { get; set; } = 110;
    public bool AutoDodge { get; set; } = true;
    public bool ShowVanillaOnlyHeroes { get; set; }
    public bool FiveActs { get; set; } = true;
    public bool ShortRun { get; set; }
    /// <summary>0 in snapshots predating B2.5; stamped only for NEW runs. Never upgrade saved balance silently.</summary>
    public int EconomyRevision { get; set; }
    public int ActVariant { get; set; }
    public int RequiemLevelSetting { get; set; } = -1;
    public int Superbosses { get; set; } = 1;
    public bool WideMaps { get; set; } = true;
    public bool ShopVariety { get; set; } = true;
    public bool AllowTheft { get; set; }
    public bool EnablePantheon { get; set; } = true;
    public bool EnableCampMeta { get; set; }
    public int CampBuildingsUnlocked { get; set; }
    public bool EnableMemoryStone { get; set; } = true;
    public int MemoryStoneCandles { get; set; } = -1;

    /// <summary>Личный выбор по сетевому id игрока.</summary>
    public Dictionary<ulong, PersonalChoices> Personal { get; } = new();

    /// <summary>Unique unknown v1 keys survive Clone/migration; this is not permission to read a future schema.</summary>
    public Dictionary<string, string> Extensions { get; } = new(StringComparer.Ordinal);

    /// <summary>Личный выбор игрока; неизвестный игрок — всё «Случайно».</summary>
    public PersonalChoices ChoicesFor(ulong netId) => Personal.TryGetValue(netId, out var c) ? c : PersonalChoices.Random;

    public RunSettingsData Clone()
    {
        var copy = Decode(Encode()) ?? throw new FormatException("Cannot clone invalid saved Requiem rules");
        return copy;
    }

    public string Encode()
    {
        var sb = new StringBuilder();
        void Add(string key, int value)
        {
            if (sb.Length > 0) sb.Append(';');
            sb.Append(key).Append('=').Append(value.ToString(CultureInfo.InvariantCulture));
        }
        static int B(bool b) => b ? 1 : 0;

        Add("v", Version);
        Add("sys", B(EnableRequiemSystems));
        Add("ovs", B(EnableOverstrain));
        Add("hp", EnemyHpPercent);
        Add("dodge", B(AutoDodge));
        Add("vanhero", B(ShowVanillaOnlyHeroes));
        Add("five", B(FiveActs));
        Add("short", B(ShortRun));
        if (EconomyRevision is < 0 or > EconomyRules.CurrentRevision) throw new FormatException("Unsupported economy revision");
        Add("economy", EconomyRevision);
        Add("variant", ActVariant);
        Add("level", RequiemLevelSetting);
        Add("boss", Superbosses);
        Add("wide", B(WideMaps));
        Add("shop", B(ShopVariety));
        Add("theft", B(AllowTheft));
        Add("pantheon", B(EnablePantheon));
        Add("camp", B(EnableCampMeta));
        Add("campn", CampBuildingsUnlocked);
        Add("stone", B(EnableMemoryStone));
        Add("candles", MemoryStoneCandles);
        if (RunId.Length > 0)
        {
            if (!Guid.TryParseExact(RunId, "N", out var runId)) throw new FormatException("Invalid saved run identity");
            sb.Append(";run=").Append(runId.ToString("N"));
        }
        if (Personal.Count > 0)
        {
            // Порядок по id — одинаковая строка на всех машинах.
            var entries = Personal.OrderBy(kv => kv.Key).Select(kv =>
                string.Create(CultureInfo.InvariantCulture,
                    $"{kv.Key}:{kv.Value.PlatebearerOrigin},{kv.Value.SilentOrigin},{kv.Value.RegentOrigin},{kv.Value.Background}"));
            sb.Append(";p=").Append(string.Join("|", entries));
        }
        foreach (var (key, value) in Extensions.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (key.Length == 0 || key.Contains('=') || key.Contains(';') || value.Contains(';')
                || key is "v" or "run" or "p" or "sys" or "ovs" or "hp" or "dodge" or "vanhero" or "five" or "short" or "economy"
                    or "variant" or "level" or "boss" or "wide" or "shop" or "theft" or "pantheon" or "camp" or "campn" or "stone" or "candles")
                throw new FormatException("Invalid extension in saved rules");
            sb.Append(';').Append(key).Append('=').Append(value);
        }
        return sb.ToString();
    }

    /// <summary>Разбор строки снимка; null — пустая строка или чужой формат.</summary>
    public static RunSettingsData? Decode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 16384) return null;
        var data = new RunSettingsData();
        var sawVersion = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string[] booleanKeys = ["sys", "ovs", "dodge", "vanhero", "five", "short", "wide", "shop", "theft", "pantheon", "camp", "stone"];
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) return null;
            var key = part[..eq];
            var value = part[(eq + 1)..];
            if (!seen.Add(key)) return null;
            if (key == "run")
            {
                if (!Guid.TryParseExact(value, "N", out var id)) return null;
                data.RunId = id.ToString("N");
                continue;
            }
            if (key == "p")
            {
                if (!ParsePersonal(value, data.Personal)) return null;
                continue;
            }
            var known = booleanKeys.Contains(key) || key is "v" or "hp" or "economy" or "variant" or "level" or "boss" or "campn" or "candles";
            if (!known) { data.Extensions.Add(key, value); continue; }
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return null;
            if (booleanKeys.Contains(key) && n is not (0 or 1)) return null;
            switch (key)
            {
                case "v": if (n != Version) return null; sawVersion = true; break;
                case "sys": data.EnableRequiemSystems = n != 0; break;
                case "ovs": data.EnableOverstrain = n != 0; break;
                case "hp": data.EnemyHpPercent = n; break;
                case "dodge": data.AutoDodge = n != 0; break;
                case "vanhero": data.ShowVanillaOnlyHeroes = n != 0; break;
                case "five": data.FiveActs = n != 0; break;
                case "short": data.ShortRun = n != 0; break;
                case "economy": if (n is < 0 or > EconomyRules.CurrentRevision) return null; data.EconomyRevision = n; break;
                case "variant": data.ActVariant = n; break;
                case "level": data.RequiemLevelSetting = n; break;
                case "boss": data.Superbosses = n; break;
                case "wide": data.WideMaps = n != 0; break;
                case "shop": data.ShopVariety = n != 0; break;
                case "theft": data.AllowTheft = n != 0; break;
                case "pantheon": data.EnablePantheon = n != 0; break;
                case "camp": data.EnableCampMeta = n != 0; break;
                case "campn": data.CampBuildingsUnlocked = n; break;
                case "stone": data.EnableMemoryStone = n != 0; break;
                case "candles": data.MemoryStoneCandles = n; break;
            }
        }
        return sawVersion && data.EnemyHpPercent is >= 80 and <= 150 && data.ActVariant is >= 0 and <= 1
            && data.RequiemLevelSetting is >= -1 and <= 10 && data.Superbosses is >= 0 and <= 2
            && data.CampBuildingsUnlocked is >= 0 and <= 5 && data.MemoryStoneCandles is >= -1 and <= 9 ? data : null;
    }

    private static bool ParsePersonal(string value, Dictionary<ulong, PersonalChoices> into)
    {
        foreach (var entry in value.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = entry.IndexOf(':');
            if (colon <= 0 || !ulong.TryParse(entry[..colon], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || into.ContainsKey(id)) return false;
            var nums = entry[(colon + 1)..].Split(',');
            if (nums.Length != 4) return false;
            var parsed = new int[4];
            var ok = true;
            for (var i = 0; i < 4; i++)
                ok &= int.TryParse(nums[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed[i]);
            if (!ok || parsed[0] is < 0 or > 6 || parsed[1] is < 0 or > 5 || parsed[2] is < 0 or > 5 || parsed[3] is < 0 or > 6) return false;
            into[id] = new PersonalChoices(parsed[0], parsed[1], parsed[2], parsed[3]);
        }
        return true;
    }
}
