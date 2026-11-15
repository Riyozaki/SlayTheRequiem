using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Potions;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using VanillaPoison = MegaCrit.Sts2.Core.Models.Powers.PoisonPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Cards.Silent;

// Карты Безмолвной в системе Реквиема (docs/design/17-silent-cards.md): Подлые удары, Уклонение, яды и алхимия.
// Безмолвная играет на ванильной модели Silent — карты в пуле Silent.

/// <summary>База карт Безмолвной: «Подлый» — атака по цели без атакующего намерения / из Невидимости.</summary>
public abstract class SilentCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : RequiemCard(cost, type, rarity, target)
{
    public const string SneakVar = "Sneak";
    protected bool IsSneak(Creature? target) => SneakRules.IsSneak(Owner.Creature, target);
}

// ═════════════ Атаки ═════════════

/// <summary>Удар в спину: 6 урона; Подлый — ещё +6.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class Backstab : SilentCard, IWeaponCard
{
    public Backstab() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(6, 3);
        WithVar(SneakVar, 6, 2);
        WithStamina(1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var damage = DynamicVars.Damage.BaseValue + (IsSneak(play.Target) ? Var(SneakVar) : 0);
        return Hit(ctx, play.Target, "vfx/vfx_attack_slash", damage);
    }
}

/// <summary>Отравленный клинок (09 §5): урон оружием; если на оружии яд — его эффект ещё дважды (без траты зарядов).</summary>
[Pool(typeof(SilentCardPool))]
public sealed class PoisonedBlade : SilentCard, IWeaponCard
{
    public PoisonedBlade() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(5, 2);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target);
        if (Owner.Creature.Powers.OfType<WeaponPoisonPower>().FirstOrDefault() is { } coat && play.Target.IsAlive)
            await coat.Afflict(ctx, play.Target, 2);
    }
}

/// <summary>Удавка: урон + Кровотечение; Подлый — ещё Слабость 1.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class Garrote : SilentCard, IWeaponCard
{
    public Garrote() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(4, 2);
        WithPower<BleedPower>(3, 1);
        WithPower<WeakPower>(1);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var sneak = IsSneak(play.Target);
        await Hit(ctx, play.Target);
        if (!play.Target.IsAlive) return;
        await Apply<BleedPower>(ctx, play.Target, Var(nameof(BleedPower)));
        if (sneak) await Apply<WeakPower>(ctx, play.Target, Var(nameof(WeakPower)));
    }
}

/// <summary>Гнилостный надрез: урон + Хворь (вместе с Ядом — каждый тик больнее).</summary>
[Pool(typeof(SilentCardPool))]
public sealed class RottingCut : SilentCard
{
    public RottingCut() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(5, 2);
        WithPower<BlightPower>(1, 1);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target);
        if (play.Target.IsAlive) await Apply<BlightPower>(ctx, play.Target, Var(nameof(BlightPower)));
    }
}

/// <summary>Обчистить (Плут): урон и золото; Подлый — ещё случайное зелье I. Изгнание.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class PickPocket : SilentCard
{
    public const string GoldVar = "Gold";
    private static readonly Brew[] Loot = [Brew.Health, Brew.Stamina, Brew.Mana, Brew.Toxin];

    public PickPocket() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(4, 2);
        WithVar(GoldVar, 12, 6);
        WithStamina(1);
        WithKeyword(CardKeyword.Exhaust);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var sneak = IsSneak(play.Target);
        await Hit(ctx, play.Target);
        await PlayerCmd.GainGold((int)Var(GoldVar), Owner);
        if (sneak && RequiemPotion.Create(Owner.PlayerRng.Rewards.NextItem(Loot), 1) is { } potion)
            await PotionCmd.TryToProcure(potion, Owner);
    }
}

/// <summary>Засада: урон всем; каждая цель без атакующего намерения — ещё Кровотечение 2.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class Ambush : SilentCard
{
    public Ambush() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithDamage(7, 3);
        WithPower<BleedPower>(2, 1);
        WithStamina(2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var sneakTargets = CombatState.HittableEnemies.Where(IsSneak).ToList();
        await HitAll(ctx);
        foreach (var enemy in sneakTargets.Where(e => e.IsAlive))
            await Apply<BleedPower>(ctx, enemy, Var(nameof(BleedPower)));
    }
}

/// <summary>Удар из тени: сильный удар; из Невидимости — бьёт дважды.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class ShadowStrike : SilentCard, IWeaponCard
{
    public ShadowStrike() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(12, 4);
        WithStamina(2);
        WithTip(typeof(InvisibilityPower));
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var invisible = Owner.Creature.HasPower<InvisibilityPower>();
        await Hit(ctx, play.Target);
        if (invisible && play.Target.IsAlive) await Hit(ctx, play.Target);
    }
}

// ═════════════ Навыки ═════════════

/// <summary>Шаг в сторону: Блок и +1 заряд Уворота до следующего хода.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class Sidestep : SilentCard
{
    public Sidestep() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(5, 3);
        WithPower<DodgePower>(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Block(play);
        await TemporaryDodgePower.Grant(ctx, Owner.Creature, (int)Var(nameof(DodgePower)));
    }
}

/// <summary>Шаг тени: Невидимость 1.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class ShadowStep : SilentCard
{
    public ShadowStep() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<InvisibilityPower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<InvisibilityPower>(ctx, Owner.Creature, Var(nameof(InvisibilityPower)));
}

