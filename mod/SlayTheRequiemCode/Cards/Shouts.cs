using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Cards;

/// <summary>
/// Полка Кличей (docs/design/04 «Глас»). Клич в системе Реквиема после розыгрыша не уходит в сброс:
/// <see cref="RequiemCard"/> отправляет его «никуда» (снимается со стола, но остаётся в бою), а эта сила держит его
/// N ходов и возвращает в руку в начале хода. Счётчик силы — число Кличей на полке.
/// </summary>
public sealed class ShoutShelfPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private sealed class Entry(CardModel card, int turns)
    {
        public CardModel Card { get; } = card;
        public int Turns { get; set; } = turns;
    }

    private readonly List<Entry> _shelf = [];

    public int Count => _shelf.Count;

    /// <summary>Ходов до возвращения ближайшего Клича (для описания).</summary>
    public int NextTurns => _shelf.Count == 0 ? 0 : _shelf.Min(e => e.Turns);

    public void Put(CardModel card, int turns) => _shelf.Add(new Entry(card, turns));

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner) || Owner.Player == null || _shelf.Count == 0) return;
        var ready = new List<CardModel>();
        foreach (var entry in _shelf.ToList())
        {
            entry.Turns--;
            if (entry.Turns > 0) continue;
            _shelf.Remove(entry);
            ready.Add(entry.Card);
        }
        foreach (var card in ready)
        {
            // Игра, отправляя карту «никуда» (PileType.None), снимает её с боя через RemoveFromCombat → RemoveFromState:
            // HasBeenRemovedFromState = true, и CardPileCmd.Add молча отказывает (success = false). Из-за этого Кличи
            // не возвращались в руку (плейтест 2026-11). Карта остаётся в CombatState — снимаем флаг, как RunState
            // делает для украденных карт, и только потом кладём в руку (полная рука — игра сама отправит в сброс).
            if (card.Owner == null || card.CombatState == null) continue;
            card.HasBeenRemovedFromState = false;
            var result = await CardPileCmd.Add(card, PileType.Hand);
            if (!result.success) MainFile.Logger.Warn($"Клич {card.Id.Entry} не вернулся в руку.");
        }
        if (ready.Count == 0) return;
        Flash();
        if (_shelf.Count == 0) await PowerCmd.Remove(this);
        else await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -ready.Count, null, null, silent: true);
    }
}

/// <summary>Правила полки: кто держит Кличи и сколько.</summary>
public static class Shouts
{
    /// <summary>Полка работает в системе Реквиема (снимать Клич со стола без Летописи героя некому).</summary>
    public static bool Active(Player? player) =>
        player != null && RequiemMode.IsInSystem(player) && Progression.HeroRecord.Of(player) != null;

    public static int Capacity(Player player) => ShoutRules.ShelfCapacity(player.Relics.OfType<SkaldBackground>().Any());

    public static int CooldownOf(Player player, IShoutCard shout) =>
        ShoutRules.Cooldown(shout.ShoutCooldown, BoonRelic.ShoutCooldownReduction(player));

    /// <summary>Есть ли место на полке (иначе Клич уходит в сброс как обычная карта).</summary>
    public static bool HasRoom(Player player) =>
        (player.Creature.GetPower<ShoutShelfPower>()?.Count ?? 0) < Capacity(player);

    /// <summary>Положить разыгранный Клич на полку (вызывается из <see cref="RequiemCard"/> после розыгрыша).</summary>
    public static async Task Shelve(CardModel card, IShoutCard shout)
    {
        var player = card.Owner;
        var turns = CooldownOf(player, shout);
        var shelf = player.Creature.GetPower<ShoutShelfPower>();
        if (shelf == null)
        {
            await PowerCmd.Apply<ShoutShelfPower>(new ThrowingPlayerChoiceContext(), player.Creature, 1, player.Creature, null, silent: true);
            shelf = player.Creature.GetPower<ShoutShelfPower>();
        }
        else
        {
            await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), shelf, 1, null, null, silent: true);
        }
        shelf?.Put(card, turns);
    }
}

// ═════════════ Кличи (04, таблица «Глас») ═════════════

/// <summary>Безжалостная сила: 6 (9) урона + Ошеломление 4. Перезарядка 3. Как все Кличи — без ◆ (04).</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class UnrelentingForce : RequiemCard, IShoutCard
{
    public int ShoutCooldown => 3;

    public UnrelentingForce() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(6, 3);
        WithPower<StaggerPower>(4);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target, "vfx/vfx_attack_blunt");
        if (play.Target.IsAlive) await Apply<StaggerPower>(ctx, play.Target, Var(nameof(StaggerPower)));
    }
}

/// <summary>Огненный выдох: 4 огнём всем + Горение 4. Перезарядка 3.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class FireBreath : RequiemCard, IShoutCard
{
    public int ShoutCooldown => 3;
    public override DamageType DamageType => DamageType.Fire;

    public FireBreath() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithDamage(4, 2);
        WithPower<BurningPower>(4, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await HitAll(ctx, "vfx/vfx_attack_blunt");
        await ApplyAll<BurningPower>(ctx, Var(nameof(BurningPower)));
    }
}

/// <summary>Морозный выдох: Озноб 4 всем; по уже охлаждённым — вдвое. Перезарядка 3.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class FrostBreath : RequiemCard, IShoutCard
{
    public int ShoutCooldown => 3;

    public FrostBreath() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithPower<ChillPower>(4, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var amount = Var(nameof(ChillPower));
        foreach (var enemy in CombatState.HittableEnemies.ToList())
            await Apply<ChillPower>(ctx, enemy, enemy.HasPower<ChillPower>() ? amount * 2 : amount);
    }
}

/// <summary>Зов грозы: 5 разрядом всем + Шок (Ошеломление 2). Перезарядка 4.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class StormCall : RequiemCard, IShoutCard
{
    public int ShoutCooldown => 4;
    public override DamageType DamageType => DamageType.Shock;

    public StormCall() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
        WithDamage(5, 3);
        WithPower<StaggerPower>(2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await HitAll(ctx, "vfx/vfx_attack_blunt");
        await ApplyAll<StaggerPower>(ctx, Var(nameof(StaggerPower)));
    }
}

/// <summary>Разрыв души: 10 урона; добивает цель с ♥ ниже 25 % (не боссов). Перезарядка 5.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class SoulTear : RequiemCard, IShoutCard
{
    public int ShoutCooldown => 5;

    public SoulTear() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(10, 4);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target);
        var target = play.Target;
        if (target.IsAlive && ShoutRules.SoulTearExecutes(target.CurrentHp, target.MaxHp, EnemySetup.RankOf(target.CombatState) == EnemyRank.Boss))
            await CreatureCmd.Kill(target);
    }
}
