using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Цвет названия стихийной карты (по отзыву плейтеста к M1): огонь — оранжевый, холод — голубой, разряд — сиреневый,
/// яд — зелёный и т. д., те же оттенки, что у значка стихии. Постфикс приватного <c>NCard.UpdateTitleLabel</c>:
/// игра красит название при каждой перерисовке (кремовый, у улучшенных — зелёный), мы перекрашиваем после неё.
/// Улучшенная карта сохраняет ванильную зелёную обводку, а «+» в названии остаётся — улучшение по-прежнему видно.
/// Выключается настройкой <see cref="Config.RequiemConfig.ColorElementalTitles"/>.
/// </summary>
[HarmonyPatch(typeof(NCard), "UpdateTitleLabel")]
public static class ElementalTitlePatch
{
    private static readonly AccessTools.FieldRef<NCard, MegaLabel> TitleLabel =
        AccessTools.FieldRefAccess<NCard, MegaLabel>("_titleLabel");

    [HarmonyPostfix]
    public static void Postfix(NCard __instance)
    {
        try
        {
            var model = __instance.Model;
            if (model == null || __instance.Visibility != ModelVisibility.Visible) return;
            if (RequiemUi.TitleDamageType(model) is not { } type) return;
            var label = TitleLabel(__instance);
            if (label == null) return;
            label.AddThemeColorOverride(ThemeConstants.Label.FontColor, RequiemUi.TitleColor(type));
            label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor,
                model.CurrentUpgradeLevel > 0 ? StsColors.cardTitleOutlineSpecial : RequiemUi.TitleOutline(type));
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"UI: цвет названия стихийной карты: {e.Message}");
        }
    }
}
