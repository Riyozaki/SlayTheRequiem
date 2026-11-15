using System.Globalization;

namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Звуковые события Реквиема (ключи строк data/sfx.csv).</summary>
public static class SfxEvents
{
    public const string HitFire = "hit.fire";
    public const string HitFrost = "hit.frost";
    public const string HitShock = "hit.shock";
    public const string HitPoison = "hit.poison";
    public const string HitBlight = "hit.blight";
    public const string HitLight = "hit.light";
    public const string HitMagic = "hit.magic";
    public const string HitTrue = "hit.true";
    public const string Overstrain = "overstrain";
    public const string Broken = "broken";
    public const string Dodge = "dodge";
    public const string ShoutWar = "shout.war";
    public const string ShoutGuard = "shout.guard";
    public const string ShoutRally = "shout.rally";
    public const string Summon = "summon";
    public const string Rewind = "rewind";
    public const string PortalEnter = "portal.enter";
    public const string CacheOpen = "cache.open";
    public const string CacheTrap = "cache.trap";

    public static readonly IReadOnlyList<string> All =
    [
        HitFire, HitFrost, HitShock, HitPoison, HitBlight, HitLight, HitMagic, HitTrue,
        Overstrain, Broken, Dodge, ShoutWar, ShoutGuard, ShoutRally, Summon, Rewind, PortalEnter, CacheOpen, CacheTrap,
    ];
}

/// <summary>Откуда звук: событие FMOD игры, файл игры (res://debug_audio) или свой файл мода (audio/sfx).</summary>
public enum SfxSource
{
    Fmod,
    Game,
    Own,
}

/// <summary>Один слой звука события.</summary>
public sealed record SfxLayer(SfxSource Source, string Sound, float Volume);

/// <summary>
/// Таблица «событие → слои звука» (data/sfx.csv, M7 п. 5). Несколько строк одного события играют вместе; событие без строк —
/// тишина. Ошибка в строке — исключение с номером строки (RequiemData тогда пишет в лог и звуков нет, игра не падает).
/// </summary>
public sealed class SfxTable
{
    private readonly Dictionary<string, List<SfxLayer>> _layers;

    private SfxTable(Dictionary<string, List<SfxLayer>> layers) => _layers = layers;

    public int EventCount => _layers.Count;
    public int LayerCount => _layers.Values.Sum(l => l.Count);

    public IReadOnlyList<SfxLayer> For(string sfxEvent) => _layers.TryGetValue(sfxEvent, out var l) ? l : [];

    public IEnumerable<SfxLayer> AllLayers => _layers.Values.SelectMany(l => l);

    public static SfxTable Parse(string csv)
    {
        var layers = new Dictionary<string, List<SfxLayer>>();
        var line = 0; // номер записи (без заголовка и комментариев)
        foreach (var row in CsvRows.Read(csv))
        {
            line++;
            var ev = row.Get("event");
            if (!SfxEvents.All.Contains(ev)) throw new FormatException($"sfx.csv: неизвестное событие «{ev}» (запись {line})");
            var source = row.Get("source") switch
            {
                "fmod" => SfxSource.Fmod,
                "game" => SfxSource.Game,
                "own" => SfxSource.Own,
                var s => throw new FormatException($"sfx.csv: неизвестный источник «{s}» у {ev} (запись {line})"),
            };
            var sound = row.Get("sound");
            if (sound.Length == 0 || (source == SfxSource.Fmod) != sound.StartsWith("event:/", StringComparison.Ordinal))
                throw new FormatException($"sfx.csv: звук «{sound}» не подходит к источнику {source} у {ev} (запись {line})");
            if (!float.TryParse(row.Get("volume"), NumberStyles.Float, CultureInfo.InvariantCulture, out var volume)
                || volume < 0 || volume > SfxRules.MaxVolume)
                throw new FormatException($"sfx.csv: громкость «{row.Get("volume")}» у {ev} — нужно 0…{SfxRules.MaxVolume} (запись {line})");
            if (!layers.TryGetValue(ev, out var list)) layers[ev] = list = [];
            list.Add(new SfxLayer(source, sound, volume));
        }
        return new SfxTable(layers);
    }
}

/// <summary>
/// Звуки Реквиема (M7 п. 5, art spec §6): какой звук у события. Основа — ванильные банки FMOD, свои звуки (синтез
/// tools/audio/build_sfx.py) — там, где у игры подходящего нет. Без Godot — для xUnit и зеркала tools/sim (M7SfxRules).
/// </summary>
public static class SfxRules
{
    public const float MaxVolume = 2f;

    /// <summary>Тот же звук удара на одном существе — не чаще, чем раз в столько мс (многоудары, тики статусов).</summary>
    public const int HitThrottleMs = 120;

    /// <summary>Громкость звука урона не-атакой (тики горения и яда, взрывы) — тише удара.</summary>
    public const float TickVolume = 0.5f;

    /// <summary>Звук удара по типу урона; физический — null (ванильный звук атаки).</summary>
    public static string? HitEvent(DamageType type) => type switch
    {
        DamageType.Fire => SfxEvents.HitFire,
        DamageType.Frost => SfxEvents.HitFrost,
        DamageType.Shock => SfxEvents.HitShock,
        DamageType.Poison => SfxEvents.HitPoison,
        DamageType.Blight => SfxEvents.HitBlight,
        DamageType.Light => SfxEvents.HitLight,
        DamageType.Magic => SfxEvents.HitMagic,
        DamageType.True => SfxEvents.HitTrue,
        _ => null,
    };

    /// <summary>Множитель громкости удара или 0 (не звучит): как у вспышки (<see cref="VfxRules.For"/>) — атака всегда, не-атака — если прошёл урон.</summary>
    public static float HitVolume(DamageType type, bool poweredAttack, int totalDamage) =>
        HitEvent(type) == null || (!poweredAttack && totalDamage <= 0) ? 0f : poweredAttack ? 1f : TickVolume;

    public static bool Throttled(ulong lastMs, ulong nowMs) => lastMs != 0 && nowMs >= lastMs && nowMs - lastMs < HitThrottleMs;

    public static string ShoutEvent(ShoutFx fx) => fx switch
    {
        ShoutFx.War => SfxEvents.ShoutWar,
        ShoutFx.Rally => SfxEvents.ShoutRally,
        _ => SfxEvents.ShoutGuard,
    };

    /// <summary>Итоговая громкость слоя (0…2).</summary>
    public static float LayerVolume(SfxLayer layer, float scale) => Math.Clamp(layer.Volume * scale, 0f, MaxVolume);

    /// <summary>Путь файла: свой — в .pck мода, игровой — res://debug_audio (как у TmpSfx игры); FMOD — путь события.</summary>
    public static string FilePath(SfxLayer layer, string modResPath) => layer.Source switch
    {
        SfxSource.Own => $"{modResPath}/audio/sfx/{layer.Sound}",
        SfxSource.Game => $"res://debug_audio/{layer.Sound}",
        _ => layer.Sound,
    };
}
