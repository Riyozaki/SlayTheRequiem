using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Connection;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Net;

namespace SlayTheRequiem.SlayTheRequiemCode.Rollback;

/// <summary>
/// Камень Памяти в коопе (docs/design/15 §5). Экран поражения у всех; дальше:
/// <list type="number">
/// <item>Хост предлагает возврат (у него Отпечаток и сохранение кооп-забега) → <see cref="StonePhase.Proposal"/>.</item>
/// <item>Каждый клиент голосует → <see cref="StonePhase.Vote"/>. Большинство «за» (ничья — решает хост, его голос «за»);
///       не ответившие за <see cref="VoteTimeout"/> не учитываются.</item>
/// <item>Хост объявляет итог → <see cref="StonePhase.Decision"/>. При «да» каждый выбирает свой Шрам →
///       <see cref="StonePhase.Scar"/>; кто не выбрал за <see cref="ScarTimeout"/>, получает Шрам по умолчанию.</item>
/// <item>Хост дописывает в Отпечаток общие Свечи, Шрамы всех игроков и «новую карту», записывает его как сохранение
///       кооп-забега и рассылает <see cref="StonePhase.Go"/>. Все выходят в главное меню; хост сразу открывает лобби
///       загрузки этого сохранения, клиенты сами ищут его лобби и входят (<see cref="RejoinAsClient"/>; экран
///       «Присоединиться к другу» — запасной ручной путь).</item>
/// </list>
/// Свечи общие: возврат тратит одну Свечу всего отряда (у всех Камней одинаковый счётчик потраченных).
/// </summary>
public static class CoopMemoryStone
{
    public static readonly TimeSpan VoteTimeout = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan ScarTimeout = TimeSpan.FromSeconds(60);

    private static int _round, _clientStep, _generation;
    private static bool _proposing;
    internal static string MessageKey(RunState state)
    {
        var timeline = state.Players.Select(TravelPack.Of).OfType<TravelPack>().Select(p => p.OperationRunId).FirstOrDefault(id => id.Length > 0);
        return string.IsNullOrEmpty(timeline) ? "" : OperationIdentity.Hash(state.Rng.StringSeed + "|" + timeline);
    }
    private static readonly Dictionary<ulong, bool> Votes = new();
    private static readonly Dictionary<ulong, Scar> Scars = new();
    private static TaskCompletionSource? _votesDone;
    private static TaskCompletionSource? _scarsDone;
    private static int _expected;

    /// <summary>Состояние погибшего забега у клиента (для выбора Шрама и счётчиков в окне).</summary>
    private static RunState? _clientState;

    /// <summary>
    /// Сброс между забегами (<see cref="Rules.RunLifecycle"/>, ревью R-31). Голоса и Шрамы нужны только до сборки
    /// сохранения возврата — к запуску следующего забега раунд закончен. Пакеты привязаны к сохранённой timeline,
    /// а generation не даёт finally/таймауту старого забега очистить новый раунд.
    /// </summary>
    internal static void ResetForNewRun()
    {
        _generation++;
        Votes.Clear();
        Scars.Clear();
        _round = _clientStep = 0; // packets also carry the saved timeline: an old run's round can never match
        _proposing = false;
        _votesDone = null;
        _scarsDone = null;
        _expected = 0;
        _clientState = null;
    }

    // ───── хост ─────

    public static async Task Propose(RunState state, SerializableRun ended)
    {
        if (_proposing || MemoryStone.Restoring || !RequiemNet.IsHost || !ReferenceEquals(state, RequiemNet.State) || MessageKey(state).Length == 0) return;
        _proposing = true;
        var generation = _generation;
        try { await ProposeCore(state, ended, generation); }
        finally { if (generation == _generation) { _proposing = false; _votesDone = _scarsDone = null; } }
    }

    private static bool Current(RunState state, int generation) =>
        generation == _generation && ReferenceEquals(state, RequiemNet.State) && RequiemNet.IsHost;

