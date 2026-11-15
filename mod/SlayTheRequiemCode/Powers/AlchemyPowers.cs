using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using VanillaPoison = MegaCrit.Sts2.Core.Models.Powers.PoisonPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

// ═════════════ Длительные зелья (docs/design/09 §3.2) ═════════════

/// <summary>
/// Восстановление на N ходов: Amount = оставшиеся ходы, <see cref="PerTurn"/> — сколько в начале хода.
/// Повторное зелье продлевает действие и берёт большую силу.
/// </summary>
public abstract class TimedRestorePower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public int PerTurn { get; private set; } = 1;

    public static async Task Grant<T>(PlayerChoiceContext ctx, Creature hero, int perTurn, int turns) where T : TimedRestorePower
    {
        await PowerCmd.Apply<T>(ctx, hero, turns, hero, null);
        if (hero.GetPower<T>() is { } power) power.PerTurn = Math.Max(power.PerTurn, perTurn);
    }

    protected abstract Task Restore(Player player, int amount);

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || Amount <= 0) return;
        Flash();
        await Restore(player, PerTurn);
        await PowerCmd.Decrement(this);
    }
}

/// <summary>Омоложение: лечит N в начале хода.</summary>
public sealed class RejuvenationPower : TimedRestorePower
{
    protected override Task Restore(Player player, int amount) => CreatureCmd.Heal(player.Creature, amount);
}

/// <summary>Второе дыхание: +N ◆ в начале хода (сверх прироста, не выше пула).</summary>
public sealed class SecondWindPower : TimedRestorePower
{
    protected override Task Restore(Player player, int amount)
    {
        if (RequiemResource.Of<StaminaResource>(player) is { } stamina)
            stamina.Amount = Math.Min(stamina.Pool, stamina.Amount + amount);
        return Task.CompletedTask;
    }
}

/// <summary>Ясность: +N ✦ в начале хода.</summary>
public sealed class ClearMindPower : TimedRestorePower
{
    protected override Task Restore(Player player, int amount) => Mana.Gain(player, amount);
}

// ═════════════ Усиления на бой (docs/design/09 §3.3) ═════════════

/// <summary>Зелье Великана: Стойкость героя +N на бой (читает <see cref="HeroStatRules.Poise"/>).</summary>
public sealed class GiantPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>Зелье Чародея: первые N заклинаний каждого хода стоят на 1 ✦ меньше.</summary>
public sealed class SorcererPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _usedThisTurn;

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card is not SpellCard || card.Owner?.Creature != Owner || originalCost <= 0 || _usedThisTurn >= Amount) return false;
        modifiedCost = originalCost - 1;
        return true;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card is SpellCard && cardPlay.Card.Owner?.Creature == Owner && cardPlay.IsLastInSeries) _usedThisTurn++;
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner) _usedThisTurn = 0;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Эликсир лунного сахара: N ходов заклинания стоят на 1 ✦ меньше; когда действие кончается — «Отходняк», −2 ✦.
/// </summary>
public sealed class MoonSugarPower : RequiemPower
{
    public const int Withdrawal = 2;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card is not SpellCard || card.Owner?.Creature != Owner || originalCost <= 0) return false;
        modifiedCost = originalCost - 1;
        return true;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;
        if (Amount <= 1)
        {
            Flash();
            await Mana.Lose(Owner.Player, Withdrawal);
        }
        await PowerCmd.Decrement(this);
    }
}

// ═════════════ Безмолвная: скрытность ═════════════

