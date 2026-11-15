using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Acts;
using SlayTheRequiem.SlayTheRequiemCode.Rollback;

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
    }

    private static void OnRunStarted(RunState state)
    {
        try
        {
            ActMarks.Reset();
            CoopMemoryStone.ResetForNewRun();
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"RunLifecycle: сброс состояния не удался: {e}");
        }
    }
}