    private static async Task ProposeCore(RunState state, SerializableRun ended, int generation)
    {
        var imprint = MemoryStone.ImprintFor(state, ended);
        var me = LocalContext.GetMe(state);
        if (imprint == null || me == null) return;
        var candles = MemoryStone.CandlesLeft(state);
        var shownCandles = candles == MemoryStoneRules.Infinite ? -1 : candles;

        if (!await RequiemNet.Confirm(MemoryStone.LocKey, "coopOffer", body =>
            {
                body.Add("Act", imprint.ActIndex + 1);
                body.Add("Candles", shownCandles);
            }))
            return;

        if (!Current(state, generation)) return;
        var round = ++_round;
        _expected = state.Players.Count;
        Votes.Clear();
        Scars.Clear();
        Votes[me.NetId] = true;
        _votesDone = new TaskCompletionSource();
        Send(StonePhase.Proposal, round, imprint.ActIndex + 1, shownCandles, state);
        CheckVotes();
        await WaitOrTimeout(_votesDone.Task, VoteTimeout);
        if (!Current(state, generation)) return;

        var yes = Votes.Values.Count(v => v);
        var no = Votes.Values.Count(v => !v);
        var approved = MemoryStoneRules.VoteApproved(yes, no);
        MainFile.Logger.Info($"[SlayTheRequiem] Камень Памяти (кооп): за {yes}, против {no} → {(approved ? "возврат" : "нет")}.");
        Send(StonePhase.Decision, round, approved ? 1 : 0, yes, state);
        if (!approved) return;

        _scarsDone = new TaskCompletionSource();
        var myScar = await MemoryStone.ChooseScar(state, me, canSkip: false) ?? MemoryStoneRules.DefaultScar;
        if (!Current(state, generation)) return;
        Scars[me.NetId] = myScar;
        CheckScars();
        await WaitOrTimeout(_scarsDone.Task, ScarTimeout);

        if (!Current(state, generation)) return;
        var save = MemoryStone.BuildReturnSave(imprint, state, Scars);
        if (save == null)
        {
            Send(StonePhase.Abort, round, 0, 0, state);
            return;
        }

        using var transition = MemoryStone.TryBeginRestore();
        if (transition == null) return;
        try
        {
            // Записать как сохранение кооп-забега: если лобби не соберётся, «Мультиплеер → Загрузить» продолжит с Камня.
            OperationRecovery.CapturePreparedRun(save, "memory-stone-return", coop: true);
            await SaveManager.Instance.IncrementNumReloads(save, NetGameType.Host, false);
            if (!Current(state, generation)) return;
            Send(StonePhase.Go, round, 0, 0, state);
            // дать сообщению уйти до разрыва соединения; тем временем — трещины отката
            await Task.WhenAll(Task.Delay(500), UI.RequiemVfx.Rewind(round));
            if (!Current(state, generation)) return;
            await NGame.Instance.ReturnToMainMenu();
            if (generation != _generation || RequiemNet.State != null) return;
            var submenu = NGame.Instance.MainMenu?.OpenMultiplayerSubmenu();
            submenu?.StartHost(save);
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"[SlayTheRequiem] Камень Памяти (кооп): возврат не удался ({e.Message}).");
            Send(StonePhase.Abort, round, 0, 0, state);
        }
    }

    private static void CheckVotes()
    {
        if (Votes.Count >= _expected) _votesDone?.TrySetResult();
    }

    private static void CheckScars()
    {
        if (Scars.Count >= _expected) _scarsDone?.TrySetResult();
    }

    private static async Task WaitOrTimeout(Task task, TimeSpan timeout) => await Task.WhenAny(task, Task.Delay(timeout));

    // ───── клиент ─────

    /// <summary>Клиент запоминает погибший забег, когда открывается его экран поражения.</summary>
    public static void OnGameOver(RunState state)
    {
        if (RequiemNet.IsClient) _clientState = state;
    }

    private static async Task VoteAsClient(int round, int act, int candles, RunState state)
    {
        var yes = await RequiemNet.Confirm(MemoryStone.LocKey, "coopVote", body =>
        {
            body.Add("Act", act);
            body.Add("Candles", candles);
        });
        Send(StonePhase.Vote, round, yes ? 1 : 0, 0, state);
    }

    private static async Task ScarAsClient(int round, RunState state)
    {
        var me = state == null ? null : LocalContext.GetMe(state);
        var scar = state != null && me != null
            ? await MemoryStone.ChooseScar(state, me, canSkip: false) ?? MemoryStoneRules.DefaultScar
            : MemoryStoneRules.DefaultScar;
        Send(StonePhase.Scar, round, (int)scar, 0, state);
    }

    /// <summary>
    /// Автопереподключение (15 §5, M5 п. 6): клиент выходит в меню, открывает «Присоединиться к другу» (ручной вход остаётся
    /// запасным путём) и сам ищет лобби хоста — паузы <see cref="MemoryStoneRules.RejoinDelayMs"/>, не больше
    /// <see cref="MemoryStoneRules.RejoinAttempts"/> попыток. Steam: ждём, пока хост появится среди друзей с открытым лобби,
    /// и входим один раз (неудачный вход показывает окно ошибки — поэтому не «долбим» вход вслепую). Без Steam (ENet)
    /// оставляем штатный ручной ввод адреса: endpoint хоста нельзя безопасно угадать. Уход с экрана завершает поиск.
    /// </summary>
    internal static async Task RejoinAsClient(ulong hostNetId)
    {
        var original = RequiemNet.State;
        var service = RunManager.Instance.NetService;
        if (original == null || !RequiemNet.IsClient) return;
        var generation = _generation;
        var platform = service?.Platform ?? PlatformType.None;
        NJoinFriendScreen? screen = null;
        using (var transition = MemoryStone.TryBeginRestore())
        {
            if (transition == null) return;
            try
            {
                await UI.RequiemVfx.Rewind((int)(hostNetId % int.MaxValue));
                if (!ReferenceEquals(original, RequiemNet.State) || !ReferenceEquals(service, RunManager.Instance.NetService)) return;
                await NGame.Instance.ReturnToMainMenu();
                if (generation != _generation || RequiemNet.State != null) return;
                screen = NGame.Instance.MainMenu?.OpenMultiplayerSubmenu()?.OnJoinFriendsPressed();
            }
            finally { if (ReferenceEquals(_clientState, original)) _clientState = null; }
        }
        if (screen == null || hostNetId == 0) return;

        for (var attempt = 0; ; attempt++)
        {
            if (!MemoryStoneRules.ShouldRetryRejoin(attempt, Connected(), ScreenOpen(screen))) break;
            await Task.Delay(MemoryStoneRules.RejoinDelayMs(attempt));
            if (!ScreenOpen(screen) || Connected()) break;
            try
            {
                if (platform == PlatformType.Steam)
                {
                    var open = await PlatformUtil.GetFriendsWithOpenLobbies(PlatformType.Steam);
                    if (!ScreenOpen(screen) || Connected() || generation != _generation) return;
                    if (!open.Contains(hostNetId)) continue;
                    MainFile.Logger.Info($"[SlayTheRequiem] Камень Памяти (кооп): лобби хоста найдено (попытка {attempt + 1}), вход.");
                    await screen.JoinGameAsync(SteamClientConnectionInitializer.FromPlayer(hostNetId));
                }
                else
                {
                    // The public game API does not expose the connected ENet host endpoint. Guessing loopback
                    // would join a different machine. Keep the native join screen for explicit LAN reconnection.
                    MainFile.Logger.Info("[MemoryStone] LAN host endpoint unavailable; rejoin the same host manually from the open join screen.");
                    return;
                }
                return;
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"[SlayTheRequiem] Камень Памяти (кооп): переподключение не удалось ({e.Message}).");
                if (platform != PlatformType.Steam) return;
            }
        }
        MainFile.Logger.Info("[SlayTheRequiem] Камень Памяти (кооп): лобби хоста не найдено — остаётся ручной вход.");
    }

    private static bool Connected() => RunManager.Instance.NetService is { IsConnected: true, Type: NetGameType.Client };

    private static bool ScreenOpen(NJoinFriendScreen screen) => GodotObject.IsInstanceValid(screen) && screen.IsVisibleInTree();

    // ───── сообщения ─────

    private static void Send(StonePhase phase, int round, int value, int extra, RunState state)
    {
        if (!ReferenceEquals(state, RequiemNet.State)) return;
        RequiemNet.Send(new MemoryStoneMessage { run = MessageKey(state), phase = (int)phase, round = round, value = value, extra = extra });
    }

    public static void OnMessage(StonePhase phase, int round, int value, int extra, string run, ulong senderId)
    {
        var state = RequiemNet.State;
        if (state == null || !OperationIdentity.IsDigest(run) || run != MessageKey(state) || round <= 0
            || senderId == RequiemNet.LocalNetId || !state.Players.Any(p => p.NetId == senderId)) return;
        var fromHost = RunManager.Instance.NetService is INetClientGameService { NetClient: { } client } && senderId == client.HostNetId;
        switch (phase)
        {
            case StonePhase.Proposal when RequiemNet.IsClient && fromHost && round > _round:
                _round = round;
                _clientStep = 1;
                TaskHelper.RunSafely(VoteAsClient(round, value, extra, state));
                break;
            case StonePhase.Decision when RequiemNet.IsClient && fromHost && round == _round && _clientStep == 1:
                _clientStep = 2;
                if (value == 1) TaskHelper.RunSafely(ScarAsClient(round, state));
                break;
            case StonePhase.Go when RequiemNet.IsClient && fromHost && round == _round && _clientStep == 2:
                _clientStep = 3; // authenticated duplicates cannot start another menu transition
                TaskHelper.RunSafely(RejoinAsClient(senderId));
                break;
            case StonePhase.Abort when RequiemNet.IsClient && fromHost && round == _round && _clientStep < 3:
                _clientStep = 3;
                MainFile.Logger.Warn("[MemoryStone] Host cancelled the rollback; current files are retained.");
                break;
            case StonePhase.Vote when RequiemNet.IsHost && _proposing && _votesDone != null && _scarsDone == null && round == _round && value is 0 or 1:
                Votes.TryAdd(senderId, value == 1); // first immutable vote wins
                CheckVotes();
                break;
            case StonePhase.Scar when RequiemNet.IsHost && _proposing && _scarsDone != null && round == _round && Enum.IsDefined(typeof(Scar), value):
                Scars.TryAdd(senderId, (Scar)value);
                CheckScars();
                break;
        }
    }
}
