using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.UI;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

/// <summary>
/// Интерфейс лавки (docs/design/14 §4, art spec 4.9, M6 п. 5). Слева от витрины — колонка вкладок и шкала груза:
/// <list type="bullet">
/// <item><b>Купить</b> — ванильная витрина; наведение на товар показывает на шкале груза вес «после покупки»
/// (<see cref="NBurdenMeter.PreviewDelta"/>), покупка, поднимающая ступень перегруза, спрашивает «Перегруз?».</item>
/// <item><b>Продать</b> — вместо витрины список снятого снаряжения из рюкзака (цена, вес, «Продать» по одному или всё
/// разом) и реагенты у Травницы; наведение на строку — превью груза «после продажи».</item>
/// <item><b>Услуги</b> — прилавок (<see cref="ShopServices.OpenCounter"/>) поверх витрины; раньше он открывался сам при
/// закрытии витрины.</item>
/// </list>
/// Продажа — те же синхронные действия <see cref="ShopAction.Sell"/> / <see cref="ShopAction.SellReagents"/> (кооп).
/// Вне системы мода — только ванильная витрина; без «Разнообразия лавок» — только шкала груза.
/// </summary>
public partial class NShopTabs : Control
{
    public const string NodeName = "StrShopTabs";
    private static readonly Color Gold = new("EFC851");
    private static readonly Color Dim = new(0.55f, 0.52f, 0.48f);
    private static readonly Color PanelColor = new(0.07f, 0.055f, 0.05f, 0.94f);

    private static readonly AccessTools.FieldRef<NMerchantInventory, Control> SlotsRef =
        AccessTools.FieldRefAccess<NMerchantInventory, Control>("_slotsContainer");

    private readonly NMerchantInventory _inventory;
    private readonly Player _player;
    private readonly bool _tabsOn;
    private readonly Dictionary<ShopTab, Button> _buttons = new();
    private NBurdenMeter _meter = null!;
    private PanelContainer _sellPanel = null!;
    private VBoxContainer _sellRows = null!;
    private ScrollContainer _sellScroll = null!;
    private ShopTab _tab = ShopTab.Buy;
    private bool _busy, _wasOpen;
    private double _sinceRefresh = 1;
    private int _sellCount, _serviceCount;

    public static void Attach(NMerchantInventory inventory)
    {
        var player = inventory.Inventory?.Player;
        if (player == null || !LocalContext.IsMe(player) || !RequiemMode.IsInSystem(player)) return;
        if (inventory.GetNodeOrNull(NodeName) != null) return;
        var tabs = new NShopTabs(inventory, player);
        inventory.AddChild(tabs);
        tabs.SetAnchorsPreset(LayoutPreset.FullRect);
        tabs.Build();
        foreach (var slot in inventory.GetAllSlots())
        {
            slot.Connect(NMerchantSlot.SignalName.Hovered, Callable.From<NMerchantSlot>(s => tabs.Preview(EntryWeight(player, s.Entry))));
            slot.Connect(NMerchantSlot.SignalName.Unhovered, Callable.From<NMerchantSlot>(_ => tabs.Preview(0)));
        }
    }

    /// <summary>Для движка Godot (пересоздание скрипта); в игре узел строит <see cref="Attach"/>.</summary>
    public NShopTabs()
    {
        _inventory = null!;
        _player = null!;
    }

