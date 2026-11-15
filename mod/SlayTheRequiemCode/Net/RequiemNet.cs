using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace SlayTheRequiem.SlayTheRequiemCode.Net;

/// <summary>
/// Сеть Реквиема для коопа. Свои сообщения не реализуют <c>INetMessage</c> напрямую (его подтипы регистрирует
/// генератор кода игры), а идут через обёртки BaseLib: <see cref="CustomMessageWrapper"/> (сразу) и
/// <see cref="CustomTargetedMessageWrapper"/> (с привязкой к месту забега — получатель обработает сообщение,
/// когда окажется в той же комнате, как ванильные сообщения лавки).
///
/// Правило всех действий вне боя: инициатор выполняет действие у себя и рассылает его; остальные выполняют то же
/// самое для игрока-отправителя. Результат детерминирован (никаких локальных случайностей в обработчике),
/// поэтому состояние у всех совпадает и попадает в сохранение хоста.
/// </summary>
public static class RequiemNet
{
    public static NetGameType Type => RunManager.Instance.NetService?.Type ?? NetGameType.None;

    public static bool IsMultiplayer => Type is NetGameType.Host or NetGameType.Client;
    public static bool IsHost => Type == NetGameType.Host;
    public static bool IsClient => Type == NetGameType.Client;
    public static bool IsSingleplayer => Type == NetGameType.Singleplayer;

    public static ulong LocalNetId => RunManager.Instance.NetService?.NetId ?? 0;

    public static RunState? State => RunManager.Instance.DebugOnlyGetState();

    public static Player? PlayerOf(ulong netId) => State?.GetPlayer(netId);

    public static RunLocation CurrentLocation => RunManager.Instance.RunLocationTargetedBuffer.CurrentLocation;

    /// <summary>Разослать немедленное сообщение (экран поражения, голосование). В одиночной игре — ничего.</summary>
    public static void Send(ICustomMessage message)
    {
        if (!IsMultiplayer) return;
        try { CustomMessageWrapper.Send(message); }
        catch (Exception e) { MainFile.Logger.Warn($"[SlayTheRequiem] Сеть: {message.GetType().Name} не отправлено ({e.Message})."); }
    }

    /// <summary>Разослать сообщение, привязанное к текущему месту забега (действия в лавке).</summary>
    public static void SendTargeted(ICustomTargetedMessage message)
    {
        if (!IsMultiplayer) return;
        try { CustomTargetedMessageWrapper.Send(message); }
        catch (Exception e) { MainFile.Logger.Warn($"[SlayTheRequiem] Сеть: {message.GetType().Name} не отправлено ({e.Message})."); }
    }

    /// <summary>
    /// Чисто локальный выбор карты (экран «выбери карту» без PlayerChoiceSynchronizer): для прилавка и экрана поражения,
    /// где результат рассылается своим сообщением. null — пропуск.
    /// </summary>
    public static async Task<CardModel?> ChooseCard(IReadOnlyList<CardModel> cards, bool canSkip)
    {
        if (cards.Count == 0) return null;
        var screen = NChooseACardSelectionScreen.ShowScreen(cards, canSkip);
        if (screen == null) return null;
        return (await screen.CardsSelected()).FirstOrDefault();
    }

    /// <summary>Окно «да/нет» с текстами из таблицы <c>relics</c>: ключи <c>{key}.{prefix}Body/Header/No/Yes</c>.</summary>
    public static async Task<bool> Confirm(string key, string prefix, Action<LocString>? fill = null)
    {
        var popup = NGenericPopup.Create();
        if (popup == null || NModalContainer.Instance == null) return false;
        NModalContainer.Instance.Add(popup);
        var body = new LocString("relics", $"{key}.{prefix}Body");
        fill?.Invoke(body);
        return await popup.WaitForConfirmation(body, new LocString("relics", $"{key}.{prefix}Header"),
            new LocString("relics", $"{key}.{prefix}No"), new LocString("relics", $"{key}.{prefix}Yes"));
    }
}
