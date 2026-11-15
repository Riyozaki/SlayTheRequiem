using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Pantheon;

/// <summary>
/// Знамение у Нео (docs/design/05 §1): вместо одного из его даров — «Принять Знамение». Выбор открывает страницу
/// с 3 случайными Знамениями (на уровне «Реквиема» 8+ — все 10, страницами по 4). Одно на забег.
/// </summary>
[HarmonyPatch(typeof(Neow), "GenerateInitialOptions")]
public static class NeowOmenPatch
{
    public const int PageSize = 4;
    private const string Table = "ancients";
    private const string Key = "SLAYTHEREQUIEM-NEOW_OMEN";

    private static readonly MethodInfo? SetEventState =
        AccessTools.Method(typeof(EventModel), "SetEventState", [typeof(LocString), typeof(IEnumerable<EventOption>)]);
    private static readonly MethodInfo? Done = AccessTools.Method(typeof(AncientEventModel), "Done");

    [HarmonyPostfix]
    public static void OfferOmen(Neow __instance, ref IReadOnlyList<EventOption> __result)
    {
        try
        {
            var player = __instance.Owner;
            if (player == null || !RequiemConfig.EnablePantheon || !RequiemMode.IsInSystem(player)) return;
            if (OmenRelic.Of(player) != null || __result.Count < 3 || SetEventState == null || Done == null) return;
            var list = __result.ToList();
            var option = new EventOption(__instance, () => ShowOmens(__instance, player),
                new LocString(Table, $"{Key}.title"), new LocString(Table, $"{Key}.description"), Key, Array.Empty<MegaCrit.Sts2.Core.HoverTips.IHoverTip>());
            // Первые два — дары Нео, третий — проклятый: заменяем первый дар.
            list[0] = option;
            __result = list;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Знамение у Нео не добавлено ({e.Message}).");
        }
    }

    private static Task ShowOmens(Neow neow, Player player)
    {
        var count = OmenRules.OmenOfferCount(Acts.RequiemLevel.Of(player.RunState));
        var picks = OmenRules.PickDistinct(OmenRules.OmenCount, count, n => n <= 0 ? 0 : neow.Rng.NextInt(n));
        ShowPage(neow, player, picks, 0);
        return Task.CompletedTask;
    }

    private static void ShowPage(Neow neow, Player player, List<int> picks, int page)
    {
        var pages = (picks.Count + PageSize - 1) / PageSize;
        var shown = picks.Count <= PageSize + 1 ? picks : picks.Skip(page * PageSize).Take(PageSize).ToList();
        var options = new List<EventOption>();
        foreach (var index in shown)
        {
            var relic = OmenRelic.Create((Omen)index);
            relic.Owner = player;
            options.Add(EventOption.FromRelic(relic, neow, () => Take(neow, player, relic), $"{Key}.{relic.Id.Entry}"));
        }
        if (shown.Count < picks.Count)
        {
            var next = (page + 1) % Math.Max(1, pages);
            options.Add(new EventOption(neow, () =>
                {
                    ShowPage(neow, player, picks, next);
                    return Task.CompletedTask;
                },
                new LocString(Table, $"{Key}.more.title"), new LocString(Table, $"{Key}.more.description"), $"{Key}.more",
                Array.Empty<MegaCrit.Sts2.Core.HoverTips.IHoverTip>()));
        }
        SetEventState!.Invoke(neow, [new LocString(Table, $"{Key}.page"), options]);
    }

    private static async Task Take(Neow neow, Player player, RelicModel relic)
    {
        if (OmenRelic.Of(player) == null) await RelicCmd.Obtain(relic, player);
        Done!.Invoke(neow, null);
    }
}
