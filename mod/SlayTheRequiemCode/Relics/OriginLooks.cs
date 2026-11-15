using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.UI;

namespace SlayTheRequiem.SlayTheRequiemCode.Relics;

/// <summary>
/// Экран выбора происхождения и предыстории (M7 п. 3) — ванильный «выбери реликвию» (<see cref="OriginRelic.Pick"/>,
/// <see cref="BackgroundRelic.Pick"/>). Значки свои (images/relics, подхватываются RequiemRelic сами), а заголовок вместо
/// «Выберите реликвию» говорит, что выбирается: клан, племя, династия или предыстория (<see cref="OriginLookRules.HeadingKey"/>).
/// </summary>
[HarmonyPatch(typeof(NChooseARelicSelection), nameof(NChooseARelicSelection._Ready))]
public static class OriginPickHeadingPatch
{
    private static readonly AccessTools.FieldRef<NChooseARelicSelection, IReadOnlyList<RelicModel>> RelicsRef =
        AccessTools.FieldRefAccess<NChooseARelicSelection, IReadOnlyList<RelicModel>>("_relics");

    private static readonly AccessTools.FieldRef<NChooseARelicSelection, NCommonBanner> BannerRef =
        AccessTools.FieldRefAccess<NChooseARelicSelection, NCommonBanner>("_banner");

    [HarmonyPostfix]
    public static void Postfix(NChooseARelicSelection __instance)
    {
        try
        {
            var relics = RelicsRef(__instance);
            if (relics == null || OriginLookRules.HeadingKey(relics.Select(r => r.GetType().Name).ToList()) is not { } key) return;
            BannerRef(__instance)?.label?.SetTextAutoSize(RequiemUi.TipText(key));
        }
        catch (Exception e)
        {
            MainFile.Logger.Info($"OriginPickHeadingPatch: {e.Message}");
        }
    }
}
