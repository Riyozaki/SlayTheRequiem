using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Цена на намерении (art spec 4.5, 11 §1): «12 ◆2» у атаки, «12 ✦3» у заклинания врага-мага. Намерение
/// «Отдышаться» / «Сосредоточиться» (игра видит его как оглушение) получает свою иконку.
/// Цена считается теми же формулами, что и списание (<see cref="EnemyRules.AttackCost"/>, <see cref="EnemyRules.SpellCost"/>).
/// </summary>
[HarmonyPatch(typeof(NIntent), "UpdateVisuals")]
public static class IntentCostPatch
{
    private const string OverlayName = "StrIntentOverride";

    private static readonly AccessTools.FieldRef<NIntent, AbstractIntent> IntentRef = AccessTools.FieldRefAccess<NIntent, AbstractIntent>("_intent");
    private static readonly AccessTools.FieldRef<NIntent, Creature> OwnerRef = AccessTools.FieldRefAccess<NIntent, Creature>("_owner");
    private static readonly AccessTools.FieldRef<NIntent, IEnumerable<Creature>> TargetsRef = AccessTools.FieldRefAccess<NIntent, IEnumerable<Creature>>("_targets");
    private static readonly AccessTools.FieldRef<NIntent, MegaRichTextLabel> LabelRef = AccessTools.FieldRefAccess<NIntent, MegaRichTextLabel>("_valueLabel");
    private static readonly AccessTools.FieldRef<NIntent, Sprite2D> SpriteRef = AccessTools.FieldRefAccess<NIntent, Sprite2D>("_intentSprite");

    [HarmonyPostfix]
    public static void Postfix(NIntent __instance)
    {
        try
        {
            var intent = IntentRef(__instance);
            var owner = OwnerRef(__instance);
            if (intent == null || owner == null) return;
            var sprite = SpriteRef(__instance);
            var rest = RunSettings.EnableRequiemSystems && intent is StunIntent
                ? EnemyResponses.RestingAs(owner)
                : EnemyResponses.RestKind.None;
            SetOverlay(sprite, rest);
            if (!RunSettings.EnableRequiemSystems || intent is not AttackIntent attack) return;

            var caster = owner.HasPower<EnemyManaPower>();
            if (!caster && !owner.HasPower<EnemyStaminaPower>()) return;
            var cost = Rules.EnemyActionBudget.CostOf(owner);
            var color = (caster ? RequiemUi.ManaColor : RequiemUi.StaminaColor).ToHtml(false);
            var label = LabelRef(__instance);
            if (label != null) label.Text += $" [color=#{color}]{(caster ? "✦" : "◆")}{cost}[/color]";
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"UI: цена намерения: {e.Message}");
        }
    }

    /// <summary>Своя иконка поверх спрайта намерения (сам спрайт анимирует NIntent._Process — его только прячем).</summary>
    private static void SetOverlay(Sprite2D? sprite, EnemyResponses.RestKind rest)
    {
        if (sprite == null) return;
        var parent = sprite.GetParent();
        var overlay = parent?.GetNodeOrNull<TextureRect>(OverlayName);
        if (rest == EnemyResponses.RestKind.None)
        {
            if (overlay != null) overlay.Visible = false;
            sprite.Visible = true;
            return;
        }
        var tex = RequiemUi.Texture(RequiemUi.UiImage(rest == EnemyResponses.RestKind.Breather ? "intents/breather" : "intents/spell"));
        if (tex == null || parent == null) return;
        if (overlay == null)
        {
            overlay = RequiemUi.MakeIcon(null, new Vector2(64, 64));
            overlay.Name = OverlayName;
            parent.AddChild(overlay);
        }
        overlay.Texture = tex;
        overlay.Position = sprite.Position - overlay.Size * 0.5f;
        overlay.Visible = true;
        sprite.Visible = false;
    }
}