/// <summary>
/// Невидимость N (docs/design/08 §6, 07): атаки врагов по носителю наносят вдвое меньше урона, а все его атаки —
/// Подлые. Первая атака носителя снимает Невидимость (после удара). −1 в начале хода носителя.
/// </summary>
public sealed class InvisibilityPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>Выдана посреди атаки (Клинок Сумеречной Вдовы): эта атака её не снимает, спадает в конце хода героя.</summary>
    private bool _grantedMidAttack;
    private bool _untilTurnEnd;

    /// <summary>Невидимость до конца хода героя, переживающая текущую атаку (убийство Клинком Вдовы).</summary>
    public static async Task GrantUntilTurnEnd(PlayerChoiceContext ctx, Creature hero)
    {
        var existing = hero.GetPower<InvisibilityPower>();
        if (existing != null)
        {
            existing.AssertMutable();
            existing._grantedMidAttack = true;
            return;
        }
        var power = await PowerCmd.Apply<InvisibilityPower>(ctx, hero, 1, hero, null);
        if (power == null) return;
        power.AssertMutable();
        power._grantedMidAttack = true;
        power._untilTurnEnd = true;
    }

    /// <summary>Сапоги Безмолвия: атаки (а из Невидимости все атаки Подлые) её не снимают.</summary>
    private bool KeptByGear => Owner.Player?.Relics.OfType<IInvisibilityKeeper>().Any(k => k.KeepsInvisibility) == true;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && dealer != null && dealer != Owner && props.IsPoweredAttack() ? 0.5m : 1m;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Type != CardType.Attack || cardPlay.Card.Owner?.Creature != Owner || !cardPlay.IsLastInSeries) return;
        if (_grantedMidAttack)
        {
            AssertMutable();
            _grantedMidAttack = false;
            return;
        }
        if (KeptByGear) return;
        await PowerCmd.Remove(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && _untilTurnEnd) await PowerCmd.Remove(this);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner) await PowerCmd.Decrement(this);
    }
}

/// <summary>Лишние заряды яда на оружии (Знамение Змея: +2).</summary>
public interface IPoisonChargeSource
{
    int ExtraPoisonCharges { get; }
}

/// <summary>Снаряжение, при котором атаки не снимают Невидимость (Сапоги Безмолвия).</summary>
public interface IInvisibilityKeeper
{
    bool KeepsInvisibility { get; }
}

/// <summary>Приглушённые шаги: следующая атака — Подлая (снимается после атаки).</summary>
public sealed class SneakNextPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Type == CardType.Attack && cardPlay.Card.Owner?.Creature == Owner && cardPlay.IsLastInSeries)
            await PowerCmd.Decrement(this);
    }
}

/// <summary>
/// Смятение N (Иллюзия): атаки носителя по героям не доходят до ♥ (Блок всё же тратится) — дошедший урон получает
/// случайный другой враг (или сам носитель, если он один). −1 в конце хода носителя.
/// </summary>
public sealed class ConfusionPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _redirected;

    /// <summary>Урон по героям (после Блока) не доходит до ♥ — копится и уходит в другого врага после атаки.</summary>
    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (dealer != Owner || !target.IsPlayer || amount <= 0 || !props.IsCardOrMonsterMove()) return amount;
        _redirected += (int)amount;
        return 0m;
    }

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (command.Attacker != Owner || Owner.CombatState == null) return;
        var damage = _redirected;
        _redirected = 0;
        if (damage <= 0) return;
        var others = Owner.CombatState.Enemies.Where(e => e.IsAlive && e != Owner).ToList();
        var victim = others.Count > 0 ? Owner.CombatState.RunState.Rng.CombatTargets.NextItem(others) : Owner;
        if (victim == null) return;
        Flash();
        await CreatureCmd.Damage(choiceContext, victim, damage, ValueProp.Unpowered, Owner, null);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Decrement(this);
    }
}

// ═════════════ Яды на оружии (docs/design/09 §4) ═════════════

