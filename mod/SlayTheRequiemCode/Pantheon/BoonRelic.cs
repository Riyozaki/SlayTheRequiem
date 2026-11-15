using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Pantheon;

/// <summary>
/// Реликвия, чей эффект описан набором чисел <see cref="Boon"/> (docs/design/05): Знамения, Осколки отлива,
/// благословения Небесных, дары Князей Бездны. Применение единое:
/// ♥ — синхронизация «выданного» запаса (<see cref="GrantedMaxHp"/>), чтобы бонус, зависящий от праведности или
/// Обета, можно было и выдать, и забрать; пулы и Стойкость — через <see cref="IResourceSource"/>; Латы, Покров,
/// сопротивления и ауры — в первый ход боя; урон, рука, золото, цены, отдых — хуками.
/// Не продаются и не выпадают: выдаются событиями Пантеона.
/// </summary>
public abstract class BoonRelic : RequiemRelic, IWeightless, IResourceSource, IPierceSource, ISummonModifier, IMasterySource
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => false;

    /// <summary>Текущий набор (может зависеть от праведности, Обета, надетых вещей).</summary>
    protected abstract Boon ComputeBoon();

    /// <summary>Набор с учётом системы Реквиема (вне системы — ничего).</summary>
    public Boon ActiveBoon => Owned && RequiemMode.IsInSystem(Owner) ? ComputeBoon() : Boon.None;

    /// <summary>Реликвия у игрока (у канонической модели Owner бросает исключение).</summary>
    protected bool Owned => IsMutable && Owner != null;

    // ───── пулы (IResourceSource) ─────

    public int StaminaPoolBonus => ActiveBoon.StaminaPool;
    public int StaminaRegenBonus => ActiveBoon.StaminaRegen;
    public int ManaPoolBonus => ActiveBoon.ManaPool;
    public int ManaRegenBonus => ActiveBoon.ManaRegen;
    public int ManaStartBonus => ActiveBoon.ManaStart;
    public int PoiseBonus => ActiveBoon.Poise;
    public int BurdenBonus => ActiveBoon.Burden;

    public int Pierce => ActiveBoon.Pierce;
    public int SummonSlotBonus => ActiveBoon.SummonSlots;
    public virtual int SummonBonusDamage => ActiveBoon.SummonDamage;
    public int SummonDamageTakenPercent => ActiveBoon.SummonTakenPercent;
    public virtual int MasteryBonus(SpellSchool school) => ActiveBoon.MasteryAll;

    // ───── ♥ ─────

    private int _grantedMaxHp;

    /// <summary>Сколько макс. ♥ этот дар сейчас добавил (может быть отрицательным — Цена).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int GrantedMaxHp
    {
        get => _grantedMaxHp;
        set
        {
            AssertMutable();
            _grantedMaxHp = value;
        }
    }

    /// <summary>Довести выданный запас ♥ до текущего набора.</summary>
    public async Task SyncMaxHp()
    {
        if (Owner?.Creature == null) return;
        var target = ActiveBoon.MaxHp;
        var delta = target - _grantedMaxHp;
        if (delta == 0) return;
        if (delta > 0) await CreatureCmd.GainMaxHp(Owner.Creature, delta);
        else await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner.Creature, Math.Min(-delta, Owner.Creature.MaxHp - 1), false);
        GrantedMaxHp = target;
    }

    /// <summary>Забрать выданное перед снятием реликвии (смена благословения, конец акта у Осколка).</summary>
    public async Task Revoke()
    {
        if (Owner?.Creature == null || _grantedMaxHp == 0) return;
        if (_grantedMaxHp > 0)
            await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner.Creature, Math.Min(_grantedMaxHp, Owner.Creature.MaxHp - 1), false);
        else await CreatureCmd.GainMaxHp(Owner.Creature, -_grantedMaxHp);
        GrantedMaxHp = 0;
    }

    /// <summary>Снять реликвию, вернув ♥.</summary>
    public async Task RemoveSelf()
    {
        await Revoke();
        await RelicCmd.Remove(this);
    }

    public override async Task AfterObtained()
    {
        await SyncMaxHp();
        await OnObtained();
    }

    protected virtual Task OnObtained() => Task.CompletedTask;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        await SyncMaxHp();
    }

    // ───── начало боя ─────

    private bool _aurasApplied;

    public override Task BeforeCombatStart()
    {
        _aurasApplied = false;
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || combatState.RoundNumber != 1 || Owner?.Creature == null) return;
        if (!RequiemMode.IsInSystem(Owner)) return;
        var boon = ActiveBoon;
        var hero = Owner.Creature;
        var ctx = new ThrowingPlayerChoiceContext();
        if (boon.Plating != 0) await PowerCmd.Apply<PlatingPower>(ctx, hero, boon.Plating, hero, null, silent: true);
        if (boon.Ward > 0) await PowerCmd.Apply<WardPower>(ctx, hero, boon.Ward, hero, null, silent: true);
        if (boon.SpellAbsorption > 0) await PowerCmd.Apply<SpellAbsorptionPower>(ctx, hero, boon.SpellAbsorption, hero, null, silent: true);
        if (boon.FireRes != 0) await PowerCmd.Apply<FireResistancePower>(ctx, hero, boon.FireRes, hero, null, silent: true);
        if (boon.FrostRes != 0) await PowerCmd.Apply<FrostResistancePower>(ctx, hero, boon.FrostRes, hero, null, silent: true);
        if (boon.ShockRes != 0) await PowerCmd.Apply<ShockResistancePower>(ctx, hero, boon.ShockRes, hero, null, silent: true);
        if (boon.PoisonRes != 0) await PowerCmd.Apply<PoisonResistancePower>(ctx, hero, boon.PoisonRes, hero, null, silent: true);
        if (boon.MagicRes != 0) await PowerCmd.Apply<MagicResistancePower>(ctx, hero, boon.MagicRes, hero, null, silent: true);
        if (boon.BlightRes != 0) await PowerCmd.Apply<BlightResistancePower>(ctx, hero, boon.BlightRes, hero, null, silent: true);

        foreach (var enemy in combatState.Enemies.Where(e => e.IsAlive).ToList())
            await ApplyAuras(ctx, enemy, boon);
        _aurasApplied = true;
        await OnCombatStarted(combatState);
    }

    /// <summary>Враг, появившийся посреди боя, тоже попадает под ауры.</summary>
    public override async Task AfterCreatureAddedToCombat(Creature creature)
    {
        if (!_aurasApplied || creature.Monster == null || Owner?.Creature == null) return;
        await ApplyAuras(new ThrowingPlayerChoiceContext(), creature, ActiveBoon);
    }

    private async Task ApplyAuras(PlayerChoiceContext ctx, Creature enemy, Boon boon)
    {
        var hero = Owner.Creature;
        if (boon.EnemyFireRes != 0) await PowerCmd.Apply<FireResistancePower>(ctx, enemy, boon.EnemyFireRes, hero, null, silent: true);
        if (boon.EnemyFrostRes != 0) await PowerCmd.Apply<FrostResistancePower>(ctx, enemy, boon.EnemyFrostRes, hero, null, silent: true);
        if (boon.EnemyShockRes != 0) await PowerCmd.Apply<ShockResistancePower>(ctx, enemy, boon.EnemyShockRes, hero, null, silent: true);
        if (boon.EnemyPoisonRes != 0) await PowerCmd.Apply<PoisonResistancePower>(ctx, enemy, boon.EnemyPoisonRes, hero, null, silent: true);
        if (boon.EnemyMagicRes != 0) await PowerCmd.Apply<MagicResistancePower>(ctx, enemy, boon.EnemyMagicRes, hero, null, silent: true);
    }

    /// <summary>Особые эффекты начала боя у наследников (после применения набора).</summary>
    protected virtual Task OnCombatStarted(ICombatState combatState) => Task.CompletedTask;

    // ───── каждый ход ─────

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner) return;
        var regen = ActiveBoon.Regen;
        if (regen > 0 && Owner.Creature.CurrentHp < Owner.Creature.MaxHp)
        {
            Flash();
            using (Transformations.Healing.As(Transformations.HealSource.Regeneration))
                await CreatureCmd.Heal(Owner.Creature, regen);
        }
        await OnTurnStarted(choiceContext);
    }

    protected virtual Task OnTurnStarted(PlayerChoiceContext ctx) => Task.CompletedTask;

    // ───── урон ─────

    /// <summary>Своя атака картой (не заклинание) по существу.</summary>
    protected bool IsOwnAttack(Creature? dealer, ValueProp props, CardModel? card) =>
        Owner != null && dealer == Owner.Creature && card != null && card.Owner == Owner
        && card.Type == CardType.Attack && card is not SpellCard && props.IsPoweredAttack();

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Owner?.Creature == null) return 0m;
        var boon = ActiveBoon;
        decimal bonus = 0;
        if (IsOwnAttack(dealer, props, cardSource))
        {
            bonus += boon.AttackDamage;
            if (boon.HeavyAttackDamage != 0 && IsHeavy(cardSource!)) bonus += boon.HeavyAttackDamage;
            if (boon.UndeadDamage != 0 && target != null && EnemySetup.IsUndeadOrAbyss(target)) bonus += boon.UndeadDamage;
        }
        // Кличи — и атаки, и заклинания-кличи.
        if (boon.ShoutDamage != 0 && cardSource is IShoutCard && cardSource.Owner == Owner && dealer == Owner.Creature
            && props.IsPoweredAttack())
            bonus += boon.ShoutDamage;
        // Штраф: физический урон атак по вам.
        if (boon.PhysicalTaken != 0 && target == Owner.Creature && dealer != null && dealer != Owner.Creature
            && props.IsPoweredAttack() && DamageTyping.Resolve(props, dealer, cardSource) == DamageType.Physical)
            bonus += boon.PhysicalTaken;
        return bonus + ExtraDamage(target, props, dealer, cardSource);
    }

    /// <summary>Особые надбавки урона у наследников.</summary>
    protected virtual decimal ExtraDamage(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource) => 0m;

    /// <summary>«Тяжёлая» атака — стоит 2 ◆ и больше.</summary>
    public static bool IsHeavy(CardModel card) => StaminaCosts.BaseCostFor(card) >= 2;

    /// <summary>Сияние: атака по нежити/Бездне добавляет урон светом.</summary>
    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (Owner?.Creature == null || command.Attacker != Owner.Creature || command.ModelSource is not CardModel card
            || card.Owner != Owner) return;
        var light = ActiveBoon.UndeadLight;
        if (light > 0)
        {
            foreach (var target in command.Results.SelectMany(h => h).Select(r => r.Receiver).Distinct().ToList())
            {
                if (target == null || !target.IsAlive || !EnemySetup.IsUndeadOrAbyss(target)) continue;
                using (DamageTyping.As(DamageType.Light))
                    await CreatureCmd.Damage(choiceContext, target, light, ValueProp.Unpowered, Owner.Creature, null);
            }
        }
        await OnOwnAttack(choiceContext, command, card);
    }

    protected virtual Task OnOwnAttack(PlayerChoiceContext ctx, AttackCommand command, CardModel card) => Task.CompletedTask;

    // ───── рука, золото, лавки, отдых ─────

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner || player.Creature.CombatState?.RoundNumber != 1) return count;
        var delta = ActiveBoon.OpeningHand;
        return delta == 0 ? count : Math.Max(1, count + delta);
    }

    public override decimal ModifyGoldGained(Player player, decimal amount)
    {
        if (player != Owner || amount <= 0) return amount;
        var percent = ActiveBoon.GoldPercent;
        return percent == 0 ? amount : Math.Floor(amount * (100 + percent) / 100m);
    }

    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal cost)
    {
        if (player != Owner) return cost;
        var percent = ActiveBoon.MerchantPricePercent;
        return percent == 0 ? cost : Math.Ceiling(cost * (100 + percent) / 100m);
    }

    public override decimal ModifyRestSiteHealAmount(Creature creature, decimal amount)
    {
        if (Owner?.Creature != creature) return amount;
        var percent = Math.Max(-100, ActiveBoon.RestHealPercent);
        return percent == 0 ? amount : Math.Floor(amount * (100 + percent) / 100m);
    }

    // ───── общее ─────

    /// <summary>Перерисовать описание (числа в DynamicVars).</summary>
    protected void Refresh() => InvokeDisplayAmountChanged();

    /// <summary>Сумма наборов всех реликвий Пантеона у игрока (для подсказок и статов вне хуков).</summary>
    public static Boon TotalOf(Player? player) =>
        player == null ? Boon.None : player.Relics.OfType<BoonRelic>().Aggregate(Boon.None, (acc, r) => acc + r.ActiveBoon);

    /// <summary>Иммунитеты к контролю героя (для статусов врагов этапа 7).</summary>
    public static bool ImmuneToFrozen(Player? player) => TotalOf(player).ImmuneFrozen;
    public static bool ImmuneToParalysis(Player? player) => TotalOf(player).ImmuneParalysis;
    public static bool ImmuneToShock(Player? player) => TotalOf(player).ImmuneShock;

    /// <summary>Насколько короче перезарядка Кличей (полка Кличей — Cards/Shouts.cs).</summary>
    public static int ShoutCooldownReduction(Player? player) => Math.Max(0, TotalOf(player).ShoutCooldown);
}
