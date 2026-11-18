using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Acts;
using SlayTheRequiem.SlayTheRequiemCode.Rollback;
using SlayTheRequiem.SlayTheRequiemCode.Shops;
using SlayTheRequiem.SlayTheRequiemCode.UI;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Сброс статического состояния мода при запуске забега — нового, загруженного или возврата Камнем Памяти
/// (<c>RunManager.Launch</c> → событие <c>RunStarted</c>). Ревью R-31: кэши и кооп-раунды не должны переживать забег.
/// Не сбрасывается намеренно: Отпечаток Камня Памяти (переживает гибель по замыслу, 15), кэш типов зелий (не зависит
/// от забега), ConditionalWeakTable (очищаются вместе с объектами).
/// </summary>
public static class RunLifecycle
{
    private static bool _hooked;

    public static void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        RunManager.Instance.RunStarted += OnRunStarted;
        RunManager.Instance.RoomEntered += () =>
        {
            MemoryStone.OnRoomEntered();
            Debugging.RunDiagnostics.Log(RunManager.Instance.DebugOnlyGetState(), "room");
        };
    }

    private static void OnRunStarted(RunState state)
    {
        try
        {
            ActMarks.Reset();
            MapGraph.Reset();
            ActDoors.Reset();
            CoopMemoryStone.ResetForNewRun();
            OperationRecovery.ResetForNewRun();
            // Флаги незакрытых окон прошлого забега (ревью F-06): иначе, например, «Дальше» на наградах босса молчит.
            PortalPromptPatch.ResetForNewRun();
            ShopServices.ResetForNewRun();
            Alchemy.Camp.ResetForNewRun();
            OverloadWarningPatch.ResetForNewRun();
            MapVisuals.ResetForNewRun();
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"RunLifecycle: сброс состояния не удался: {e}");
        }
        Debugging.RunDiagnostics.OnLaunch(state);
        MemoryStone.OnLaunch(state);
    }
}
