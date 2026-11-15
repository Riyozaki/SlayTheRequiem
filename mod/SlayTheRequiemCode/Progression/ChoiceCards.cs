using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Net;

namespace SlayTheRequiem.SlayTheRequiemCode.Progression;

// ───────────── карты выбора лагеря (только для показа, в колоду не попадают) ─────────────

/// <summary>
/// Вариант выбора в лагере (характеристика, узел дерева, блюдо, чары, Медитация, Молитва) в виде карты на экране
/// «выбери карту». Число и цена — переменные описания. Создаётся на время выбора и сразу удаляется из состояния забега.
/// </summary>
public abstract class PickCard : RequiemCard
{
    public const string ValueVar = "Value";
    public const string CostVar = "Cost";

    protected PickCard() : base(-1, CardType.Status, CardRarity.Token, TargetType.None)
    {
        WithKeyword(CardKeyword.Unplayable);
        WithVar(ValueVar, 0);
        WithVar(CostVar, 0);
    }

    public override bool IsMenuToken => true;
    public override int MaxUpgradeLevel => 0;

    public void SetDisplay(int value, int cost = 0)
    {
        if (DynamicVars.TryGetValue(ValueVar, out var v)) v.BaseValue = value;
        if (DynamicVars.TryGetValue(CostVar, out var c)) c.BaseValue = cost;
    }
}

/// <summary>Экран выбора одной карты-варианта (синхронный в коопе: выбор идёт через CardSelectCmd).</summary>
public static class CampMenu
{
    public sealed record Choice(Type CardType, int Value, int Cost, object Tag);

