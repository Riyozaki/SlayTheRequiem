using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Extensions;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using VanillaPoison = MegaCrit.Sts2.Core.Models.Powers.PoisonPower;
using PlatingPower = SlayTheRequiem.SlayTheRequiemCode.Powers.PlatingPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Potions;

// Зелья и яды Реквиема (docs/design/09 §3–4). Файл генерируется по таблице семейств: у каждого семейства три класса
// ступеней I/III/V (ступень — часть типа, потому что сейв зелья хранит только его id).
// Все — в TokenPotionPool: случайно не выпадают, приходят из Варки, предысторий, карт алхимии и (этап 3.5) Лавки алхимика.

/// <summary>База зелий мода: семейство (<see cref="Brew"/>), ступень и картинка (своя или ванильная).</summary>
public abstract class RequiemPotion : CustomPotionModel
{
    public abstract Brew Kind { get; }
    /// <summary>1, 3 или 5 (I / III / V).</summary>
    public abstract int Tier { get; }

    public override PotionRarity Rarity => Tier >= 5 ? PotionRarity.Rare : Tier >= 3 ? PotionRarity.Uncommon : PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected virtual string VanillaArt => "blood_potion";
    private string ImageName => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png";
    public override string? CustomPackedImagePath => ImageName.PotionImagePath(VanillaArt);
    public override string? CustomPackedOutlinePath => ImageName.PotionImagePath(VanillaArt, outline: true);

    /// <summary>Сила по семейству и ступени (на карточке зелья).</summary>
    protected virtual int BasePotency => AlchemyRules.PotionPotency(Kind, Tier);
    /// <summary>Сила при применении: Кольцо Пьяницы — +50 % (07 §5.2).</summary>
    protected int Potency => IsMutable ? Equipment.RingOfTheDrunkard.Potency(Owner, BasePotency) : BasePotency;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Amount", BasePotency), new DynamicVar("Turns", AlchemyRules.RestorationTurns)];

    protected Creature Hero => Owner.Creature;

    protected void RestoreStamina(int amount)
    {
        if (RequiemResource.Of<StaminaResource>(Owner) is { } stamina)
            stamina.Amount = Math.Min(stamina.Pool, (int)Math.Min((long)stamina.Amount + amount, int.MaxValue));
    }

    public static string TypeName(Brew brew, int tier) =>
        $"{brew}{(AlchemyRules.IsPoison(brew) ? "Poison" : "Potion")}{tier switch { >= 5 => "V", >= 3 => "III", _ => "I" }}";

    private static Dictionary<string, Type>? _types;

    /// <summary>Каноническая модель зелья по семейству и ступени (null, если такого класса нет).</summary>
    public static PotionModel? Canonical(Brew brew, int tier)
    {
        _types ??= typeof(RequiemPotion).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(RequiemPotion))).ToDictionary(t => t.Name);
        return _types.TryGetValue(TypeName(brew, tier), out var type) ? ModelDb.GetByIdOrNull<PotionModel>(ModelDb.GetId(type)) : null;
    }

    /// <summary>Мутабельная копия для выдачи игроку.</summary>
    public static PotionModel? Create(Brew brew, int tier) => Canonical(brew, tier)?.ToMutable();
}

/// <summary>Яд на оружие: наносится из слота зелий, даёт заряды (<see cref="WeaponPoisonPower"/>).</summary>
public abstract class RequiemPoison<TPower> : RequiemPotion where TPower : WeaponPoisonPower
{
    protected override int BasePotency => AlchemyRules.PoisonPotency(Kind, Tier);
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Amount", BasePotency), new DynamicVar("Charges", AlchemyRules.BasePoisonCharges)];

    protected override Task OnUse(PlayerChoiceContext ctx, Creature? target) =>
        WeaponPoisonPower.Coat<TPower>(ctx, Hero, Tier, WeaponPoisonPower.ChargesFor(Owner));
}

// ───── Health ─────

