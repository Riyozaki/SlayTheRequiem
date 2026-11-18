using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Подсказки систем мода к любой карте — ванильной и своей (M1 п. 4, 9): ◆ (и Устал/Изнурён/Надрыв по текущей ◆),
/// тип урона, ✦ у заклинаний, Оружейная атака, Клич, Подлый, вес. Текст карты не трогаем — только список подсказок.
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
public static class CardHoverTipsPatch
{
    [HarmonyPostfix]
    public static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (!RunSettings.EnableRequiemSystems) return;
        try
        {
            var extra = Extra(__instance).ToList();
            if (extra.Count > 0) __result = __result.Concat(extra).Distinct().ToList();
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"UI: подсказки карты {__instance.Id}: {e.Message}");
        }
    }

    private static IEnumerable<IHoverTip> Extra(CardModel card)
    {
        var owner = StaminaCosts.OwnerOf(card);
        if (owner != null && !RequiemMode.IsInSystem(owner)) yield break;

        if (RequiemUi.PaysStamina(card))
        {
            yield return HoverTipFactory.FromKeyword(card is IRequiredStaminaCard ? RequiemKeywords.RequiredStamina : RequiemKeywords.Stamina);
            var cost = StaminaCosts.IsX(card) ? 0 : StaminaCosts.CostFor(card);
            if (card is not IRequiredStaminaCard && RequiemUi.StaminaStateFor(owner, cost) is { } st)
            {
                if (st.state == StaminaState.Tired) yield return HoverTipFactory.FromKeyword(RequiemKeywords.Tired);
                if (st.state == StaminaState.Exhausted) yield return HoverTipFactory.FromKeyword(RequiemKeywords.Exhausted);
                if (st.overstrain) yield return HoverTipFactory.FromKeyword(RequiemKeywords.Overstrain);
            }
        }
        if (card is Spells.SpellCard) yield return HoverTipFactory.FromKeyword(RequiemKeywords.Mana);
        if (RequiemUi.VisibleDamageType(card) is { } type && RequiemKeywords.ForDamageType(type) is { } kw)
            yield return HoverTipFactory.FromKeyword(kw);
        if (EquipmentCombatRules.IsWeaponAttack(card)) yield return HoverTipFactory.FromKeyword(RequiemKeywords.WeaponAttack);
        if (card is IShoutCard) yield return HoverTipFactory.FromKeyword(RequiemKeywords.Shout);
        if (card.DynamicVars.ContainsKey(Cards.Silent.SilentCard.SneakVar))
            yield return HoverTipFactory.FromKeyword(RequiemKeywords.Sneak);
        if (Loadout.CardWeight(card) > 0) yield return HoverTipFactory.FromKeyword(RequiemKeywords.Weight);
    }
}
