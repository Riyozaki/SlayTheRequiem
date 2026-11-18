using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Выдаёт врагам ресурсы и защиту Реквиема при первом появлении в бою (docs/design/11 §1):
/// ◆, Стойкость или Непоколебимость, Латы и сопротивления из таблицы врагов, надбавка ♥.
/// </summary>
public static class EnemySetup
{
    private sealed class Setup
    {
        internal bool Started;
        internal readonly TaskCompletionSource Completion = new();
    }
    private static readonly ConditionalWeakTable<Creature, Setup> Done = new();
    private sealed record SummonSource(Type Type, SummonSource? Previous) : IDisposable
    {
        public void Dispose() => Summoning.Value = Previous;
    }
    private static readonly AsyncLocal<SummonSource?> Summoning = new();
    private static readonly ConditionalWeakTable<Creature, StrongBox<EnemyRole>> DeclaredSummons = new();

    internal static IDisposable SummonScope(Type type)
    {
        var scope = new SummonSource(type, Summoning.Value);
        Summoning.Value = scope;
        return scope;
    }

    private static EnemyRole DeclaredRole(MonsterModel? monster) => monster is IEnemyRoleSource own ? own.CombatRole
        : monster == null ? EnemyRole.Normal : RequiemData.MonsterEntry(monster.GetType().Name).Role;

    public static EnemyRole RoleOf(Creature creature)
    {
        var declared = DeclaredRole(creature.Monster);
        if (declared != EnemyRole.Prop && Summoning.Value?.Type == creature.Monster?.GetType())
            DeclaredSummons.GetValue(creature, _ => new StrongBox<EnemyRole>(EnemyRole.Minion));
        if (DeclaredSummons.TryGetValue(creature, out var summoned)) declared = summoned.Value;
        return EnemyRoleRules.Resolve(declared, creature.IsSecondaryEnemy);
    }

    public static EnemyRank RankOf(Creature creature) => EnemyRoleRules.Rank(RoleOf(creature));

    public static async Task EnsureAll(ICombatState combatState)
    {
        foreach (var enemy in combatState.Enemies.ToList()) await Ensure(enemy, combatState);
    }

    public static Task Ensure(Creature creature, ICombatState? combatState)
    {
        if (!RunSettings.EnableRequiemSystems || creature.Monster == null || !creature.IsAlive || !creature.IsEnemy) return Task.CompletedTask;
        var setup = Done.GetValue(creature, _ => new Setup());
        if (setup.Started) return setup.Completion.Task; // an in-flight/faulted setup is not a completed setup
        setup.Started = true;
        _ = Finish();
        return setup.Completion.Task;

        async Task Finish()
        {
            try { await Apply(creature, combatState); setup.Completion.TrySetResult(); }
            catch (Exception e)
            {
                MainFile.Logger.Error($"Enemy setup interrupted for {creature.Monster.Id}; no partial-success retry: {e}");
                setup.Completion.TrySetException(e);
            }
        }
    }

