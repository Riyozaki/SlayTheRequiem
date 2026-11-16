using System.Reflection;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace SlayTheRequiem.SlayTheRequiemCode.Net;

/// <summary>
/// Обработчик своих сообщений мода в лобби — ровно один на сетевой сервис (найдено при исправлении F-03).
/// <para>
/// BaseLib 3.4.8 (и ветка develop на 2026-11) регистрирует <see cref="CustomMessageWrapper"/> в лобби только у хоста:
/// патч для клиента по ошибке нацелен на конструктор <c>NetHostGameService</c>. До старта забега
/// (<c>RunManager.InitializeShared</c>) клиент не принимает ни одно <see cref="ICustomMessage"/> — а снимок настроек
/// забега (<see cref="RunSettingsMessage"/>) приходит именно в лобби. У хоста при этом обработчиков может оказаться два.
/// </para>
/// Здесь после конструкторов обоих сервисов (последними из постфиксов) вызываем внутренние Unregister + Register
/// BaseLib: Unregister снимает все копии, Register ставит одну. Если BaseLib исправят — вызов останется безвредным.
/// </summary>
public static class LobbyMessageHandlers
{
    private static readonly MethodInfo? Register = AccessTools.Method(typeof(CustomMessageWrapper), "Register", [typeof(INetGameService)]);
    private static readonly MethodInfo? Unregister = AccessTools.Method(typeof(CustomMessageWrapper), "Unregister", [typeof(INetGameService)]);

    public static void Ensure(INetGameService service)
    {
        if (Register == null || Unregister == null)
        {
            MainFile.Logger.Warn("LobbyMessageHandlers: в BaseLib нет CustomMessageWrapper.Register/Unregister — " +
                                 "сообщения мода в лобби клиента могут не доходить.");
            return;
        }
        try
        {
            Unregister.Invoke(null, [service]);
            Register.Invoke(null, [service]);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"LobbyMessageHandlers: не удалось зарегистрировать обработчик: {e.Message}");
        }
    }

    [HarmonyPatch]
    public static class ClientServicePatch
    {
        public static MethodBase TargetMethod() => AccessTools.Constructor(typeof(NetClientGameService), Type.EmptyTypes);

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(NetClientGameService __instance) => Ensure(__instance);
    }

    [HarmonyPatch]
    public static class HostServicePatch
    {
        public static IEnumerable<MethodBase> TargetMethods() =>
            typeof(NetHostGameService).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(NetHostGameService __instance) => Ensure(__instance);
    }
}
