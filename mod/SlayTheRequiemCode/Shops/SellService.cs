using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

/// <summary>
/// Продажа (docs/design/14 §1): снаряжение из рюкзака (снятое, только тянет Груз) — за 35 % цены, при доверии 3 — за 50 %.
/// Ванильная сцена магазина не имеет слота «продать», поэтому при входе в лавку торговец предлагает выкупить
/// всё содержимое рюкзака одним окном (да/нет). Карты не продаются (их удаляют).
/// Пока только в одиночной игре: в коопе продажа из локального окна требует синхронизации (этап «Кооп»).
/// </summary>
public static class SellService
{
    public static IReadOnlyList<EquipmentRelic> Sellable(Player player) => Loadout.All(player).Where(r => r.IsStowed).ToList();

    public static int Value(EquipmentRelic item, int trust) => ShopRules.SellValue(item.MerchantCost, trust);

    public static Task OfferBackpackSale(Player player, MerchantLedger ledger)
    {
        if (RunManager.Instance.NetService.Type != NetGameType.Singleplayer || !LocalContext.IsMe(player)) return Task.CompletedTask;
        if (Sellable(player).Count == 0) return Task.CompletedTask;
        // Не держим хук входа в комнату, пока игрок думает: окно — отдельной задачей поверх лавки.
        TaskHelper.RunSafely(AskAndSell(player, ledger));
        return Task.CompletedTask;
    }

    private static async Task AskAndSell(Player player, MerchantLedger ledger)
    {
        var items = Sellable(player);
        if (items.Count == 0) return;
        var trust = ledger.TrustOf(ledger.CurrentShop);
        var total = items.Sum(i => Value(i, trust));

        var popup = NGenericPopup.Create();
        if (popup == null || NModalContainer.Instance == null) return;
        NModalContainer.Instance.Add(popup);

        const string key = "SLAYTHEREQUIEM-MERCHANT_LEDGER";
        var body = new LocString("relics", $"{key}.sellBody");
        body.Add("Count", items.Count);
        body.Add("Gold", total);
        body.Add("Percent", trust >= ShopRules.MaxTrust ? 50 : 35);
        var header = new LocString("relics", $"{key}.sellHeader");
        var no = new LocString("relics", $"{key}.sellNo");
        var yes = new LocString("relics", $"{key}.sellYes");
        if (!await popup.WaitForConfirmation(body, header, no, yes)) return;

        // Состав рюкзака мог измениться, пока окно было открыто.
        var still = Sellable(player).Where(i => items.Contains(i)).ToList();
        if (still.Count == 0) return;
        var gold = still.Sum(i => Value(i, trust));
        foreach (var item in still) await RelicCmd.Remove(item);
        await PlayerCmd.GainGold(gold, player);
    }
}
