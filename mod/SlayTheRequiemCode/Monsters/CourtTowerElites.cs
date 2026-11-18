using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Acts;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using V = MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

// Акт 4а — Кровавый Двор и акт 4б — Звёздная Башня (docs/design/11 §5).

// ═════════════ Кровавый Двор ═════════════

/// <summary>Кровавая знать: 150 ♥, вампиризм 50 %, Невидимость каждые 3 хода.</summary>
public sealed class BloodNoble : RequiemMonster
{
    public const int BiteDamage = 14;
    public const int LifestealPercent = 50;
    public const int RapierDamage = 6;
    public const int RapierHits = 3;

    protected override int Hp => 150;
    protected override MonsterModel LookOf => ModelDb.Monster<TheForgotten>();

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("BLOOD_BITE", Bite, Atk(BiteDamage), new HealIntent()),
        new MoveState("RAPIER", _ => Hit(RapierDamage, RapierHits), Multi(RapierDamage, RapierHits)),
        new MoveState("VANISH", async _ => { await Cast(); await Buff<InvisibilityPower>(1); await Block(10); }, new BuffIntent(), new DefendIntent()),
    ];

    private async Task Bite(IReadOnlyList<Creature> targets)
    {
        var before = Heroes.Sum(h => h.CurrentHp);
        await Hit(BiteDamage);
        await Heal(Math.Max(0, before - Heroes.Sum(h => h.CurrentHp)) * LifestealPercent / 100);
    }
}

/// <summary>Гаргулья-Страж: 170 ♥; днём — Окаменение (Латы 6, не атакует), ночью — 3 атаки по 9; Свет снимает окаменение.</summary>
public sealed class GargoyleWarden : RequiemMonster
{
    public const int StonePlating = 6;
    public const int ClawDamage = 9;
    public const int ClawHits = 3;

    protected override int Hp => 170;
    protected override MonsterModel LookOf => ModelDb.Monster<Byrdonis>();
    protected override string? CastAnim => null;

    private bool IsNight => Creature.CombatState?.RunState is { } run && run.CurrentMapCoord is { } c && ActRules.IsNight(c.row);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (!IsNight) await PowerCmd.Apply<PetrifiedPower>(Ctx, Creature, 1, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var stone = new MoveState("STONE_SKIN", _ => PowerCmd.Apply<PlatingPower>(Ctx, Creature, StonePlating, Creature, null), new DefendIntent());
        var claws = new MoveState("NIGHT_CLAWS", _ => Hit(ClawDamage, ClawHits), Multi(ClawDamage, ClawHits));
        var next = new ConditionalBranchState("GARGOYLE_NEXT");
        next.AddState(stone, () => Creature.HasPower<PetrifiedPower>());
        next.AddState(claws, () => !Creature.HasPower<PetrifiedPower>());
        stone.FollowUpState = next;
        claws.FollowUpState = next;
        return new MonsterMoveStateMachine([next, stone, claws], next);
    }
}

/// <summary>Ренегат-Охотник: 120 ♥, арбалет — Раскол 3, серебряные болты; ставит Капканы (Паралич героя при атаке).</summary>
public sealed class VampireHunter : RequiemMonster
{
    public const int BoltDamage = 12;
    public const int BoltSunder = 3;
    public const int TrapDamage = 4;

    protected override int Hp => 120;
    protected override MonsterModel LookOf => ModelDb.Monster<CrossbowRubyRaider>();
    protected override string? CastAnim => null;

    private bool _trapSet;

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("SILVER_BOLT", async _ => { await Hit(BoltDamage); await Debuff<SunderPower>(BoltSunder); }, Atk(BoltDamage), new DebuffIntent()),
        new MoveState("SET_TRAP", async _ => { _trapSet = true; await Block(12); }, new DefendIntent(), new DebuffIntent()),
        new MoveState("SILVER_BOLT_2", async _ => { await Hit(BoltDamage); await Debuff<SunderPower>(BoltSunder); }, Atk(BoltDamage), new DebuffIntent()),
    ];

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Creature || !_trapSet || dealer is not { IsPlayer: true } || !props.IsPoweredAttack()) return;
        _trapSet = false;
        await CreatureCmd.Damage(choiceContext, dealer, TrapDamage, ValueProp.Unpowered, Creature, null);
        await PowerCmd.Apply<ConcussedPower>(choiceContext, dealer, 1, Creature, null);
    }
}

