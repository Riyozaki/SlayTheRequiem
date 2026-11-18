using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Acts;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using V = MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

// Супербоссы Багрового портала (docs/design/11 §6). ♥ — ActRules.SuperbossHp; Непоколебимость/Слом даёт EnemySetup (ранг Boss).
// Контркарты боя выдаются через combat-local entitlement: полная рука не теряет карту, нет выдачи в постоянную колоду.
// Облик (M7 п. 1) — свой PNG (OwnArtMonster, tools/art/monsters/superbosses.py), вариант по состоянию — SuperbossLookRules;
// LookOf остаётся откатом, если картинки нет. Свита — ванильные модели с перекраской фракции (CourtTintPatch ниже).

/// <summary>Супербосс: ♥ из таблицы, вид — для награды и Безымянного предела; свой спрайт и картинка по состоянию.</summary>
public abstract class SuperbossMonster : OwnArtMonster
{
    public override EnemyRole CombatRole => EnemyRole.Boss;

    public abstract Superboss Kind { get; }
    protected override int Hp => ActRules.SuperbossHp(Kind);

    public override string ArtKey => SuperbossLookRules.ArtKey(Kind);

    protected override IReadOnlyList<string> ArtVariants => SuperbossLookRules.Variants(Kind);

    /// <summary>Стойка блока Чёрного Странника (видна, пока герои снова не атакуют).</summary>
    protected bool Guarding { get; set; }

    /// <summary>Показать картинку по текущему состоянию (Стойка, Неуязвимость, Полёт); та же — без вспышки.</summary>
    protected void RefreshLook()
    {
        if (!Creature.IsAlive) return;
        ShowArt(SuperbossLookRules.Variant(Kind, Guarding, Creature.HasPower<InvulnerablePower>(), Creature.HasPower<FlightPower>()));
    }

    protected int Turn { get; private set; }
    private EncounterCardGrants _counters = new();
    private int _grantWindow;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _counters = new(); // no shared mutable entitlements across canonical clones/runs
        Turn = 0;
        _grantWindow = 0;
        Guarding = false;
    }

    public virtual bool NeedsCounter(Type type) => false;
    protected int NextCounterWindow() => ++_grantWindow;
    protected void RequestCounter<T>(int window, Player? player = null) where T : EncounterCounterCard => _counters.Request<T>(this, window, player);
    protected Task FlushCounters() => _counters.Flush(this);
    protected virtual void PrepareCountersForTurn(Player player, int window) { }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (!Creature.IsAlive || Creature.CombatState?.Players.Contains(player) != true) return;
        PrepareCountersForTurn(player, NextCounterWindow());
        await FlushCounters();
    }
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        // Record before native/third-party AfterCardPlayed hooks can rescue a failed shelf insertion into
        // Discard and re-enter our pile hook. Late recording would allow an early same-turn recall.
        _counters.RecordPlay(this, cardPlay);
        return Task.CompletedTask;
    }
    public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay) => FlushCounters();
    public override Task AfterCardChangedPilesLate(CardModel card, PileType oldPileType, AbstractModel? clonedBy) =>
        oldPileType == PileType.Hand || card is EncounterCounterCard ? FlushCounters() : Task.CompletedTask;
    public override Task AfterCombatEnd(CombatRoom room) => _counters.Clear(this);

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, CardModel? source, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature != Creature || wasRemovalPrevented) return;
        await _counters.Clear(this);
        foreach (var ally in Allies.Where(a => EnemySetup.RoleOf(a) is EnemyRole.Minion or EnemyRole.Prop).ToList())
            await CreatureCmd.Kill(ally);
    }

    public override Task AfterSideTurnStart(MegaCrit.Sts2.Core.Combat.CombatSide side, IReadOnlyList<Creature> participants, MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        if (side == MegaCrit.Sts2.Core.Combat.CombatSide.Enemy) Turn++;
        return Task.CompletedTask;
    }
}

/// <summary>Энкаунтер супербосса: в Багровом портале (второй босс акта), сам в пулы актов не попадает.</summary>
public abstract class SuperbossEncounter() : RequiemEncounter(RoomType.Boss)
{
    public abstract Superboss Kind { get; }

    /// <summary>Значок на карте — пока ванильный (своих анимаций узлов нет, этап 8): портал рисуется значком Королевы.</summary>
    public override string BossNodePath => ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.QueenBoss>().BossNodePath;

    /// <summary>Энкаунтер супербосса по виду.</summary>
    public static EncounterModel For(Superboss boss) => boss switch
    {
        Superboss.BlackWanderer => ModelDb.Encounter<BlackWandererBoss>(),
        Superboss.BarrowReaper => ModelDb.Encounter<BarrowReaperBoss>(),
        Superboss.DeepMasterSmith => ModelDb.Encounter<DeepMasterSmithBoss>(),
        Superboss.GatebreakerEnvoy => ModelDb.Encounter<GatebreakerEnvoyBoss>(),
        Superboss.BloodCourtLord => ModelDb.Encounter<BloodCourtLordBoss>(),
        Superboss.FirstSummoner => ModelDb.Encounter<FirstSummonerBoss>(),
        Superboss.WorldEater => ModelDb.Encounter<WorldEaterBoss>(),
        _ => ModelDb.Encounter<NamelessShadowBoss>(),
    };
}

// ═════════════ Акт 1 — Чёрный Странник ═════════════

