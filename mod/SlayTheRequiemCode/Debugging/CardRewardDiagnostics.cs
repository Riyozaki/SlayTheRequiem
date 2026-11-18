using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;

namespace SlayTheRequiem.SlayTheRequiemCode.Debugging;

/// <summary>Event-based diagnostics. Off by default; enable with str_trace on (no frame/poll logging).</summary>
public static class CardRewardTrace
{
    public static bool Enabled { get; set; }
    public static string Context(Player? player) => player == null ? "owner=none" :
        $"seed={player.RunState.Rng.StringSeed} act={player.RunState.CurrentActIndex} floor={player.RunState.TotalFloor} player={player.NetId} hero={player.Character.Id.Entry}";
    public static string Describe(CardModel card) =>
        $"{card.Id.Entry}[{card.Type}/{card.Rarity} mutable={card.IsMutable} owner={card.Owner?.NetId} upgraded={card.IsUpgraded} pile={card.Pile?.Type} removed={card.HasBeenRemovedFromState}]";
    public static string Piles(Player? p) => p == null ? "" :
        $"deck={p.Deck.Cards.Count} hand={p.PlayerCombatState?.Hand.Cards.Count ?? 0} draw={p.PlayerCombatState?.DrawPile.Cards.Count ?? 0} discard={p.PlayerCombatState?.DiscardPile.Cards.Count ?? 0}";

    public static void Options(string phase, Player player, IEnumerable<CardCreationResult> cards, bool force = false)
    {
        if (!Enabled && !force) return;
        try
        {
            var list = cards.ToList();
            MainFile.Logger.Info($"[CardReward] phase={phase} {Context(player)} count={list.Count} rng={player.PlayerRng.Rewards.Counter} settings={RunSettings.Active?.Encode() ?? "legacy/config"} cards=" +
                string.Join(", ", list.Select(c => Describe(c.Card))));
        }
        catch (Exception e) { MainFile.Logger.Warn($"[CardReward] trace failed: {e.Message}"); }
    }

    public static void Card(string phase, CardModel card, string detail = "", bool force = false)
    {
        if (!Enabled && !force) return;
        try { MainFile.Logger.Info($"[CardReward] phase={phase} {Context(card.Owner)} {Describe(card)} {Piles(card.Owner)} {detail}"); }
        catch (Exception e) { MainFile.Logger.Warn($"[CardReward] trace failed: {e.Message}"); }
    }
}

[HarmonyPatch(typeof(CardReward), nameof(CardReward.Populate))]
public static class CardRewardDiagnostics
{
    private static readonly System.Reflection.PropertyInfo? OptionCount = AccessTools.Property(typeof(CardReward), "OptionCount");

    [HarmonyPostfix]
    public static void Postfix(CardReward __instance)
    {
        var options = __instance.Cards.Select(c => new CardCreationResult(c)).ToList();
        var missing = OptionCount?.GetValue(__instance) is int expected && options.Count < expected;
        CardRewardTrace.Options($"populated:expected={OptionCount?.GetValue(__instance)}", __instance.Player, options, missing);
    }

    [HarmonyFinalizer]
    public static Exception? Finalizer(CardReward __instance, Exception? __exception)
    {
        if (__exception != null) MainFile.Logger.Error($"[CardReward] phase=populate:exception {CardRewardTrace.Context(__instance.Player)} {__exception}");
        return __exception;
    }
}

[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForReward), typeof(Player), typeof(int), typeof(CardCreationOptions))]
public static class CardFactoryTracePatch
{
    [HarmonyPrefix]
    public static void Prefix(Player player, int cardCount, CardCreationOptions options)
    {
        if (CardRewardTrace.Enabled)
            MainFile.Logger.Info($"[CardReward] phase=pool-roll {CardRewardTrace.Context(player)} requested={cardCount} source={options.Source} flags={options.Flags} rng={player.PlayerRng.Rewards.Counter}");
    }
    [HarmonyPostfix]
    public static void Postfix(Player player, IEnumerable<CardCreationResult> __result) => CardRewardTrace.Options("factory:result", player, __result);
}

[HarmonyPatch(typeof(NCard), nameof(NCard.Create))]
public static class CardNodeTracePatch
{
    [HarmonyPostfix]
    public static void Postfix(CardModel card, NCard? __result)
    {
        if (!CardRewardTrace.Enabled) return;
        try
        {
            CardRewardTrace.Card("ncard:bind", card,
                $"node={__result?.GetInstanceId()} portrait={card.PortraitPath} resourceExists={ResourceLoader.Exists(card.PortraitPath)}");
        }
        catch (Exception e) { MainFile.Logger.Warn($"[CardReward] phase=ncard:resource card={card.Id} {e}"); }
    }
}

