using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

/// <summary>
/// Основа врагов этапа 7 (элиты актов, супербоссы, свита): облик заимствован у ванильного врага (<see cref="LookOf"/>),
/// ходы — цикл <see cref="Cycle"/> (или своя машина состояний), помощники ударов/статусов/призыва.
/// Числа ходов — константы в классах врагов (docs/design/11 §5–6), чтобы их видели тесты и лок.
/// </summary>
public abstract class RequiemMonster : CustomMonsterModel
{
    /// <summary>♥ (элиты — фиксированные, как в таблице 11 §5; надбавки акта/уровня добавляет EnemySetup).</summary>
    protected abstract int Hp { get; }

    public override int MinInitialHp => Hp;
    public override int MaxInitialHp => Hp;

    /// <summary>Ванильный враг, чьи спрайты и анимации берутся, пока нет своих (этап 8).</summary>
    protected abstract MonsterModel LookOf { get; }

    /// <summary>Имя анимации удара (у большинства ванильных — "Attack").</summary>
    protected virtual string AttackAnim => "Attack";

    /// <summary>Имя анимации «колдовства»; null — у облика её нет.</summary>
    protected virtual string? CastAnim => "Cast";

    public override string? CustomVisualPath => SceneHelper.GetScenePath($"creature_visuals/{LookOf.Id.Entry.ToLowerInvariant()}");

    public override CreatureAnimator? SetupCustomAnimationStates(MegaSprite controller) => LookOf.GenerateAnimator(controller);

