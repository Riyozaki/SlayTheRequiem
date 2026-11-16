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
/// Значок цены ◆ (art spec 4.1, 02 §6): «в верхнем левом углу под энергией», через BaseLib ExtraCardUi (Left).
/// Виден у атак персонажей в системе мода — в руке, колоде, наградах, лавке и библиотеке. X-атаки — «X».
/// Цвет в руке: норма / Устал (×0,75) / Изнурён (×0,5) / надрыв (красный, со значком ♥). 0 ◆ — полупрозрачный.
/// </summary>
public partial class NStaminaCostBadge : Control
{
    public static readonly Vector2 BadgeSize = new(52, 52);

    private readonly TextureRect _orb;
    private readonly Label _label;
    private readonly Label _heart;
    private NCard? _card;
    private double _sinceRefresh;

    public NStaminaCostBadge()
    {
        Name = "StrStaminaCostBadge";
        Size = BadgeSize;
        CustomMinimumSize = BadgeSize;
        PivotOffset = BadgeSize * 0.5f;
        MouseFilter = MouseFilterEnum.Ignore;

        _orb = RequiemUi.MakeIcon(RequiemUi.UiImage("stamina_cost_orb"), BadgeSize);
        _orb.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_orb);

        _label = RequiemUi.MakeLabel(24, RequiemUi.Outline);
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_label);

        // Надрыв: маленькое ♥ в правом нижнем углу.
        _heart = RequiemUi.MakeLabel(16, new Color("4A0000"));
        _heart.Text = "♥";
        _heart.Size = new Vector2(22, 22);
        _heart.Position = BadgeSize - new Vector2(18, 20);
        _heart.Visible = false;
        RequiemUi.SetLabelColors(_heart, RequiemUi.OverstrainColor, new Color("4A0000"));
        AddChild(_heart);

        Visible = false;
    }

    /// <summary>Обработчик ExtraCardUi: true — показать значок.</summary>
    public bool Refresh(NCard card, CardModel? model)
    {
        _card = card;
        if (model == null || card.Visibility != ModelVisibility.Visible || !RequiemUi.PaysStamina(model)) return false;
        Paint(card, model);
        return true;
    }

    private void Paint(NCard card, CardModel model)
    {
        var isX = StaminaCosts.IsX(model);
        var cost = isX ? 0 : StaminaCosts.CostFor(model);
        _label.Text = isX ? "X" : cost.ToString();

        var text = StsColors.cream;
        var outline = RequiemUi.Outline;
        var overstrain = false;
        if (card.DisplayingPile == PileType.Hand && RequiemUi.StaminaStateFor(StaminaCosts.OwnerOf(model), cost) is { } st)
        {
            var (state, over) = st;
            overstrain = over;
            text = over ? RequiemUi.OverstrainColor
                : state switch
                {
                    StaminaState.Tired => RequiemUi.TiredColor,
                    StaminaState.Exhausted => RequiemUi.ExhaustedColor,
                    _ => StsColors.cream,
                };
            if (over) outline = new Color("4A0000");
        }
        RequiemUi.SetLabelColors(_label, text, outline);
        _heart.Visible = overstrain;
        Modulate = !isX && cost == 0 ? new Color(1, 1, 1, 0.5f) : Colors.White;
    }

    public override void _Process(double delta)
    {
        // Цвет зависит от текущей ◆ — в руке перекрашиваем несколько раз в секунду (NCard.Reload на это не вызывается).
        if (!Visible || _card?.Model == null || _card.DisplayingPile != PileType.Hand) return;
        _sinceRefresh += delta;
        if (_sinceRefresh < 0.2) return;
        _sinceRefresh = 0;
        Paint(_card, _card.Model);
    }
}
