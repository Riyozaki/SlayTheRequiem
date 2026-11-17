using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Общая замена карты в награде для трёх правил мода (свои карты героя, заклинания, карты Путей).
/// <list type="bullet">
/// <item>Сохраняет рол улучшения: игра бросает его до хуков (<c>CardFactory.CreateForReward</c>), и прежде улучшенная
/// ванильная карта молча становилась неулучшенной картой мода.</item>
/// <item>Не даёт исключению из правила мода оборвать всю награду (плейтест 2026-11: «то 0 карт, то 2 из 3»):
/// <see cref="Guard"/> ловит ошибку, пишет её в лог и оставляет награду как есть.</item>
/// </list>
/// </summary>
public static class RewardSwap
{
    public static void Replace(CardCreationResult option, CardModel canonical, Player player)
    {
        var wasUpgraded = option.Card.IsUpgraded;
        var card = player.RunState.CreateCard(canonical, player);
        if (wasUpgraded && card.IsUpgradable) CardCmd.Upgrade(card);
        option.ModifyCard(card);
    }

    public static bool Guard(string rule, Func<bool> body)
    {
        try
        {
            return body();
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"[CardReward] правило «{rule}» упало, награда оставлена без его замен: {e}");
            return false;
        }
    }
}
