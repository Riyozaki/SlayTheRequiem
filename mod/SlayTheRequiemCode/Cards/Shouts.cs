using BaseLib.Utils;
using SlayTheRequiem.SlayTheRequiemCode.Debugging;
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

    private CooldownShelf<CardModel, ICombatState> _shelf = new();

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        // Powers can be copied to another creature. Copying the list would still share the *physical cards*.
        // A new power never owns another shelf's entitlements, even when cloned from a populated power.
        _shelf = new();
    }

    public int Count => _shelf.Count;
    public int NextTurns => _shelf.NextTurns;
    internal bool ContainsCaptured(CardModel card, ICombatState combat) => _shelf.Contains(card, combat);

    public bool Put(CardModel card, int turns)
    {
        var combat = Owner.CombatState;
        if (combat == null || card.Owner != Owner.Player || !_shelf.Put(card, combat, turns)) return false;
        CardRewardTrace.Card("shout:shelved", card, $"turns={turns} shelf={Count}");
        return true;
    }

    /// <summary>Production adapter shared with isolated native-model regressions; never adopts an uncaptured card.</summary>
    internal bool PrepareReturn(CardModel card, ICombatState combat)
    {
        var player = Owner.Player;
        if (player == null || !_shelf.Contains(card, combat) || !ReferenceEquals(Owner.CombatState, combat)
            || !ShoutRules.CanRestoreCaptured(card.Owner == player, card.Owner == null, card.HasBeenRemovedFromState, card.Pile != null)) return false;
        if (card.Owner == null || !combat.ContainsCard(card)) combat.AddCard(card, player);
        card.HasBeenRemovedFromState = false;
        return true;
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner) || Owner.Player == null || !Owner.IsAlive
            || !ReferenceEquals(Owner.CombatState, combatState) || _shelf.Count == 0) return;
        var before = Count;
        foreach (var entry in _shelf.Tick(combatState))
        {
            var card = entry.Card;
            // The shelf is captured BEFORE None cleanup. The supported game's RemoveFromState sets
            // HasBeenRemovedFromState/Pile=null; a further CombatState.RemoveCard also clears Owner.
            // Only this shelf's exact instance in its captured combat may reclaim that unowned removed card.
            // An explicit transfer to a different player is never permission to steal it back.
            if (card.Owner != null && card.Owner != Owner.Player
                || card.Owner == null && !card.HasBeenRemovedFromState)
            {
                CardRewardTrace.Card("shout:owner-changed", card, "entry dropped; never steal a card from another owner", force: true);
                _shelf.Complete(card);
                continue;
            }
            if (card.Pile != null)
            {
                // An intervening hook already moved the same card. Never pull it out of Exhaust/Deck or
                // create a second return. The exact instance must be off-table for shelf restoration.
                CardRewardTrace.Card("shout:externally-moved", card, "entry completed without another Add");
                _shelf.Complete(card);
                continue;
            }
            if (!CombatManager.Instance.IsInProgress || CombatManager.Instance.IsEnding) continue;
            var committed = false;
            try
            {
                if (!PrepareReturn(card, combatState)) continue;
                // Add redirects a full hand into Discard. Its bool is not proof: VFX/hooks may throw after a move.
                await CardPileCmd.Add(card, PileType.Hand);
            }
            catch (Exception e)
            {
                MainFile.Logger.Error($"[CardReward] phase=shout:return-exception card={card.Id}: {e}");
            }
            committed = card.Owner == Owner.Player && card.Pile?.Type is PileType.Hand or PileType.Discard;
            CardRewardTrace.Card("shout:return-result", card, $"committed={committed}", force: !committed);
            if (committed) _shelf.Complete(card); // failed returns stay due for the next owner turn
        }
        if (Count == before) return;
        Flash();
        if (Count == 0) await PowerCmd.Remove(this);
        else await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, Count - before, null, null, silent: true);
    }

    public override Task AfterCombatEnd(MegaCrit.Sts2.Core.Rooms.CombatRoom room)
    {
        _shelf.Clear(); // no stale combat/card references can cross a fight boundary
        return Task.CompletedTask;
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
        if (shelf == null || !shelf.Put(card, turns)) return;
        // Apply starts at one; every later physical card adds exactly one, including repeated-play hooks.
        if (shelf.Count > 1)
            await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), shelf, 1, null, null, silent: true);
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
