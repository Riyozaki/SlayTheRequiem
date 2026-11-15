using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using V = MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

// Порождения Бездны (Врата Бездны актов 3 и 5) и элиты мода в ванильных Улье и Славе (docs/design/11 §3, §5).

// ═════════════ Порождения Бездны ═════════════

/// <summary>Дремора: солдат Бездны — магический клинок и щит.</summary>
public sealed class Dremora : RequiemMinion
{
    public const int BladeDamage = 9;
    public const int ShieldBlock = 8;
    protected override int Hp => 34;
    protected override MonsterModel LookOf => ModelDb.Monster<FlailKnight>();
    protected override string AttackAnim => "FlailAttack";

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("ABYSS_BLADE", _ => Hit(BladeDamage, type: DamageType.Magic), Atk(BladeDamage)),
        new MoveState("ABYSS_SHIELD", _ => Block(ShieldBlock), new DefendIntent()),
    ];
}

/// <summary>Скамп Бездны: мелкий, Призрачный, бросает огонь.</summary>
public sealed class AbyssScamp : RequiemMinion
{
    public const int FireboltDamage = 5;
    protected override int Hp => 16;
    protected override MonsterModel LookOf => ModelDb.Monster<Parafright>();
    protected override string? CastAnim => null;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<GhostlyPower>(Ctx, Creature, 1, Creature, null);
    }

    protected override IReadOnlyList<MoveState> Cycle() =>
        [new MoveState("FIREBOLT", _ => Hit(FireboltDamage, 2, DamageType.Fire, null), Multi(FireboltDamage, 2))];
}

/// <summary>Врата (объект 20 ♥): каждые 2 хода выпускают дремору; закрываются одним ударом ≥ 20 (или убийством).</summary>
public sealed class AbyssGateObject : RequiemMinion
{
    public const int CloseThreshold = 20;
    protected override int Hp => 20;
    protected override MonsterModel LookOf => ModelDb.Monster<SoulNexus>();

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("GATE_HUM", _ => Task.CompletedTask, new SleepIntent()),
        new MoveState("GATE_SPAWN", async _ => { await Cast(); await Summon<Dremora>(); }, new SummonIntent()),
    ];

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Creature && Creature.IsAlive && result.TotalDamage >= CloseThreshold) await CreatureCmd.Kill(Creature);
    }
}

/// <summary>Бой Врат Бездны в обычном узле (Трещины Улья, Вторжение Славы).</summary>
public sealed class AbyssRiftNormal() : RequiemEncounter(RoomType.Monster)
{
    protected override IReadOnlyList<MonsterModel> Lineup =>
        [ModelDb.Monster<AbyssScamp>(), ModelDb.Monster<Dremora>(), ModelDb.Monster<Dremora>()];
}

// ═════════════ Улей ═════════════

/// <summary>Военный вождь Бездны: 165 ♥, раз в 3 хода открывает Врата (объект 20 ♥), выпускающие дремору.</summary>
public sealed class AbyssWarlord : RequiemMonster
{
    public const int CleaveDamage = 15;
    public const int WarCryStrength = 2;
    protected override int Hp => 165;
    protected override MonsterModel LookOf => ModelDb.Monster<MagiKnight>();
    protected override string AttackAnim => "RamAttack";
    protected override string? CastAnim => "BombCast";

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("OPEN_GATE", async _ => { await Cast(); await Summon<AbyssGateObject>(); }, new SummonIntent()),
        new MoveState("ABYSS_CLEAVE", _ => Hit(CleaveDamage, type: DamageType.Magic), Atk(CleaveDamage)),
        new MoveState("WAR_CRY", async _ => { await Buff<V.StrengthPower>(WarCryStrength); await BuffAllies<V.StrengthPower>(WarCryStrength); }, new BuffIntent()),
    ];
}

