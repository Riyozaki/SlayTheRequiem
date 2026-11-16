using System.Reflection;
using BaseLib.Config;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode;

/// <summary>
/// Точка входа мода. Модели (силы, ресурсы, синглтон правил) BaseLib находит сам по типам сборки;
/// здесь — только Harmony, настройки и загрузка таблиц данных.
/// </summary>
[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "SlayTheRequiem"; //Used for resource filepath
    public const string ResPath = $"res://{ModId}";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();

        ModConfigRegistry.Register(ModId, new RequiemConfig());
        RequiemData.Load();

        // По одному классу, а не PatchAll: сломанная обновлением игры цель не срывает остальные патчи (ревью F-02).
        PatchReport.PatchAll(new Harmony(ModId), assembly);
        Safe("RunLifecycle.Hook", RunLifecycle.Hook);
        Safe("RequiemUi.Register", UI.RequiemUi.Register);

        Logger.Info("Slay the Requiem loaded.");
    }

    private static void Safe(string what, Action action)
    {
        try { action(); }
        catch (Exception e) { Logger.Error($"{what}: {e}"); }
    }
}
