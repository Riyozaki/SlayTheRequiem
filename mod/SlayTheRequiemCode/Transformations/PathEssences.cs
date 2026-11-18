using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using VanillaPoison = MegaCrit.Sts2.Core.Models.Powers.PoisonPower;
using VanillaStrength = MegaCrit.Sts2.Core.Models.Powers.StrengthPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Transformations;

/// <summary>
/// Сущность Пути Героя (docs/design/06 §2): реликвия-статус без веса. Путь = Сущность + 3 карты в пул наград
/// (<see cref="PathRewardRules"/>) + Знак Пути (<see cref="PathSign"/>). Путь один на забег.
/// </summary>
public abstract class PathEssence : BoonRelic
{
    public abstract HeroPath Path { get; }

    private string _signGrant = "";
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string SignGrant { get => _signGrant; set { AssertMutable(); _signGrant = value ?? ""; } }

    public override async Task AfterRoomEntered(MegaCrit.Sts2.Core.Rooms.AbstractRoom room)
    {
        await base.AfterRoomEntered(room);
        if (SignGrant.Length > 0 && !CombatManager.Instance.IsInProgress) await PathGrants.CompleteSign(this);
    }

    protected override Boon ComputeBoon() => Boon.None;

    protected Creature Hero => Owner.Creature;

    protected bool IsEnemy(Creature? c) => c != null && c != Owner.Creature && !c.IsPlayer && c.PetOwner == null;
}

/// <summary>Пути: выдача, типы, чтение.</summary>
public static class Paths
{
    public static PathEssence? Of(Player? player) => player?.Relics.OfType<PathEssence>().FirstOrDefault();

    public static HeroPath PathOf(Player? player) => Of(player)?.Path ?? HeroPath.None;

    public static Type EssenceType(HeroPath path) => path switch
    {
        HeroPath.Paladin => typeof(PaladinEssence),
        HeroPath.BlackKnight => typeof(BlackKnightEssence),
        HeroPath.Champion => typeof(ChampionEssence),
        HeroPath.Barbarian => typeof(BarbarianEssence),
        HeroPath.Monk => typeof(MonkEssence),
        HeroPath.Assassin => typeof(AssassinEssence),
        HeroPath.Blightbringer => typeof(BlightbringerEssence),
        HeroPath.MysticArcher => typeof(MysticArcherEssence),
        HeroPath.ElementLord => typeof(ElementLordEssence),
        HeroPath.Battlemage => typeof(BattlemageEssence),
        HeroPath.Reflector => typeof(ReflectorEssence),
        HeroPath.Medium => typeof(MediumEssence),
        _ => typeof(RebornEssence),
    };

    public static Type? SignType(HeroPath path) => path switch
    {
        HeroPath.Paladin => typeof(RadiantCuirass),
        HeroPath.BlackKnight => typeof(SpikedHelm),
        HeroPath.Champion => typeof(RegimentBanner),
        HeroPath.Barbarian => typeof(HornedHelm),
        HeroPath.Monk => typeof(MonkWraps),
        HeroPath.Assassin => typeof(FacelessMask),
        HeroPath.Blightbringer => typeof(VialPendant),
        HeroPath.MysticArcher => typeof(RunicQuiver),
        HeroPath.ElementLord => typeof(ElementStaff),
        HeroPath.Battlemage => typeof(RunicBracers),
        HeroPath.Reflector => typeof(MirrorShield),
        HeroPath.Medium => typeof(SpiritBell),
        _ => null,
    };

    public static Type PickCardFor(HeroPath path) => path switch
    {
        HeroPath.Paladin => typeof(PickPathPaladin),
        HeroPath.BlackKnight => typeof(PickPathBlackKnight),
        HeroPath.Champion => typeof(PickPathChampion),
        HeroPath.Barbarian => typeof(PickPathBarbarian),
        HeroPath.Monk => typeof(PickPathMonk),
        HeroPath.Assassin => typeof(PickPathAssassin),
        HeroPath.Blightbringer => typeof(PickPathBlightbringer),
        HeroPath.MysticArcher => typeof(PickPathMysticArcher),
        HeroPath.ElementLord => typeof(PickPathElementLord),
        HeroPath.Battlemage => typeof(PickPathBattlemage),
        HeroPath.Reflector => typeof(PickPathReflector),
        HeroPath.Medium => typeof(PickPathMedium),
        _ => typeof(PickPathReborn),
    };

