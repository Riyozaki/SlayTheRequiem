using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Shops;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// «Выбросить» карту из колоды (02 §3: «выбрасывать можно в любой момент вне боя»; плейтест 2026-11 — при перегрузе
/// больше 10 игрок застревал: новые карты не брались, а избавиться от старых было нечем, кроме Сундука в лагере).
/// <para>
/// Кнопка на экране осмотра карты, открытом из экрана колоды. Чтобы не обесценить удаление карт в лавке (02 §3:
/// «удаление становится ещё ценнее»), выбросить можно только при перегрузе и только то, что игрок сам подобрал:
/// не базовые карты, не проклятия и статусы (их снимают лавка и события), не «Вечные» и не квестовые.
/// </para>
/// В коопе действие рассылается как <see cref="ShopAction.DiscardCard"/> (выполняется у всех одинаково).
/// </summary>
public static class DeckDiscard
{
    public static bool CanDiscard(CardModel card) =>
        card.IsRemovable
        && card.Rarity is not (CardRarity.Basic or CardRarity.Quest or CardRarity.Curse or CardRarity.Status)
        && card.Type is not (CardType.Curse or CardType.Status);

    /// <summary>Можно ли сейчас выбросить эту карту (локальный игрок, его колода, вне боя, перегруз).</summary>
    public static bool Available(CardModel card, out string reasonKey)
    {
        reasonKey = "";
        if (!card.IsMutable) return false;
        var owner = card.Owner;
        if (owner == null || !LocalContext.IsMe(owner) || !RequiemMode.IsInSystem(owner)) return false;
        if (card.Pile?.Type != PileType.Deck) return false;
        if (NCapstoneContainer.Instance?.CurrentCapstoneScreen is not NDeckViewScreen) return false;
        if (CombatManager.Instance.IsInProgress) return false;
        if (!CanDiscard(card)) { reasonKey = "DISCARD_NOT_THIS.title"; return true; }
        if (Loadout.Overload(owner).Excess <= 0) { reasonKey = "DISCARD_NOT_OVERLOADED.title"; return true; }
        return true;
    }

    public static async Task Discard(CardModel card)
    {
        var owner = card.Owner;
        if (owner == null) return;
        var index = owner.Deck.Cards.ToList().IndexOf(card);
        if (index < 0) return;
        await ShopActions.Perform(owner, ShopAction.DiscardCard, index, 0, card.Id.Entry);
    }
}

/// <summary>Кнопка «Выбросить» под крупной картой на экране осмотра.</summary>
[HarmonyPatch(typeof(NInspectCardScreen), "SetCard")]
public static class DeckDiscardInspectPatch
{
    private const string NodeName = "RequiemDiscard";

    private static readonly AccessTools.FieldRef<NInspectCardScreen, List<CardModel>?> CardsRef =
        AccessTools.FieldRefAccess<NInspectCardScreen, List<CardModel>?>("_cards");

    private static readonly AccessTools.FieldRef<NInspectCardScreen, int> IndexRef =
        AccessTools.FieldRefAccess<NInspectCardScreen, int>("_index");

    // Экран осмотра один на игру (NGame.GetInspectCardScreen), поэтому достаточно одного «текущего» состояния.
    private static CardModel? _card;
    private static bool _armed;

    [HarmonyPostfix]
    public static void Postfix(NInspectCardScreen __instance)
    {
        try
        {
            Refresh(__instance);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"UI: кнопка «Выбросить»: {e.Message}");
        }
    }

    private static void Refresh(NInspectCardScreen screen)
    {
        var cards = CardsRef(screen);
        var index = IndexRef(screen);
        var card = cards != null && index >= 0 && index < cards.Count ? cards[index] : null;
        _card = null;
        _armed = false;

        var holder = screen.GetNodeOrNull<VBoxContainer>(NodeName);
        if (card == null || !card.IsMutable || !DeckDiscard.Available(card, out var reason))
        {
            if (holder != null) holder.Visible = false;
            return;
        }

        holder ??= Build(screen);
        var btn = holder.GetNode<Button>("Button");
        var label = holder.GetNode<Label>("Hint");
        var weight = Loadout.CardWeight(card);
        btn.Text = RequiemUi.TipText("DISCARD_BUTTON.title", ("Weight", weight));
        btn.Disabled = reason != "";
        label.Text = reason != ""
            ? RequiemUi.TipText(reason)
            : RequiemUi.TipText("DISCARD_HINT.title", ("Weight", weight), ("Excess", Loadout.Overload(card.Owner).Excess));
        _card = card;
        holder.Visible = true;
    }

    private static VBoxContainer Build(NInspectCardScreen screen)
    {
        var holder = new VBoxContainer { Name = NodeName, MouseFilter = Control.MouseFilterEnum.Ignore };
        holder.AddThemeConstantOverride("separation", 6);
        var button = RequiemKit.MakeButton("", 24, new Vector2(300, 58), RequiemKit.Danger);
        button.Name = "Button";
        button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        var hint = RequiemUi.MakeLabel(17, RequiemUi.Outline);
        hint.Name = "Hint";
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hint.CustomMinimumSize = new Vector2(420, 0);
        holder.AddChild(button);
        holder.AddChild(hint);
        screen.AddChild(holder);
        // Внизу по центру, под картой: стрелки — по бокам, галочка «Улучшения» — справа.
        holder.AnchorLeft = 0.5f;
        holder.AnchorRight = 0.5f;
        holder.AnchorTop = 1f;
        holder.AnchorBottom = 1f;
        holder.OffsetLeft = -210;
        holder.OffsetRight = 210;
        holder.OffsetTop = -150;
        holder.OffsetBottom = -24;

        button.Pressed += () =>
        {
            var card = _card;
            if (card == null) return;
            if (!_armed)
            {
                // Первый клик — «Точно?», второй — выбросить. Без всплывающего окна поверх экрана осмотра.
                _armed = true;
                button.Text = RequiemUi.TipText("DISCARD_CONFIRM.title");
                RequiemKit.Shake(button, 5f);
                return;
            }
            _card = null;
            button.Disabled = true;
            holder.Visible = false;
            screen.Close();
            TaskHelper.RunSafely(DeckDiscard.Discard(card));
        };
        return holder;
    }
}
