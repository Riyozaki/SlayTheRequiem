using BaseLib.Config;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Net;

namespace SlayTheRequiem.SlayTheRequiemCode.Alchemy;

/// <summary>
/// Метапрогресс лагеря (docs/design/10 §5, completion-plan M5 п. 5): отдельная настройка, по умолчанию выключена.
/// Победа в забеге открывает следующую постройку (<see cref="CampBuilding"/>), открытая стоит в каждом лагере. Только
/// бонус к обычному лагерю — без построек всё работает как раньше (стол и алтарь в половине лагерей по зерну).
/// Счётчик хранится в конфиге мода (переживает забеги). В коопе не действует: настройки у игроков разные, а список
/// действий лагеря каждый клиент строит для всех игроков сам.
/// </summary>
public static class CampMeta
{
    public static int Unlocked =>
        RequiemConfig.EnableCampMeta && !RequiemNet.IsMultiplayer
            ? Math.Clamp(RequiemConfig.CampBuildingsUnlocked, 0, CampRules.BuildingCount)
            : 0;

    public static bool Has(CampBuilding building) => CampRules.HasBuilding(Unlocked, building);
}

/// <summary>Победа в забеге (сохранение прогресса профиля) открывает следующую постройку лагеря.</summary>
[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.UpdateProgressWithRunData))]
public static class CampMetaVictoryPatch
{
    [HarmonyPostfix]
    public static void Postfix(bool victory)
    {
        try
        {
            if (!victory || !RequiemConfig.EnableCampMeta || !RequiemConfig.EnableRequiemSystems || RequiemNet.IsMultiplayer) return;
            var before = RequiemConfig.CampBuildingsUnlocked;
            var next = CampRules.UnlockAfterVictory(before);
            if (next == before) return;
            RequiemConfig.CampBuildingsUnlocked = next;
            ModConfig.SaveDebounced<RequiemConfig>();
            MainFile.Logger.Info($"[SlayTheRequiem] Лагерь: открыта постройка {(CampBuilding)next}.");
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Метапрогресс лагеря: {e.Message}");
        }
    }
}