    public static bool Unarmed(Player player) => HeroRecord.Of(player)?.MainHand is null or WeaponKind.None;

    public static bool Apostate(Player player) => Faith.Effective(player) == Piety.Apostate;

    /// <summary>Варианты Пути для героя (2 своих + 1 чужой).</summary>
    public static List<HeroPath> Offer(Player player) =>
        TransformationRules.OfferPaths(player.Character.GetType().Name, Forms.Worldview(player), Apostate(player), Unarmed(player),
            n => n <= 0 ? 0 : player.PlayerRng.Rewards.NextInt(n), Forms.IsOrdinator(player));

    /// <summary>Принять Путь: Сущность и Знак.</summary>
    public static async Task Grant(Player player, HeroPath path)
    {
        if (!Enum.IsDefined(path)) throw new ArgumentOutOfRangeException(nameof(path));
        using var lease = await InventoryGate.For(player).Enter();
        if (lease == null || path == HeroPath.None) return;
        if (Of(player) is { } current)
        {
            if (current.Path == path) await PathGrants.CompleteSign(current, lease); // same issuer, never replay its one-shot effects
            return;
        }
        using var mutation = InventoryGate.Track(player, lease, "path:" + path);
        try
        {
            var essence = (PathEssence)Forms.Create(EssenceType(path));
            PathGrants.Prepare(essence, player); // exact companion/source exists in an insertion save, before AfterObtained
            await RelicCmd.Obtain(essence, player);
            if (!player.Relics.Contains(essence)) throw new InvalidOperationException("Path essence did not commit");
            essence.SignGrant = RelicGrantState.Read(essence.SignGrant)!.IssuerCompleted().Encode();
            await PathGrants.CompleteSign(essence, lease);
            mutation.Complete();
        }
        catch (Exception e)
        {
            InventoryGate.Block(player, $"Path grant interrupted; restore its pre-transition checkpoint: {e}");
            mutation.Complete();
            throw;
        }
    }
}

// ═════════════ 13 Сущностей ═════════════

/// <summary>Паладин: Латы +1; нежить бьёт вас на 1 слабее за удар; «Кара» стоит 0 ✦.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class PaladinEssence : PathEssence
{
    public override HeroPath Path => HeroPath.Paladin;
    protected override Boon ComputeBoon() => new() { Plating = TransformationRules.PaladinPlating };

    protected override decimal ExtraDamage(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner.Creature && dealer != null && dealer != target && props.IsPoweredAttack() && EnemySetup.IsUndead(dealer)
            ? -TransformationRules.PaladinUndeadReduction
            : 0m;

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!Owned || card is not Smite || card.Owner != Owner || originalCost <= 0) return false;
        modifiedCost = 0;
        return true;
    }
}

/// <summary>Чёрный рыцарь: Аура страха — враги −1 Сила в начале каждого 2-го хода; атаки лечат 1 за удар.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class BlackKnightEssence : PathEssence
{
    public override HeroPath Path => HeroPath.BlackKnight;

    protected override async Task OnTurnStarted(PlayerChoiceContext ctx)
    {
        var combat = Hero.CombatState;
        if (combat == null || !TransformationRules.FearTurn(combat.RoundNumber)) return;
        Flash();
        foreach (var enemy in combat.HittableEnemies.Where(e => e.IsAlive).ToList())
            await PowerCmd.Apply<VanillaStrength>(ctx, enemy, -TransformationRules.BlackKnightFearStrength, Hero, null);
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (!Owned || !IsOwnAttack(dealer, props, cardSource) || !IsEnemy(target) || result.UnblockedDamage <= 0 || !Hero.IsAlive) return;
        using (Healing.As(HealSource.Feeding))
            await CreatureCmd.Heal(Hero, TransformationRules.BlackKnightAttackHeal);
    }
}

