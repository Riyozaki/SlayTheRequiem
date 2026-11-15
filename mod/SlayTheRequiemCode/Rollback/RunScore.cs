using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Badges;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rollback;

/// <summary>
/// Счёт забега (docs/design/15 §5): каждый возврат Камнем памяти — −15 % итогового счёта (<see cref="MemoryStoneRules.ScoreMultiplier"/>).
/// Постфиксы на обе публичные перегрузки ScoreUtility.CalculateScore (живой забег и сохранённая история).
/// </summary>
public static class RunScore
{
    public static int Rollbacks(IRunState run) =>
        run.Players.Select(p => MemoryStoneRelic.Of(p)?.Rollbacks ?? 0).DefaultIfEmpty(0).Max();

    public static int Rollbacks(SerializableRun run) =>
        run.Players.Select(p => SavedInt(p, nameof(MemoryStoneRelic.Rollbacks)) ?? 0).DefaultIfEmpty(0).Max();

    /// <summary>Свойство Камня памяти из сохранения игрока (null — Камня нет).</summary>
    public static int? SavedInt(SerializablePlayer player, string name)
    {
        var stoneId = ModelDb.GetId(typeof(MemoryStoneRelic));
        var relic = player.Relics?.FirstOrDefault(r => Equals(r.Id, stoneId));
        if (relic == null) return null;
        // Нулевые значения не пишутся (SaveIfNotTypeDefault).
        return relic.Props?.ints?.FirstOrDefault(p => p.name == name) is { name: not null } prop ? prop.value : 0;
    }

    public static int Apply(int score, int rollbacks) =>
        rollbacks <= 0 ? score : (int)Math.Floor(score * MemoryStoneRules.ScoreMultiplier(rollbacks));
}

[HarmonyPatch(typeof(ScoreUtility), nameof(ScoreUtility.CalculateScore), typeof(IRunState), typeof(bool))]
public static class LiveScorePatch
{
    [HarmonyPostfix]
    public static void Postfix(IRunState runState, ref int __result) =>
        __result = RunScore.Apply(__result, RunScore.Rollbacks(runState));
}

[HarmonyPatch(typeof(ScoreUtility), nameof(ScoreUtility.CalculateScore), typeof(SerializableRun), typeof(bool))]
public static class SavedScorePatch
{
    [HarmonyPostfix]
    public static void Postfix(SerializableRun run, ref int __result) =>
        __result = RunScore.Apply(__result, RunScore.Rollbacks(run));
}

/// <summary>Значок «Ни одной свечи»: победа в режиме Реквиема без единого возврата Камнем памяти.</summary>
public sealed class NoCandle() : CustomBadge(requiresWin: true, multiplayerOnly: false)
{
    public override BadgeRarity Rarity(SerializableRun run, SerializablePlayer player) => BadgeRarity.Gold;

    public override bool IsObtained(SerializableRun run, SerializablePlayer player) =>
        RunScore.SavedInt(player, nameof(MemoryStoneRelic.CandlesUsed)) == 0;
}