    private NShopTabs(NMerchantInventory inventory, Player player)
    {
        Name = NodeName;
        _inventory = inventory;
        _player = player;
        _tabsOn = MerchantLedger.Active(player);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    /// <summary>Вес товара витрины для превью и окна «Перегруз?»: карта — по редкости и чарам, реликвия — как в Грузе.</summary>
    public static int EntryWeight(Player player, MerchantEntry? entry)
    {
        if (entry is not { IsStocked: true }) return 0;
        return entry switch
        {
            MerchantCardEntry { CreationResult.Card: { } card } => Loadout.CardWeight(card),
            MerchantRelicEntry { Model: { } relic } => Loadout.RelicWeight(player, relic),
            _ => 0,
        };
    }

    private void Preview(int delta)
    {
        if (_meter != null && GodotObject.IsInstanceValid(_meter)) _meter.PreviewDelta = delta;
    }

    // ───── построение ─────

    private void Build()
    {
        var view = GetViewportRect().Size;
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(36, view.Y * 0.24f) };
        column.AddThemeConstantOverride("separation", 10);
        AddChild(column);
        if (_tabsOn)
        {
            foreach (var tab in new[] { ShopTab.Buy, ShopTab.Sell, ShopTab.Services })
            {
                var button = MakeButton("", 24, new Vector2(Math.Min(300, view.X * .23f), 58));
                button.ToggleMode = true;
                var t = tab;
                button.Pressed += () => SetTab(t);
                _buttons[tab] = button;
                column.AddChild(button);
            }
            column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8), MouseFilter = MouseFilterEnum.Ignore });
        }
        _meter = new NBurdenMeter(_player, compact: false);
        column.AddChild(_meter);
        column.AddChild(NHeroCreationScreen.MakeText(RequiemUi.TipText("UI.shopControls.title"), 17));

        _sellPanel = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Stop };
        _sellPanel.AddThemeStyleboxOverride("panel", Box(PanelColor, Gold.Darkened(0.35f), 3, 14));
        AddChild(_sellPanel);
        _sellPanel.AnchorLeft = .29f; _sellPanel.AnchorRight = .94f;
        _sellPanel.AnchorTop = .12f; _sellPanel.AnchorBottom = .85f;
        _sellScroll = new ScrollContainer { FollowFocus = true, SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _sellPanel.AddChild(_sellScroll);
        _sellRows = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _sellRows.AddThemeConstantOverride("separation", 8);
        _sellRows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _sellScroll.AddChild(_sellRows);
        RefreshTabs();
    }

    public override void _Process(double delta)
    {
        if (_inventory == null || !GodotObject.IsInstanceValid(_inventory) || _sellPanel == null) return;
        var open = _inventory.IsOpen;
        if (open != _wasOpen)
        {
            _wasOpen = open;
            if (open)
            {
                Visible = true;
                Modulate = Colors.White;
                RequiemKit.PopIn(this, dy: 0);
                SetTab(ShopTab.Buy);
            }
            else
            {
                Visible = false;
                ShowSlots(true);
                _sellPanel.Visible = false;
                _tab = ShopTab.Buy;
                Preview(0);
            }
        }
        if (!open) return;
        _sinceRefresh += delta;
        if (_sinceRefresh < 0.4) return;
        _sinceRefresh = 0;
        RefreshTabs();
    }

    public override void _Input(InputEvent input)
    {
        if (!Visible || !_tabsOn || !_inventory.IsOpen || !ActiveScreenContext.Instance.IsCurrent(_inventory)) return;
        var focused = GetViewport().GuiGetFocusOwner();
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.I }
            || input is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.RightStick })
        {
            _buttons[ShopTab.Buy].GrabFocus(); GetViewport().SetInputAsHandled(); return;
        }
        if (focused == null || !IsAncestorOf(focused)) return;
        if (_tab == ShopTab.Sell && RequiemFocus.Scroll(this, input, _sellScroll)) return;
        var controls = _buttons.Values.Cast<Control>().Concat(_tab == ShopTab.Sell
            ? _sellRows.FindChildren("*", nameof(Button), true, false).OfType<Control>() : []).ToList();
        RequiemFocus.Handle(this, input, _busy ? [] : controls, _busy ? null : () =>
        { SetTab(ShopTab.Buy); _inventory.DefaultFocusedControl?.GrabFocus(); });
    }

    private void RefreshTabs()
    {
        if (!_tabsOn) return;
        _sellCount = SellRows().Count + (ReagentRow() != null ? 1 : 0);
        _serviceCount = ShopServices.ServiceCount(_player);
        var active = ShopServices.CounterOpen ? ShopTab.Services : _tab;
        foreach (var (tab, button) in _buttons)
        {
            var badge = ShopUiRules.TabBadge(tab, _sellCount, _serviceCount);
            var title = RequiemUi.TipText($"SHOP_TAB_{tab.ToString().ToUpperInvariant()}.title");
            button.Text = badge > 0 ? $"{title} ({badge})" : title;
            button.Disabled = _busy || !ShopUiRules.TabEnabled(tab, _sellCount, _serviceCount) && tab != active;
            button.SetPressedNoSignal(tab == active);
        }
        if (_tab == ShopTab.Sell && _sellCount == 0 && !_busy) SetTab(ShopTab.Buy);
    }

    private void SetTab(ShopTab tab)
    {
        if (_busy) return;
        switch (tab)
        {
            case ShopTab.Sell when _tabsOn:
                _tab = ShopTab.Sell;
                ShowSlots(false);
                RebuildSell();
                _sellPanel.Visible = true;
                break;
            case ShopTab.Services when _tabsOn:
                _tab = ShopTab.Buy;
                ShowSlots(true);
                _sellPanel.Visible = false;
                if (!ShopServices.CounterOpen && ShopServices.HasOptions(_player))
                    TaskHelper.RunSafely(ShopServices.OpenCounter(_player));
                break;
            default:
                _tab = ShopTab.Buy;
                ShowSlots(true);
                _sellPanel.Visible = false;
                break;
        }
        Preview(0);
        RefreshTabs();
    }

    private void ShowSlots(bool shown)
    {
        var slots = SlotsRef(_inventory);
        if (slots != null && GodotObject.IsInstanceValid(slots)) slots.Visible = shown;
    }

    // ───── продажа ─────

    private (int Trust, bool Fence) Terms()
    {
        var ledger = MerchantLedger.Of(_player);
        return (ledger?.CurrentTrust ?? 0, ledger?.CurrentShop == ShopType.Fence);
    }

    private List<(EquipmentRelic Item, int Value, int Weight)> SellRows()
    {
        var (trust, fence) = Terms();
        var items = SellService.Sellable(_player)
            .Select(i => (Item: i, Value: SellService.Value(i, trust, fence), Weight: Loadout.RelicWeight(_player, i)))
            .ToList();
        return ShopUiRules.SellOrder(items.Select(i => (i.Value, i.Weight)).ToList()).Select(i => items[i]).ToList();
    }

    /// <summary>Реагенты у Травницы: (штук, выручка) или null.</summary>
    private (int Count, int Value)? ReagentRow()
    {
        if (MerchantLedger.Of(_player)?.CurrentShop != ShopType.Herbalist) return null;
        var total = HerbalistPouch.Of(_player)?.Total ?? 0;
        return total > 0 ? (total, ShopRules.ReagentSellValue(total)) : null;
    }

    private void RebuildSell()
    {
        foreach (var child in _sellRows.GetChildren()) { _sellRows.RemoveChild(child); child.QueueFree(); }
        var (trust, fence) = Terms();
        var header = RequiemUi.MakeLabel(30, RequiemUi.Outline);
        header.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        header.Text = RequiemUi.TipText("SHOP_SELL_HEADER.title", ("Percent", ShopRules.SellPercent(trust, fence)));
        RequiemUi.SetLabelColors(header, Gold, RequiemUi.Outline);
        _sellRows.AddChild(header);
        var hint = RequiemUi.MakeLabel(18, RequiemUi.Outline);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hint.Text = RequiemUi.TipText("SHOP_SELL_HINT.title");
        RequiemUi.SetLabelColors(hint, Dim, RequiemUi.Outline);
        _sellRows.AddChild(hint);
        _sellRows.AddChild(new HSeparator());

        var rows = SellRows();
        foreach (var row in rows)
        {
            var item = row.Item;
            AddRow(item.Icon, item.Title.GetFormattedText(), row.Weight, row.Value, -row.Weight,
                () => Sell(() => SellService.Sell(_player, [item], trust, fence)));
        }
        if (ReagentRow() is { } reagents)
        {
            AddRow(HerbalistPouch.Of(_player)?.Icon,
                RequiemUi.TipText("SHOP_SELL_REAGENTS.title", ("Count", reagents.Count)), 0, reagents.Value, 0,
                () => Sell(() => ShopActions.Perform(_player, ShopAction.SellReagents, reagents.Value, 0)));
        }
        if (rows.Count == 0 && ReagentRow() == null)
        {
            var empty = RequiemUi.MakeLabel(22, RequiemUi.Outline);
            empty.Text = RequiemUi.TipText("SHOP_SELL_EMPTY.title");
            _sellRows.AddChild(empty);
        }

        _sellRows.AddChild(new HSeparator());
        var footer = new HFlowContainer { MouseFilter = MouseFilterEnum.Ignore };
        footer.AddThemeConstantOverride("separation", 16);
        _sellRows.AddChild(footer);
        if (rows.Count > 1)
        {
            var total = rows.Sum(r => r.Value);
            var all = MakeButton(RequiemUi.TipText("SHOP_SELL_ALL.title", ("Gold", total)), 22, new Vector2(340, 52));
            var items = rows.Select(r => r.Item).ToList();
            var weight = rows.Sum(r => r.Weight);
            all.MouseEntered += () => Preview(-weight);
            all.MouseExited += () => Preview(0);
            all.Pressed += () => Sell(() => SellService.Sell(_player, items, trust, fence));
            footer.AddChild(all);
        }
        var back = MakeButton(RequiemUi.TipText("SHOP_SELL_BACK.title"), 22, new Vector2(240, 52));
        back.Pressed += () => SetTab(ShopTab.Buy);
        footer.AddChild(back);
        var focus = _buttons.Values.Concat(_sellRows.FindChildren("*", nameof(Button), true, false).OfType<Button>()).ToList();
        RequiemFocus.Wire(focus);
        back.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void AddRow(Texture2D? icon, string title, int weight, int value, int preview, Action sell)
    {
        var row = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 6);
        var heading = new HBoxContainer(); row.AddChild(heading);
        heading.AddChild(new TextureRect { Texture = icon, CustomMinimumSize = new Vector2(52, 52),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        var name = NHeroCreationScreen.MakeText(title, 22); heading.AddChild(name);
        row.AddChild(NHeroCreationScreen.MakeText(RequiemUi.TipText("UI.sellQuote.title", ("Weight", weight), ("Gold", value)), 20));
        var button = MakeButton(RequiemUi.TipText("SHOP_SELL_ONE.title"), 20, new Vector2(0, 48));
        button.MouseEntered += () => Preview(preview); button.MouseExited += () => Preview(0);
        button.FocusEntered += () => Preview(preview); button.FocusExited += () => Preview(0);
        button.Pressed += sell; row.AddChild(button);
        _sellRows.AddChild(row);
    }

    private void Sell(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        foreach (var button in _sellRows.FindChildren("*", nameof(Button), true, false).OfType<Button>()) button.Disabled = true;
        TaskHelper.RunSafely(SellAndRefresh(action));
    }

    private async Task SellAndRefresh(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Продажа: {e.Message}");
        }
        finally
        {
            _busy = false;
            if (GodotObject.IsInstanceValid(this))
            {
                Preview(0);
                if (_tab == ShopTab.Sell) RebuildSell();
                RefreshTabs();
            }
        }
    }

    // ───── оформление ─────

    private static StyleBoxFlat Box(Color bg, Color border, int width, int radius = 10)
    {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        box.SetBorderWidthAll(width);
        box.SetCornerRadiusAll(radius);
        box.SetContentMarginAll(12);
        return box;
    }

    private static Button MakeButton(string text, int fontSize, Vector2 size)
    {
        var button = new Button { Text = text, CustomMinimumSize = size, FocusMode = FocusModeEnum.All };
        button.AddThemeFontOverride("font", RequiemUi.Font);
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeColorOverride("font_color", StsColors.cream);
        button.AddThemeColorOverride("font_hover_color", Gold);
        button.AddThemeColorOverride("font_pressed_color", Gold);
        button.AddThemeColorOverride("font_hover_pressed_color", Gold);
        button.AddThemeColorOverride("font_disabled_color", Dim);
        button.AddThemeColorOverride("font_outline_color", RequiemUi.Outline);
        button.AddThemeConstantOverride("outline_size", 8);
        button.AddThemeStyleboxOverride("normal", Box(new Color(0.06f, 0.05f, 0.07f, 0.92f), Gold.Darkened(0.45f), 3));
        button.AddThemeStyleboxOverride("hover", Box(new Color(0.1f, 0.08f, 0.1f, 0.95f), Gold, 4));
        button.AddThemeStyleboxOverride("pressed", Box(new Color(0.16f, 0.12f, 0.08f, 0.96f), Gold.Lightened(0.3f), 5));
        button.AddThemeStyleboxOverride("hover_pressed", Box(new Color(0.18f, 0.14f, 0.09f, 0.96f), Gold.Lightened(0.4f), 5));
        button.AddThemeStyleboxOverride("focus", Box(new Color(0, 0, 0, 0), Gold.Lightened(0.2f), 4));
        button.AddThemeStyleboxOverride("disabled", Box(new Color(0.05f, 0.05f, 0.06f, 0.85f), Gold.Darkened(0.65f), 3));
        return button;
    }
}

