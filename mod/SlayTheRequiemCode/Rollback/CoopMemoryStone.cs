using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using SlayTheRequiem.SlayTheRequiemCode.Core;
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
///       загрузки этого сохранения, клиентам открывается экран «Присоединиться к другу».</item>
/// </list>
/// Свечи общие: возврат тратит одну Свечу всего отряда (у всех Камней одинаковый счётчик потраченных).
/// </summary>
public static class CoopMemoryStone
{
    public static readonly TimeSpan VoteTimeout = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan ScarTimeout = TimeSpan.FromSeconds(60);

    private static int _round;
    private static readonly Dictionary<ulong, bool> Votes = new();
    private static readonly Dictionary<ulong, Scar> Scars = new();
    private static TaskCompletionSource? _votesDone;
    private static TaskCompletionSource? _scarsDone;
    private static int _expected;

    /// <summary>Состояние погибшего забега у клиента (для выбора Шрама и счётчиков в окне).</summary>
    private static RunState? _clientState;

    // ───── хост ─────

    public static async Task Propose(RunState state, SerializableRun ended)
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

        var round = ++_round;
        _expected = state.Players.Count;
        Votes.Clear();
        Scars.Clear();
        Votes[me.NetId] = true;
        _votesDone = new TaskCompletionSource();
        Send(StonePhase.Proposal, round, imprint.ActIndex + 1, shownCandles);
        CheckVotes();
        await WaitOrTimeout(_votesDone.Task, VoteTimeout);

        var yes = Votes.Values.Count(v => v);
        var no = Votes.Values.Count(v => !v);
        var approved = MemoryStoneRules.VoteApproved(yes, no);
        MainFile.Logger.Info($"[SlayTheRequiem] Камень Памяти (кооп): за {yes}, против {no} → {(approved ? "возврат" : "нет")}.");
        Send(StonePhase.Decision, round, approved ? 1 : 0, yes);
        if (!approved) return;

        _scarsDone = new TaskCompletionSource();
        var myScar = await MemoryStone.ChooseScar(state, me, canSkip: false) ?? MemoryStoneRules.DefaultScar;
        Scars[me.NetId] = myScar;
        CheckScars();
        await WaitOrTimeout(_scarsDone.Task, ScarTimeout);

        var save = MemoryStone.BuildReturnSave(imprint, state, Scars);
        if (save == null)
        {
            Send(StonePhase.Abort, round, 0, 0);
            return;
        }

        MemoryStone.Restoring = true;
        try
        {
            // Записать как сохранение кооп-забега: если лобби не соберётся, «Мультиплеер → Загрузить» продолжит с Камня.
            await SaveManager.Instance.IncrementNumReloads(save, isMultiplayer: true);
            Send(StonePhase.Go, round, 0, 0);
            await Task.Delay(500); // дать сообщению уйти до разрыва соединения
            await NGame.Instance.ReturnToMainMenu();
            var submenu = NGame.Instance.MainMenu?.OpenMultiplayerSubmenu();
            submenu?.StartHost(save);
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"[SlayTheRequiem] Камень Памяти (кооп): возврат не удался ({e.Message}).");
            Send(StonePhase.Abort, round, 0, 0);
        }
        finally
        {
            MemoryStone.Restoring = false;
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

    private static async Task VoteAsClient(int round, int act, int candles)
    {
        var yes = await RequiemNet.Confirm(MemoryStone.LocKey, "coopVote", body =>
        {
            body.Add("Act", act);
            body.Add("Candles", candles);
        });
        Send(StonePhase.Vote, round, yes ? 1 : 0, 0);
    }

    private static async Task ScarAsClient(int round)
    {
        var state = _clientState ?? RequiemNet.State;
        var me = state == null ? null : LocalContext.GetMe(state);
        var scar = state != null && me != null
            ? await MemoryStone.ChooseScar(state, me, canSkip: false) ?? MemoryStoneRules.DefaultScar
            : MemoryStoneRules.DefaultScar;
        Send(StonePhase.Scar, round, (int)scar, 0);
    }

    private static async Task RejoinAsClient()
    {
        MemoryStone.Restoring = true;
        try
        {
            await NGame.Instance.ReturnToMainMenu();
            NGame.Instance.MainMenu?.OpenMultiplayerSubmenu()?.OnJoinFriendsPressed();
        }
        finally
        {
            MemoryStone.Restoring = false;
            _clientState = null;
        }
    }

    // ───── сообщения ─────

    private static void Send(StonePhase phase, int round, int value, int extra) =>
        RequiemNet.Send(new MemoryStoneMessage { phase = (int)phase, round = round, value = value, extra = extra });

    public static void OnMessage(StonePhase phase, int round, int value, int extra, ulong senderId)
    {
        switch (phase)
        {
            // хост → клиенты
            case StonePhase.Proposal when RequiemNet.IsClient:
                _round = round;
                TaskHelper.RunSafely(VoteAsClient(round, value, extra));
                break;
            case StonePhase.Decision when RequiemNet.IsClient && round == _round:
                if (value == 1) TaskHelper.RunSafely(ScarAsClient(round));
                break;
            case StonePhase.Go when RequiemNet.IsClient && round == _round:
                TaskHelper.RunSafely(RejoinAsClient());
                break;
            case StonePhase.Abort when RequiemNet.IsClient:
                MainFile.Logger.Warn("[SlayTheRequiem] Камень Памяти (кооп): хост отменил возврат.");
                break;

            // клиенты → хост
            case StonePhase.Vote when RequiemNet.IsHost && round == _round:
                Votes[senderId] = value == 1;
                CheckVotes();
                break;
            case StonePhase.Scar when RequiemNet.IsHost && round == _round:
                Scars[senderId] = Enum.IsDefined(typeof(Scar), value) ? (Scar)value : MemoryStoneRules.DefaultScar;
                CheckScars();
                break;
        }
    }
}
