using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Фильтр пулов карт в обе стороны — постфикс <c>CardPoolModel.GetUnlockedCards</c>: через него игра собирает награды,
/// лавку, трансформации и случайные карты (сохранённые в колоде карты не трогаем — загрузка забега их найдёт).
/// <list type="bullet">
/// <item><b>Системы выключены</b>: карты героев мода лежат в ванильных пулах (Ironclad, Silent, Regent), но без систем их
/// правила (◆, ✦-магия, Шаткость, Подлый, яды на оружии) не работают — ванильным героям они не выпадают
/// (01 «Не ломаем базовые карты», card spec §3.4 п. 5).</item>
/// <item><b>Системы включены</b>: ванильные карты из списка изъятия (19 §2, <c>data/requiem_pools.csv</c>) не выпадают —
/// дубли механик мода, тёзки карт мода, формы и чужие фантазии.</item>
/// </list>
/// </summary>
[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
public static class VanillaPoolGuard
{
    private static readonly System.Reflection.Assembly ModAssembly = typeof(VanillaPoolGuard).Assembly;

    public static bool IsModCard(CardModel card) => card.GetType().Assembly == ModAssembly;

    /// <summary>Ванильная карта изъята из пула Реквиема (19 §2). Карты мода не изымаются.</summary>
    public static bool IsCut(CardPoolModel pool, CardModel card) =>
        !IsModCard(card) && RequiemData.Pools?.IsCut(PoolTable.PoolKey(pool.GetType().Name), card.GetType().Name) == true;

    [HarmonyPostfix]
    public static void Postfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        if (__instance.GetType().Assembly == ModAssembly) return; // свои пулы (заклинания, Пути) и так вне ванильных наград
        if (RequiemConfig.EnableRequiemSystems)
        {
            if (RequiemData.Pools is { CutCount: > 0 }) __result = __result.Where(c => !IsCut(__instance, c)).ToList();
            return;
        }
        if (__instance.IsColorless) return;
        __result = __result.Where(c => !IsModCard(c)).ToList();
    }
}