/// <summary>
/// Яд на оружии: Amount = заряды. Каждая Оружейная атака владельца тратит заряд и накладывает эффект яда
/// (сила по ступени <see cref="Tier"/>). На оружии один яд — новый смывает прежний.
/// Нежить и конструкты невосприимчивы ко всему, кроме Укуса змеи, Разъедания и Пустоцвета.
/// </summary>
public abstract class WeaponPoisonPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public abstract Brew Kind { get; }
    public int Tier { get; private set; } = 1;
    protected int Potency => Boosted(AlchemyRules.PoisonPotency(Kind, Tier));

    /// <summary>«Заговорщик» (Дюнные клинки, 04): яды на оружии +25 %; Кольцо Пьяницы (07 §5.2) — ещё +50 %.</summary>
    protected int Boosted(int potency)
    {
        var player = Owner?.Player;
        if (player?.Relics.OfType<Relics.ConspiratorBackground>().Any() == true) potency = OriginRules.ConspiratorPotency(potency);
        return Equipment.RingOfTheDrunkard.Potency(player, potency);
    }
    protected virtual bool BypassesImmunity => false;

    /// <summary>Нанести яд на оружие героя: смыть прежний, наложить заряды, запомнить ступень.</summary>
    public static async Task Coat<T>(PlayerChoiceContext ctx, Creature hero, int tier, int charges) where T : WeaponPoisonPower
    {
        foreach (var old in hero.Powers.OfType<WeaponPoisonPower>().Where(p => p is not T).ToList())
            await PowerCmd.Remove(old);
        await PowerCmd.Apply<T>(ctx, hero, charges, hero, null);
        if (hero.GetPower<T>() is { } power) power.Tier = Math.Max(power.Tier, tier);
    }

    public static int ChargesFor(Player player) =>
        AlchemyRules.BasePoisonCharges + player.Relics.OfType<IPoisonChargeSource>().Sum(s => s.ExtraPoisonCharges);

    public static bool IsImmune(Creature target) =>
        target.Monster != null && RequiemData.MonsterEntry(target.Monster.GetType().Name).Faction is "undead" or "construct";

    /// <summary>Эффект яда по цели (без траты заряда) — его же зовут «Отравленный клинок» и «Флакон в лицо».</summary>
    public async Task Afflict(PlayerChoiceContext ctx, Creature target, int multiplier = 1)
    {
        if (!target.IsAlive || (!BypassesImmunity && IsImmune(target))) return;
        await ApplyEffect(ctx, target, Potency * multiplier);
    }

    protected abstract Task ApplyEffect(PlayerChoiceContext ctx, Creature target, int potency);

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (command.Attacker != Owner || command.ModelSource is not CardModel card || Amount <= 0
            || !Equipment.EquipmentCombatRules.IsWeaponAttack(card)) return;
        foreach (var target in command.Results.SelectMany(h => h).Select(r => r.Receiver).Distinct())
            if (target != null) await Afflict(choiceContext, target);
        Flash();
        await PowerCmd.Decrement(this);
    }
}

/// <summary>Укус змеи: +N прямого урона (мимо Лат и сопротивлений).</summary>
public sealed class SnakeBitePoisonPower : WeaponPoisonPower
{
    public override Brew Kind => Brew.SnakeBite;
    protected override bool BypassesImmunity => true;
    protected override Task ApplyEffect(PlayerChoiceContext ctx, Creature target, int potency) =>
        CreatureCmd.Damage(ctx, target, potency, ValueProp.Unpowered, Owner, null);
}

/// <summary>Токсин: ванильный Яд N.</summary>
public sealed class ToxinPoisonPower : WeaponPoisonPower
{
    public override Brew Kind => Brew.Toxin;
    protected override Task ApplyEffect(PlayerChoiceContext ctx, Creature target, int potency) =>
        PowerCmd.Apply<VanillaPoison>(ctx, target, potency, Owner, null);
}

/// <summary>Гнилец: Хворь N (вместе с Токсином — урон каждый ход).</summary>
public sealed class RotPoisonPower : WeaponPoisonPower
{
    public override Brew Kind => Brew.Rot;
    protected override Task ApplyEffect(PlayerChoiceContext ctx, Creature target, int potency) =>
        PowerCmd.Apply<BlightPower>(ctx, target, potency, Owner, null);
}

/// <summary>Истощение: −N ◆ цели (как холод без урона).</summary>
public sealed class ExhaustionPoisonPower : WeaponPoisonPower
{
    public override Brew Kind => Brew.Exhaustion;
    protected override async Task ApplyEffect(PlayerChoiceContext ctx, Creature target, int potency)
    {
        if (target.GetPower<EnemyStaminaPower>() is { } stamina) await stamina.Drain(potency);
        else if (RequiemResource.Of<StaminaResource>(target.Player) is { } hero) hero.Amount = Math.Max(0, hero.Amount - potency);
    }
}

/// <summary>Пустоцвет: −N ✦ цели (как разряд без урона).</summary>
public sealed class EmptybloomPoisonPower : WeaponPoisonPower
{
    public override Brew Kind => Brew.Emptybloom;
    protected override bool BypassesImmunity => true;
    protected override async Task ApplyEffect(PlayerChoiceContext ctx, Creature target, int potency)
    {
        if (target.GetPower<EnemyManaPower>() is { } mana) await mana.Drain(potency);
        else if (target.IsPlayer) await Mana.Lose(target.Player, potency);
    }
}

/// <summary>Слабость: ванильная Слабость N.</summary>
public sealed class WeaknessPoisonPower : WeaponPoisonPower
{
    public override Brew Kind => Brew.Weakness;
    protected override Task ApplyEffect(PlayerChoiceContext ctx, Creature target, int potency) =>
        PowerCmd.Apply<WeakPower>(ctx, target, potency, Owner, null);
}

