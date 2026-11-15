using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Config;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Карты героев мода лежат в ванильных пулах (`IroncladCardPool`, `SilentCardPool`), чтобы выпадать Латоносцу и
/// Безмолвной. При выключенных системах мода (<see cref="RequiemConfig.EnableRequiemSystems"/> = false) они не должны
/// попадать ванильному Ironclad и Silent: их правила (◆, Шаткость, Подлый, яды на оружии) без систем не работают
/// (01 «Не ломаем базовые карты», card spec §3.4 п. 5). Постфикс <c>CardPoolModel.GetUnlockedCards</c> — через него
/// игра собирает награды, лавку и случайные карты. Сохранённые в колоде карты не трогаем: загрузка забега их найдёт.
/// </summary>
[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
public static class VanillaPoolGuard
{
    private static readonly System.Reflection.Assembly ModAssembly = typeof(VanillaPoolGuard).Assembly;

    public static bool IsModCard(CardModel card) => card.GetType().Assembly == ModAssembly;

    [HarmonyPostfix]
    public static void Postfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        if (RequiemConfig.EnableRequiemSystems || __instance.IsColorless) return;
        if (__instance.GetType().Assembly == ModAssembly) return; // свои пулы (заклинания, Пути) и так вне ванильных наград
        __result = __result.Where(c => !IsModCard(c)).ToList();
    }
}