[Pool(typeof(TokenPotionPool))]
public sealed class HealthPotionI : RequiemPotion
{
    public override Brew Kind => Brew.Health;
    public override int Tier => 1;
    protected override string VanillaArt => "blood_potion";
    public override PotionUsage Usage => PotionUsage.AnyTime;

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await CreatureCmd.Heal(Hero, Potency);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class HealthPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.Health;
    public override int Tier => 3;
    protected override string VanillaArt => "blood_potion";
    public override PotionUsage Usage => PotionUsage.AnyTime;

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await CreatureCmd.Heal(Hero, Potency);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class HealthPotionV : RequiemPotion
{
    public override Brew Kind => Brew.Health;
    public override int Tier => 5;
    protected override string VanillaArt => "blood_potion";
    public override PotionUsage Usage => PotionUsage.AnyTime;

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await CreatureCmd.Heal(Hero, Potency);
        await Task.CompletedTask;
    }
}

// ───── Stamina ─────

[Pool(typeof(TokenPotionPool))]
public sealed class StaminaPotionI : RequiemPotion
{
    public override Brew Kind => Brew.Stamina;
    public override int Tier => 1;
    protected override string VanillaArt => "energy_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        RestoreStamina(Tier >= 5 ? int.MaxValue : Potency);
        if (Tier >= 5) await PowerCmd.Apply<StaminaSurgePower>(ctx, Hero, 1, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class StaminaPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.Stamina;
    public override int Tier => 3;
    protected override string VanillaArt => "energy_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        RestoreStamina(Tier >= 5 ? int.MaxValue : Potency);
        if (Tier >= 5) await PowerCmd.Apply<StaminaSurgePower>(ctx, Hero, 1, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class StaminaPotionV : RequiemPotion
{
    public override Brew Kind => Brew.Stamina;
    public override int Tier => 5;
    protected override string VanillaArt => "energy_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        RestoreStamina(Tier >= 5 ? int.MaxValue : Potency);
        if (Tier >= 5) await PowerCmd.Apply<StaminaSurgePower>(ctx, Hero, 1, Hero, null);
        await Task.CompletedTask;
    }
}

// ───── Mana ─────

[Pool(typeof(TokenPotionPool))]
public sealed class ManaPotionI : RequiemPotion
{
    public override Brew Kind => Brew.Mana;
    public override int Tier => 1;
    protected override string VanillaArt => "star_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await Mana.Gain(Owner, Tier >= 5 ? Math.Max(0, Mana.Pool(Owner) - Mana.Amount(Owner)) : Potency);
        if (Tier >= 5) await PowerCmd.Apply<InsightPower>(ctx, Hero, 1, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class ManaPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.Mana;
    public override int Tier => 3;
    protected override string VanillaArt => "star_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await Mana.Gain(Owner, Tier >= 5 ? Math.Max(0, Mana.Pool(Owner) - Mana.Amount(Owner)) : Potency);
        if (Tier >= 5) await PowerCmd.Apply<InsightPower>(ctx, Hero, 1, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class ManaPotionV : RequiemPotion
{
    public override Brew Kind => Brew.Mana;
    public override int Tier => 5;
    protected override string VanillaArt => "star_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await Mana.Gain(Owner, Tier >= 5 ? Math.Max(0, Mana.Pool(Owner) - Mana.Amount(Owner)) : Potency);
        if (Tier >= 5) await PowerCmd.Apply<InsightPower>(ctx, Hero, 1, Hero, null);
        await Task.CompletedTask;
    }
}

// ───── Rejuvenation ─────

[Pool(typeof(TokenPotionPool))]
public sealed class RejuvenationPotionI : RequiemPotion
{
    public override Brew Kind => Brew.Rejuvenation;
    public override int Tier => 1;
    protected override string VanillaArt => "regen_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await TimedRestorePower.Grant<RejuvenationPower>(ctx, Hero, Potency, AlchemyRules.RestorationTurns);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class RejuvenationPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.Rejuvenation;
    public override int Tier => 3;
    protected override string VanillaArt => "regen_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await TimedRestorePower.Grant<RejuvenationPower>(ctx, Hero, Potency, AlchemyRules.RestorationTurns);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class RejuvenationPotionV : RequiemPotion
{
    public override Brew Kind => Brew.Rejuvenation;
    public override int Tier => 5;
    protected override string VanillaArt => "regen_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await TimedRestorePower.Grant<RejuvenationPower>(ctx, Hero, Potency, AlchemyRules.RestorationTurns);
        await Task.CompletedTask;
    }
}

// ───── SecondWind ─────

[Pool(typeof(TokenPotionPool))]
public sealed class SecondWindPotionI : RequiemPotion
{
    public override Brew Kind => Brew.SecondWind;
    public override int Tier => 1;
    protected override string VanillaArt => "speed_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await TimedRestorePower.Grant<SecondWindPower>(ctx, Hero, Potency, AlchemyRules.RestorationTurns);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class SecondWindPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.SecondWind;
    public override int Tier => 3;
    protected override string VanillaArt => "speed_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await TimedRestorePower.Grant<SecondWindPower>(ctx, Hero, Potency, AlchemyRules.RestorationTurns);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class SecondWindPotionV : RequiemPotion
{
    public override Brew Kind => Brew.SecondWind;
    public override int Tier => 5;
    protected override string VanillaArt => "speed_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await TimedRestorePower.Grant<SecondWindPower>(ctx, Hero, Potency, AlchemyRules.RestorationTurns);
        await Task.CompletedTask;
    }
}

// ───── Clarity ─────

[Pool(typeof(TokenPotionPool))]
public sealed class ClarityPotionI : RequiemPotion
{
    public override Brew Kind => Brew.Clarity;
    public override int Tier => 1;
    protected override string VanillaArt => "clarity";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await TimedRestorePower.Grant<ClearMindPower>(ctx, Hero, Potency, AlchemyRules.RestorationTurns);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class ClarityPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.Clarity;
    public override int Tier => 3;
    protected override string VanillaArt => "clarity";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await TimedRestorePower.Grant<ClearMindPower>(ctx, Hero, Potency, AlchemyRules.RestorationTurns);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class ClarityPotionV : RequiemPotion
{
    public override Brew Kind => Brew.Clarity;
    public override int Tier => 5;
    protected override string VanillaArt => "clarity";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await TimedRestorePower.Grant<ClearMindPower>(ctx, Hero, Potency, AlchemyRules.RestorationTurns);
        await Task.CompletedTask;
    }
}

// ───── Giant ─────

[Pool(typeof(TokenPotionPool))]
public sealed class GiantPotionI : RequiemPotion
{
    public override Brew Kind => Brew.Giant;
    public override int Tier => 1;
    protected override string VanillaArt => "gigantification_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<GiantPower>(ctx, Hero, Potency, Hero, null);
        if (Tier >= 5) await PowerCmd.Apply<StrengthPower>(ctx, Hero, 1, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class GiantPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.Giant;
    public override int Tier => 3;
    protected override string VanillaArt => "gigantification_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<GiantPower>(ctx, Hero, Potency, Hero, null);
        if (Tier >= 5) await PowerCmd.Apply<StrengthPower>(ctx, Hero, 1, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class GiantPotionV : RequiemPotion
{
    public override Brew Kind => Brew.Giant;
    public override int Tier => 5;
    protected override string VanillaArt => "gigantification_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<GiantPower>(ctx, Hero, Potency, Hero, null);
        if (Tier >= 5) await PowerCmd.Apply<StrengthPower>(ctx, Hero, 1, Hero, null);
        await Task.CompletedTask;
    }
}

// ───── Knight ─────

[Pool(typeof(TokenPotionPool))]
public sealed class KnightPotionI : RequiemPotion
{
    public override Brew Kind => Brew.Knight;
    public override int Tier => 1;
    protected override string VanillaArt => "heart_of_iron";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<PlatingPower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class KnightPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.Knight;
    public override int Tier => 3;
    protected override string VanillaArt => "heart_of_iron";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<PlatingPower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class KnightPotionV : RequiemPotion
{
    public override Brew Kind => Brew.Knight;
    public override int Tier => 5;
    protected override string VanillaArt => "heart_of_iron";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<PlatingPower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

// ───── Sorcerer ─────

[Pool(typeof(TokenPotionPool))]
public sealed class SorcererPotionI : RequiemPotion
{
    public override Brew Kind => Brew.Sorcerer;
    public override int Tier => 1;
    protected override string VanillaArt => "focus_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<SorcererPower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class SorcererPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.Sorcerer;
    public override int Tier => 3;
    protected override string VanillaArt => "focus_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<SorcererPower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class SorcererPotionV : RequiemPotion
{
    public override Brew Kind => Brew.Sorcerer;
    public override int Tier => 5;
    protected override string VanillaArt => "focus_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<SorcererPower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

// ───── FireWard ─────

[Pool(typeof(TokenPotionPool))]
public sealed class FireWardPotionI : RequiemPotion
{
    public override Brew Kind => Brew.FireWard;
    public override int Tier => 1;
    protected override string VanillaArt => "fire_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<FireResistancePower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class FireWardPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.FireWard;
    public override int Tier => 3;
    protected override string VanillaArt => "fire_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<FireResistancePower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class FireWardPotionV : RequiemPotion
{
    public override Brew Kind => Brew.FireWard;
    public override int Tier => 5;
    protected override string VanillaArt => "fire_potion";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<FireResistancePower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

// ───── FrostWard ─────

[Pool(typeof(TokenPotionPool))]
public sealed class FrostWardPotionI : RequiemPotion
{
    public override Brew Kind => Brew.FrostWard;
    public override int Tier => 1;
    protected override string VanillaArt => "liquid_bronze";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<FrostResistancePower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class FrostWardPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.FrostWard;
    public override int Tier => 3;
    protected override string VanillaArt => "liquid_bronze";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<FrostResistancePower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class FrostWardPotionV : RequiemPotion
{
    public override Brew Kind => Brew.FrostWard;
    public override int Tier => 5;
    protected override string VanillaArt => "liquid_bronze";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<FrostResistancePower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

// ───── ShockWard ─────

[Pool(typeof(TokenPotionPool))]
public sealed class ShockWardPotionI : RequiemPotion
{
    public override Brew Kind => Brew.ShockWard;
    public override int Tier => 1;
    protected override string VanillaArt => "stable_serum";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<ShockResistancePower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class ShockWardPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.ShockWard;
    public override int Tier => 3;
    protected override string VanillaArt => "stable_serum";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<ShockResistancePower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class ShockWardPotionV : RequiemPotion
{
    public override Brew Kind => Brew.ShockWard;
    public override int Tier => 5;
    protected override string VanillaArt => "stable_serum";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<ShockResistancePower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

// ───── Feather ─────

[Pool(typeof(TokenPotionPool))]
public sealed class FeatherPotionI : RequiemPotion
{
    public override Brew Kind => Brew.Feather;
    public override int Tier => 1;
    protected override string VanillaArt => "swift_potion";
    public override PotionUsage Usage => PotionUsage.AnyTime;

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        if (TravelPack.Of(Owner) is { } pack) { pack.FeatherBonus += Potency; pack.Refresh(); }
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class FeatherPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.Feather;
    public override int Tier => 3;
    protected override string VanillaArt => "swift_potion";
    public override PotionUsage Usage => PotionUsage.AnyTime;

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        if (TravelPack.Of(Owner) is { } pack) { pack.FeatherBonus += Potency; pack.Refresh(); }
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class FeatherPotionV : RequiemPotion
{
    public override Brew Kind => Brew.Feather;
    public override int Tier => 5;
    protected override string VanillaArt => "swift_potion";
    public override PotionUsage Usage => PotionUsage.AnyTime;

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        if (TravelPack.Of(Owner) is { } pack) { pack.FeatherBonus += Potency; pack.Refresh(); }
        await Task.CompletedTask;
    }
}

// ───── WitchHunter ─────

[Pool(typeof(TokenPotionPool))]
public sealed class WitchHunterPotionI : RequiemPotion
{
    public override Brew Kind => Brew.WitchHunter;
    public override int Tier => 1;
    protected override string VanillaArt => "potion_of_binding";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<SpellAbsorptionPower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class WitchHunterPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.WitchHunter;
    public override int Tier => 3;
    protected override string VanillaArt => "potion_of_binding";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<SpellAbsorptionPower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class WitchHunterPotionV : RequiemPotion
{
    public override Brew Kind => Brew.WitchHunter;
    public override int Tier => 5;
    protected override string VanillaArt => "potion_of_binding";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<SpellAbsorptionPower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

// ───── Invisibility ─────

[Pool(typeof(TokenPotionPool))]
public sealed class InvisibilityPotionI : RequiemPotion
{
    public override Brew Kind => Brew.Invisibility;
    public override int Tier => 1;
    protected override string VanillaArt => "ghost_in_a_jar";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<InvisibilityPower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class InvisibilityPotionIII : RequiemPotion
{
    public override Brew Kind => Brew.Invisibility;
    public override int Tier => 3;
    protected override string VanillaArt => "ghost_in_a_jar";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<InvisibilityPower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

[Pool(typeof(TokenPotionPool))]
public sealed class InvisibilityPotionV : RequiemPotion
{
    public override Brew Kind => Brew.Invisibility;
    public override int Tier => 5;
    protected override string VanillaArt => "ghost_in_a_jar";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await PowerCmd.Apply<InvisibilityPower>(ctx, Hero, Potency, Hero, null);
        await Task.CompletedTask;
    }
}

// ───── яд: SnakeBite ─────

[Pool(typeof(TokenPotionPool))]
public sealed class SnakeBitePoisonI : RequiemPoison<SnakeBitePoisonPower>
{
    public override Brew Kind => Brew.SnakeBite;
    public override int Tier => 1;
    protected override string VanillaArt => "orobic_acid";
}

[Pool(typeof(TokenPotionPool))]
public sealed class SnakeBitePoisonIII : RequiemPoison<SnakeBitePoisonPower>
{
    public override Brew Kind => Brew.SnakeBite;
    public override int Tier => 3;
    protected override string VanillaArt => "orobic_acid";
}

[Pool(typeof(TokenPotionPool))]
public sealed class SnakeBitePoisonV : RequiemPoison<SnakeBitePoisonPower>
{
    public override Brew Kind => Brew.SnakeBite;
    public override int Tier => 5;
    protected override string VanillaArt => "orobic_acid";
}

// ───── яд: Комбинированный (09 §4: Токсин + Истощение; дорогой, старт «Заговорщика» 04) ─────

[Pool(typeof(TokenPotionPool))]
public sealed class CombinedPoisonI : RequiemPoison<CombinedPoisonPower>
{
    public override Brew Kind => Brew.Toxin;
    public override int Tier => 1;
    protected override string VanillaArt => "poison_potion";
}

// ───── яд: Toxin ─────

[Pool(typeof(TokenPotionPool))]
public sealed class ToxinPoisonI : RequiemPoison<ToxinPoisonPower>
{
    public override Brew Kind => Brew.Toxin;
    public override int Tier => 1;
    protected override string VanillaArt => "poison_potion";
}

[Pool(typeof(TokenPotionPool))]
public sealed class ToxinPoisonIII : RequiemPoison<ToxinPoisonPower>
{
    public override Brew Kind => Brew.Toxin;
    public override int Tier => 3;
    protected override string VanillaArt => "poison_potion";
}

[Pool(typeof(TokenPotionPool))]
public sealed class ToxinPoisonV : RequiemPoison<ToxinPoisonPower>
{
    public override Brew Kind => Brew.Toxin;
    public override int Tier => 5;
    protected override string VanillaArt => "poison_potion";
}

// ───── яд: Rot ─────

[Pool(typeof(TokenPotionPool))]
public sealed class RotPoisonI : RequiemPoison<RotPoisonPower>
{
    public override Brew Kind => Brew.Rot;
    public override int Tier => 1;
    protected override string VanillaArt => "foul_potion";
}

[Pool(typeof(TokenPotionPool))]
public sealed class RotPoisonIII : RequiemPoison<RotPoisonPower>
{
    public override Brew Kind => Brew.Rot;
    public override int Tier => 3;
    protected override string VanillaArt => "foul_potion";
}

[Pool(typeof(TokenPotionPool))]
public sealed class RotPoisonV : RequiemPoison<RotPoisonPower>
{
    public override Brew Kind => Brew.Rot;
    public override int Tier => 5;
    protected override string VanillaArt => "foul_potion";
}

// ───── яд: Exhaustion ─────

[Pool(typeof(TokenPotionPool))]
public sealed class ExhaustionPoisonI : RequiemPoison<ExhaustionPoisonPower>
{
    public override Brew Kind => Brew.Exhaustion;
    public override int Tier => 1;
    protected override string VanillaArt => "beetle_juice";
}

[Pool(typeof(TokenPotionPool))]
public sealed class ExhaustionPoisonIII : RequiemPoison<ExhaustionPoisonPower>
{
    public override Brew Kind => Brew.Exhaustion;
    public override int Tier => 3;
    protected override string VanillaArt => "beetle_juice";
}

[Pool(typeof(TokenPotionPool))]
public sealed class ExhaustionPoisonV : RequiemPoison<ExhaustionPoisonPower>
{
    public override Brew Kind => Brew.Exhaustion;
    public override int Tier => 5;
    protected override string VanillaArt => "beetle_juice";
}

// ───── яд: Emptybloom ─────

[Pool(typeof(TokenPotionPool))]
public sealed class EmptybloomPoisonI : RequiemPoison<EmptybloomPoisonPower>
{
    public override Brew Kind => Brew.Emptybloom;
    public override int Tier => 1;
    protected override string VanillaArt => "essence_of_darkness";
}

[Pool(typeof(TokenPotionPool))]
public sealed class EmptybloomPoisonIII : RequiemPoison<EmptybloomPoisonPower>
{
    public override Brew Kind => Brew.Emptybloom;
    public override int Tier => 3;
    protected override string VanillaArt => "essence_of_darkness";
}

[Pool(typeof(TokenPotionPool))]
public sealed class EmptybloomPoisonV : RequiemPoison<EmptybloomPoisonPower>
{
    public override Brew Kind => Brew.Emptybloom;
    public override int Tier => 5;
    protected override string VanillaArt => "essence_of_darkness";
}

// ───── яд: Weakness ─────

[Pool(typeof(TokenPotionPool))]
public sealed class WeaknessPoisonI : RequiemPoison<WeaknessPoisonPower>
{
    public override Brew Kind => Brew.Weakness;
    public override int Tier => 1;
    protected override string VanillaArt => "weak_potion";
}

[Pool(typeof(TokenPotionPool))]
public sealed class WeaknessPoisonIII : RequiemPoison<WeaknessPoisonPower>
{
    public override Brew Kind => Brew.Weakness;
    public override int Tier => 3;
    protected override string VanillaArt => "weak_potion";
}

[Pool(typeof(TokenPotionPool))]
public sealed class WeaknessPoisonV : RequiemPoison<WeaknessPoisonPower>
{
    public override Brew Kind => Brew.Weakness;
    public override int Tier => 5;
    protected override string VanillaArt => "weak_potion";
}

// ───── яд: Corrosion ─────

[Pool(typeof(TokenPotionPool))]
public sealed class CorrosionPoisonI : RequiemPoison<CorrosionPoisonPower>
{
    public override Brew Kind => Brew.Corrosion;
    public override int Tier => 1;
    protected override string VanillaArt => "ashwater";
}

[Pool(typeof(TokenPotionPool))]
public sealed class CorrosionPoisonIII : RequiemPoison<CorrosionPoisonPower>
{
    public override Brew Kind => Brew.Corrosion;
    public override int Tier => 3;
    protected override string VanillaArt => "ashwater";
}

[Pool(typeof(TokenPotionPool))]
public sealed class CorrosionPoisonV : RequiemPoison<CorrosionPoisonPower>
{
    public override Brew Kind => Brew.Corrosion;
    public override int Tier => 5;
    protected override string VanillaArt => "ashwater";
}

// ───── яд: Paralytic ─────

[Pool(typeof(TokenPotionPool))]
public sealed class ParalyticPoisonI : RequiemPoison<ParalyticPoisonPower>
{
    public override Brew Kind => Brew.Paralytic;
    public override int Tier => 1;
    protected override string VanillaArt => "shackling_potion";
}

[Pool(typeof(TokenPotionPool))]
public sealed class ParalyticPoisonIII : RequiemPoison<ParalyticPoisonPower>
{
    public override Brew Kind => Brew.Paralytic;
    public override int Tier => 3;
    protected override string VanillaArt => "shackling_potion";
}

[Pool(typeof(TokenPotionPool))]
public sealed class ParalyticPoisonV : RequiemPoison<ParalyticPoisonPower>
{
    public override Brew Kind => Brew.Paralytic;
    public override int Tier => 5;
    protected override string VanillaArt => "shackling_potion";
}

// ───── особые ─────

/// <summary>Панацея: снять Хворь, Яд, Кровотечение, Горение и Озноб.</summary>
[Pool(typeof(TokenPotionPool))]
public sealed class Panacea : RequiemPotion
{
    public override Brew Kind => Brew.Panacea;
    public override int Tier => 3;
    protected override string VanillaArt => "cure_all";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        foreach (var power in Hero.Powers.Where(p => p is BlightPower or VanillaPoison or BleedPower or BurningPower or ChillPower).ToList())
            await PowerCmd.Remove(power);
    }
}

/// <summary>Эликсир лунного сахара: +3 ✦, заклинания −1 ✦ на 3 хода, затем «Отходняк» −2 ✦.</summary>
[Pool(typeof(TokenPotionPool))]
public sealed class MoonSugarElixir : RequiemPotion
{
    public const int ManaGain = 3, Turns = 3;
    public override Brew Kind => Brew.Mana;
    public override int Tier => 3;
    protected override string VanillaArt => "bottled_potential";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Amount", ManaGain), new DynamicVar("Turns", Turns)];

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        await Mana.Gain(Owner, ManaGain);
        await PowerCmd.Apply<MoonSugarPower>(ctx, Hero, Turns, Hero, null);
    }
}

/// <summary>Слёзы Матери Очага (редкое): срабатывают сами при смерти — воскрешение с 25 % ♥.</summary>
[Pool(typeof(TokenPotionPool))]
public sealed class HearthMotherTears : RequiemPotion
{
    public const int ReviveDivisor = 4;
    public override Brew Kind => Brew.Health;
    public override int Tier => 5;
    public override PotionUsage Usage => PotionUsage.Automatic;
    public override bool CanBeGeneratedInCombat => false;
    protected override string VanillaArt => "fairy_in_a_bottle";

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        AssertValidForTargetedPotion(target);
        await CreatureCmd.Heal(target, Math.Max(1, target.MaxHp / ReviveDivisor));
    }

    public override bool ShouldDie(Creature creature) => creature != Owner.Creature;

    public override Task AfterPreventingDeath(Creature creature) =>
        OnUseWrapper(new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(), creature);
}

/// <summary>
/// «Похмелье?» (Бутыль Хмельного, 07 §7): бесплатное зелье на один бой со случайным эффектом —
/// лечение, ◆, ✦, Сила или «головная боль» (Слабость 1 себе, добор 2). Само не выпадает.
/// </summary>
[Pool(typeof(TokenPotionPool))]
public sealed class HangoverPotion : RequiemPotion
{
    public override Brew Kind => Brew.Health;
    public override int Tier => 1;
    public override bool CanBeGeneratedInCombat => false;
    protected override string VanillaArt => "gamblers_brew";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Heal", BackpackRules.HangoverHeal), new DynamicVar("Stamina", BackpackRules.HangoverStamina),
        new DynamicVar("Mana", BackpackRules.HangoverMana), new DynamicVar("Strength", BackpackRules.HangoverStrength),
        new DynamicVar("Draw", BackpackRules.HangoverDraw),
    ];

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        var roll = Owner.RunState.Rng.CombatTargets.NextInt(BackpackRules.Outcomes.Length);
        switch (BackpackRules.Roll(roll))
        {
            case Hangover.Heal:
                await CreatureCmd.Heal(Hero, Equipment.RingOfTheDrunkard.Potency(Owner, BackpackRules.HangoverHeal));
                break;
            case Hangover.Stamina:
                RestoreStamina(Equipment.RingOfTheDrunkard.Potency(Owner, BackpackRules.HangoverStamina));
                break;
            case Hangover.Mana:
                await Mana.Gain(Owner, Equipment.RingOfTheDrunkard.Potency(Owner, BackpackRules.HangoverMana));
                break;
            case Hangover.Strength:
                await PowerCmd.Apply<StrengthPower>(ctx, Hero, BackpackRules.HangoverStrength, Hero, null);
                break;
            default:
                await PowerCmd.Apply<WeakPower>(ctx, Hero, BackpackRules.HangoverWeak, Hero, null);
                await CardPileCmd.Draw(ctx, BackpackRules.HangoverDraw, Owner);
                break;
        }
    }
}