/// <summary>
/// Флакон в лицо (09 §5): эффект случайного яда из слотов ×2 по цели, без траты флакона. Нет ядов в слотах —
/// яд с оружия; нет и его — Токсин I.
/// </summary>
[Pool(typeof(SilentCardPool))]
public sealed class FlaskToFace : SilentCard
{
    public FlaskToFace() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithVar("Multiplier", 2);
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var poisons = Owner.PotionSlots.OfType<RequiemPotion>().Where(p => AlchemyRules.IsPoison(p.Kind)).ToList();
        var mult = (int)Var("Multiplier");
        if (poisons.Count > 0)
        {
            var pick = Owner.RunState.Rng.CombatTargets.NextItem(poisons)!;
            if (WeaponPoisonPower.IsImmune(play.Target) && pick.Kind is not (Brew.SnakeBite or Brew.Corrosion or Brew.Emptybloom)) return;
            await ApplyPoison(ctx, play.Target, pick.Kind, AlchemyRules.PoisonPotency(pick.Kind, pick.Tier) * mult);
        }
        else if (Owner.Creature.Powers.OfType<WeaponPoisonPower>().FirstOrDefault() is { } coat)
            await coat.Afflict(ctx, play.Target, mult);
        else if (!WeaponPoisonPower.IsImmune(play.Target))
            await Apply<VanillaPoison>(ctx, play.Target, AlchemyRules.PoisonPotency(Brew.Toxin, 1) * mult);
    }

    private async Task ApplyPoison(PlayerChoiceContext ctx, Creature target, Brew kind, int potency)
    {
        switch (kind)
        {
            case Brew.SnakeBite: await CreatureCmd.Damage(ctx, target, potency, ValueProp.Unpowered, Owner.Creature, this); break;
            case Brew.Toxin: await Apply<VanillaPoison>(ctx, target, potency); break;
            case Brew.Rot: await Apply<BlightPower>(ctx, target, potency); break;
            case Brew.Exhaustion: if (target.GetPower<EnemyStaminaPower>() is { } st) await st.Drain(potency); break;
            case Brew.Emptybloom: if (target.GetPower<EnemyManaPower>() is { } mp) await mp.Drain(potency); break;
            case Brew.Weakness: await Apply<WeakPower>(ctx, target, potency); break;
            case Brew.Corrosion: await Apply<SunderPower>(ctx, target, potency); break;
            case Brew.Paralytic: await EnemyResponses.TryParalyze(ctx, target, SpellTier.Master, Owner.Creature, this); break;
        }
    }
}

/// <summary>Перегонка (09 §5): создать зелье случайного вида ступени I. Изгнание.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class Distillation : SilentCard
{
    public Distillation() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithKeyword(CardKeyword.Exhaust);
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var kinds = AlchemyRules.Recipes.Select(r => r.Brew).Where(b => b is not Brew.Feather).ToList();
        if (RequiemPotion.Create(Owner.RunState.Rng.CombatTargets.NextItem(kinds), 1) is { } potion)
            await PotionCmd.TryToProcure(potion, Owner);
    }
}

/// <summary>Ядовитая смазка: нанести на оружие Токсин I (2 заряда). Изгнание.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class ToxicCoating : SilentCard
{
    public const string ChargesVar = "Charges";

    public ToxicCoating() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithVar(ChargesVar, 2, 1);
        WithKeyword(CardKeyword.Exhaust);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        WeaponPoisonPower.Coat<ToxinPoisonPower>(ctx, Owner.Creature, 1, (int)Var(ChargesVar));
}

/// <summary>Летучий состав (09 §5): Яд и Хворь на всех врагах детонируют — урон = сумма стаков, стаки снимаются.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class VolatileMixture : SilentCard
{
    public VolatileMixture() : base(2, CardType.Skill, CardRarity.Rare, TargetType.AllEnemies)
    {
        WithCostUpgradeBy(-1);
        WithTip(typeof(BlightPower));
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        foreach (var enemy in CombatState.HittableEnemies.ToList())
        {
            var poison = enemy.GetPower<VanillaPoison>();
            var blight = enemy.GetPower<BlightPower>();
            var total = (poison?.Amount ?? 0) + (blight?.Amount ?? 0);
            if (total <= 0) continue;
            if (poison != null) await PowerCmd.Remove(poison);
            if (blight != null) await PowerCmd.Remove(blight);
            using (DamageTyping.As(DamageType.Poison))
                await CreatureCmd.Damage(ctx, enemy, total, ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this);
        }
    }
}

// ═════════════ Силы ═════════════

/// <summary>Контрудар: после Уворота атакующий получает урон, а следующая атака — Подлая.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class Riposte : SilentCard
{
    public Riposte() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<RipostePower>(4, 2);
        WithTip(typeof(DodgePower));
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<RipostePower>(ctx, Owner.Creature, Var(nameof(RipostePower)));
}

/// <summary>Уклонение: +1 заряд Уворота каждый ход до конца боя.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class Evasion : SilentCard
{
    public Evasion() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<DodgePower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<DodgePower>(ctx, Owner.Creature, Var(nameof(DodgePower)));
}

/// <summary>Мастер ядов: в начале хода яд на оружии получает +1 заряд.</summary>
[Pool(typeof(SilentCardPool))]
public sealed class VenomMastery : SilentCard
{
    public VenomMastery() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<VenomMasteryPower>(1, 1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<VenomMasteryPower>(ctx, Owner.Creature, Var(nameof(VenomMasteryPower)));
}