/// <summary>Вкладки и шкала груза появляются, когда витрина заполнена (у неё уже есть игрок и слоты).</summary>
[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Initialize))]
public static class ShopTabsPatch
{
    [HarmonyPostfix]
    public static void Postfix(NMerchantInventory __instance)
    {
        try
        {
            NShopTabs.Attach(__instance);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Вкладки лавки: {e.Message}");
        }
    }
}

/// <summary>
/// «Перегруз?» (14 §1): покупка, поднимающая ступень перегруза (<see cref="ShopUiRules.NeedsOverloadWarning"/>), — только
/// после подтверждения. Заклинание выше владения сначала спрашивает своё окно (<see cref="SpellMasteryWarningPatch"/>),
/// потом это — оба окна по очереди при любом порядке префиксов.
/// </summary>
[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.OnTryPurchaseWrapper))]
public static class OverloadWarningPatch
{
    private static readonly AccessTools.FieldRef<MerchantEntry, Player> PlayerRef =
        AccessTools.FieldRefAccess<MerchantEntry, Player>("_player");

    private static bool _confirmed;

    /// <summary>Сброс при новом забеге (ревью F-06).</summary>
    internal static void ResetForNewRun() => _confirmed = false;

    [HarmonyPrefix]
    public static bool WarnAboutOverload(MerchantEntry __instance, MerchantInventory? inventory, bool ignoreCost, ref Task<bool> __result)
    {
        if (ignoreCost || _confirmed || !__instance.IsStocked || !__instance.EnoughGold) return true;
        var player = PlayerRef(__instance);
        if (player == null || !LocalContext.IsMe(player) || !RequiemMode.IsInSystem(player)) return true;
        if (ShopServices.MasteryGap(player, __instance) > 0 && !ShopServices.MasteryConfirmed) return true; // сначала окно владения
        var weight = NShopTabs.EntryWeight(player, __instance);
        if (weight <= 0) return true;
        var preview = ShopUiRules.Preview(Loadout.TotalWeight(player), Loadout.Capacity(player), weight);
        if (!ShopUiRules.NeedsOverloadWarning(preview)) return true;
        __result = ConfirmAndBuy(__instance, inventory, weight, preview);
        return false;
    }

    private static async Task<bool> ConfirmAndBuy(MerchantEntry entry, MerchantInventory? inventory, int weight, BurdenPreview preview)
    {
        if (!await RequiemNet.Confirm("SLAYTHEREQUIEM-MERCHANT_LEDGER", "overload", body =>
            {
                body.Add("Weight", weight);
                body.Add("After", preview.After);
                body.Add("Capacity", preview.Capacity);
                body.Add("Excess", preview.ExcessAfter);
            }))
            return false;
        _confirmed = true;
        try { return await entry.OnTryPurchaseWrapper(inventory); }
        finally { _confirmed = false; }
    }
}
