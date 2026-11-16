using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Вес карты (art spec 4.2, 02 §3): гиря с цифрой в правом нижнем углу карты — рука, колода, награды, лавка.
/// Статусы весят 0 — для них значка нет. Учитывает чары «Облегчённая».
/// </summary>
public partial class NWeightBadge : Control
{
    public const string NodeName = "StrWeightBadge";
    public static readonly Vector2 BadgeSize = new(46, 46);
    /// <summary>Центр карты — начало координат CardContainer (карта 300×422).</summary>
    public static readonly Vector2 CornerPosition = new(150 - 46 - 10, 211 - 46 - 12);

    private readonly Label _label;

    public NWeightBadge()
    {
        Name = NodeName;
        Size = BadgeSize;
        Position = CornerPosition;
        MouseFilter = MouseFilterEnum.Ignore;
        var icon = RequiemUi.MakeIcon(RequiemUi.UiImage("weight_badge"), BadgeSize);
        icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(icon);
        _label = RequiemUi.MakeLabel(18, new Color("2B1E0E"));
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // Цифра на «теле» гири, чуть ниже центра.
        _label.Position += new Vector2(0, 4);
        AddChild(_label);
        Visible = false;
    }

    public void Refresh(NCard card, CardModel? model)
    {
        if (model == null || card.Visibility != ModelVisibility.Visible || !RunSettings.EnableRequiemSystems)
        {
            Visible = false;
            return;
        }
        var owner = StaminaCosts.OwnerOf(model);
        if (owner != null && !RequiemMode.IsInSystem(owner))
        {
            Visible = false;
            return;
        }
        var weight = Loadout.CardWeight(model);
        Visible = weight > 0;
        _label.Text = weight.ToString();
    }

    public static NWeightBadge For(NCard card)
    {
        var container = card.GetChildCount() > 0 ? card.GetChild(0) : card;
        var badge = container.GetNodeOrNull<NWeightBadge>(NodeName);
        if (badge != null) return badge;
        badge = new NWeightBadge();
        container.AddChild(badge);
        return badge;
    }
}

/// <summary>Вешает <see cref="NWeightBadge"/> на каждую перерисовку карты (тот же момент, что ExtraCardUi BaseLib).</summary>
[HarmonyPatch(typeof(NCard), "Reload")]
public static class CardWeightBadgePatch
{
    [HarmonyPostfix]
    public static void Postfix(NCard __instance)
    {
        try
        {
            NWeightBadge.For(__instance).Refresh(__instance, __instance.Model);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"UI: значок веса: {e.Message}");
        }
    }
}