/// <summary>Матерь Мора: 130 ♥, аура Хвори 1 в начале хода героев; «Рой мух» — статус-карты в колоду героя; уязвима к огню.</summary>
public sealed class PlagueMother : RequiemMonster
{
    public const int AuraBlight = 1;
    public const int SwarmCards = 2;
    public const int RotDamage = 12;
    protected override int Hp => 130;
    protected override MonsterModel LookOf => ModelDb.Monster<CalcifiedCultist>();

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("FLY_SWARM", Swarm, new StatusIntent(SwarmCards)),
        new MoveState("ROT_TOUCH", async _ => { await Hit(RotDamage, type: DamageType.Poison, fx: null); }, Atk(RotDamage)),
    ];

    private async Task Swarm(IReadOnlyList<Creature> targets)
    {
        await Cast();
        foreach (var hero in Heroes.ToList())
            for (var i = 0; i < SwarmCards; i++)
            {
                var card = Creature.CombatState!.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Dazed>(hero.Player);
                await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, hero.Player);
            }
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (!Creature.IsAlive || player.Creature == null || !player.Creature.IsAlive) return;
        await PowerCmd.Apply<BlightPower>(choiceContext, player.Creature, AuraBlight, Creature, null);
    }
}

/// <summary>Пожиратель Роя (редкий): 180 ♥, поедает союзного насекомого — лечится на его ♥ и забирает его силу.</summary>
public sealed class SwarmDevourer : RequiemMonster
{
    public const int GnashDamage = 13;
    public const int DevourStrength = 3;
    protected override int Hp => 180;
    protected override MonsterModel LookOf => ModelDb.Monster<TheInsatiable>();
    protected override string AttackAnim => "Bite";
    protected override string? CastAnim => "Salivate";

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("GNASH", _ => Hit(GnashDamage), Atk(GnashDamage)),
        new MoveState("DEVOUR", Devour, new HealIntent(), new BuffIntent()),
        new MoveState("GNASH_2", _ => Hit(GnashDamage), Atk(GnashDamage)),
    ];

    private async Task Devour(IReadOnlyList<Creature> targets)
    {
        await Cast();
        var prey = Allies.OrderByDescending(a => a.CurrentHp).FirstOrDefault();
        if (prey == null) { await Block(15); return; }
        var hp = prey.CurrentHp;
        await CreatureCmd.Kill(prey);
        await Heal(hp);
        await Buff<V.StrengthPower>(DevourStrength);
    }
}

public sealed class SwarmlingBug : RequiemMinion
{
    public const int StingDamage = 4;
    protected override int Hp => 14;
    protected override MonsterModel LookOf => ModelDb.Monster<Inklet>();
    protected override string? CastAnim => null;

    protected override IReadOnlyList<MoveState> Cycle() => [new MoveState("STING", _ => Hit(StingDamage, 2), Multi(StingDamage, 2))];
}

public sealed class AbyssWarlordElite() : RequiemEncounter(RoomType.Elite)
{
    protected override Func<ActModel, bool> ValidFor => act => act is Hive;
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<Dremora>(), ModelDb.Monster<AbyssWarlord>()];
}

public sealed class PlagueMotherElite() : RequiemEncounter(RoomType.Elite)
{
    protected override Func<ActModel, bool> ValidFor => act => act is Hive;
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<PlagueMother>()];
}

public sealed class SwarmDevourerElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup =>
        [ModelDb.Monster<SwarmlingBug>(), ModelDb.Monster<SwarmDevourer>(), ModelDb.Monster<SwarmlingBug>()];
}

// ═════════════ Слава ═════════════

/// <summary>Жрец Кургана: 170 ♥, маска задаёт стихию ударов и ауру; щит-фантом (Призрачность), который снимает Свет.</summary>
public sealed class BarrowPriest : RequiemMonster
{
    public const int MaskBoltDamage = 8;
    public const int MaskBoltHits = 2;
    public const int AuraDamage = 3;
    protected override int Hp => 170;
    protected override MonsterModel LookOf => ModelDb.Monster<OwlMagistrate>();
    protected override string? CastAnim => null;