/// <summary>Разъедание: Раскол N (против Лат).</summary>
public sealed class CorrosionPoisonPower : WeaponPoisonPower
{
    public override Brew Kind => Brew.Corrosion;
    protected override bool BypassesImmunity => true;
    protected override Task ApplyEffect(PlayerChoiceContext ctx, Creature target, int potency) =>
        PowerCmd.Apply<SunderPower>(ctx, target, potency, Owner, null);
}

/// <summary>Паралитик: шанс паралича (I 10 %, III 25 %, V — гарантированно, но один раз за бой; не боссы).</summary>
public sealed class ParalyticPoisonPower : WeaponPoisonPower
{
    public override Brew Kind => Brew.Paralytic;
    private bool _guaranteedUsed;

    protected override async Task ApplyEffect(PlayerChoiceContext ctx, Creature target, int potency)
    {
        var chance = Math.Min(100, potency);
        if (chance >= 100)
        {
            if (_guaranteedUsed) return;
            _guaranteedUsed = true;
        }
        else if (Owner.CombatState == null || Owner.CombatState.RunState.Rng.CombatTargets.NextInt(100) >= chance) return;
        await EnemyResponses.TryParalyze(ctx, target, SpellTier.Master, Owner, null);
    }
}

/// <summary>Комбинированный: Токсин и Истощение одной ступени (дорогой, из лавок и у Заговорщика).</summary>
public sealed class CombinedPoisonPower : WeaponPoisonPower
{
    public override Brew Kind => Brew.Toxin;
    protected override async Task ApplyEffect(PlayerChoiceContext ctx, Creature target, int potency)
    {
        await PowerCmd.Apply<VanillaPoison>(ctx, target, potency, Owner, null);
        var drain = Boosted(AlchemyRules.PoisonPotency(Brew.Exhaustion, Tier));
        if (target.GetPower<EnemyStaminaPower>() is { } stamina) await stamina.Drain(drain);
    }
}

// ═════════════ Восполнения V: прирост на бой ═════════════

/// <summary>Бодрость (зелье Выносливости V): +N к приросту ◆ до конца боя.</summary>
public sealed class StaminaSurgePower : RequiemPower, IResourceSource
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public int StaminaRegenBonus => Amount;
}

/// <summary>Прозрение (зелье Маны V): +N к приросту ✦ до конца боя.</summary>
public sealed class InsightPower : RequiemPower, IResourceSource
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public int ManaRegenBonus => Amount;
}

// ═════════════ Безмолвная: Уклонение и яды ═════════════

/// <summary>Контрудар N: после успешного Уворота атакующий получает N урона; следующая атака — Подлая.</summary>
public sealed class RipostePower : RequiemPower, IDodgeListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task AfterDodged(PlayerChoiceContext ctx, Creature? attacker)
    {
        if (attacker is { IsAlive: true })
        {
            Flash();
            await CreatureCmd.Damage(ctx, attacker, Amount, ValueProp.Unpowered, Owner, null);
        }
        await PowerCmd.Apply<SneakNextPower>(ctx, Owner, 1, Owner, null);
    }
}

/// <summary>Рывок N: +N зарядов Уворота до начала следующего хода носителя.</summary>
public sealed class TemporaryDodgePower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public static async Task Grant(PlayerChoiceContext ctx, Creature hero, int charges)
    {
        await PowerCmd.Apply<DodgePower>(ctx, hero, charges, hero, null);
        await PowerCmd.Apply<TemporaryDodgePower>(ctx, hero, charges, hero, null);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner) return;
        if (Owner.GetPower<DodgePower>() is { } dodge)
        {
            if (dodge.Amount <= Amount) await PowerCmd.Remove(dodge);
            else await PowerCmd.ModifyAmount(choiceContext, dodge, -Amount, null, null, silent: true);
        }
        await PowerCmd.Remove(this);
    }
}

/// <summary>Мастер ядов N: в начале хода яд на оружии получает +N зарядов.</summary>
public sealed class VenomMasteryPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner) return;
        if (Owner.Powers.OfType<WeaponPoisonPower>().FirstOrDefault() is not { } coat) return;
        Flash();
        await PowerCmd.ModifyAmount(choiceContext, coat, Amount, Owner, null);
    }
}
