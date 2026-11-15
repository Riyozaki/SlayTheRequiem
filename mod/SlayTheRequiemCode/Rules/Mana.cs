using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// ✦ Мана = звёзды игры (PlayerCombatState.Stars) у всех героев в системе (docs/design/02 §2, tech §3).
/// Так цена ✦ рисуется на картах игрой, «не хватает ✦» блокирует розыгрыш, а ванильные звёздные карты
/// Регента платят из той же маны. Мод добавляет только пул, прирост и стартовое значение.
/// </summary>
public static class Mana
{
    public static int Amount(Player? player) => player?.PlayerCombatState?.Stars ?? 0;
    public static int Amount(Creature? creature) => Amount(creature?.Player);

    public static int Pool(Player? player)
    {
        if (player == null) return 0;
        var pool = RequiemMode.StatsFor(player).ManaPool;
        foreach (var source in ManaSources(player)) pool += source.ManaPoolBonus;
        return Math.Max(0, pool);
    }

    public static int Regen(Player? player)
    {
        if (player == null) return 0;
        var regen = RequiemMode.StatsFor(player).ManaRegen - Loadout.Bonuses(player).ManaRegenPenalty;
        foreach (var source in ManaSources(player)) regen += source.ManaRegenBonus;
        return Math.Max(0, regen);
    }

    public static Task Gain(Player? player, int amount) =>
        player == null || amount <= 0 ? Task.CompletedTask : PlayerCmd.GainStars(amount, player);

    public static Task Lose(Player? player, int amount) =>
        player == null || amount <= 0 ? Task.CompletedTask : PlayerCmd.LoseStars(Math.Min(amount, Amount(player)), player);

    /// <summary>Синхронное списание (для мест, где нельзя ждать — тики статусов внутри расчёта).</summary>
    public static void LoseNow(Player? player, int amount)
    {
        if (player?.PlayerCombatState == null || amount <= 0) return;
        player.PlayerCombatState.LoseStars(Math.Min(amount, player.PlayerCombatState.Stars));
    }

    public static int StartBonus(Player? player) => player == null ? 0 : ManaSources(player).Sum(s => s.ManaStartBonus);

    /// <summary>Все источники пулов и приростов: реликвии, силы и штраф переработанных ванильных реликвий (07 §7).</summary>
    public static IEnumerable<IResourceSource> ManaSources(Player player) =>
        player.Relics.OfType<IResourceSource>().Concat(player.Creature.Powers.OfType<IResourceSource>())
            .Concat(VanillaRelicRework.Sources(player));
}

/// <summary>Реликвия/сила, меняющая пулы и приросты ◆/✦ (происхождения, посохи, Божественное право).</summary>
public interface IResourceSource
{
    int StaminaPoolBonus => 0;
    /// <summary>Прирост ◆ за ход (Отдохнувший, Бодрость, зелье Выносливости V).</summary>
    int StaminaRegenBonus => 0;
    int ManaPoolBonus => 0;
    int ManaRegenBonus => 0;
    /// <summary>✦ сверх стартовой половины пула в начале боя.</summary>
    int ManaStartBonus => 0;
    /// <summary>Стойкость героя (порог Оглушения).</summary>
    int PoiseBonus => 0;
    /// <summary>Вместимость Груза.</summary>
    int BurdenBonus => 0;
}

/// <summary>Сводные статы героя поверх базовых: Стойкость и вместимость Груза из снаряжения, происхождений, зелий.</summary>
public static class HeroStatRules
{
    public static int Poise(Player? player)
    {
        if (player == null) return 0;
        var poise = RequiemMode.StatsFor(player).Poise + Loadout.Bonuses(player).PoiseBonus
            + Mana.ManaSources(player).Sum(s => s.PoiseBonus)
            + (player.Creature?.GetPowerAmount<Powers.GiantPower>() ?? 0);
        return Math.Max(1, poise);
    }

    public static int BurdenCapacity(Player? player)
    {
        if (player == null) return 0;
        return RequiemMode.StatsFor(player).BurdenCapacity + Mana.ManaSources(player).Sum(s => s.BurdenBonus);
    }
}

/// <summary>Стартовое ✦ и прирост в начале каждого хода героя.</summary>
public sealed class ManaCombatRules() : CustomSingletonModel(CustomSingletonModel.HookType.Combat)
{
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (!RequiemMode.IsInSystem(player)) return;
        var pool = Mana.Pool(player);
        var round = player.Creature.CombatState?.RoundNumber ?? 1;
        if (round == 1) await Mana.Gain(player, ManaRules.StartAmount(pool) + Mana.StartBonus(player));
        await Mana.Gain(player, ManaRules.RegenGain(Mana.Amount(player), pool, Mana.Regen(player)));
    }
}
