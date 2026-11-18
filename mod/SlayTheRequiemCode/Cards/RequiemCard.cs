using BaseLib.Abstracts;
using BaseLib.Extensions;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Art;
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

    // Свой арт: images/card_portraits/big/<id>.png 1000x760 (500x380 тоже подойдёт), малый — card_portraits/<id>.png.
    // Пока своего нет — ванильный портрет по таблице tools/art/vanilla_art.csv (Art/VanillaArt).
    private string ArtFile => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png";
    public override string CustomPortraitPath => ArtFile.BigCardImagePath(VanillaArt.CardPortrait(GetType().Name));
    public override string PortraitPath => ArtFile.CardImagePath(VanillaArt.CardPortrait(GetType().Name));
    public override string BetaPortraitPath => PortraitPath;

    public virtual DamageType DamageType => DamageType.Physical;

    // ── Служебный жетон (card spec §4): рамка-«табличка» ──
    // Жетоны выбора, услуги лавки и Шрамы — «меню», а не карты боя. Они получают ванильную рамку квестовых карт
    // (рамка, её материал и лента — как у Яйца Бирдониса): свиток вместо рамки героя, без своего арта.
    /// <summary>Карта — служебный жетон-«меню» (рамка-табличка вместо рамки карты боя).</summary>
    public virtual bool IsMenuToken => false;

    private const string PlaqueFramePath = "atlases/ui_atlas.sprites/card/card_frame_quest_s.tres";
    private const string PlaqueFrameMaterialPath = "res://materials/cards/frames/card_frame_quest_mat.tres";
    private const string PlaqueBannerMaterialPath = "res://materials/cards/banners/card_banner_quest_mat.tres";

    public override Texture2D? CustomFrame => IsMenuToken
        ? ResourceLoader.Load<Texture2D>(ImageHelper.GetImagePath(PlaqueFramePath), null, ResourceLoader.CacheMode.Reuse)
        : null;

    public override Material? CreateCustomFrameMaterial => IsMenuToken ? PreloadManager.Cache.GetMaterial(PlaqueFrameMaterialPath) : null;

    public override string? CustomBannerMaterialPath => IsMenuToken ? PlaqueBannerMaterialPath : null;

    public int StaminaCost => DynamicVars.TryGetValue(StaminaVar, out var v) ? (int)v.BaseValue : 0;

    /// <summary>
    /// Объявить цену ◆: для атак — обязательно (кроме <see cref="StaminaCosts.IsStaminaExempt"/>: заклинания, магические атаки
    /// с ✦, Кличи), для навыков-рывков — по желанию. Проверяет tools/cards/audit_cards.py.
    /// </summary>
    protected RequiemCard WithStamina(int cost, int upgrade = 0)
    {
        WithVar(StaminaVar, cost, upgrade);
        return this;
    }

    protected decimal Var(string name) => DynamicVars[name].BaseValue;

    /// <summary>Клич, отправленный на полку текущим розыгрышем (см. <see cref="Shouts"/>).</summary>
    public bool ShelvedByThisPlay { get; set; }

    protected override CardLocation GetResultLocationForCardPlay()
    {
        var location = base.GetResultLocationForCardPlay();
        ShelvedByThisPlay = false;
        if (this is not IShoutCard || location.pileType == PileType.None || !Shouts.Active(Owner) || !Shouts.HasRoom(Owner)) return location;
        ShelvedByThisPlay = true;
        return new CardLocation(location.player, PileType.None, CardPilePosition.None);
    }

    protected Task Hit(PlayerChoiceContext ctx, Creature target, string vfx = "vfx/vfx_attack_slash", decimal? damage = null) =>
        DamageCmd.Attack(damage ?? DynamicVars.Damage.BaseValue).FromCard(this, null).Targeting(target).WithHitFx(vfx).Execute(ctx);

    protected Task HitAll(PlayerChoiceContext ctx, string vfx = "vfx/vfx_attack_slash") =>
        DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, null).TargetingAllOpponents(CombatState).WithHitFx(vfx).Execute(ctx);

    protected Task Apply<T>(PlayerChoiceContext ctx, Creature target, decimal amount) where T : PowerModel =>
        PowerCmd.Apply<T>(ctx, target, amount, Owner.Creature, this);

    protected Task ApplyAll<T>(PlayerChoiceContext ctx, decimal amount) where T : PowerModel =>
        PowerCmd.Apply<T>(ctx, CombatState.HittableEnemies, amount, Owner.Creature, this);

    protected Task Block(CardPlay play, decimal? amount = null) => amount is { } a
        ? CreatureCmd.GainBlock(Owner.Creature, a, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, play)
        : CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
}

/// <summary>
/// Клич (docs/design/04 «Глас»): в системе Реквиема после розыгрыша уходит на полку и через
/// <see cref="ShoutCooldown"/> ходов возвращается в руку (<see cref="ShoutShelfPower"/>). Знамение Гласа и Крылатый Час
/// сокращают перезарядку, Знамение Гласа и Громовой Венценосец добавляют урон.
/// </summary>
public interface IShoutCard
{
    /// <summary>Базовая перезарядка в ходах.</summary>
    int ShoutCooldown => 3;
}