/// <summary>
/// Чёрный Странник: 220 ♥, Латы 2. Стойка блока — если в прошлый ход герои не атаковали: Латы +3. Удары накладывают стихию,
/// к которой отряд менее всего устойчив (Горение 4 / Озноб 3 / Статика 2). Соли душ (3): раз в 2 хода — раб-призрак;
/// Подлый удар крадёт Соль.
/// </summary>
public sealed class BlackWanderer : SuperbossMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<WraithThrall>()];
    public const int Plating = 2;
    public const int GuardPlating = BossTimingRules.WandererGuardPlating;
    public const int Salts = 3;
    public const int DuelDamage = 14;
    public const int FlurryDamage = 6;
    public const int FlurryHits = 3;

    public override Superboss Kind => Superboss.BlackWanderer;
    protected override MonsterModel LookOf => ModelDb.Monster<MysteriousKnight>();
    protected override string? CastAnim => null;

    public override DamageType CurrentAttackType => NextMove?.Id == "DUELIST_STRIKE" ? WeakestElement() : base.CurrentAttackType;
    private bool _heroesAttacked;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        // Base armor comes from the monster table once, not a second initial grant here.
        await PowerCmd.Apply<SoulSaltsPower>(Ctx, Creature, Salts, Creature, null);
    }

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("DUELIST_STRIKE", async _ => { var e = EnemyActionBudget.TypeOf(Creature, CurrentAttackType); await Hit(DuelDamage, type: e); await ElementStatus(e, 4, 3, 2); },
            Atk(DuelDamage), new DebuffIntent()),
        new MoveState("SOUL_SALTS", Salt, new SummonIntent(), new DefendIntent()),
        new MoveState("EBONY_FLURRY", _ => Hit(FlurryDamage, FlurryHits), Multi(FlurryDamage, FlurryHits)),
        new MoveState("SOUL_SALTS_2", Salt, new SummonIntent(), new DefendIntent()),
    ];

    private async Task Salt(IReadOnlyList<Creature> targets)
    {
        await Block(10);
        if (Creature.GetPower<SoulSaltsPower>() is not { Amount: > 0 } salts) return;
        await PowerCmd.Decrement(salts);
        await Summon<WraithThrall>();
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Type != CardType.Attack || Creature.CombatState?.Players.Contains(cardPlay.Card.Owner) != true) return;
        _heroesAttacked = true; // one shared party flag; one ally's Attack is enough, irrespective of its damage/target
        if (!cardPlay.IsLastInSeries) return;
        if (Creature.GetPower<WandererGuardPower>() is { } guard) await PowerCmd.Remove(guard);
        Guarding = false;
        RefreshLook();
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Player || !Creature.IsAlive) return;
        Guarding = BossTimingRules.WandererGuardsAfterTurn(_heroesAttacked);
        if (Guarding && !Creature.HasPower<WandererGuardPower>())
            await PowerCmd.Apply<WandererGuardPower>(choiceContext, Creature, 1, Creature, null);
        else if (!Guarding && Creature.GetPower<WandererGuardPower>() is { } guard) await PowerCmd.Remove(guard);
        RefreshLook();
        _heroesAttacked = false;
    }
}

public sealed class WraithThrall : RequiemMinion
{
    public const int TouchDamage = 7;
    protected override int Hp => 20;
    protected override MonsterModel LookOf => ModelDb.Monster<Parafright>();
    protected override string? CastAnim => null;
    protected override IReadOnlyList<MoveState> Cycle() =>
        [new MoveState("WRAITH_TOUCH", _ => Hit(TouchDamage, type: DamageType.Frost, fx: null), Atk(TouchDamage))];
}

public sealed class BlackWandererBoss() : SuperbossEncounter
{
    public override Superboss Kind => Superboss.BlackWanderer;
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<BlackWanderer>()];
}

// ═════════════ Акт 2а — Жнец Курганов ═════════════

/// <summary>
/// Жнец Курганов: 300 ♥. Холодная коса — Раскол 4 и вампиризм 50 % (Горение снимает вампиризм).
/// Цикл 4 хода: коса → коса → 2 курганных мертвеца (3/7/11) → клич (4/8/12, Латы −3).
/// </summary>
public sealed class BarrowReaper : SuperbossMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<BarrowDead>()];
    public const int ScytheDamage = 16;
    public const int ScytheSunder = 4;
    public const int LifestealPercent = 50;
    public const int CursedCryPlating = 3;

    public override Superboss Kind => Superboss.BarrowReaper;
    protected override MonsterModel LookOf => ModelDb.Monster<TheForgotten>();

    protected override IReadOnlyList<MoveState> Cycle() => Enumerable.Range(1, BossTimingRules.ReaperCycleLength)
        .Select(turn => BossTimingRules.ReaperMoveAt(turn) switch
        {
            ReaperMove.Raise => new MoveState("RAISE_BARROW_DEAD", async _ => { await Cast(); await Summon<BarrowDead>(); await Summon<BarrowDead>(); }, new SummonIntent()),
            ReaperMove.Cry => new MoveState("CURSED_CRY", CursedCry, new DebuffIntent()),
            _ => new MoveState(turn == 1 ? "FROST_SCYTHE" : "FROST_SCYTHE_2", Scythe, Atk(ScytheDamage), new DebuffIntent()),
        }).ToList();

    private async Task Scythe(IReadOnlyList<Creature> targets)
    {
        var before = Heroes.Sum(h => h.CurrentHp);
        await Hit(ScytheDamage, type: DamageType.Frost);
        await Debuff<SunderPower>(ScytheSunder);
        if (!Creature.HasPower<BurningPower>())
            await Heal(Math.Max(0, before - Heroes.Sum(h => h.CurrentHp)) * LifestealPercent / 100);
    }

    private async Task CursedCry(IReadOnlyList<Creature> targets)
    {
        await Cast();
        foreach (var hero in Heroes.ToList())
        {
            await PowerCmd.Apply<CursedCryPower>(Ctx, hero, CursedCryPlating, Creature, null);
            // The source-qualified curse is read by the armor stage, including below zero. Do not also
            // subtract the permanent armor counter: that loses the penalty at 0 and double-applies it above 0.
        }
    }
}

