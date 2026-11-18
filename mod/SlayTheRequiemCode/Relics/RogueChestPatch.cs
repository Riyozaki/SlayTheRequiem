using HarmonyLib;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Relics;

/// <summary>
/// Плут (04: «+1 к выбору в сундуках»; ревью F-09): в сундуке комнаты сокровищ на одну реликвию больше, чем игроков.
/// Одиночная игра — выбор 1 из 2 (экран сундука сам переходит на раскладку с несколькими реликвиями), кооп — лишняя
/// реликвия, за которую можно голосовать. Список строится одинаково на всех машинах: та же общая «сумка» реликвий
/// и свой генератор редкости от сида забега и этажа (ванильный <c>_rng</c> сундуков не сдвигается).
/// Не больше четырёх реликвий — столько мест на экране сундука.
/// </summary>
[HarmonyPatch(typeof(TreasureRoomRelicSynchronizer), nameof(TreasureRoomRelicSynchronizer.BeginRelicPicking))]
public static class RogueChestPatch
{
    private const int MaxRelicsOnScreen = 4;

    private static readonly AccessTools.FieldRef<TreasureRoomRelicSynchronizer, List<RelicModel>?> CurrentRef =
        AccessTools.FieldRefAccess<TreasureRoomRelicSynchronizer, List<RelicModel>?>("_currentRelics");

    private static readonly AccessTools.FieldRef<TreasureRoomRelicSynchronizer, RelicGrabBag> BagRef =
        AccessTools.FieldRefAccess<TreasureRoomRelicSynchronizer, RelicGrabBag>("_sharedGrabBag");

    // Игра 0.113: _playerCollection заменён на _runState (IRunState).
    private static readonly AccessTools.FieldRef<TreasureRoomRelicSynchronizer, IRunState> RunRef =
        AccessTools.FieldRefAccess<TreasureRoomRelicSynchronizer, IRunState>("_runState");

    [HarmonyPostfix]
    public static void Postfix(TreasureRoomRelicSynchronizer __instance)
    {
        try
        {
            var relics = CurrentRef(__instance);
            if (relics is not { Count: > 0 } || relics.Count >= MaxRelicsOnScreen) return;
            var rogue = RunRef(__instance).Players.FirstOrDefault(p => RequiemMode.IsInSystem(p) && p.Relics.OfType<RogueBackground>().Any());
            if (rogue == null) return;
            var run = rogue.RunState;
            var rng = new Rng(run.Rng.Seed, $"requiem_rogue_chest_{run.TotalFloor}");
            var extra = BagRef(__instance).PullFromFront(RelicFactory.RollRarity(rng), run);
            if (extra == null) return;
            relics.Add(extra);
            MainFile.Logger.Info($"[SlayTheRequiem] Плут: в сундуке лишняя реликвия {extra.Id}.");
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Плут: лишняя реликвия в сундуке не добавлена: {e.Message}");
        }
    }
}