    private static async Task Apply(Creature creature, ICombatState? combatState)
    {
        var ctx = new ThrowingPlayerChoiceContext();
        var role = RoleOf(creature);
        var rank = EnemyRoleRules.Rank(role);
        var entry = RequiemData.MonsterEntry(creature.Monster.GetType().Name);

        var state0 = combatState ?? creature.CombatState;
        var run0 = state0?.RunState;
        // An explicit leader, not a boss ROOM, makes initial entourage secondary. Standalone rift
        // creatures stay primary; a late MinionPower from CreatureCmd.Add cannot change their setup role.
        if (role is EnemyRole.Minion or EnemyRole.Prop && !creature.IsSecondaryEnemy
            && state0?.Encounter?.AllPossibleMonsters.Any(m => DeclaredRole(m) is EnemyRole.Boss or EnemyRole.Elite) == true)
            await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.MinionPower>(ctx, creature, 1, null, null, silent: true);
        var hpMultiplier = (decimal)RunSettings.EnemyHpPercent / 100m
                           * RequiemLevelRules.EnemyHpMultiplier(Acts.RequiemLevel.Of(run0))
                           * (100 + ActHpBonusPercent(run0, rank)) / 100m;
        if (EnemyRoleRules.ScalesHp(role) && hpMultiplier != 1m && creature.CurrentHp == creature.MaxHp)
            await CreatureCmd.SetMaxAndCurrentHp(creature, EnemyRules.ScaledMaxHp(creature.MaxHp, hpMultiplier));

        if (EnemyRoleRules.UsesResources(role))
        {
            await PowerCmd.Apply<EnemyStaminaPower>(ctx, creature, EnemyRules.StaminaPool(rank), null, null, silent: true);
            if (EnemyRules.IsCaster(entry.AttackType))
                await PowerCmd.Apply<EnemyManaPower>(ctx, creature, EnemyRules.CasterManaPool(rank), null, null, silent: true);
            if (rank == EnemyRank.Boss)
                await PowerCmd.Apply<UnshakeablePower>(ctx, creature, EnemyRules.BreakThreshold(creature.MaxHp), null, null, silent: true);
            else
                await PowerCmd.Apply<PoisePower>(ctx, creature, EnemyRules.Poise(rank, creature.MaxHp) + entry.PoiseBonus, null, null, silent: true);
        }

        if (entry.Plating > 0)
            await PowerCmd.Apply<PlatingPower>(ctx, creature, entry.Plating, null, null, silent: true);

        foreach (var (type, tier) in entry.Resistances)
        {
            if (tier == 0) continue;
            switch (type)
            {
                case DamageType.Fire: await PowerCmd.Apply<FireResistancePower>(ctx, creature, tier, null, null, silent: true); break;
                case DamageType.Frost: await PowerCmd.Apply<FrostResistancePower>(ctx, creature, tier, null, null, silent: true); break;
                case DamageType.Shock: await PowerCmd.Apply<ShockResistancePower>(ctx, creature, tier, null, null, silent: true); break;
                case DamageType.Poison: await PowerCmd.Apply<PoisonResistancePower>(ctx, creature, tier, null, null, silent: true); break;
                case DamageType.Magic: await PowerCmd.Apply<MagicResistancePower>(ctx, creature, tier, null, null, silent: true); break;
            }
        }

        if (EnemyRoleRules.RollsAffixes(role) && run0 is { } run)
        {
            var kind = Acts.ActKinds.Current(run);
            var fullRun = Acts.ActKinds.IsFullRun(run);
            // «Ученичество» акта 1 — обычные без аффиксов; в коротком забеге акт 3 — второй по счёту, но с правилами слота 3.
            var actIndex = ActRules.SlotOf(run.CurrentActIndex, run.Acts.Count);
            var count = EnemyRules.AffixCount(rank, actIndex, run.Rng.Niche.NextInt(100), creature.MaxHp,
                Acts.RequiemLevel.Of(run), ActRules.EliteAffixBonus(kind, fullRun));
            if (count > 0)
            {
                var rolls = Enumerable.Range(0, EnemyRules.MaxAffixes).Select(_ => run.Rng.Niche.NextInt(100)).ToList();
                foreach (var affix in AffixRules.Pick(count, rolls))
                    await ApplyAffix(ctx, creature, affix);
            }
        }
    }

    /// <summary>
    /// Надбавка ♥ от акта (docs/design/13 §2): Улей/Слава с оверлеями и новые акты в полном забеге, ночь Кровавого Двора
    /// (только обычные и элиты). Супербоссы и боссы новых актов — по своим числам.
    /// </summary>
    public static int ActHpBonusPercent(MegaCrit.Sts2.Core.Runs.IRunState? run, EnemyRank rank)
    {
        if (run == null || run.Acts.Count == 0) return 0;
        var kind = Acts.ActKinds.Current(run);
        var bonus = ActRules.EnemyHpBonusPercent(kind, Acts.ActKinds.IsFullRun(run));
        if (kind == ActKind.BloodCourt && rank != EnemyRank.Boss && run.CurrentMapCoord is { } coord && ActRules.IsNight(coord.row))
            bonus += ActRules.NightHpBonusPercent;
        return bonus;
    }