public sealed class BarrowDead : RequiemMinion
{
    public const int SlashDamage = 8;
    protected override int Hp => 28;
    protected override MonsterModel LookOf => ModelDb.Monster<TheLost>();
    protected override IReadOnlyList<MoveState> Cycle() => [new MoveState("BARROW_SLASH", _ => Hit(SlashDamage), Atk(SlashDamage))];
}

public sealed class BarrowReaperBoss() : SuperbossEncounter
{
    public override Superboss Kind => Superboss.BarrowReaper;
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<BarrowReaper>()];
}

// ═════════════ Акт 2б — Мастер-кузнец Глубин ═════════════

/// <summary>
/// Мастер-кузнец Глубин: 320 ♥, Латы 5. Молот — Раскол 4 + Оглушение героя. Раз в 4 хода Плавильный взрыв — 30 огнём
/// всем, если открыт хоть один вентиль. Атака закрывает вентиль на 2 хода врагов; его также можно уничтожить.
/// Каждые 2 хода паучки чинят 15 ♥ и +1 Латы
/// (Озноб на кузнеце — вдвое меньше).
/// </summary>
public sealed class DeepMasterSmith : SuperbossMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<SteamValve>()];
    public const int Plating = 5;
    public const int HammerDamage = 18;
    public const int HammerSunder = 4;
    public const int SmeltingDamage = 30;
    public const int RepairHeal = 15;

    public override Superboss Kind => Superboss.DeepMasterSmith;
    protected override MonsterModel LookOf => ModelDb.Monster<Fabricator>();
    protected override string? CastAnim => "fabricate";

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("FORGE_HAMMER", Hammer, Atk(HammerDamage), new DebuffIntent()),
        new MoveState("OPEN_VALVES", OpenValves, new SummonIntent(), new HealIntent()),
        new MoveState("FORGE_HAMMER_2", Hammer, Atk(HammerDamage), new DebuffIntent()),
        new MoveState("SMELTING_BLAST", Smelt, new SingleAttackIntent(() => CurrentSmeltingDamage)),
    ];

    public int CurrentSmeltingDamage => BossCounterplayRules.SmeltingDamage(
        Allies.Count(a => a.Monster is SteamValve && !a.HasPower<ValveClosedPower>()), SmeltingDamage);

    private async Task OpenValves(IReadOnlyList<Creature> targets)
    {
        await Cast();
        // Existing valves keep their closure timer; a new cycle cannot silently reopen them or grow past two.
        var missing = Math.Max(0, 2 - Allies.Count(a => a.Monster is SteamValve));
        for (var i = 0; i < missing; i++) await Summon<SteamValve>();
        await Repair();
    }

    private async Task Hammer(IReadOnlyList<Creature> targets)
    {
        await Hit(HammerDamage, fx: "vfx/vfx_attack_blunt");
        await Debuff<SunderPower>(HammerSunder);
        await Debuff<ConcussedPower>(1);
    }

    private async Task Repair()
    {
        var heal = Creature.HasPower<ChillPower>() ? RepairHeal / 2 : RepairHeal;
        await Heal(heal);
        await PowerCmd.Apply<PlatingPower>(Ctx, Creature, 1, Creature, null);
    }

    private async Task Smelt(IReadOnlyList<Creature> targets)
    {
        var damage = CurrentSmeltingDamage; // exactly the same open/closed query as the intent
        if (damage > 0) await Blast(damage, DamageType.Fire);
        await Repair();
    }
}

/// <summary>Вентиль: атака по нему закрывает на два полных действия стороны врагов. Multihit лишь обновляет срок.</summary>
public sealed class SteamValve : RequiemMinion
{
    public override EnemyRole CombatRole => EnemyRole.Prop;
    protected override int Hp => 14;
    protected override MonsterModel LookOf => ModelDb.Monster<Noisebot>();
    protected override IReadOnlyList<MoveState> Cycle() => [new MoveState("HISS", _ => Task.CompletedTask, new SleepIntent())];

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Creature || !Creature.IsAlive || dealer is not { IsPlayer: true }
            || cardSource?.Type != CardType.Attack || !props.IsCardOrMonsterMove()) return;
        var current = Creature.GetPowerAmount<ValveClosedPower>();
        var desired = BossCounterplayRules.RefreshClosure(current);
        if (current != desired) await PowerCmd.Apply<ValveClosedPower>(choiceContext, Creature, desired - current, dealer, cardSource);
    }
}

public sealed class DeepMasterSmithBoss() : SuperbossEncounter
{
    public override Superboss Kind => Superboss.DeepMasterSmith;
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<DeepMasterSmith>()];
}

// ═════════════ Акт 3 — Посланник Разрушителя Врат ═════════════

