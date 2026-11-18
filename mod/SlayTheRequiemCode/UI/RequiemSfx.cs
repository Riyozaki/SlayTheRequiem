using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.TestSupport;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Звуки Реквиема (M7 п. 5, art spec §6). Что звучит — таблица data/sfx.csv (<see cref="SfxTable"/>, правится без
/// пересборки): события FMOD игры — через её NAudioManager; файлы игры (res://debug_audio) — через её NDebugAudioManager;
/// свои звуки (audio/sfx/*.wav, синтез tools/audio/build_sfx.py) — своим AudioStreamPlayer на шине «SFX» игры, так что
/// ползунок «Звуки» действует на всё. Настройка «Звуки Реквиема» (<see cref="RequiemConfig.RequiemSounds"/>) — отдельно
/// от боевых эффектов. Ошибка звука гасится (одна строка в лог) — игра не ломается.
/// </summary>
public static class RequiemSfx
{
    private static readonly StringName SfxBus = new("SFX");
    private static readonly ConditionalWeakTable<Creature, Dictionary<string, ulong>> LastHit = new();
    // Eight fixed hit-event keys only. The per-creature gate alone still layered five identical samples on an
    // AoE; coalesce that short same-element burst globally too. Never gates an actual hit, status or telegraph.
    private static readonly Dictionary<string, ulong> LastGroupHit = [];
    private static readonly Dictionary<string, AudioStream?> Streams = [];
    private static readonly HashSet<string> Reported = [];

    public static bool Enabled =>
        RunSettings.EnableRequiemSystems && RequiemConfig.RequiemSounds && !TestMode.IsOn && !NonInteractiveMode.IsActive;

    /// <summary>Сыграть все слои события (громкость слоя × scale).</summary>
    public static void Play(string sfxEvent, float scale = 1f)
    {
        if (!Enabled || scale <= 0f || RequiemData.Sfx is not { } table) return;
        foreach (var layer in table.For(sfxEvent))
        {
            try { PlayLayer(layer, SfxRules.LayerVolume(layer, scale)); }
            catch (Exception e)
            {
                if (Reported.Add(layer.Sound)) MainFile.Logger.Info($"[SlayTheRequiem] SFX {sfxEvent} ({layer.Sound}): {e.Message}");
            }
        }
    }

    /// <summary>Удар по типу урона (физический — ванильный звук атаки, без нашего); многоудары не сливаются в треск.</summary>
    public static void Hit(Creature target, DamageType type, bool poweredAttack, int totalDamage)
    {
        if (!Enabled || SfxRules.HitEvent(type) is not { } sfxEvent) return;
        var volume = SfxRules.HitVolume(type, poweredAttack, totalDamage);
        if (volume <= 0f) return;
        var last = LastHit.GetOrCreateValue(target);
        var now = Time.GetTicksMsec();
        if (SfxRules.Throttled(last.GetValueOrDefault(sfxEvent), now)
            || SfxRules.Throttled(LastGroupHit.GetValueOrDefault(sfxEvent), now)) return;
        last[sfxEvent] = LastGroupHit[sfxEvent] = now;
        Play(sfxEvent, volume);
    }

    public static void Shout(ShoutFx variant) => Play(SfxRules.ShoutEvent(variant));

    private static void PlayLayer(SfxLayer layer, float volume)
    {
        if (volume <= 0f) return;
        switch (layer.Source)
        {
            case SfxSource.Fmod:
                NAudioManager.Instance?.PlayOneShot(layer.Sound, volume);
                break;
            case SfxSource.Game:
                NGame.Instance?.DebugAudio?.Play(layer.Sound, volume, PitchVariance.Small);
                break;
            case SfxSource.Own:
                PlayOwn(SfxRules.FilePath(layer, MainFile.ResPath), volume);
                break;
        }
    }

    /// <summary>Свой файл: одноразовый AudioStreamPlayer под NGame (переживает смену сцены — откат уходит в меню).</summary>
    private static void PlayOwn(string path, float volume)
    {
        if (NGame.Instance is not { } game || Stream(path) is not { } stream) return;
        var player = new AudioStreamPlayer
        {
            Name = "RequiemSfx",
            Stream = stream,
            VolumeLinear = volume,
            PitchScale = 1f + (GD.Randf() - 0.5f) * 0.04f,
            Bus = SfxBus,
            Autoplay = true, // AddChildSafely может отложить добавление — звук начнётся, когда узел войдёт в дерево
        };
        player.Finished += player.QueueFree;
        RequiemMotion.Watch(player, () => { if (!Enabled) { player.Stop(); player.QueueFree(); } });
        game.AddChildSafely(player);
    }

    private static AudioStream? Stream(string path)
    {
        if (Streams.TryGetValue(path, out var cached)) return cached;
        AudioStream? stream = null;
        if (ResourceLoader.Exists(path)) stream = ResourceLoader.Load<AudioStream>(path);
        if (stream == null && Reported.Add(path)) MainFile.Logger.Warn($"[SlayTheRequiem] SFX: нет файла {path} — звук пропущен.");
        Streams[path] = stream;
        return stream;
    }
}
