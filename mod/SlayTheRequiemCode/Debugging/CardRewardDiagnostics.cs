using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Rewards;

namespace SlayTheRequiem.SlayTheRequiemCode.Debugging;

/// <summary>
/// Диагностика наград картами (плейтест 2026-11: «то нет карт, то 2 из 3»). Пишет в лог игры, если награда собрала
/// меньше карт, чем должна, или упала с исключением, — со списком карт. Поведение игры не меняет (исключение
/// пробрасывается дальше). Лог: %APPDATA%/SlayTheSpire2/logs/godot.log, строки с «[CardReward]».
/// </summary>
[HarmonyPatch(typeof(CardReward), nameof(CardReward.Populate))]
public static class CardRewardDiagnostics
{
    private static readonly AccessTools.FieldRef<CardReward, List<CardCreationResult>>? Cards =
        AccessTools.Field(typeof(CardReward), "_cards") is { } f
            ? AccessTools.FieldRefAccess<CardReward, List<CardCreationResult>>(f)
            : null;

    private static readonly System.Reflection.PropertyInfo? OptionCount =
        AccessTools.Property(typeof(CardReward), "OptionCount");

    [HarmonyPostfix]
    public static void Postfix(CardReward __instance)
    {
        try
        {
            if (Cards == null || OptionCount?.GetValue(__instance) is not int expected) return;
            var cards = Cards(__instance);
            if (cards.Count >= expected) return;
            MainFile.Logger.Warn($"[CardReward] карт {cards.Count} из {expected}: " +
                                 string.Join(", ", cards.Select(c => c.Card.Id.Entry)));
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[CardReward] диагностика: {e.Message}");
        }
    }

    [HarmonyFinalizer]
    public static Exception? Finalizer(Exception? __exception)
    {
        if (__exception != null)
            MainFile.Logger.Error($"[CardReward] награда не собрана: {__exception}");
        return __exception;
    }
}