/// <summary>
/// Посланник Разрушителя Врат: 420 ♥, Латы 3. Молот Бездны — Раскол 4 и Пробитие 4. Огненный круг: герой, не сыгравший
/// за ход ни одного навыка, получает Горение 8. Врата каждые 3 хода; дважды при ♥ &lt; 30 % — полное лечение, если на поле есть Врата.
/// </summary>
public sealed class GatebreakerEnvoy : SuperbossMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<AbyssGateObject>(), ModelDb.Monster<SolarStandard>()];
    public const int Plating = 3;
    public const int HammerDamage = 20;
    public const int HammerSunder = 4;
    public const int HammerPierce = 4;
    public const int CircleBurning = 8;
    public const int MaxFullHeals = 2;

    public override Superboss Kind => Superboss.GatebreakerEnvoy;
    protected override MonsterModel LookOf => ModelDb.Monster<MagiKnight>();
    protected override string AttackAnim => "RamAttack";
    protected override string? CastAnim => "BombCast";

    private HashSet<ulong> _sidestepped = [];
    private int _fullHeals;
    public bool HasStandard => Creature.IsAlive && Allies.Any(a => a.Monster is SolarStandard);
    public override bool NeedsCounter(Type type) => type == typeof(EnvoySidestep) || type == typeof(CarrySolarStandard) && HasStandard;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _sidestepped = [];
        _fullHeals = 0;
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await Buff<ArmorPiercingPower>(HammerPierce);
        await Summon<SolarStandard>();
    }

    protected override void PrepareCountersForTurn(Player player, int window)
    {
        RequestCounter<EnvoySidestep>(window, player);
        if (HasStandard) RequestCounter<CarrySolarStandard>(window, player);
    }

    public void Sidestep(Player player) => _sidestepped.Add(player.NetId);

    public async Task CarryStandard(PlayerChoiceContext ctx, Creature hero, CardModel card)
    {
        if (!HasStandard || hero.Player == null || !Heroes.Contains(hero)) return;
        await PowerCmd.Apply<SolarStandardGuardPower>(ctx, hero, 1, Creature, card);
        await KillMinions<AbyssGateObject>();
    }

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("ABYSS_HAMMER", Hammer, Atk(HammerDamage), new DebuffIntent()),
        new MoveState("ABYSS_HAMMER_2", Hammer, Atk(HammerDamage), new DebuffIntent()),
        new MoveState("OPEN_GATE", async _ => { await Cast(); await Summon<AbyssGateObject>(); }, new SummonIntent()),
    ];

    private async Task Hammer(IReadOnlyList<Creature> targets)
    {
        await Hit(HammerDamage, type: DamageType.Magic, fx: "vfx/vfx_attack_blunt");
        await Debuff<SunderPower>(HammerSunder);
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // The issued Skill records success only AFTER its exact stamina payment. Failed payment/replay is
        // not an ordinary free Skill fallback. Other Skills deliberately remain a second way to leave the circle.
        if (cardPlay.Card.Type == CardType.Skill && cardPlay.Card is not EnvoySidestep
            && Creature.CombatState?.Players.Contains(cardPlay.Card.Owner) == true)
            _sidestepped.Add(cardPlay.Card.Owner.NetId);
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Player || !Creature.IsAlive) return;
        foreach (var hero in Heroes.ToList())
            if (!_sidestepped.Contains(hero.Player!.NetId))
                await PowerCmd.Apply<BurningPower>(choiceContext, hero, CircleBurning, Creature, null);
        _sidestepped.Clear();
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Creature || !Creature.IsAlive || _fullHeals >= MaxFullHeals || !BelowPercent(30)) return;
        if (!Allies.Any(a => a.Monster is AbyssGateObject)) return;
        _fullHeals++;
        await Heal(Creature.MaxHp - Creature.CurrentHp);
    }
}

/// <summary>Visible, destructible standard. Never takes a boss's resources/HP overlay or attacks the heroes.</summary>
public sealed class SolarStandard : RequiemMinion
{
    public override EnemyRole CombatRole => EnemyRole.Prop;
    protected override int Hp => 30;
    protected override MonsterModel LookOf => ModelDb.Monster<Noisebot>();
    protected override IReadOnlyList<MoveState> Cycle() => [new MoveState("SUN_BANNER", _ => Task.CompletedTask, new SleepIntent())];
}

public sealed class GatebreakerEnvoyBoss() : SuperbossEncounter
{
    public override Superboss Kind => Superboss.GatebreakerEnvoy;
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<GatebreakerEnvoy>()];
}

// ═════════════ Акт 4а — Владыка Кровавого Двора ═════════════

