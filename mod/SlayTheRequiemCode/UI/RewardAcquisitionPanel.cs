using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Debugging;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>A durable pre-choice explanation and a route to unloading. The original explicit skip is untouched.</summary>
[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.RefreshOptions))]
public static class RewardAcquisitionPanel
{
    private const string PanelName = "RequiemAcquisition";
    private static readonly AccessTools.FieldRef<NCardRewardSelectionScreen, IReadOnlyList<CardCreationResult>> Options =
        AccessTools.FieldRefAccess<NCardRewardSelectionScreen, IReadOnlyList<CardCreationResult>>("_options");

    [HarmonyPostfix]
    public static void Postfix(NCardRewardSelectionScreen __instance) => Refresh(__instance);

    internal static void Refresh(NCardRewardSelectionScreen screen)
    {
        try
        {
            var options = Options(screen);
            var player = options.FirstOrDefault()?.Card.Owner;
            var panel = screen.GetNodeOrNull<VBoxContainer>(PanelName);
            if (player == null || !RequiemMode.IsInSystem(player))
            { if (panel != null) panel.Visible = false; return; }
            if (panel == null)
            {
                panel = new VBoxContainer { Name = PanelName, MouseFilter = Control.MouseFilterEnum.Ignore };
                screen.AddChild(panel);
                panel.AnchorLeft = 0.5f; panel.AnchorRight = 0.5f;
                panel.AnchorTop = 1f; panel.AnchorBottom = 1f;
                panel.OffsetLeft = -520; panel.OffsetRight = 520; panel.OffsetTop = -218; panel.OffsetBottom = -28;
                var text = RequiemUi.MakeLabel(19, RequiemUi.Outline);
                text.Name = "State";
                text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                panel.AddChild(text);
                var button = RequiemKit.MakeButton(RequiemUi.TipText("REWARD_UNLOAD.title"), 22, new Vector2(360, 48));
                button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
                panel.AddChild(button);
                // CapstoneContainer hides overlays and restores THIS reward on close; the rolled models are retained.
                button.Pressed += () =>
                {
                    // This screen can be reused; never retain a player from a previous reward/run in the callback.
                    if (Options(screen).FirstOrDefault()?.Card.Owner is { } current) NDeckViewScreen.ShowScreen(current);
                };
            }
            panel.Visible = true;
            var weight = Loadout.TotalWeight(player);
            var capacity = Loadout.Capacity(player);
            var lines = new List<string>();
            foreach (var option in options)
            {
                var card = option.Card;
                var allowed = CardAcquisition.CanTake(card, out var preventer);
                var reason = allowed ? RewardPurchase.PreviewReason(card) ?? RequiemUi.TipText("REWARD_READY.title")
                    : preventer is TravelPack ? RequiemUi.TipText(InventoryGate.For(player).Blocked
                        ? "REWARD_OPERATION_BLOCKED.title" : "REWARD_OVERLOAD.title")
                    : RequiemUi.TipText("REWARD_VETO.title", ("Source", preventer?.Id.Entry ?? "?"));
                lines.Add(RequiemUi.TipText("REWARD_CARD_STATE.title", ("Card", card.Title), ("Weight", Loadout.CardWeight(card)), ("Reason", reason)));
            }
            panel.GetNode<Label>("State").Text = RequiemUi.TipText("REWARD_STATE.title", ("Weight", weight), ("Capacity", capacity))
                + "\n" + string.Join("\n", lines);
            if (!CardRewardTrace.Enabled) return;
            var holders = screen.GetNode<Control>("UI/CardRow").GetChildren().OfType<NGridCardHolder>()
                .Where(h => !h.IsQueuedForDeletion()).ToList();
            MainFile.Logger.Info($"[CardReward] phase=holders models={options.Count} liveHolders={holders.Count}");
            foreach (var holder in holders)
                CardRewardTrace.Card("holder:bind", holder.CardModel,
                    $"holder={holder.GetInstanceId()} node={holder.CardNode?.GetInstanceId()} visible={holder.Visible} inTree={holder.IsInsideTree()}");
        }
        catch (Exception e) { MainFile.Logger.Warn($"[CardReward] Acquisition panel: {e}"); }
    }
}

[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.AfterOverlayShown))]
public static class RewardAcquisitionResumePatch
{
    [HarmonyPostfix]
    public static void Postfix(NCardRewardSelectionScreen __instance) => RewardAcquisitionPanel.Refresh(__instance);
}

[HarmonyPatch(typeof(NCardRewardSelectionScreen), "SelectCard")]
public static class RewardSelectionTracePatch
{
    [HarmonyPrefix]
    public static void Prefix(NCardHolder cardHolder) => CardRewardTrace.Card("selection", cardHolder.CardModel);
}
