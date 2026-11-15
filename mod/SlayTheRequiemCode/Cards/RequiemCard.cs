using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Extensions;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Cards;

/// <summary>
/// База карт мода: картинки из ресурсов мода, цена ◆ (переменная "Stamina", улучшается как обычная переменная),
/// тип урона и короткие помощники для атак/статусов.
/// </summary>
public abstract class RequiemCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : ConstructedCardModel(cost, type, rarity, target), IRequiemCard
{
    public const string StaminaVar = "Stamina";

    //Normal art: 1000x760 (500x380 тоже подойдёт). Full art: 606x852.
    public override string CustomPortraitPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".BigCardImagePath();
    public override string PortraitPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();
    public override string BetaPortraitPath => $"beta/{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();

    public virtual DamageType DamageType => DamageType.Physical;

    public int StaminaCost => DynamicVars.TryGetValue(StaminaVar, out var v) ? (int)v.BaseValue : 0;

    /// <summary>Объявить цену ◆ (для атак — обязательно, для навыков-рывков — по желанию).</summary>
    protected RequiemCard WithStamina(int cost, int upgrade = 0)
    {
        WithVar(StaminaVar, cost, upgrade);
        return this;
    }

    protected decimal Var(string name) => DynamicVars[name].BaseValue;

    protected Task Hit(PlayerChoiceContext ctx, Creature target, string vfx = "vfx/vfx_attack_slash", decimal? damage = null) =>
        DamageCmd.Attack(damage ?? DynamicVars.Damage.BaseValue).FromCard(this).Targeting(target).WithHitFx(vfx).Execute(ctx);

    protected Task HitAll(PlayerChoiceContext ctx, string vfx = "vfx/vfx_attack_slash") =>
        DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).TargetingAllOpponents(CombatState).WithHitFx(vfx).Execute(ctx);

    protected Task Apply<T>(PlayerChoiceContext ctx, Creature target, decimal amount) where T : PowerModel =>
        PowerCmd.Apply<T>(ctx, target, amount, Owner.Creature, this);

    protected Task ApplyAll<T>(PlayerChoiceContext ctx, decimal amount) where T : PowerModel =>
        PowerCmd.Apply<T>(ctx, CombatState.HittableEnemies, amount, Owner.Creature, this);

    protected Task Block(CardPlay play, decimal? amount = null) => amount is { } a
        ? CreatureCmd.GainBlock(Owner.Creature, a, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, play)
        : CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
}

/// <summary>
/// Клич (docs/design/04 «Глас»): карта, усиливаемая Знамением Гласа и Громовым Венценосцем (+урон).
/// Перезарядка Кличей — с системой Кличей (этап 5), до тех пор Кличи — обычные карты.
/// </summary>
public interface IShoutCard { }