/// <summary>
/// Владыка Кровавого Двора: 450 ♥ (−10 %, если в этом акте побеждена Невеста). Каждый 2-й ход — Туман (атаки мимо, кроме Света
/// и площадных заклинаний); Регенерация 10 (Горение снимает); на 70/40/20 % — кокон: Неуязвим, пока живы 3 слуги-гаргульи.
/// </summary>
public sealed class BloodCourtLord : SuperbossMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<GargoyleServant>()];
    public const int Regeneration = 10;
    public const int ClawDamage = 12;
    public const int ClawHits = 2;
    public const int DrainDamage = 18;
    public const int BridePenaltyPercent = 10;
    public static readonly int[] CocoonThresholds = [70, 40, 20];

    public override Superboss Kind => Superboss.BloodCourtLord;
    protected override MonsterModel LookOf => ModelDb.Monster<TheForgotten>();

    private int _cocoons;
    public bool Cocooned => Creature.IsAlive && Creature.HasPower<InvulnerablePower>();
    public override bool NeedsCounter(Type type) => type == typeof(CocoonSunbeam) && Cocooned;

    public async Task BreakCocoon()
    {
        if (Creature.GetPower<InvulnerablePower>() is { } cocoon) await PowerCmd.Remove(cocoon);
        RefreshLook();
        await FlushCounters();
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (Creature.CombatState?.RunState is { } run && ActRunState.BrideDefeated(run, run.CurrentActIndex))
        {
            var hp = Creature.MaxHp * (100 - BridePenaltyPercent) / 100;
            await CreatureCmd.SetMaxAndCurrentHp(Creature, hp);
        }
    }

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("BLOOD_CLAWS", _ => Hit(ClawDamage, ClawHits), Multi(ClawDamage, ClawHits)),
        new MoveState("MIST_FORM", async _ => { await Cast(); await Buff<MistFormPower>(1); }, new BuffIntent()),
        new MoveState("SANGUINE_DRAIN", async _ =>
        {
            var before = Heroes.Sum(h => h.CurrentHp);
            await Hit(DrainDamage, type: DamageType.Magic, fx: null);
            await Heal(Math.Max(0, before - Heroes.Sum(h => h.CurrentHp)));
        }, Atk(DrainDamage), new HealIntent()),
        new MoveState("MIST_FORM_2", async _ => { await Cast(); await Buff<MistFormPower>(1); }, new BuffIntent()),
    ];

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Enemy || !Creature.IsAlive) return;
        if (!Creature.HasPower<BurningPower>()) await Heal(Regeneration);
        if (Creature.GetPower<InvulnerablePower>() is { } cocoon && !Allies.Any(a => a.Monster is GargoyleServant))
        {
            await PowerCmd.Remove(cocoon);
            RefreshLook();
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, CardModel? source, bool wasRemovalPrevented, float deathAnimLength)
    {
        await base.AfterDeath(choiceContext, creature, source, wasRemovalPrevented, deathAnimLength);
        if (!wasRemovalPrevented && Creature.IsAlive && creature.Monster is GargoyleServant
            && !Allies.Any(a => a.Monster is GargoyleServant) && Creature.GetPower<InvulnerablePower>() is { } cocoon)
        {
            await PowerCmd.Remove(cocoon); // open immediately, not an unexplained extra enemy turn of immunity
            RefreshLook();
        }
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // A zero packet into the current immunity cannot consume another threshold/issue another phase card.
        // Multiple thresholds crossed in one hit remain finite: the next positive hit after opening starts the next cocoon.
        if (target != Creature || !Creature.IsAlive || _cocoons >= CocoonThresholds.Length
            || !BossCounterplayRules.CanStartCocoon(Cocooned, result.UnblockedDamage, Creature.CurrentHp, Creature.MaxHp, CocoonThresholds[_cocoons])) return;
        _cocoons++;
        var missing = BossCounterplayRules.MissingCocoonGuards(Allies.Count(a => a.Monster is GargoyleServant));
        for (var i = 0; i < missing; i++) await Summon<GargoyleServant>();
        await PowerCmd.Apply<InvulnerablePower>(choiceContext, Creature, 1, Creature, null);
        RequestCounter<CocoonSunbeam>(NextCounterWindow());
        RefreshLook();
        await FlushCounters();
    }
}

public sealed class GargoyleServant : RequiemMinion
{
    public const int RakeDamage = 6;
    protected override int Hp => 20;
    protected override MonsterModel LookOf => ModelDb.Monster<Byrdonis>();
    protected override string? CastAnim => null;
    protected override IReadOnlyList<MoveState> Cycle() => [new MoveState("STONE_RAKE", _ => Hit(RakeDamage), Atk(RakeDamage))];
}

public sealed class BloodCourtLordBoss() : SuperbossEncounter
{
    public override Superboss Kind => Superboss.BloodCourtLord;
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<BloodCourtLord>()];
}

// ═════════════ Акт 4б — Первый Призыватель Чёрной Книги ═════════════