/// <summary>Чемпион: тяжёлые атаки (≥ 2 ◆) накладывают Ошеломление 2 (по Непоколебимым — половину).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ChampionEssence : PathEssence
{
    public override HeroPath Path => HeroPath.Champion;

    protected override async Task OnOwnAttack(PlayerChoiceContext ctx, AttackCommand command, CardModel card)
    {
        if (!IsHeavy(card) || card is SpellCard) return;
        foreach (var target in command.Results.SelectMany(h => h).Select(r => r.Receiver).Distinct().ToList())
        {
            if (target == null || !target.IsAlive || !IsEnemy(target)) continue;
            var stagger = target.HasPower<UnshakeablePower>()
                ? TransformationRules.ChampionUnshakeableStagger(TransformationRules.ChampionHeavyStagger)
                : TransformationRules.ChampionHeavyStagger;
            await PowerCmd.Apply<StaggerPower>(ctx, target, stagger, Hero, null);
        }
    }
}

/// <summary>Варвар: Ярость — при ♥ &lt; 50 % атаки +50 % урона и не тратят ◆.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class BarbarianEssence : PathEssence, IAttackStaminaModifier
{
    public override HeroPath Path => HeroPath.Barbarian;

    public bool Raging => Owned && Hero.CombatState != null && TransformationRules.BarbarianRage(Hero.CurrentHp, Hero.MaxHp);

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        Raging && IsOwnAttack(dealer, props, cardSource) ? TransformationRules.BarbarianRageMultiplier : 1m;

    public int ModifyAttackStamina(CardModel card, int cost) => Raging && card.Owner == Owner ? 0 : cost;
}

/// <summary>Монах (без оружия): безоружные атаки бьют как 4 + уровень/2; каждая 3-я — Ошеломление 3.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class MonkEssence : PathEssence
{
    public override HeroPath Path => HeroPath.Monk;

    private int _unarmedAttacks;

    private bool IsUnarmedAttack(CardModel? card) =>
        card != null && card.Owner == Owner && Paths.Unarmed(Owner) && EquipmentCombatRules.IsWeaponAttack(card);

    public override Task BeforeCombatStart()
    {
        _unarmedAttacks = 0;
        return base.BeforeCombatStart();
    }

    /// <summary>Надбавка к безоружному удару (база 4): уровень/2.</summary>
    protected override decimal ExtraDamage(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        IsOwnAttack(dealer, props, cardSource) && IsUnarmedAttack(cardSource)
            ? TransformationRules.MonkUnarmedDamage(HeroRecord.Of(Owner)?.Level ?? 1) - 4
            : 0m;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!Owned || !cardPlay.IsLastInSeries || cardPlay.Card.Type != CardType.Attack || !IsUnarmedAttack(cardPlay.Card)) return;
        if (++_unarmedAttacks % TransformationRules.MonkEveryNthStagger != 0) return;
        if (cardPlay.Target is { IsAlive: true } target && IsEnemy(target))
        {
            Flash();
            await PowerCmd.Apply<StaggerPower>(choiceContext, target, TransformationRules.MonkStagger, Hero, null);
        }
    }
}

/// <summary>Ассасин: Шаг тени — первая атака каждого хода «в спину», ×2 урона.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class AssassinEssence : PathEssence
{
    public override HeroPath Path => HeroPath.Assassin;

    private bool _stepUsed;

    protected override Task OnTurnStarted(PlayerChoiceContext ctx)
    {
        _stepUsed = false;
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        Owned && !_stepUsed && IsOwnAttack(dealer, props, cardSource) && IsEnemy(target)
            ? TransformationRules.AssassinShadowStepMultiplier
            : 1m;

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owned && cardPlay.Card.Owner == Owner && cardPlay.Card.Type == CardType.Attack && cardPlay.IsLastInSeries && !_stepUsed)
        {
            _stepUsed = true;
            Flash();
        }
        return Task.CompletedTask;
    }
}

/// <summary>Губитель: ваш Яд +1 стак; Яд на целях с Хворью не убывает.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class BlightbringerEssence : PathEssence
{
    public override HeroPath Path => HeroPath.Blightbringer;

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (!Owned || canonicalPower is not VanillaPoison || applier != Hero || target == Hero || amount <= 0) return false;
        modifiedAmount = amount + TransformationRules.BlightbringerPoisonBonus;
        return true;
    }

    /// <summary>Яд тикает в начале хода врагов и убывает на 1 — возвращаем этот 1 тем, на ком Хворь.</summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        await base.AfterSideTurnStart(side, participants, combatState);
        if (!Owned || side != CombatSide.Enemy) return;
        foreach (var enemy in participants.Where(e => e.IsAlive && e.HasPower<BlightPower>()).ToList())
            if (enemy.GetPower<VanillaPoison>() is { Amount: > 0 } poison)
                await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), poison, 1, null, null, silent: true);
    }
}

