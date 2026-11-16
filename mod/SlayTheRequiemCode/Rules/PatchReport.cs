using System.Reflection;
using HarmonyLib;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Патч-«несущая стена» (ревью R-32/33, F-02): без него системы Реквиема не работают согласованно (акты, стартовые
/// реликвии, синхронизация настроек). Если такой патч не встал, системы мода выключаются на всю сессию — игра остаётся
/// ванильной, а не «наполовину модовой».
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CriticalPatchAttribute : Attribute { }

/// <summary>
/// Установка патчей Harmony по одному классу (ревью R-32/33, F-02). Ванильный <c>PatchAll</c> останавливается на первом
/// исключении — после обновления игры одна сменившаяся цель сорвала бы весь мод. Здесь каждый класс патчится отдельно,
/// сбои копятся в <see cref="Failed"/> и пишутся в лог одним списком; консоль: <c>str_patches</c>.
/// </summary>
public static class PatchReport
{
    private static readonly List<string> _failed = new();
    private static int _applied;

    /// <summary>Классы-патчи, которые не встали (имя и сообщение об ошибке).</summary>
    public static IReadOnlyList<string> Failed => _failed;

    /// <summary>Сколько классов-патчей встало без ошибок.</summary>
    public static int Applied => _applied;

    /// <summary>Не встал патч с <see cref="CriticalPatchAttribute"/> — системы Реквиема выключены до перезапуска.</summary>
    public static bool Degraded { get; private set; }

    public static void PatchAll(Harmony harmony, Assembly assembly)
    {
        foreach (var type in AccessTools.GetTypesFromAssembly(assembly))
        {
            if (!type.GetCustomAttributes(typeof(HarmonyAttribute), false).Any()) continue;
            try
            {
                harmony.CreateClassProcessor(type).Patch();
                _applied++;
            }
            catch (Exception e)
            {
                var critical = type.GetCustomAttributes(typeof(CriticalPatchAttribute), false).Any();
                if (critical) Degraded = true;
                var reason = (e.InnerException ?? e).Message;
                _failed.Add($"{type.FullName}{(critical ? " [critical]" : string.Empty)}: {reason}");
                MainFile.Logger.Error($"Патч {type.FullName} не установлен{(critical ? " (несущий)" : string.Empty)}: {e}");
            }
        }

        if (_failed.Count == 0)
        {
            MainFile.Logger.Info($"Патчи Harmony: установлено {_applied}.");
            return;
        }
        MainFile.Logger.Error($"Патчи Harmony: установлено {_applied}, не установлено {_failed.Count}:\n  " + string.Join("\n  ", _failed));
        if (Degraded)
            MainFile.Logger.Error("Не встал несущий патч — системы Slay the Requiem выключены до перезапуска игры (игра идёт по ванильным правилам).");
    }
}