/// <summary>
/// Первый Призыватель: 430 ♥. Цепь молний каждые 2 хода (8 ×3 разрядом, выжигание 2 ✦ за удар); каждое попадание —
/// Слово подчинения (−I сопротивления магии за попадание, максимум −III); аура −I сопротивлений. Три видимых начальных яйца:
/// на ≤30 % съедает одно и полностью лечится, не более трёх раз. Один культист в начале, второй — когда освободится место.
/// </summary>
public sealed class FirstSummoner : SuperbossMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<CultistThrall>(), ModelDb.Monster<DragonEgg>()];
    public const int ChainDamage = 8;
    public const int ChainHits = 3;
    public const int ChainManaBurn = 2;
    public const int TomeDamage = 15;
    public const int MaxDevours = BossCounterplayRules.SummonerEggs; // all three are visible/attackable before the first threshold

    public override Superboss Kind => Superboss.FirstSummoner;
    protected override MonsterModel LookOf => ModelDb.Monster<KinPriest>();
    protected override string AttackAnim => "AttackLaser";
    protected override string? CastAnim => "Rally";

    private int _devours;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        for (var i = 0; i < BossCounterplayRules.SummonerEggs; i++) await Summon<DragonEgg>();
        await Summon<CultistThrall>(); // boss + three eggs + one cultist = the five-body limit
        await PowerCmd.Apply<BlackBookAuraPower>(Ctx, Creature, 1, Creature, null);
    }

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("CHAIN_LIGHTNING", Chain, Multi(ChainDamage, ChainHits), new DebuffIntent()),
        new MoveState("BLACK_TOME", async _ =>
        {
            await Hit(TomeDamage, type: DamageType.Magic, fx: null);
            await Debuff<WordOfBindingPower>(1);
            if (Allies.Count(a => a.Monster is CultistThrall) < BossCounterplayRules.SummonerCultists) await Summon<CultistThrall>();
        }, Atk(TomeDamage), new SummonIntent()),
    ];

    private async Task Chain(IReadOnlyList<Creature> targets)
    {
        for (var i = 0; i < ChainHits; i++)
        {
            await Hit(ChainDamage, type: DamageType.Shock, fx: null);
            await DrainMana(ChainManaBurn);
            await Debuff<WordOfBindingPower>(1); // one word per declared hit, not one per three-hit command
        }
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Creature || !Creature.IsAlive || !BossCounterplayRules.CanDevour(_devours,
            Allies.Count(a => a.Monster is DragonEgg), Creature.CurrentHp, Creature.MaxHp)) return;
        var egg = Allies.FirstOrDefault(a => a.Monster is DragonEgg);
        if (egg == null) return;
        await CreatureCmd.Kill(egg);
        if (egg.IsAlive || !Creature.IsAlive) return; // a prevented sacrifice never buys a free full heal
        _devours++;
        await Heal(Creature.MaxHp - Creature.CurrentHp);
    }
}

public sealed class DragonEgg : RequiemMinion
{
    public override EnemyRole CombatRole => EnemyRole.Prop;
    protected override int Hp => 25;
    protected override MonsterModel LookOf => ModelDb.Monster<ToughEgg>();
    protected override string? CastAnim => null;
    protected override IReadOnlyList<MoveState> Cycle() => [new MoveState("SLUMBER", _ => Task.CompletedTask, new SleepIntent())];
}

public sealed class CultistThrall : RequiemMinion
{
    public const int DaggerDamage = 6;
    protected override int Hp => 24;
    protected override MonsterModel LookOf => ModelDb.Monster<DampCultist>();
    protected override IReadOnlyList<MoveState> Cycle() =>
        [new MoveState("RITUAL_DAGGER", _ => Hit(DaggerDamage), Atk(DaggerDamage)), new MoveState("CHANT", async _ => { await Cast(); await BuffAllies<V.StrengthPower>(1); }, new BuffIntent())];
}

public sealed class FirstSummonerBoss() : SuperbossEncounter
{
    public override Superboss Kind => Superboss.FirstSummoner;
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<FirstSummoner>()];
}

// ═════════════ Акт 5 — Пожиратель Миров ═════════════