    /// <summary>Цикл ходов по кругу (первый — начальный). Для ветвлений переопределите <see cref="GenerateMoveStateMachine"/>.</summary>
    protected virtual IReadOnlyList<MoveState> Cycle() => [];

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var moves = Cycle();
        for (var i = 0; i < moves.Count; i++) moves[i].FollowUpState = moves[(i + 1) % moves.Count];
        return new MonsterMoveStateMachine(moves.Cast<MonsterState>().ToList(), moves[0]);
    }

    // ───── помощники ─────

    protected static PlayerChoiceContext Ctx => new ThrowingPlayerChoiceContext();

    protected IEnumerable<Creature> Heroes => Creature.CombatState?.Players.Select(p => p.Creature).Where(c => c is { IsAlive: true }) ?? [];

    protected IEnumerable<Creature> Allies => Creature.CombatState?.Enemies.Where(e => e.IsAlive && e != Creature) ?? [];

    protected bool BelowPercent(int percent) => Creature.CurrentHp * 100 < Creature.MaxHp * percent;

    /// <summary>Удар по целям намерения (типом <paramref name="type"/>; физический — без обёртки).</summary>
    protected async Task Hit(int damage, int hits = 1, DamageType type = DamageType.Physical, string? fx = "vfx/vfx_attack_slash")
    {
        using var _ = type == DamageType.Physical ? null : DamageTyping.As(type);
        var cmd = DamageCmd.Attack(damage).FromMonster(this).WithAttackerAnim(AttackAnim, 0.35f);
        if (hits > 1) cmd = cmd.WithHitCount(hits).OnlyPlayAnimOnce();
        if (fx != null) cmd = cmd.WithHitFx(fx);
        await cmd.Execute(null);
    }

    /// <summary>Удар по одной цели.</summary>
    protected async Task HitOne(Creature target, int damage, DamageType type = DamageType.Physical)
    {
        using var _ = type == DamageType.Physical ? null : DamageTyping.As(type);
        await DamageCmd.Attack(damage).FromMonster(this).Targeting(target).WithAttackerAnim(AttackAnim, 0.35f)
            .WithHitFx("vfx/vfx_attack_slash").Execute(null);
    }

    /// <summary>Неблокируемый урон всем героям (взрывы, ауры): не атака, аффиксы не срабатывают.</summary>
    protected async Task Blast(int damage, DamageType type)
    {
        using var _ = DamageTyping.As(type);
        foreach (var hero in Heroes.ToList())
            await CreatureCmd.Damage(Ctx, hero, damage, ValueProp.Unpowered, Creature, null);
    }

    protected Task Cast() => CastAnim == null ? Task.CompletedTask : CreatureCmd.TriggerAnim(Creature, CastAnim, 0.3f);

    protected Task Block(int amount) => CreatureCmd.GainBlock(Creature, amount, ValueProp.Move, null);

    protected async Task Buff<T>(int amount) where T : PowerModel =>
        await PowerCmd.Apply<T>(Ctx, Creature, amount, Creature, null);

    protected async Task BuffAllies<T>(int amount) where T : PowerModel
    {
        foreach (var ally in Allies.ToList()) await PowerCmd.Apply<T>(Ctx, ally, amount, Creature, null);
    }

    /// <summary>Статус всем живым героям.</summary>
    protected async Task Debuff<T>(int amount) where T : PowerModel
    {
        foreach (var hero in Heroes.ToList()) await PowerCmd.Apply<T>(Ctx, hero, amount, Creature, null);
    }

    protected async Task DrainMana(int amount)
    {
        foreach (var hero in Heroes.ToList()) await Mana.Lose(hero.Player, amount);
    }

    protected void DrainStamina(int amount)
    {
        foreach (var hero in Heroes.ToList())
            if (RequiemResource.Of<StaminaResource>(hero.Player) is { } stamina)
                stamina.Amount = Math.Max(0, stamina.Amount - amount);
    }

    protected async Task Heal(int amount)
    {
        if (amount > 0 && Creature.IsAlive) await CreatureCmd.Heal(Creature, amount);
    }

    /// <summary>Призыв свиты (не больше <see cref="MaxEnemies"/> врагов на поле).</summary>
    protected async Task<Creature?> Summon<T>() where T : MonsterModel
    {
        var state = Creature.CombatState;
        if (state == null || state.Enemies.Count(e => e.IsAlive) >= MaxEnemies) return null;
        var slot = state.Encounter?.GetNextSlot(state);
        var minion = await CreatureCmd.Add<T>(state, string.IsNullOrEmpty(slot) ? null : slot);
        await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.MinionPower>(Ctx, minion, 1, Creature, null, silent: true);
        return minion;
    }

    public const int MaxEnemies = 5;

    /// <summary>Свита умирает вместе с хозяином (призыватели, Матка, Механик).</summary>
    protected async Task KillMinions<T>() where T : MonsterModel
    {
        foreach (var minion in Allies.Where(a => a.Monster is T).ToList()) await CreatureCmd.Kill(minion);
    }

    /// <summary>Стихия, к которой у героев в среднем меньше всего сопротивления (Чёрный Странник, Илварре).</summary>
    protected DamageType WeakestElement()
    {
        var heroes = Heroes.ToList();
        DamageType[] elements = [DamageType.Fire, DamageType.Frost, DamageType.Shock];
        return elements.OrderBy(t => heroes.Sum(h => ResistancePowers.TierOf(h, t))).First();
    }

    /// <summary>Статус по стихии: Горение / Озноб / Статика.</summary>
    protected async Task ElementStatus(DamageType type, int fire, int frost, int shock)
    {
        switch (type)
        {
            case DamageType.Fire: await Debuff<BurningPower>(fire); break;
            case DamageType.Frost: await Debuff<ChillPower>(frost); break;
            case DamageType.Shock: await Debuff<StaticPower>(shock); break;
        }
    }

    // ───── интенты ─────
    protected static SingleAttackIntent Atk(int damage) => new(damage);
    protected static MultiAttackIntent Multi(int damage, int hits) => new(damage, hits);
}

/// <summary>Свита/призыв: простой цикл, без своей логики.</summary>
public abstract class RequiemMinion : RequiemMonster { }

/// <summary>
/// Энкаунтер элиты/супербосса этапа 7. <see cref="Acts"/> — ванильные акты, в пул элит которых он добавляется
/// через BaseLib; новые акты перечисляют свои элиты сами (<c>RequiemActModel.OwnElites</c>), поэтому для них false.
/// </summary>
public abstract class RequiemEncounter(RoomType roomType) : CustomEncounterModel(roomType)
{
    protected virtual Func<ActModel, bool> ValidFor => _ => false;

    public override bool IsValidForAct(ActModel act) => ValidFor(act);

    protected abstract IReadOnlyList<MonsterModel> Lineup { get; }

    public override IEnumerable<MonsterModel> AllPossibleMonsters => Lineup.Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        Lineup.Select(m => (m.ToMutable(), (string?)null)).ToList();
}
