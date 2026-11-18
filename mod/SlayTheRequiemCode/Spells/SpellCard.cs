using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Spells;

/// <summary>
/// Заклинание (docs/design/08): цена ✦ = цена в звёздах игры (рисуется на карте, проверяется и списывается игрой),
/// школа и ступень. Цену по владению правит <see cref="SpellRules"/> через TryModifyStarCost; «Непостижимо» — карта
/// неиграбельна; «С трудом» — эффект ×0.75. Урон заклинаний «несиловой» (Unpowered): Сила/Слабость и Латы его не
/// трогают, зато режут сопротивления стихии/магии.
/// </summary>
public abstract class SpellCard(int energyCost, int manaCost, SpellSchool school, SpellTier tier, CardType type, TargetType target)
    : RequiemCard(energyCost, type, RarityFor(tier), target)
{
    public SpellSchool School => school;
    public SpellTier Tier => tier;
    public int BaseManaCost => manaCost;

    public override int CanonicalStarCost => manaCost;

    private bool _fromScroll;

    /// <summary>
    /// Сотворено свитком или посохом (14 §2.3, 07 §3.4): владение игнорируется, цена ✦ — 0. Флаг экземпляра (карта создаётся
    /// в бою и в колоду не попадает), поэтому не сохраняется.
    /// </summary>
    public virtual bool FromScroll => _fromScroll;

    /// <summary>
    /// Сделать карту «со свитка»: 0 ✦ без проверки владения, Истощение; <paramref name="energy"/> — цена ⚡ на этот бой
    /// (свиток — 1, посох — без изменения при null).
    /// </summary>
    public void MakeFreeCast(int? energy)
    {
        _fromScroll = true;
        AddKeyword(CardKeyword.Exhaust);
        if (energy is { } e) EnergyCost.SetThisCombat(e);
    }

    public static CardRarity RarityFor(SpellTier tier) => tier switch
    {
        SpellTier.Novice or SpellTier.Apprentice => CardRarity.Common,
        SpellTier.Adept => CardRarity.Uncommon,
        _ => CardRarity.Rare,
    };

    public SpellAffinity.CastCost Affinity =>
        SpellAffinity.Evaluate(manaCost, tier, Mastery.ForSpell(Owner, this), FromScroll);

    /// <summary>Вне системы Реквиема (чужие моды/персонажи) владение не проверяется.</summary>
    protected override bool IsPlayable => Owner == null || !RequiemMode.IsInSystem(Owner) || Affinity.Castable;

    public decimal EffectMultiplier => Owner != null && RequiemMode.IsInSystem(Owner) ? Affinity.EffectMultiplier : 1m;

    /// <summary>Число эффекта (стаки, лечение, Латы) с учётом «С трудом».</summary>
    protected int Eff(decimal amount) => SpellAffinity.ScaleEffect((int)amount, EffectMultiplier);
    protected int Eff(string varName) => Eff(DynamicVars[varName].BaseValue);

    /// <summary>Урон заклинания (несиловой): превью и расчёт идут без Силы/Слабости.</summary>
    protected SpellCard WithSpellDamage(int baseValue, int upgrade = 0)
    {
        WithVar(new DamageVar(baseValue, ValueProp.Move | ValueProp.Unpowered).WithUpgrade(upgrade));
        return this;
    }

    protected Task Bolt(PlayerChoiceContext ctx, Creature target, string vfx = "vfx/vfx_attack_slash", decimal? damage = null) =>
        DamageCmd.Attack(damage ?? DynamicVars.Damage.BaseValue).FromCard(this).Targeting(target).Unpowered().WithHitFx(vfx).Execute(ctx);

    protected Task Nova(PlayerChoiceContext ctx, string vfx = "vfx/vfx_attack_slash", decimal? damage = null) =>
        DamageCmd.Attack(damage ?? DynamicVars.Damage.BaseValue).FromCard(this).TargetingAllOpponents(CombatState).Unpowered().WithHitFx(vfx).Execute(ctx);
}