/// <summary>
/// Пожиратель Миров: крылья → укус → предупреждающий вдох → метеоры (4/8/12).
/// Выдаваемые Кличи: Сорвать крыло — два хода на земле; Ясное небо — отмена ближайших метеоров.
/// На 90/65/50/25 % — жрецы. Живой жрец на ходу хозяина даёт лечение и призыв призрака (не сверх лимита поля).
/// </summary>
public sealed class WorldEater : SuperbossMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<FacelessPriest>(), ModelDb.Monster<DevouredSoulGhost>()];
    public const int Plating = 2;
    public const int MeteorDamage = 25;
    public const int BiteDamage = 22;
    public const int WingDamage = 9;
    public const int WingHits = 3;
    public const int SoulDevour = 20;
    public static readonly int[] PriestPhases = [90, 65, 50, 25];

    public override Superboss Kind => Superboss.WorldEater;
    protected override MonsterModel LookOf => ModelDb.Monster<Byrdonis>();
    protected override string? CastAnim => null;

    private int _phase;
    private bool _meteorCleared;
    private bool _skiesUnlocked;
    public bool Flying => Creature.IsAlive && Creature.HasPower<FlightPower>();
    // Removing the cosmetic warning power alone must not disable the supplied answer to a warned intent.
    public bool MeteorThreat => Creature.IsAlive && !_meteorCleared
        && (Creature.HasPower<MeteorWarningPower>() || NextMove?.Id == "METEOR_RAIN");
    public int CurrentMeteorDamage => BossCounterplayRules.MeteorDamage(_meteorCleared, MeteorDamage);
    public override bool NeedsCounter(Type type) => type == typeof(TearTheWing) || type == typeof(ClearTheSkies) && _skiesUnlocked;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<FlightPower>(Ctx, Creature, 1, Creature, null);
    }

    protected override void PrepareCountersForTurn(Player player, int window)
    {
        RequestCounter<TearTheWing>(window, player);
        if (_skiesUnlocked) RequestCounter<ClearTheSkies>(window, player);
    }

    protected override IReadOnlyList<MoveState> Cycle() => Enumerable.Range(1, BossCounterplayRules.WorldEaterCycleLength)
        .Select(turn => BossCounterplayRules.WorldEaterMoveAt(turn) switch
        {
            WorldEaterMove.Wings => new MoveState("WING_STORM", _ => Hit(WingDamage, WingHits), Multi(WingDamage, WingHits)),
            WorldEaterMove.Bite => new MoveState("DEVOURING_BITE", _ => Hit(BiteDamage), Atk(BiteDamage)),
            WorldEaterMove.Breath => new MoveState("DEEP_BREATH", WarnMeteors, new DefendIntent(), new BuffIntent()),
            _ => new MoveState("METEOR_RAIN", Meteors, new SingleAttackIntent(() => CurrentMeteorDamage)),
        }).ToList();

    private async Task WarnMeteors(IReadOnlyList<Creature> targets)
    {
        _meteorCleared = false;
        _skiesUnlocked = true;
        await Block(20);
        await PowerCmd.Apply<MeteorWarningPower>(Ctx, Creature, 1, Creature, null);
        RequestCounter<ClearTheSkies>(NextCounterWindow());
        await FlushCounters(); // outside a player's Play phase this stays pending until their normal turn setup
    }

    private async Task Meteors(IReadOnlyList<Creature> targets)
    {
        var damage = CurrentMeteorDamage; // same cancellation query as the displayed intent
        if (damage > 0) await Blast(damage, DamageType.Fire);
        if (Creature.GetPower<MeteorWarningPower>() is { } warning) await PowerCmd.Remove(warning);
        _meteorCleared = false;
    }

    public async Task ClearMeteors(PlayerChoiceContext ctx)
    {
        if (!MeteorThreat) return;
        _meteorCleared = true;
        if (Creature.GetPower<MeteorWarningPower>() is { } warning) await PowerCmd.Remove(warning);
        Creature.PrepareForNextTurn(Heroes, rollNewMove: false);
    }

    public async Task Ground(PlayerChoiceContext ctx)
    {
        if (!Flying) return;
        if (Creature.GetPower<FlightPower>() is { } flight) await PowerCmd.Remove(flight);
        var current = Creature.GetPowerAmount<GroundedByShoutPower>();
        if (current != BossCounterplayRules.GroundedTurns)
            await PowerCmd.Apply<GroundedByShoutPower>(ctx, Creature, BossCounterplayRules.GroundedTurns - current, Creature, null);
        RefreshLook();
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || !Creature.IsAlive || Creature.GetPower<GroundedByShoutPower>() is not { } grounded) return;
        if (BossCounterplayRules.TurnsAfterEnemyAction(grounded.Amount) == 0)
        {
            await PowerCmd.Remove(grounded);
            await PowerCmd.Apply<FlightPower>(choiceContext, Creature, 1, Creature, null);
            RefreshLook();
        }
        else await PowerCmd.Decrement(grounded);
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        await base.AfterSideTurnStart(side, participants, combatState);
        if (side != CombatSide.Enemy || !Creature.IsAlive) return;
        if (Allies.Any(a => a.Monster is FacelessPriest))
        {
            await Heal(SoulDevour);
            await Summon<DevouredSoulGhost>();
        }
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Creature || !Creature.IsAlive || result.UnblockedDamage <= 0) return;
        while (_phase < PriestPhases.Length && BelowPercent(PriestPhases[_phase]))
        {
            _phase++;
            await Summon<FacelessPriest>();
            await Summon<FacelessPriest>();
            _skiesUnlocked = true;
            RequestCounter<ClearTheSkies>(NextCounterWindow());
        }
        await FlushCounters();
    }
}

/// <summary>Finite field body from Soul Devouring, not another priest or a hidden source of healing.</summary>
public sealed class DevouredSoulGhost : RequiemMinion
{
    public const int TouchDamage = 6;
    protected override int Hp => 20;
    protected override MonsterModel LookOf => ModelDb.Monster<Parafright>();
    protected override string? CastAnim => null;
    protected override IReadOnlyList<MoveState> Cycle() =>
        [new MoveState("DEVOUR_GHOST_TOUCH", _ => Hit(TouchDamage, type: DamageType.Magic, fx: null), Atk(TouchDamage))];
}

public sealed class FacelessPriest : RequiemMinion
{
    public const int WardBlock = 8;
    public const int DrainDamage = 5;
    protected override int Hp => 40;
    protected override MonsterModel LookOf => ModelDb.Monster<CalcifiedCultist>();
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<SpellShieldPower>(Ctx, Creature, WardBlock, Creature, null);
    }
    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("FACELESS_WARD", async _ =>
        {
            var lord = Allies.FirstOrDefault(a => a.Monster is WorldEater);
            if (lord != null) await CreatureCmd.GainBlock(lord, WardBlock, ValueProp.Move, null);
            await Block(WardBlock);
            await PowerCmd.Apply<SpellShieldPower>(Ctx, Creature, WardBlock, Creature, null);
        }, new DefendIntent()),
        new MoveState("SOUL_DRAIN", _ => Hit(DrainDamage, 2, DamageType.Magic, null), Multi(DrainDamage, 2)),
    ];
}

public sealed class WorldEaterBoss() : SuperbossEncounter
{
    public override Superboss Kind => Superboss.WorldEater;
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<WorldEater>()];
}

// ═════════════ ★ Тень Без Имени ═════════════

