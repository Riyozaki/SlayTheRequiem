using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
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
            var panel = screen.GetNodeOrNull<NRewardActionRail>(PanelName);
            if (player == null || !RequiemMode.IsInSystem(player))
            { if (panel != null) panel.Visible = false; return; }
            if (panel == null)
            {
                panel = new NRewardActionRail { Name = PanelName, Screen = screen, MouseFilter = Control.MouseFilterEnum.Ignore };
                screen.AddChild(panel);
                panel.AnchorLeft = .02f; panel.AnchorRight = .23f;
                panel.AnchorTop = .22f; panel.AnchorBottom = .80f;
                var text = RequiemUi.MakeLabel(19, RequiemUi.Outline);
                text.Name = "State";
                text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                panel.AddChild(text);
                panel.AddChild(NHeroCreationScreen.MakeText(RequiemUi.TipText("UI.rewardControls.title"), 17));
                var details = RequiemKit.MakeButton(RequiemUi.TipText("UI.rewardDetails.title"), 21, new Vector2(0, 48));
                panel.AddChild(details);
                details.Pressed += () =>
                {
                    if (Options(screen).FirstOrDefault()?.Card.Owner is { } current)
                        NRequiemSheet.Show(current, RequiemUi.TipText("UI.rewardDetails.title"), () =>
                            [new NRequiemSheet.Page(RequiemUi.TipText("UI.rewardDetails.title"), Options(screen).Select(option =>
                            {
                                var card = option.Card; var allowed = CardAcquisition.CanTake(card, out var preventer);
                                var reason = allowed ? RewardPurchase.PreviewReason(card) ?? RequiemUi.TipText("REWARD_READY.title")
                                    : preventer is TravelPack ? RequiemUi.TipText(InventoryGate.For(current).Blocked
                                        ? "REWARD_OPERATION_BLOCKED.title" : "REWARD_OVERLOAD.title")
                                    : RequiemUi.TipText("REWARD_VETO.title", ("Source", preventer?.Id.Entry ?? "?"));
                                return new NRequiemSheet.Entry(card.Title, RequiemUi.TipText("REWARD_CARD_STATE.title",
                                    ("Card", card.Title), ("Weight", Loadout.CardWeight(card)), ("Reason", reason)) + "\n"
                                    + card.GetDescriptionForPile(PileType.None), RequiemUi.Texture(card.PortraitPath));
                            }).ToList())]);
                };
                var button = RequiemKit.MakeButton(RequiemUi.TipText("REWARD_UNLOAD.title"), 22, new Vector2(0, 48));
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
            panel.GetNode<Label>("State").Text = RequiemUi.TipText("REWARD_STATE.title", ("Weight", weight), ("Capacity", capacity));
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

/// <summary>The native card row keeps its own navigation. I/right-stick enters the side actions; B returns to it.</summary>
public partial class NRewardActionRail : VBoxContainer
{
    internal NCardRewardSelectionScreen Screen = null!;
    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || !ActiveScreenContext.Instance.IsCurrent(Screen)) return;
        var controls = GetChildren().OfType<Button>().Cast<Control>().ToList();
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.I }
            || input is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.RightStick })
        {
            controls.FirstOrDefault()?.GrabFocus(); GetViewport().SetInputAsHandled(); return;
        }
        if (GetViewport().GuiGetFocusOwner() is { } focus && IsAncestorOf(focus))
            RequiemFocus.Handle(this, input, controls, () => Screen.DefaultFocusedControl.GrabFocus());
    }
}
