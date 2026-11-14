using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Resources;

/// <summary>
/// Общая основа ◆ и ✦: копится между ходами (не сбрасывается как энергия), восстанавливается на «прирост»
/// в начале хода и не превышает пул. Пул = <see cref="BasicCustomResource.Max"/> (с учётом IModifyMaxResource —
/// так снаряжение и уровни будут менять его без правки этого класса).
/// </summary>
public abstract class RequiemResource(string id) : BasicCustomResource(id)
{
    protected abstract int BasePool(HeroStats stats);
    protected abstract int BaseRegen(HeroStats stats);

    /// <summary>Прирост за ход с учётом модификаторов (Груз, Озноб и т.п. добавляются позже).</summary>
    public int Regen => Owner == null ? 0 : Math.Max(0, BaseRegen(RequiemMode.StatsFor(Owner)) + RegenBonus);

    /// <summary>Надбавки/штрафы к приросту (силы, Груз).</summary>
    protected virtual int RegenBonus => 0;

    /// <summary>Надбавка к пулу (снаряжение, уровни).</summary>
    protected virtual int PoolBonus => 0;

    public int Pool => Max;

    public override void PrepForCombat<T>(PlayerCombatState playerCombatState)
    {
        var stats = RequiemMode.StatsFor(Owner);
        DefaultMax = RequiemMode.IsInSystem(Owner) ? Math.Max(1, BasePool(stats) + PoolBonus) : 0;
        base.PrepForCombat<T>(playerCombatState);
        // Бой начинается с полным запасом.
        Amount = DefaultMax;
    }

    public override void StartOfTurnReset(PlayerCombatState playerCombatState, ICombatState combatState)
    {
        if (!RequiemMode.IsInSystem(Owner)) return;
        Amount = StaminaRules.Regenerate(Amount, Pool, Regen);
    }

    public override bool ShouldShowDisplay() => RequiemMode.IsInSystem(Owner);

    public override string? IconPath => $"{MainFile.ResPath}/images/ui/{IconName}.png";
    public override string TexturePath => $"{MainFile.ResPath}/images/ui/{IconName}.png";
    protected abstract string IconName { get; }

    /// <summary>Безопасно получить ресурс игрока (null вне боя или если BaseLib его не создал).</summary>
    public static T? Of<T>(Player? player) where T : CustomResource, new() =>
        player?.PlayerCombatState != null && CustomResources<T>.TryGet(player.PlayerCombatState, out var r) ? r : null;
}

/// <summary>◆ Выносливость — топливо атак (docs/design/02 §1).</summary>
public sealed class StaminaResource() : RequiemResource(ResourceId)
{
    public const string ResourceId = "SLAYTHEREQUIEM-STAMINA";
    protected override int BasePool(HeroStats stats) => stats.StaminaPool;
    protected override int BaseRegen(HeroStats stats) => stats.StaminaRegen;
    protected override string IconName => "stamina";
    protected override int RegenBonus =>
        (Owner?.Creature.GetPowerAmount<TemperingPower>() ?? 0)
        + Loadout.Bonuses(Owner).StaminaRegenBonus
        - BurdenPenalty.StaminaRegenPenalty(Owner);
    protected override int PoolBonus => Loadout.Bonuses(Owner).StaminaPoolBonus;
    // Атаки не блокируются нехваткой ◆ — вместо этого надрыв (см. RequiemCombatRules).
    public override bool IsDefaultOptional => true;
}

/// <summary>
/// ✦ Мана — топливо заклинаний, есть у всех героев (docs/design/02 §2).
/// TODO(Этап 1): у Регента звёзды игры должны стать ✦ (перенаправление StarsSpent → ManaResource).
/// </summary>
public sealed class ManaResource() : RequiemResource(ResourceId)
{
    public const string ResourceId = "SLAYTHEREQUIEM-MANA";
    protected override int BasePool(HeroStats stats) => stats.ManaPool;
    protected override int BaseRegen(HeroStats stats) => stats.ManaRegen;
    protected override string IconName => "mana";
    protected override int RegenBonus => -Loadout.Bonuses(Owner).ManaRegenPenalty;
}
