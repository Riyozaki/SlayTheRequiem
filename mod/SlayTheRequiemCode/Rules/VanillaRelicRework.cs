using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Переработка ванильных реликвий (docs/design/07 §7, <c>data/vanilla_relics.csv</c>):
/// <list type="bullet">
/// <item><b>В бижутерию</b> — реликвия убирается из мешка реликвий героя Реквиема (награды, сундуки, лавки); её место в пуле
/// занимает кольцо или амулет мода (они и так лежат в <c>SharedRelicPool</c>). Реликвию, выданную событием по имени,
/// не трогаем — у игры нет хука подмены получаемой реликвии.</item>
/// <item><b>Перерабатываются</b> — энергетические реликвии: +1 энергия остаётся, прирост ◆ (или ✦ у Регента) −1
/// (<see cref="PenaltySource"/> подмешивается к источникам ресурсов в <see cref="Mana.ManaSources"/>).</item>
/// <item><b>Остаются</b> — без изменений; вес в Грузе — из таблицы (<see cref="Equipment.Loadout.TotalWeight"/>).</item>
/// </list>
/// </summary>
public static class VanillaRelicRework
{
    private static readonly System.Reflection.Assembly ModAssembly = typeof(VanillaRelicRework).Assembly;

    public static bool IsModRelic(RelicModel relic) => relic.GetType().Assembly == ModAssembly;

    /// <summary>Строка таблицы для ванильной реликвии (у реликвий мода — нет).</summary>
    public static VanillaRelicTable.Entry? EntryOf(RelicModel relic) =>
        !IsModRelic(relic) && RequiemData.Relics is { } table && table.TryGet(relic.GetType().Name, out var e) ? e : null;

    /// <summary>Убрать из мешка реликвии, ушедшие в бижутерию.</summary>
    public static void Strip(RelicGrabBag bag)
    {
        if (RequiemData.Relics is not { } table) return;
        foreach (var relic in ModelDb.AllRelics.Where(r => !IsModRelic(r) && table.IsCut(r.GetType().Name)).ToList())
            bag.Remove(relic);
    }

    /// <summary>Штраф прироста от переработанных реликвий героя (0, если герой вне систем).</summary>
    public static (int Stamina, int Mana) Penalty(Player player)
    {
        if (!RequiemMode.IsInSystem(player) || RequiemData.Relics is not { } table) return (0, 0);
        var keys = player.Relics.Where(r => !IsModRelic(r)).Select(r => r.GetType().Name);
        return table.RegenPenalty(player.Character.GetType().Name, keys);
    }

    /// <summary>Источник ресурса «−1 прирост» — не реликвия, а расчёт по реликвиям игрока.</summary>
    public sealed class PenaltySource(int stamina, int mana) : IResourceSource
    {
        public int StaminaRegenBonus => -stamina;
        public int ManaRegenBonus => -mana;
    }

    public static IEnumerable<IResourceSource> Sources(Player player)
    {
        var (stamina, mana) = Penalty(player);
        if (stamina != 0 || mana != 0) yield return new PenaltySource(stamina, mana);
    }
}

/// <summary>Мешок реликвий игрока: убрать ушедшие в бижутерию, если герой в системе Реквиема.</summary>
[HarmonyPatch(typeof(RelicGrabBag), nameof(RelicGrabBag.Populate), typeof(Player), typeof(Rng))]
public static class PlayerRelicGrabBagPatch
{
    [HarmonyPostfix]
    public static void Postfix(RelicGrabBag __instance, Player player)
    {
        try
        {
            if (RequiemMode.IsInSystem(player)) VanillaRelicRework.Strip(__instance);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"Реликвии: мешок игрока не очищен: {e.Message}");
        }
    }
}

/// <summary>Общий мешок (сундуки коопа): при включённых системах — тоже без ушедших в бижутерию.</summary>
[HarmonyPatch(typeof(RelicGrabBag), nameof(RelicGrabBag.Populate), typeof(IEnumerable<RelicModel>), typeof(Rng))]
public static class SharedRelicGrabBagPatch
{
    [HarmonyPostfix]
    public static void Postfix(RelicGrabBag __instance)
    {
        try
        {
            if (RunSettings.EnableRequiemSystems) VanillaRelicRework.Strip(__instance);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"Реликвии: общий мешок не очищен: {e.Message}");
        }
    }
}

/// <summary>Подсказки к ванильным реликвиям: вес в Грузе и правка переработки (текст реликвии не трогаем).</summary>
[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.HoverTips), MethodType.Getter)]
public static class VanillaRelicHoverTipsPatch
{
    [HarmonyPostfix]
    public static void Postfix(RelicModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (!RunSettings.EnableRequiemSystems) return;
        try
        {
            if (VanillaRelicRework.EntryOf(__instance) is not { } entry) return;
            Player? owner = __instance.IsMutable ? __instance.Owner : null;
            if (owner != null && !RequiemMode.IsInSystem(owner)) return;
            var extra = new List<IHoverTip>
            {
                new HoverTip(UI.RequiemUi.Tip("RELIC_WEIGHT.title"), UI.RequiemUi.TipLoc("RELIC_WEIGHT.description", ("Weight", entry.Weight))),
            };
            if (entry.Change == RelicChange.RegenMinusOne)
                extra.Add(new HoverTip(UI.RequiemUi.Tip("RELIC_REWORK_ENERGY.title"), UI.RequiemUi.Tip("RELIC_REWORK_ENERGY.description")));
            __result = __result.Concat(extra).ToList();
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"UI: подсказки реликвии {__instance.Id}: {e.Message}");
        }
    }
}