/// <summary>
/// Тень Без Имени: 700 ♥, Неуязвима, пока не убита душа: раз в 2 хода — 2 души (12 ♥); убитая душа снимает неуязвимость на ход.
/// Душа, прожившая 3 хода, взрывается бурей/бураном/параличом. Каждый удар Тени — −5 ко всем шкалам героя (♥, ◆, ✦).
/// </summary>
public sealed class NamelessShadow : SuperbossMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<ShadowSoul>()];
    public const int ScaleDrain = 5;
    public const int UmbraDamage = 15;
    public const int UmbraHits = 2;

    public override Superboss Kind => Superboss.NamelessShadow;
    protected override MonsterModel LookOf => ModelDb.Monster<TheForgotten>();

    private int _exposedTurns;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<InvulnerablePower>(Ctx, Creature, 1, Creature, null);
    }

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("CALL_SOULS", async _ => { await Cast(); await Summon<ShadowSoul>(); await Summon<ShadowSoul>(); }, new SummonIntent()),
        new MoveState("UMBRA_STRIKE", Umbra, Multi(UmbraDamage, UmbraHits), new DebuffIntent()),
    ];

    private async Task Umbra(IReadOnlyList<Creature> targets)
    {
        for (var i = 0; i < UmbraHits; i++)
        {
            await Hit(UmbraDamage, type: DamageType.Magic, fx: null);
            await DrainStamina(ScaleDrain);
            await DrainMana(ScaleDrain);
            foreach (var hero in Heroes.ToList())
                if (!await SpellInterception.InterceptEffect(Ctx, hero, Creature, null))
                {
                    using var drain = DamageTyping.As(DamageType.True);
                    await CreatureCmd.Damage(Ctx, hero, ScaleDrain, ValueProp.Unblockable | ValueProp.Unpowered, Creature, null, null);
                }
        }
    }

    /// <summary>Убита душа — неуязвимость спадает на ход.</summary>
    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, CardModel? source, bool wasRemovalPrevented, float deathAnimLength)
    {
        await base.AfterDeath(choiceContext, creature, source, wasRemovalPrevented, deathAnimLength);
        if (creature == Creature || creature.Monster is not ShadowSoul { Expired: false } || wasRemovalPrevented || !Creature.IsAlive) return;
        _exposedTurns = 1;
        if (Creature.GetPower<InvulnerablePower>() is { } inv) await PowerCmd.Remove(inv);
        RefreshLook();
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Player || !Creature.IsAlive) return;
        if (_exposedTurns > 0 && --_exposedTurns == 0 && !Creature.HasPower<InvulnerablePower>())
        {
            await PowerCmd.Apply<InvulnerablePower>(choiceContext, Creature, 1, Creature, null);
            RefreshLook();
        }
    }
}

/// <summary>Душа (12 ♥): через 3 хода взрывается — буря (разряд), буран (холод) или паралич, по своему цвету.</summary>
public sealed class ShadowSoul : RequiemMinion
{
    public override EnemyRole CombatRole => EnemyRole.Prop;
    public const int ExplosionDamage = 12;
    public const int LifeTurns = 3;
    protected override int Hp => 12;
    protected override MonsterModel LookOf => ModelDb.Monster<Parafright>();
    protected override string? CastAnim => null;

    private int _color;
    public bool Expired { get; private set; }
    public override DamageType CurrentAttackType => _color switch { 0 => DamageType.Shock, 1 => DamageType.Frost, _ => DamageType.Magic };

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _color = Creature.CombatState?.RunState?.Rng.Niche.NextInt(3) ?? 0; // chosen before the first telegraph, never on impact
    }

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("SOUL_GATHER", _ => Task.CompletedTask, new SleepIntent()),
        new MoveState("SOUL_GATHER_2", _ => Task.CompletedTask, new SleepIntent()),
        new MoveState("SOUL_BURST", Burst, Atk(ExplosionDamage), new DebuffIntent()),
    ];

    private async Task Burst(IReadOnlyList<Creature> targets)
    {
        Expired = true; // a timed-out soul is not a player kill and must not open its master's immunity
        switch (_color)
        {
            case 0: await Blast(ExplosionDamage, DamageType.Shock); await Debuff<StaticPower>(3); break;
            case 1: await Blast(ExplosionDamage, DamageType.Frost); await Debuff<ChillPower>(4); break;
            default: await Blast(ExplosionDamage, DamageType.Magic); await Debuff<ConcussedPower>(1); break;
        }
        await CreatureCmd.Kill(Creature);
    }
}

/// <summary>Главный босс Безымянного предела (не через портал; в пул предела добавлен явно).</summary>
public sealed class NamelessShadowBoss() : SuperbossEncounter
{
    public override Superboss Kind => Superboss.NamelessShadow;
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<NamelessShadow>()];
}

/// <summary>Элита-эхо Безымянного предела: отзвук Чёрного Странника (150 ♥), свой спрайт — призрачный двойник Странника.</summary>
public sealed class EchoKnight : OwnArtMonster
{
    public override string ArtKey => SuperbossLookRules.EchoKnightArt;
    public const int EchoDamage = 12;
    public const int EchoHits = 2;
    protected override int Hp => 150;
    protected override MonsterModel LookOf => ModelDb.Monster<MysteriousKnight>();
    protected override string? CastAnim => null;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<GhostlyPower>(Ctx, Creature, 1, Creature, null);
    }

    public override DamageType CurrentAttackType => NextMove?.Id == "ECHO_FLURRY" ? WeakestElement() : base.CurrentAttackType;

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("ECHO_FLURRY", async _ => { var e = EnemyActionBudget.TypeOf(Creature, CurrentAttackType); await Hit(EchoDamage, EchoHits, e); await ElementStatus(e, 3, 2, 2); },
            Multi(EchoDamage, EchoHits), new DebuffIntent()),
        new MoveState("ECHO_GUARD", _ => Block(20), new DefendIntent()),
    ];
}

public sealed class EchoKnightElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<EchoKnight>()];
}
