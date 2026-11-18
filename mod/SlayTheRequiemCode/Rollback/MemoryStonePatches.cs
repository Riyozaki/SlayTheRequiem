using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace SlayTheRequiem.SlayTheRequiemCode.Rollback;

/// <summary>
/// Экран поражения: если есть Отпечаток и Свечи — окно «Вернуться к Камню Памяти» (docs/design/15 §2–3);
/// в коопе — предложение хоста и голосование (<see cref="CoopMemoryStone"/>).
/// AfterOverlayOpened вызывается, когда экран поражения показан (и при победе — тогда CanOffer откажет).
/// </summary>
[HarmonyPatch(typeof(NGameOverScreen), nameof(NGameOverScreen.AfterOverlayOpened))]
public static class GameOverMemoryStonePatch
{
    private static readonly AccessTools.FieldRef<NGameOverScreen, RunState> RunStateRef =
        AccessTools.FieldRefAccess<NGameOverScreen, RunState>("_runState");

    // Игра 0.113: поле _serializableRun заменено на _result (PostRunProgressUpdateResult, сам забег — в .run).
    private static readonly AccessTools.FieldRef<NGameOverScreen, MegaCrit.Sts2.Core.Entities.Save.PostRunProgressUpdateResult> ResultRef =
        AccessTools.FieldRefAccess<NGameOverScreen, MegaCrit.Sts2.Core.Entities.Save.PostRunProgressUpdateResult>("_result");

    [HarmonyPostfix]
    public static void OfferReturn(NGameOverScreen __instance)
    {
        var state = RunStateRef(__instance);
        var ended = ResultRef(__instance).run;
        if (state == null || ended == null) return;
        CoopMemoryStone.OnGameOver(state);
        if (!MemoryStone.CanOffer(state, ended)) return;
        TaskHelper.RunSafely(MemoryStone.Offer(state, ended));
    }
}