/// <summary>Невеста Владыки (редкая): 140 ♥, вампиризм; при смерти — Владыка в супербою этого акта получает −10 % ♥.</summary>
public sealed class LordsBride : RequiemMonster
{
    public const int KissDamage = 11;
    public const int LifestealPercent = 50;

    protected override int Hp => 140;
    protected override MonsterModel LookOf => ModelDb.Monster<Queen>();

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("CRIMSON_KISS", async _ =>
        {
            var before = Heroes.Sum(h => h.CurrentHp);
            await Hit(KissDamage);
            await Heal(Math.Max(0, before - Heroes.Sum(h => h.CurrentHp)) * LifestealPercent / 100);
        }, Atk(KissDamage), new HealIntent()),
        new MoveState("SWAP_PLACES", async _ => { await Cast(); await BuffAllies<V.StrengthPower>(2); await Block(16); }, new DefendIntent(), new BuffIntent()),
    ];

    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature != Creature || wasRemovalPrevented || Creature.CombatState?.RunState is not { } run) return Task.CompletedTask;
        foreach (var player in run.Players)
            if (ActLedger.Of(player) is { } ledger) ledger.BrideMask |= 1 << run.CurrentActIndex;
        return Task.CompletedTask;
    }
}

public sealed class BloodThrall : RequiemMinion
{
    public const int ClawDamage = 6;
    protected override int Hp => 22;
    protected override MonsterModel LookOf => ModelDb.Monster<TheLost>();
    protected override IReadOnlyList<MoveState> Cycle() => [new MoveState("THRALL_CLAW", _ => Hit(ClawDamage), Atk(ClawDamage))];
}

public sealed class BloodNobleElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<BloodThrall>(), ModelDb.Monster<BloodNoble>()];
}

public sealed class GargoyleWardenElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<GargoyleWarden>()];
}

public sealed class VampireHunterElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<VampireHunter>()];
}

public sealed class LordsBrideElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<BloodThrall>(), ModelDb.Monster<LordsBride>()];
}

// ═════════════ Звёздная Башня ═════════════

/// <summary>
/// Чародейка Илварре: 150 ♥; каждый ход бьёт стихией, к которой у героев меньше всего сопротивления; Оберег (Блок 20)
/// каждые 2 хода; Сопр. магии II (таблица врагов); при ♥ &lt; 30 % лечится на 40 (раз).
/// </summary>
public sealed class SorceressIlvarre : RequiemMonster
{
    public const int SpellDamage = 13;
    public const int WardBlock = 20;
    public const int EmergencyHeal = 40;
    public const int EmergencyPercent = 30;

    protected override int Hp => 150;
    protected override MonsterModel LookOf => ModelDb.Monster<OwlMagistrate>();
    protected override string? CastAnim => null;

    public override DamageType CurrentAttackType => WeakestElement();
    private bool _healed;

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("ADAPTIVE_SPELL", Spell, Atk(SpellDamage), new DebuffIntent()),
        new MoveState("WARD", async _ => { await Block(WardBlock); await Spell(_); }, Atk(SpellDamage), new DefendIntent()),
    ];

    private async Task Spell(IReadOnlyList<Creature> targets)
    {
        var element = EnemyActionBudget.TypeOf(Creature, CurrentAttackType);
        await Hit(SpellDamage, type: element, fx: null);
        await ElementStatus(element, 3, 2, 2);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Creature || !Creature.IsAlive || _healed || !BelowPercent(EmergencyPercent)) return;
        _healed = true;
        await Heal(EmergencyHeal);
    }
}

/// <summary>Отступник-Призыватель: 110 ♥, каждые 2 хода атронах (огонь/лёд/гроза по очереди); смерть призывателя рассеивает их.</summary>
public sealed class RenegadeSummoner : RequiemMonster
{
    public override IEnumerable<MonsterModel> SummonModels => [ModelDb.Monster<FlameAtronachMinion>(), ModelDb.Monster<IceAtronach>(), ModelDb.Monster<StormAtronachMinion>()];
    public const int ArcDamage = 8;
    protected override int Hp => 110;
    protected override MonsterModel LookOf => ModelDb.Monster<DevotedSculptor>();