    /// <summary>Выдать аффикс (docs/design/11 §2): маркер-сила и её числа.</summary>
    public static async Task ApplyAffix(PlayerChoiceContext ctx, Creature creature, EnemyAffix affix)
    {
        switch (affix)
        {
            case EnemyAffix.Hardened:
                await PowerCmd.Apply<HardenedAffixPower>(ctx, creature, 1, null, null, silent: true);
                await PowerCmd.Apply<PlatingPower>(ctx, creature, EnemyRules.AffixPlating, null, null, silent: true);
                break;
            case EnemyAffix.Fiery:
                await ApplyElemental<FireCloakPower, FireResistancePower>(ctx, creature);
                break;
            case EnemyAffix.Icy:
                await ApplyElemental<FrostCloakPower, FrostResistancePower>(ctx, creature);
                break;
            case EnemyAffix.Stormy:
                await ApplyElemental<ShockCloakPower, ShockResistancePower>(ctx, creature);
                break;
            case EnemyAffix.Draining:
                await PowerCmd.Apply<DrainingAffixPower>(ctx, creature, 1, null, null, silent: true);
                break;
            case EnemyAffix.ManaEater:
                await PowerCmd.Apply<ManaEaterAffixPower>(ctx, creature, 1, null, null, silent: true);
                break;
            case EnemyAffix.Vampiric:
                await PowerCmd.Apply<VampiricAffixPower>(ctx, creature, 1, null, null, silent: true);
                break;
            case EnemyAffix.PoisonedBlade:
                await PowerCmd.Apply<PoisonedBladeAffixPower>(ctx, creature, 1, null, null, silent: true);
                break;
            case EnemyAffix.Bannerman:
                await PowerCmd.Apply<BannermanAffixPower>(ctx, creature, 1, null, null, silent: true);
                break;
            case EnemyAffix.Absorber:
                await PowerCmd.Apply<AbsorberAffixPower>(ctx, creature, 1, null, null, silent: true);
                await PowerCmd.Apply<SpellAbsorptionPower>(ctx, creature, EnemyRules.AbsorberStacks, null, null, silent: true);
                break;
            case EnemyAffix.Frenzied:
                await PowerCmd.Apply<FrenziedAffixPower>(ctx, creature, 1, null, null, silent: true);
                break;
            case EnemyAffix.Ghostly:
                await PowerCmd.Apply<GhostlyAffixPower>(ctx, creature, 1, null, null, silent: true);
                break;
            case EnemyAffix.Avenger:
                await PowerCmd.Apply<AvengerAffixPower>(ctx, creature, 1, null, null, silent: true);
                break;
        }
    }

    private static async Task ApplyElemental<TCloak, TResist>(PlayerChoiceContext ctx, Creature creature)
        where TCloak : CloakPower where TResist : ResistancePower
    {
        await PowerCmd.Apply<ElementalAffixPower>(ctx, creature, 1, null, null, silent: true);
        var cloak = await PowerCmd.Apply<TCloak>(ctx, creature, EnemyRules.AffixCloak, null, null, silent: true);
        cloak?.MakePermanent();
        await PowerCmd.Apply<TResist>(ctx, creature, EnemyRules.AffixResistance, null, null, silent: true);
    }

    /// <summary>Иммунитет к Кровотечению: нежить, конструкты, призраки (docs/design/03).</summary>
    public static bool IsBleedImmune(Creature creature) =>
        creature.Monster != null && RequiemData.MonsterEntry(creature.Monster.GetType().Name).Faction is "undead" or "construct" or "spirit";

    /// <summary>Нежить и духи — цели «Изгнания нежити» и двойного урона светом.</summary>
    public static bool IsUndead(Creature creature) =>
        creature.Monster != null && RequiemData.MonsterEntry(creature.Monster.GetType().Name).Faction is "undead" or "spirit";

    /// <summary>Звери — цели Амулета Ловчего (07 §5.1).</summary>
    public static bool IsBeast(Creature creature) =>
        creature.Monster != null && RequiemData.MonsterEntry(creature.Monster.GetType().Name).Faction == "beast";

    /// <summary>Порождения Бездны (даэдра).</summary>
    public static bool IsAbyss(Creature creature) =>
        creature.Monster != null && RequiemData.MonsterEntry(creature.Monster.GetType().Name).Faction == "abyss";

    /// <summary>Нежить, духи и Бездна — цели Сияния и бонусов Небесных (docs/design/05).</summary>
    public static bool IsUndeadOrAbyss(Creature creature) => IsUndead(creature) || IsAbyss(creature);

    /// <summary>Иммунитет к Хвори: нежить, конструкты, Бездна (docs/design/03).</summary>
    public static bool IsBlightImmune(Creature creature) =>
        creature.Monster != null && RequiemData.MonsterEntry(creature.Monster.GetType().Name).Faction is "undead" or "construct" or "abyss";
}
