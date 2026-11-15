using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Transformations;

/// <summary>Откуда лечение: обычное, кормление (Укус, вампиризм, поглощение) или Регенерация.</summary>
public enum HealSource
{
    Normal,
    Feeding,
    Regeneration,
    /// <summary>Воскрешение (Филактерия, Ореол): запреты и проценты не действуют.</summary>
    Revival,
}

/// <summary>Реликвия или сила героя, меняющая его лечение (формы, Дух Матери Очага).</summary>
public interface IHealModifier
{
    /// <summary>±% к лечению владельца из источника <paramref name="source"/>.</summary>
    int HealPercent(HealSource source);

    /// <summary>Запрет лечения из этого источника (голод вампира, «кость» Лича, оборотень вне формы — Регенерация).</summary>
    bool BlocksHealing(HealSource source);
}

/// <summary>Иммунитет героя к статусу (Лич, Заводное сердце: Яд, Хворь, Кровотечение).</summary>
public interface IStatusImmunity
{
    bool ImmuneTo(PowerModel canonicalPower);
}

/// <summary>
/// Лечение героя Реквиема (docs/design/06): все проценты складываются (<see cref="TransformationRules.HealPercent"/>),
/// запреты действуют по источнику. Источник задаётся блоком <c>using (Healing.As(HealSource.Feeding))</c> — переживает await,
/// как <see cref="Rules.DamageTyping"/>. Вызывается из патча CreatureCmd.Heal (Patches/StartingRelicsPatch.cs).
/// </summary>
public static class Healing
{
    private static readonly AsyncLocal<HealSource?> Ambient = new();

    public static HealSource Current => Ambient.Value ?? HealSource.Normal;

    public static IDisposable As(HealSource source)
    {
        var previous = Ambient.Value;
        Ambient.Value = source;
        return new Restore(previous);
    }

    /// <summary>Итоговое лечение существа (у врагов не меняется).</summary>
    public static decimal Modify(Creature creature, decimal amount)
    {
        var player = creature.Player;
        if (player == null || amount <= 0 || !Rules.RequiemMode.IsInSystem(player)) return amount;
        var source = Current;
        if (source == HealSource.Revival) return amount;
        var modifiers = player.Relics.OfType<IHealModifier>().Concat(creature.Powers.OfType<IHealModifier>()).ToList();
        if (modifiers.Count == 0) return amount;
        var blocked = modifiers.Any(m => m.BlocksHealing(source));
        var percent = TransformationRules.HealPercent(modifiers.Select(m => m.HealPercent(source)));
        return TransformationRules.ApplyHeal(amount, percent, blocked);
    }

    /// <summary>Иммунен ли герой к статусу (для правил стаков).</summary>
    public static bool IsImmune(Creature target, PowerModel canonicalPower) =>
        target.Player != null
        && (target.Player.Relics.OfType<IStatusImmunity>().Any(i => i.ImmuneTo(canonicalPower))
            || target.Powers.OfType<IStatusImmunity>().Any(i => i.ImmuneTo(canonicalPower)));

    private sealed class Restore(HealSource? previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = previous;
    }
}
