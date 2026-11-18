using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Read-only view of EXACT shelf entitlements. Never moves a card or infers Exhaust from a zero timer.
/// Cells survive timer changes (keyboard/gamepad focus is not destroyed every owner turn). Persistent text,
/// used/capacity and observed return-to-Hand/Discard remain with motion/VFX/SFX off. No new awaited animation.
/// </summary>
public partial class NShoutShelfBar : VBoxContainer
{
    private sealed class Slot(Button button, Label timer)
    {
        internal readonly Button Button = button;
        internal readonly Label Timer = timer;
        internal int Turns = int.MinValue;
        internal string Language = "";
    }
    private readonly Dictionary<CardModel, Slot> _slots = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<CardModel> _present = new(ReferenceEqualityComparer.Instance);
    private readonly List<CardModel> _removed = [];
    private HBoxContainer _row = null!;
    private Button _details = null!;
    private Label _notice = null!;
    private Player? _owner;
    private object? _combat;
    private double _elapsed = .25, _noticeLeft;
    private bool _hadSnapshot;

    public override void _Ready()
    {
        _row = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        AddChild(_row);
        _details = RequiemKit.MakeButton("", 18, new Vector2(170, 68));
        _details.Pressed += () => { if (_owner is { } owner) HeroJournal.ShowShelf(owner); };
        _row.AddChild(_details);
        _notice = RequiemUi.MakeLabel(18, RequiemUi.Outline);
        _notice.HorizontalAlignment = HorizontalAlignment.Left;
        _notice.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        AddChild(_notice);
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_noticeLeft > 0)
        {
            _noticeLeft = Math.Max(0, _noticeLeft - delta);
            if (_noticeLeft == 0) { _notice.Visible = false; Visible = _slots.Count > 0; }
        }
        _elapsed += delta;
        if (_elapsed < .2) return;
        _elapsed = 0;
        if (RequiemNet.State is not { } run || LocalContext.GetMe(run) is not { } owner || !RequiemMode.IsInSystem(owner)
            || owner.Creature.CombatState == null)
        {
            ClearSlots(); _owner = null; _combat = null; _hadSnapshot = false; _noticeLeft = 0; Visible = false;
            return;
        }
        if (!ReferenceEquals(owner, _owner) || !ReferenceEquals(owner.Creature.CombatState, _combat))
        {
            ClearSlots(); _owner = owner; _combat = owner.Creature.CombatState; _hadSnapshot = false; _noticeLeft = 0;
        }
        var entries = owner.Creature.GetPower<ShoutShelfPower>()?.Entries ?? [];
        var language = LocManager.Instance.Language;
        _present.Clear();
        foreach (var entry in entries)
        {
            _present.Add(entry.Card);
            if (!_slots.TryGetValue(entry.Card, out var slot))
            {
                slot = MakeSlot(entry.Card);
                _slots.Add(entry.Card, slot);
                if (_hadSnapshot) Notice("UI.shelf.departed.title", entry.Card);
                RequiemKit.PopIn(slot.Button, dy: 0);
            }
            if (slot.Turns == entry.Turns && slot.Language == language) continue;
            slot.Turns = entry.Turns; slot.Language = language;
            slot.Timer.Text = entry.Turns == 0 ? RequiemUi.TipText("UI.shelf.readyLabel.title")
                : RequiemUi.TipText("UI.shelf.waitLabel.title", ("Turns", entry.Turns));
            slot.Button.TooltipText = RequiemUi.TipText(entry.Turns == 0 ? "UI.shelf.pending.title" : "UI.shelf.waiting.title",
                ("Name", RequiemText.Plain(entry.Card.Title.GetFormattedText())), ("Turns", entry.Turns));
        }
        _removed.Clear();
        foreach (var card in _slots.Keys) if (!_present.Contains(card)) _removed.Add(card);
        foreach (var card in _removed)
        {
            var slot = _slots[card]; _slots.Remove(card);
            _row.RemoveChild(slot.Button); slot.Button.QueueFree();
            // Absence alone is NOT a return: combat cleanup, owner transfer or an external Exhaust has no return cue.
            if (!_hadSnapshot || !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsEnding
                || card.Owner != owner || !ReferenceEquals(owner.Creature.CombatState, _combat)) continue;
            if (card.Pile?.Type == PileType.Hand) Notice("UI.shelf.returnedHand.title", card);
            else if (card.Pile?.Type == PileType.Discard) Notice("UI.shelf.returnedDiscard.title", card);
        }
        _details.Text = RequiemUi.TipText("UI.shelf.capacity.title", ("Used", entries.Count), ("Capacity", Shouts.Capacity(owner)));
        _row.Visible = entries.Count > 0;
        _notice.Visible = _noticeLeft > 0;
        Visible = entries.Count > 0 || _noticeLeft > 0;
        _hadSnapshot = true;
    }

    private Slot MakeSlot(CardModel card)
    {
        var button = RequiemKit.MakeButton("", 18, new Vector2(90, 78));
        button.Pressed += () => { if (_owner is { } owner) HeroJournal.ShowShelf(owner); };
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        column.SetAnchorsPreset(LayoutPreset.FullRect);
        column.OffsetLeft = column.OffsetTop = 4; column.OffsetRight = column.OffsetBottom = -4;
        var portrait = new TextureRect
        {
            Texture = RequiemUi.Texture(card.PortraitPath), CustomMinimumSize = new Vector2(54, 41),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        column.AddChild(portrait);
        var timer = RequiemUi.MakeLabel(18, RequiemUi.Outline); column.AddChild(timer);
        button.AddChild(column); _row.AddChild(button);
        return new Slot(button, timer);
    }

    private void Notice(string key, CardModel card)
    {
        _notice.Text = RequiemUi.TipText(key, ("Name", RequiemText.Plain(card.Title.GetFormattedText())));
        _notice.TooltipText = _notice.Text;
        _noticeLeft = 3;
    }
    private void ClearSlots()
    {
        foreach (var slot in _slots.Values) { _row.RemoveChild(slot.Button); slot.Button.QueueFree(); }
        _slots.Clear(); _present.Clear(); _removed.Clear();
    }
}

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom._Ready))]
public static class ShoutShelfBarPatch
{
    [HarmonyPostfix]
    public static void Postfix(NCombatRoom __instance)
    {
        if (__instance.GetNodeOrNull("RequiemShoutShelf") != null) return;
        var shelf = new NShoutShelfBar { Name = "RequiemShoutShelf", MouseFilter = Control.MouseFilterEnum.Pass };
        __instance.AddChild(shelf);
        shelf.AnchorLeft = .60f; shelf.AnchorRight = .94f; shelf.AnchorTop = .10f; shelf.AnchorBottom = .205f;
    }
}