    /// <summary>Показать варианты; вернуть Tag выбранного или null (отмена).</summary>
    public static async Task<object?> Pick(Player owner, IReadOnlyList<Choice> choices, string promptKey)
    {
        if (choices.Count == 0) return null;
        var state = RequiemNet.State;
        if (state == null) return null;
        var cards = new List<CardModel>();
        try
        {
            foreach (var choice in choices)
            {
                var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(choice.CardType));
                var card = state.CreateCard(canonical, owner);
                if (card is PickCard pick) pick.SetDisplay(choice.Value, choice.Cost);
                cards.Add(card);
            }
            var prefs = new CardSelectorPrefs(new LocString("rest_site_ui", promptKey), 1)
            {
                Cancelable = true,
            };
            var chosen = (await CardSelectCmd.FromSimpleGrid(new ThrowingPlayerChoiceContext(), cards, owner, prefs)).FirstOrDefault();
            if (chosen == null) return null;
            var index = cards.IndexOf(chosen);
            return index >= 0 ? choices[index].Tag : null;
        }
        finally
        {
            foreach (var card in cards) state.RemoveCard(card);
        }
    }

    public static Type StatCard(StatAxis axis) => axis switch
    {
        StatAxis.Health => typeof(PickStatHealth),
        StatAxis.Stamina => typeof(PickStatStamina),
        StatAxis.Mana => typeof(PickStatMana),
        StatAxis.Burden => typeof(PickStatBurden),
        _ => typeof(PickStatMastery),
    };

    public static Type SchoolCard(SpellSchool school) => school switch
    {
        SpellSchool.Destruction => typeof(PickSchoolDestruction),
        SpellSchool.Restoration => typeof(PickSchoolRestoration),
        SpellSchool.Alteration => typeof(PickSchoolAlteration),
        SpellSchool.Conjuration => typeof(PickSchoolConjuration),
        SpellSchool.Illusion => typeof(PickSchoolIllusion),
        _ => typeof(PickSchoolIllusion),
    };

    public static Type DishCard(Dish dish) => dish switch
    {
        Dish.BloodmossStew => typeof(PickDishBloodmossStew),
        Dish.VenisonRoast => typeof(PickDishVenisonRoast),
        Dish.FishSoup => typeof(PickDishFishSoup),
        Dish.SpicedSausage => typeof(PickDishSpicedSausage),
        Dish.MushroomStew => typeof(PickDishMushroomStew),
        Dish.SweetRoll => typeof(PickDishSweetRoll),
        Dish.Feast => typeof(PickDishFeast),
        _ => typeof(PickDishFeast),
    };

    public static Type EnchantCard(Enchant enchant) => enchant switch
    {
        Enchant.Health => typeof(PickEnchantHealth),
        Enchant.Stamina => typeof(PickEnchantStamina),
        Enchant.Magic => typeof(PickEnchantMagic),
        Enchant.FireResistance => typeof(PickEnchantFireResistance),
        Enchant.FrostResistance => typeof(PickEnchantFrostResistance),
        Enchant.ShockResistance => typeof(PickEnchantShockResistance),
        Enchant.WeaponMight => typeof(PickEnchantWeaponMight),
        Enchant.Burden => typeof(PickEnchantBurden),
        Enchant.FireBrand => typeof(PickEnchantFireBrand),
        Enchant.FrostBrand => typeof(PickEnchantFrostBrand),
        Enchant.ShockBrand => typeof(PickEnchantShockBrand),
        Enchant.CraftDestruction => typeof(PickEnchantCraftDestruction),
        Enchant.CraftRestoration => typeof(PickEnchantCraftRestoration),
        Enchant.CraftAlteration => typeof(PickEnchantCraftAlteration),
        Enchant.CraftConjuration => typeof(PickEnchantCraftConjuration),
        Enchant.CraftIllusion => typeof(PickEnchantCraftIllusion),
        Enchant.AbsorbHealth => typeof(PickEnchantAbsorbHealth),
        Enchant.AbsorbStamina => typeof(PickEnchantAbsorbStamina),
        Enchant.AbsorbMagic => typeof(PickEnchantAbsorbMagic),
        Enchant.Banish => typeof(PickEnchantBanish),
        Enchant.Paralysis => typeof(PickEnchantParalysis),
        _ => typeof(PickEnchantShockBrand),
    };

    public static Type StoneCard(int power) => power switch
    {
        1 => typeof(PickStoneSmall),
        2 => typeof(PickStoneLarge),
        _ => typeof(PickStoneBlack),
    };

    private static readonly Dictionary<Skill, Type> NodeCards = new()
    {
        [Skill.CraftAlchemy] = typeof(NodeCraftAlchemy),
        [Skill.CraftEnchanting] = typeof(NodeCraftEnchanting),
        [Skill.CraftSmithing] = typeof(NodeCraftSmithing),
        [Skill.CraftArtisan] = typeof(NodeCraftArtisan),
        [Skill.WeaponTraining] = typeof(NodeWeaponTraining),
        [Skill.Conditioning] = typeof(NodeConditioning),
        [Skill.SecondBreath] = typeof(NodeSecondBreath),
        [Skill.Veteran] = typeof(NodeVeteran),
        [Skill.CampKettle] = typeof(NodeCampKettle),
        [Skill.DeftPacking] = typeof(NodeDeftPacking),
        [Skill.FirstAid] = typeof(NodeFirstAid),
        [Skill.Wayfarer] = typeof(NodeWayfarer),
        [Skill.Acclimatization] = typeof(NodeAcclimatization),
        [Skill.TradeName] = typeof(NodeTradeName),
        [Skill.WellRested] = typeof(NodeWellRested),
        [Skill.CampMaster] = typeof(NodeCampMaster),
        [Skill.HerbLore] = typeof(NodeHerbLore),
        [Skill.Fighter] = typeof(NodeFighter),
        [Skill.Fencer] = typeof(NodeFencer),
        [Skill.Butcher] = typeof(NodeButcher),
        [Skill.Crusher] = typeof(NodeCrusher),
        [Skill.Sweep] = typeof(NodeSweep),
        [Skill.HeavyHand] = typeof(NodeHeavyHand),
        [Skill.Shatter] = typeof(NodeShatter),
        [Skill.Relentless] = typeof(NodeRelentless),
        [Skill.ShieldBearer] = typeof(NodeShieldBearer),
        [Skill.ShieldWall] = typeof(NodeShieldWall),
        [Skill.ShieldBash] = typeof(NodeShieldBash),
        [Skill.Bastion] = typeof(NodeBastion),
        [Skill.Hardened] = typeof(NodeHardened),
        [Skill.Steadfast] = typeof(NodeSteadfast),
        [Skill.WeightBearer] = typeof(NodeWeightBearer),
        [Skill.AgainstAllOdds] = typeof(NodeAgainstAllOdds),
        [Skill.Bloodletter] = typeof(NodeBloodletter),
        [Skill.Ambush] = typeof(NodeAmbush),
        [Skill.Precision] = typeof(NodePrecision),
        [Skill.Hemorrhage] = typeof(NodeHemorrhage),
        [Skill.Marksman] = typeof(NodeMarksman),
        [Skill.QuickDraw] = typeof(NodeQuickDraw),
        [Skill.AimedShot] = typeof(NodeAimedShot),
        [Skill.ArmorPiercer] = typeof(NodeArmorPiercer),
        [Skill.Nimble] = typeof(NodeNimble),
        [Skill.Counterstrike] = typeof(NodeCounterstrike),
        [Skill.LightStep] = typeof(NodeLightStep),
        [Skill.Elusive] = typeof(NodeElusive),
        [Skill.Poisoner] = typeof(NodePoisoner),
        [Skill.Toxicologist] = typeof(NodeToxicologist),
        [Skill.VenomWard] = typeof(NodeVenomWard),
        [Skill.VenomMaster] = typeof(NodeVenomMaster),
        [Skill.Destruction1] = typeof(NodeDestructionI),
        [Skill.Destruction2] = typeof(NodeDestructionII),
        [Skill.Destruction3] = typeof(NodeDestructionIII),
        [Skill.ElementLord] = typeof(NodeElementLord),
        [Skill.Restoration1] = typeof(NodeRestorationI),
        [Skill.Restoration2] = typeof(NodeRestorationII),
        [Skill.Restoration3] = typeof(NodeRestorationIII),
        [Skill.Warden] = typeof(NodeWarden),
        [Skill.Alteration1] = typeof(NodeAlterationI),
        [Skill.Alteration2] = typeof(NodeAlterationII),
        [Skill.Alteration3] = typeof(NodeAlterationIII),
        [Skill.Stoneskin] = typeof(NodeStoneskin),
        [Skill.Conjuration1] = typeof(NodeConjurationI),
        [Skill.Conjuration2] = typeof(NodeConjurationII),
        [Skill.Conjuration3] = typeof(NodeConjurationIII),
        [Skill.BoundMaster] = typeof(NodeBoundMaster),
        [Skill.Illusion1] = typeof(NodeIllusionI),
        [Skill.Illusion2] = typeof(NodeIllusionII),
        [Skill.Illusion3] = typeof(NodeIllusionIII),
        [Skill.Phantasm] = typeof(NodePhantasm),
    };

    public static Type NodeCard(Skill skill) => NodeCards[skill];
}

