using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>View of exact shelf entitlements. Never moves a card; a zero timer is a pending return, not Exhaust.</summary>
public partial class NShoutShelfBar : HBoxContainer
{
    private string _signature = "";
    private double _elapsed;
    public override void _Process(double delta)
    {
        _elapsed += delta; if (_elapsed < .25) return; _elapsed = 0;
        if (RequiemNet.State is not { } run || LocalContext.GetMe(run) is not { } owner || !RequiemMode.IsInSystem(owner)) { Visible = false; return; }
        var entries = owner.Creature.GetPower<ShoutShelfPower>()?.Entries ?? [];
        Visible = entries.Count > 0;
        var signature = string.Join("|", entries.Select(e => $"{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(e.Card)}:{e.Turns}"));
        if (signature == _signature) return; _signature = signature;
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        if (entries.Count == 0) return;
        var details = RequiemKit.MakeButton(RequiemUi.TipText("UI.journal.shouts.title"), 19, new Vector2(0, 48));
        details.Pressed += () => HeroJournal.ShowShelf(owner); AddChild(details);
        foreach (var entry in entries)
        {
            var item = new VBoxContainer { TooltipText = entry.Card.Title.GetFormattedText() };
            AddChild(item);
            item.AddChild(new TextureRect { Texture = RequiemUi.Texture(entry.Card.PortraitPath), CustomMinimumSize = new Vector2(44, 50),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore });
            var timer = RequiemUi.MakeLabel(18, RequiemUi.Outline);
            timer.Text = entry.Turns == 0 ? "↩" : entry.Turns.ToString(System.Globalization.CultureInfo.InvariantCulture);
            item.AddChild(timer);
        }
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
        shelf.AnchorLeft = .60f; shelf.AnchorRight = .94f; shelf.AnchorTop = .10f; shelf.AnchorBottom = .19f;
    }
}