    private int _next;

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("CONJURE_ATRONACH", Conjure, new SummonIntent()),
        new MoveState("ARC", _ => Hit(ArcDamage, 2, DamageType.Shock, null), Multi(ArcDamage, 2)),
    ];

    private async Task Conjure(IReadOnlyList<Creature> targets)
    {
        await Cast();
        switch (_next++ % 3)
        {
            case 0: await Summon<FlameAtronachMinion>(); break;
            case 1: await Summon<IceAtronach>(); break;
            default: await Summon<StormAtronachMinion>(); break;
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature != Creature || wasRemovalPrevented) return;
        await KillMinions<FlameAtronachMinion>();
        await KillMinions<IceAtronach>();
        await KillMinions<StormAtronachMinion>();
    }
}

public sealed class FlameAtronachMinion : RequiemMinion
{
    public const int FlameDamage = 7;
    protected override int Hp => 26;
    protected override MonsterModel LookOf => ModelDb.Monster<LivingFog>();
    protected override IReadOnlyList<MoveState> Cycle() =>
        [new MoveState("FLAME_LASH", async _ => { await Hit(FlameDamage, type: DamageType.Fire, fx: null); await Debuff<BurningPower>(2); }, Atk(FlameDamage), new DebuffIntent())];
}

public sealed class StormAtronachMinion : RequiemMinion
{
    public const int ShockDamage = 5;
    protected override int Hp => 30;
    protected override MonsterModel LookOf => ModelDb.Monster<GlobeHead>();
    protected override IReadOnlyList<MoveState> Cycle() =>
        [new MoveState("STORM_FIST", async _ => { await Hit(ShockDamage, 2, DamageType.Shock, null); await Debuff<StaticPower>(1); }, Multi(ShockDamage, 2), new DebuffIntent())];
}

/// <summary>Зеркальный Страж: 130 ♥, отражает 50 % урона заклинаний; физический урон по нему ×1.5 — «бой для воина».</summary>
public sealed class MirrorWarden : RequiemMonster
{
    public const int ShardDamage = 7;
    public const int ShardHits = 2;
    public const int BeamDamage = 16;

    protected override int Hp => 130;
    protected override MonsterModel LookOf => ModelDb.Monster<SoulNexus>();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<SpellMirrorPower>(Ctx, Creature, 1, Creature, null);
    }

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("MIRROR_SHARDS", _ => Hit(ShardDamage, ShardHits), Multi(ShardDamage, ShardHits)),
        new MoveState("PRISM_BEAM", _ => Hit(BeamDamage, type: DamageType.Magic, fx: null), Atk(BeamDamage)),
        new MoveState("POLISH", _ => Block(15), new DefendIntent()),
    ];
}

/// <summary>
/// Звёздный Экзаменатор (редкий): 160 ♥; каждые 2 хода задаёт «вопрос» — тип карты (Атака/Навык/Заклинание);
/// сыграли такую до его хода — +2 ✦ герою, нет — Статика 3.
/// </summary>
public sealed class StarExaminer : RequiemMonster
{
    public const int CorrectMana = 2;
    public const int WrongStatic = 3;
    public const int StarfallDamage = 10;

    protected override int Hp => 160;
    protected override MonsterModel LookOf => ModelDb.Monster<GlobeHead>();

    /// <summary>Вопрос: 0 — Атака, 1 — Навык, 2 — Заклинание; −1 — вопроса нет.</summary>
    private int _question = -1;
    private readonly HashSet<ulong> _answered = [];

    public int Question => _question;

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("ASK", async _ => { await Cast(); _question = (_question + 1) % 3; _answered.Clear(); }, new DebuffIntent()),
        new MoveState("GRADE", Grade, Atk(StarfallDamage)),
    ];

    private async Task Grade(IReadOnlyList<Creature> targets)
    {
        foreach (var hero in Heroes.ToList())
        {
            if (_answered.Contains(hero.Player!.NetId)) await Rules.Mana.Gain(hero.Player, CorrectMana);
            else await PowerCmd.Apply<StaticPower>(Ctx, hero, WrongStatic, Creature, null);
        }
        await Hit(StarfallDamage, type: DamageType.Magic, fx: null);
        _answered.Clear();
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        var kind = card is Spells.SpellCard ? 2 : card.Type == CardType.Attack ? 0 : card.Type == CardType.Skill ? 1 : -1;
        if (_question >= 0 && kind == _question) _answered.Add(card.Owner.NetId);
        return Task.CompletedTask;
    }
}

public sealed class SorceressIlvarreElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<SorceressIlvarre>()];
}

public sealed class RenegadeSummonerElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<FlameAtronachMinion>(), ModelDb.Monster<RenegadeSummoner>()];
}

public sealed class MirrorWardenElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<MirrorWarden>()];
}

public sealed class StarExaminerElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<StarExaminer>()];
}
