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
/// Значок типа урона (art spec 4.6, M1 п. 4): иконка стихии под ценой ◆ у атак и заклинаний не физического урона.
/// Подсказка с сопротивлениями — ключевое слово типа урона (<see cref="CardHoverTipsPatch"/>).
/// </summary>
public partial class NDamageTypeBadge : Control
{
    public static readonly Vector2 BadgeSize = new(44, 44);
    private readonly TextureRect _icon;

    public NDamageTypeBadge()
    {
        Name = "StrDamageTypeBadge";
        Size = BadgeSize;
        CustomMinimumSize = BadgeSize;
        MouseFilter = MouseFilterEnum.Ignore;
        _icon = RequiemUi.MakeIcon(null, BadgeSize);
        _icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_icon);
        Visible = false;
    }

    public bool Refresh(NCard card, CardModel? model)
    {
        if (model == null || card.Visibility != ModelVisibility.Visible) return false;
        if (RequiemUi.VisibleDamageType(model) is not { } type) return false;
        var tex = RequiemUi.Texture(RequiemUi.DamageTypeIcon(type));
        if (tex == null) return false;
        _icon.Texture = tex;
        return true;
    }
}