// ───── характеристики ─────
[Pool(typeof(TokenCardPool))] public sealed class PickStatHealth : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickStatStamina : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickStatMana : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickStatBurden : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickStatMastery : PickCard { }

// ───── школы (владение за очки характеристик) ─────
[Pool(typeof(TokenCardPool))] public sealed class PickSchoolDestruction : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickSchoolRestoration : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickSchoolAlteration : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickSchoolConjuration : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickSchoolIllusion : PickCard { }

// ───── блюда ─────
[Pool(typeof(TokenCardPool))] public sealed class PickDishBloodmossStew : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickDishVenisonRoast : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickDishFishSoup : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickDishSpicedSausage : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickDishMushroomStew : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickDishSweetRoll : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickDishFeast : PickCard { }

// ───── чары и камни душ ─────
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantHealth : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantStamina : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantMagic : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantFireResistance : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantFrostResistance : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantShockResistance : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantWeaponMight : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantBurden : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantFireBrand : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantFrostBrand : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantShockBrand : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantCraftDestruction : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantCraftRestoration : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantCraftAlteration : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantCraftConjuration : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantCraftIllusion : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantAbsorbHealth : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantAbsorbStamina : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantAbsorbMagic : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantBanish : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickEnchantParalysis : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickStoneSmall : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickStoneLarge : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickStoneBlack : PickCard { }

// ───── Медитация и Молитва ─────
[Pool(typeof(TokenCardPool))] public sealed class PickMeditateStamina : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickMeditateMana : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickPrayRedeem : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickPraySwitch : PickCard { }

// ───── узлы деревьев навыков (Value = ранг/ярус, Cost = цена в очках навыка) ─────
[Pool(typeof(TokenCardPool))] public sealed class NodeCraftAlchemy : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeCraftEnchanting : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeCraftSmithing : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeCraftArtisan : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeWeaponTraining : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeConditioning : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeSecondBreath : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeVeteran : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeCampKettle : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeDeftPacking : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeFirstAid : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeWayfarer : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeAcclimatization : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeTradeName : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeWellRested : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeCampMaster : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeHerbLore : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeFighter : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeFencer : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeButcher : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeCrusher : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeSweep : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeHeavyHand : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeShatter : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeRelentless : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeShieldBearer : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeShieldWall : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeShieldBash : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeBastion : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeHardened : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeSteadfast : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeWeightBearer : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeAgainstAllOdds : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeBloodletter : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeAmbush : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodePrecision : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeHemorrhage : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeMarksman : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeQuickDraw : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeAimedShot : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeArmorPiercer : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeNimble : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeCounterstrike : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeLightStep : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeElusive : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodePoisoner : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeToxicologist : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeVenomWard : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeVenomMaster : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeDestructionI : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeDestructionII : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeDestructionIII : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeElementLord : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeRestorationI : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeRestorationII : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeRestorationIII : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeWarden : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeAlterationI : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeAlterationII : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeAlterationIII : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeStoneskin : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeConjurationI : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeConjurationII : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeConjurationIII : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeBoundMaster : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeIllusionI : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeIllusionII : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodeIllusionIII : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class NodePhantasm : PickCard { }
