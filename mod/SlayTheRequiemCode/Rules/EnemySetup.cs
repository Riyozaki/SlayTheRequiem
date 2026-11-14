using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
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
    private static readonly ConditionalWeakTable<Creature, object> Done = new();

    public static EnemyRank RankOf(ICombatState? combatState) => combatState?.Encounter?.RoomType switch
    {
        RoomType.Boss => EnemyRank.Boss,
        RoomType.Elite => EnemyRank.Elite,
        _ => EnemyRank.Normal,
    };

    public static async Task EnsureAll(ICombatState combatState)
    {
        foreach (var enemy in combatState.Enemies.ToList()) await Ensure(enemy, combatState);
    }

    public static async Task Ensure(Creature creature, ICombatState? combatState)
    {
        if (!RequiemConfig.EnableRequiemSystems || creature.Monster == null || !creature.IsAlive) return;
        if (Done.TryGetValue(creature, out _)) return;
        Done.Add(creature, new object());

        var ctx = new ThrowingPlayerChoiceContext();
        var rank = RankOf(combatState ?? creature.CombatState);
        var entry = RequiemData.MonsterEntry(creature.Monster.GetType().Name);

        var hpMultiplier = (decimal)RequiemConfig.EnemyHpPercent / 100m;
        if (hpMultiplier != 1m && creature.CurrentHp == creature.MaxHp)
            await CreatureCmd.SetMaxAndCurrentHp(creature, EnemyRules.ScaledMaxHp(creature.MaxHp, hpMultiplier));

        await PowerCmd.Apply<EnemyStaminaPower>(ctx, creature, EnemyRules.StaminaPool(rank), null, null, silent: true);
        if (EnemyRules.IsCaster(entry.AttackType))
            await PowerCmd.Apply<EnemyManaPower>(ctx, creature, EnemyRules.CasterManaPool(rank), null, null, silent: true);

        if (rank == EnemyRank.Boss)
            await PowerCmd.Apply<UnshakeablePower>(ctx, creature, EnemyRules.BreakThreshold(creature.MaxHp), null, null, silent: true);
        else
            await PowerCmd.Apply<PoisePower>(ctx, creature, EnemyRules.Poise(rank, creature.MaxHp) + entry.PoiseBonus, null, null, silent: true);

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

        var state = combatState ?? creature.CombatState;
        if (state?.RunState is { } run)
        {
            var count = EnemyRules.AffixCount(rank, run.CurrentActIndex, run.Rng.Niche.NextInt(100));
            var all = Enum.GetValues<EnemyAffix>();
            for (var i = 0; i < count; i++)
                await ApplyAffix(ctx, creature, all[run.Rng.Niche.NextInt(all.Length)]);
        }
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

    /// <summary>Иммунитет к Хвори: нежить, конструкты, Бездна (docs/design/03).</summary>
    public static bool IsBlightImmune(Creature creature) =>
        creature.Monster != null && RequiemData.MonsterEntry(creature.Monster.GetType().Name).Faction is "undead" or "construct" or "abyss";
}