/// <summary>Мистический лучник: выстрелы (лук, арбалет) накладывают стихию последнего сыгранного заклинания (2 стака).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class MysticArcherEssence : PathEssence
{
    public override HeroPath Path => HeroPath.MysticArcher;

    private DamageType? _lastElement;

    public override Task BeforeCombatStart()
    {
        _lastElement = null;
        return base.BeforeCombatStart();
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owned && cardPlay.Card is SpellCard spell && spell.Owner == Owner)
        {
            var type = StaminaCosts.DamageTypeFor(spell);
            if (type is DamageType.Fire or DamageType.Frost or DamageType.Shock) _lastElement = type;
        }
        return Task.CompletedTask;
    }

    protected override async Task OnOwnAttack(PlayerChoiceContext ctx, AttackCommand command, CardModel card)
    {
        if (_lastElement is not { } element || !HeroRecord.IsRanged(HeroRecord.Of(Owner)?.MainHand ?? WeaponKind.None)
            || !EquipmentCombatRules.IsWeaponAttack(card)) return;
        foreach (var target in command.Results.SelectMany(h => h).Select(r => r.Receiver).Distinct().ToList())
            if (target is { IsAlive: true } && IsEnemy(target))
                await ElementStatus.Apply(ctx, target, element, TransformationRules.MysticArcherStacks, Hero);
    }
}

/// <summary>Наложить статус стихии.</summary>
public static class ElementStatus
{
    public static Task Apply(PlayerChoiceContext ctx, Creature target, DamageType element, int stacks, Creature source) => element switch
    {
        DamageType.Fire => PowerCmd.Apply<BurningPower>(ctx, target, stacks, source, null),
        DamageType.Frost => PowerCmd.Apply<ChillPower>(ctx, target, stacks, source, null),
        DamageType.Shock => PowerCmd.Apply<StaticPower>(ctx, target, stacks, source, null),
        _ => Task.CompletedTask,
    };

    public static DamageType ToDamageType(Element element) => element switch
    {
        Element.Fire => DamageType.Fire,
        Element.Frost => DamageType.Frost,
        _ => DamageType.Shock,
    };
}

/// <summary>Повелитель стихии: выбор стихии — подписное заклинание в колоду и +1 стак её статуса.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ElementLordEssence : PathEssence
{
    public override HeroPath Path => HeroPath.ElementLord;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Element", 0)];

    private int _element;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ChosenElement
    {
        get => _element;
        set
        {
            AssertMutable();
            _element = Math.Clamp(value, 0, 2);
            if (DynamicVars.TryGetValue("Element", out var v)) v.BaseValue = _element;
        }
    }

    public Element Element => (Element)_element;

    protected override async Task OnObtained()
    {
        var choices = Enum.GetValues<Element>()
            .Select(e => new CampMenu.Choice(e switch
            {
                Element.Fire => typeof(Meteor),
                Element.Frost => typeof(IceTomb),
                _ => typeof(ChainLightning),
            }, 0, 0, e))
            .ToList();
        var picked = await CampMenu.Pick(Owner, choices, "SLAYTHEREQUIEM_ELEMENT_PROMPT") as Element? ?? Element.Fire;
        ChosenElement = (int)picked;
        var spell = picked switch
        {
            Element.Fire => typeof(Meteor),
            Element.Frost => typeof(IceTomb),
            _ => typeof(ChainLightning),
        };
        var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(spell));
        await CardAcquisition.GrantOnceOrDefer(this, Owner.RunState.CreateCard(canonical, Owner), "element-signature");
    }

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (!Owned || applier != Hero || target == Hero || amount <= 0) return false;
        var match = Element switch
        {
            Element.Fire => canonicalPower is BurningPower,
            Element.Frost => canonicalPower is ChillPower,
            _ => canonicalPower is StaticPower,
        };
        if (!match) return false;
        modifiedAmount = amount + TransformationRules.ElementLordStatusBonus;
        return true;
    }
}

