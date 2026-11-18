using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using SlayTheRequiem.SlayTheRequiemCode.Cards.Regent;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Patches;

/// <summary>Native Player.PopulateStartingDeck clones this canonical sequence only for a NEW inventory.
/// No Add/obtain hooks, save rewrite or repeatable start gift. Background/bonus summaries see the actual new deck.</summary>
[CriticalPatch]
[HarmonyPatch(typeof(Regent), nameof(Regent.StartingDeck), MethodType.Getter)]
public static class StartingDeckPatch
{
    [HarmonyPostfix]
    public static void Postfix(Regent __instance, ref IEnumerable<CardModel> __result)
    {
        if (!RequiemMode.IsInSystem(__instance)) return;
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<CardModel>();
        foreach (var card in __result)
        {
            var name = card.GetType().Name;
            var occurrence = occurrences.GetValueOrDefault(name);
            occurrences[name] = occurrence + 1;
            result.Add(BuildAccessRules.StarterReplacement("Regent", name, occurrence) switch
            {
                nameof(Firebolt) => ModelDb.Card<Firebolt>(),
                nameof(ConjureSpiritWolf) => ModelDb.Card<ConjureSpiritWolf>(),
                nameof(ArcaneFocus) => ModelDb.Card<ArcaneFocus>(),
                _ => card,
            });
        }
        __result = result;
    }
}
