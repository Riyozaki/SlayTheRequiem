using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.RestSite;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;

namespace SlayTheRequiem.SlayTheRequiemCode.Patches;

/// <summary>
/// Значок действий лагеря Реквиема: игра берёт его по OptionId из своих ресурсов
/// (ui/rest_site/option_&lt;id&gt;.png), которых у мода нет. Подменяем на свой файл или ванильную заглушку.
/// </summary>
[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.Icon), MethodType.Getter)]
public static class RestSiteIconPatch
{
    [HarmonyPrefix]
    public static bool Prefix(RestSiteOption __instance, ref Texture2D __result)
    {
        if (__instance is not RequiemRestSiteOption option) return true;
        __result = ResourceLoader.Load<Texture2D>(option.IconPathOverride, null, ResourceLoader.CacheMode.Reuse);
        return false;
    }
}