    private DamageType _mask = DamageType.Fire;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        var state = Creature.CombatState;
        var roll = state?.RunState?.Rng.Niche.NextInt(3) ?? 0;
        _mask = roll switch { 0 => DamageType.Fire, 1 => DamageType.Frost, _ => DamageType.Shock };
        await PowerCmd.Apply<GhostlyPower>(Ctx, Creature, 1, Creature, null);
    }

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("MASK_BOLT", async _ => { await Hit(MaskBoltDamage, MaskBoltHits, _mask, null); await ElementStatus(_mask, 3, 2, 2); },
            Multi(MaskBoltDamage, MaskBoltHits), new DebuffIntent()),
        new MoveState("MASK_AURA", async _ => { await Blast(AuraDamage, _mask); await Block(14); }, new DefendIntent()),
    ];

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Creature && Creature.GetPower<GhostlyPower>() is { } shield &&
            Rules.DamageTyping.Resolve(props, dealer, cardSource) == DamageType.Light)
            await PowerCmd.Remove(shield);
    }
}

/// <summary>Чемпион Костяного Трона: 180 ♥, Цепная молния (3 разрядом всем каждый ход); на 50 % неуязвим, пока живы 2 Щитоносца.</summary>
public sealed class BoneChampion : RequiemMonster
{
    public const int ChainLightning = 3;
    public const int GreatswordDamage = 18;
    protected override int Hp => 180;
    protected override MonsterModel LookOf => ModelDb.Monster<MysteriousKnight>();
    protected override string? CastAnim => null;

    private bool _shielded;

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("GREATSWORD", _ => Hit(GreatswordDamage), Atk(GreatswordDamage)),
        new MoveState("BONE_WALL", _ => Block(18), new DefendIntent()),
    ];

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Enemy || !Creature.IsAlive) return;
        await Blast(ChainLightning, DamageType.Shock);
        if (_shielded && !Allies.Any(a => a.Monster is BoneShieldbearer) && Creature.GetPower<InvulnerablePower>() is { } inv)
            await PowerCmd.Remove(inv);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Creature || !Creature.IsAlive || _shielded || !BelowPercent(50)) return;
        _shielded = true;
        await Summon<BoneShieldbearer>();
        await Summon<BoneShieldbearer>();
        await PowerCmd.Apply<InvulnerablePower>(choiceContext, Creature, 1, Creature, null);
    }
}

public sealed class BoneShieldbearer : RequiemMinion
{
    public const int BashDamage = 6;
    protected override int Hp => 30;
    protected override MonsterModel LookOf => ModelDb.Monster<FossilStalker>();

    protected override IReadOnlyList<MoveState> Cycle() =>
        [new MoveState("SHIELD_BASH", _ => Hit(BashDamage, fx: "vfx/vfx_attack_blunt"), Atk(BashDamage))];
}

/// <summary>Чёрный Рыцарь Бездны (редкий): 200 ♥, каждым ударом Раскол 2; копирует Знамение героя — здесь: +Сила по числу Знамений отряда.</summary>
public sealed class AbyssBlackKnight : RequiemMonster
{
    public const int SunderingDamage = 12;
    public const int SunderingSunder = 2;
    protected override int Hp => 200;
    protected override MonsterModel LookOf => ModelDb.Monster<MagiKnight>();
    protected override string AttackAnim => "ShieldAttack";
    protected override string? CastAnim => "BombCast";

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("MIRROR_OMEN", async _ =>
        {
            await Cast();
            var omens = Heroes.Count(h => h.Player?.Relics.OfType<Pantheon.OmenRelic>().Any() == true);
            await Buff<V.StrengthPower>(2 + omens);
        }, new BuffIntent()),
        new MoveState("SUNDERING_BLOW", async _ => { await Hit(SunderingDamage); await Debuff<SunderPower>(SunderingSunder); }, Atk(SunderingDamage), new DebuffIntent()),
        new MoveState("SUNDERING_BLOW_2", async _ => { await Hit(SunderingDamage); await Debuff<SunderPower>(SunderingSunder); }, Atk(SunderingDamage), new DebuffIntent()),
    ];
}

public sealed class BarrowPriestElite() : RequiemEncounter(RoomType.Elite)
{
    protected override Func<ActModel, bool> ValidFor => act => act is Glory;
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<BarrowPriest>()];
}

public sealed class BoneChampionElite() : RequiemEncounter(RoomType.Elite)
{
    protected override Func<ActModel, bool> ValidFor => act => act is Glory;
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<BoneChampion>()];
}

public sealed class AbyssBlackKnightElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<AbyssBlackKnight>()];
}