/// <summary>Боевой маг: Плащ одной стихии накидывает и остальные; атаки добавляют 2 урона стихией (по кругу).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class BattlemageEssence : PathEssence
{
    public override HeroPath Path => HeroPath.Battlemage;

    private bool _mirroring;
    private int _hits;

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (!Owned || _mirroring || amount <= 0 || power is not CloakPower || power.Owner != Hero) return;
        _mirroring = true;
        try
        {
            var n = (int)amount;
            if (power is not FireCloakPower) await PowerCmd.Apply<FireCloakPower>(choiceContext, Hero, n, Hero, null, silent: true);
            if (power is not FrostCloakPower) await PowerCmd.Apply<FrostCloakPower>(choiceContext, Hero, n, Hero, null, silent: true);
            if (power is not ShockCloakPower) await PowerCmd.Apply<ShockCloakPower>(choiceContext, Hero, n, Hero, null, silent: true);
        }
        finally
        {
            _mirroring = false;
        }
    }

    protected override async Task OnOwnAttack(PlayerChoiceContext ctx, AttackCommand command, CardModel card)
    {
        if (card is SpellCard) return;
        var element = (_hits++ % 3) switch
        {
            0 => DamageType.Fire,
            1 => DamageType.Frost,
            _ => DamageType.Shock,
        };
        foreach (var target in command.Results.SelectMany(h => h).Select(r => r.Receiver).Distinct().ToList())
        {
            if (target is not { IsAlive: true } || !IsEnemy(target)) continue;
            using (DamageTyping.As(element))
                await CreatureCmd.Damage(ctx, target, TransformationRules.BattlemageElementalDamage, ValueProp.Unpowered, Hero, null);
        }
    }
}

/// <summary>Отражатель: Оберег отражает 50 % поглощённого урона заклинаний в заклинателя (магией).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ReflectorEssence : PathEssence
{
    public override HeroPath Path => HeroPath.Reflector;

    /// <summary>Вызывается из <see cref="SpellShieldPower"/> после поглощения.</summary>
    public static async Task OnWardAbsorbed(PlayerChoiceContext ctx, Creature owner, Creature? dealer, int absorbed)
    {
        if (owner.Player == null || dealer == null || dealer == owner || !dealer.IsAlive) return;
        if (owner.Player.Relics.OfType<ReflectorEssence>().FirstOrDefault() is not { } essence) return;
        var reflected = TransformationRules.Reflected(absorbed);
        if (reflected <= 0) return;
        essence.Flash();
        using (DamageTyping.As(DamageType.Magic))
            await CreatureCmd.Damage(ctx, dealer, reflected, ValueProp.Unpowered, owner, null);
    }
}

/// <summary>Медиум: +1 слот призыва; призывы получают ваши ауры (+2 урона).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class MediumEssence : PathEssence
{
    public override HeroPath Path => HeroPath.Medium;
    protected override Boon ComputeBoon() => new()
    {
        SummonSlots = TransformationRules.MediumSummonSlots,
        SummonDamage = TransformationRules.MediumSummonDamage,
    };
}

/// <summary>
/// Перерождённый: без бонуса — единожды сменить предысторию (выбор среди предысторий происхождения и Бродяги)
/// и перераспределить очки характеристик (все возвращаются в свободные — распределить в лагере).
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RebornEssence : PathEssence
{
    public override HeroPath Path => HeroPath.Reborn;

    protected override async Task OnObtained()
    {
        if (HeroRecord.Of(Owner) is { } hero)
        {
            hero.StatHealth = hero.StatStamina = hero.StatMana = hero.StatBurden = hero.StatMastery = 0;
            await hero.SyncMaxHp();
        }
        var current = Owner.Relics.OfType<BackgroundRelic>().FirstOrDefault();
        var options = BackgroundRelic.Options(Owner.Relics.OfType<OriginRelic>().FirstOrDefault())
            .Append(typeof(VagrantBackground))
            .Where(t => t != null && t != current?.GetType())
            .Distinct()
            .Select(t => ModelDb.GetById<RelicModel>(ModelDb.GetId(t!)).ToMutable())
            .ToList();
        if (options.Count == 0) return;
        var chosen = await RelicSelectCmd.FromChooseARelicScreen(Owner, options);
        if (chosen == null) return;
        if (current != null) await RelicCmd.Remove(current);
        await RelicCmd.Obtain(chosen, Owner);
    }
}