[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Add), typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool))]
public static class CardPileTracePatch
{
    [HarmonyPrefix]
    public static void Prefix(IEnumerable<CardModel> cards, CardPile newPile)
    {
        if (!CardRewardTrace.Enabled) return;
        foreach (var card in cards) CardRewardTrace.Card("add:before", card, $"destination={newPile.Type}");
    }
    [HarmonyPostfix]
    public static void Postfix(ref Task<IReadOnlyList<CardPileAddResult>> __result, CardPile newPile)
    {
        if (CardRewardTrace.Enabled) __result = Trace(__result, newPile.Type);
    }
    private static async Task<IReadOnlyList<CardPileAddResult>> Trace(Task<IReadOnlyList<CardPileAddResult>> task, PileType pile)
    {
        try
        {
            var results = await task;
            foreach (var r in results)
                CardRewardTrace.Card("add:result", r.cardAdded, $"success={r.success} from={r.oldPile?.Type} requested={pile} actual={r.cardAdded.Pile?.Type}", !r.success);
            return results;
        }
        catch (Exception e) { MainFile.Logger.Error($"[CardReward] phase=add:exception destination={pile} {e}"); throw; }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.PopulateCombatState))]
public static class CombatCopyTracePatch
{
    [HarmonyPostfix]
    public static void Postfix(Player __instance)
    {
        if (!CardRewardTrace.Enabled) return;
        foreach (var card in __instance.PlayerCombatState.DrawPile.Cards)
            CardRewardTrace.Card("combat-copy", card, $"deckVersion={card.DeckVersion?.Id}");
    }
}

[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Draw), typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool))]
public static class DrawTracePatch
{
    [HarmonyPrefix]
    public static void Prefix(Player player, decimal count, bool fromHandDraw, out string __state)
    {
        __state = CardRewardTrace.Enabled ? CardRewardTrace.Piles(player) : "";
        if (CardRewardTrace.Enabled)
            MainFile.Logger.Info($"[CardReward] phase=draw:before {CardRewardTrace.Context(player)} requested={count} opening={fromHandDraw} {__state}");
    }
    [HarmonyPostfix]
    public static void Postfix(Player player, string __state, ref Task<IEnumerable<CardModel>> __result)
    {
        if (CardRewardTrace.Enabled) __result = Trace(__result, player, __state);
    }
    private static async Task<IEnumerable<CardModel>> Trace(Task<IEnumerable<CardModel>> task, Player player, string before)
    {
        try
        {
            var cards = (await task).ToList();
            MainFile.Logger.Info($"[CardReward] phase=draw:result {CardRewardTrace.Context(player)} returned={cards.Count} before=[{before}] after=[{CardRewardTrace.Piles(player)}]");
            foreach (var card in cards) CardRewardTrace.Card("draw:card", card);
            return cards;
        }
        catch (Exception e) { MainFile.Logger.Error($"[CardReward] phase=draw:exception {CardRewardTrace.Context(player)} {e}"); throw; }
    }
}

[HarmonyPatch(typeof(CardCreationOptions), nameof(CardCreationOptions.GetPossibleCards))]
public static class RewardPoolTracePatch
{
    [HarmonyPostfix]
    public static void Postfix(Player player, ref IEnumerable<CardModel> __result)
    {
        if (CardRewardTrace.Enabled) __result = Trace(__result, player);
    }
    private static IEnumerable<CardModel> Trace(IEnumerable<CardModel> source, Player player)
    {
        var ids = new List<string>();
        var complete = false;
        try
        {
            foreach (var card in source)
            {
                ids.Add($"{card.Id.Entry}:{card.Type}/{card.Rarity}");
                yield return card; // no second enumeration or RNG draw for the diagnostics
            }
            complete = true;
        }
        finally
        {
            MainFile.Logger.Info($"[CardReward] phase=pool:enumerated {CardRewardTrace.Context(player)} complete={complete} count={ids.Count} cards={string.Join(",", ids)}");
        }
    }
}


/// <summary>Observe the texture actually bound by the engine, not just the source path's existence.</summary>
[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
public static class CardPortraitBindingTracePatch
{
    [HarmonyPostfix]
    public static void Postfix(NCard __instance)
    {
        if (!CardRewardTrace.Enabled || __instance.Model is not { } card) return;
        try
        {
            var normal = __instance.GetNodeOrNull<TextureRect>("%Portrait");
            var ancient = __instance.GetNodeOrNull<TextureRect>("%AncientPortrait");
            var texture = ancient?.Visible == true ? ancient.Texture : normal?.Texture;
            var actual = texture?.ResourcePath ?? "null";
            CardRewardTrace.Card("portrait:bound", card,
                $"node={__instance.GetInstanceId()} requested={card.PortraitPath} actual={actual} size={texture?.GetSize()}",
                force: texture == null || actual.Contains("nope", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception e) { MainFile.Logger.Error($"[CardReward] phase=portrait:inspection card={card.Id}: {e}"); }
    }
    [HarmonyFinalizer]
    public static Exception? Finalizer(NCard __instance, Exception? __exception)
    {
        if (__exception != null) MainFile.Logger.Error($"[CardReward] phase=visuals:exception card={__instance.Model?.Id}: {__exception}");
        return __exception;
    }
}
