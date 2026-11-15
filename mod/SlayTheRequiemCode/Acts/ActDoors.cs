using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.UI;

namespace SlayTheRequiem.SlayTheRequiemCode.Acts;

/// <summary>
/// «Две двери» (docs/design/13 §2, M5 п. 2, ревью R-12): после босса актов 1 и 3 игрок выбирает вариант следующего акта.
/// <para>
/// Поток: кнопка «Дальше» на экране наград → <c>ActChangeSynchronizer.SetLocalPlayerReady</c> — перехватываем
/// (<see cref="DoorReadyPatch"/>), показываем <see cref="NActChoiceScreen"/>, голос рассылаем (<see cref="ActDoorMessage"/>)
/// и только потом пускаем ванильную готовность. Решает хост (как в Камне Памяти): когда голоса всех у него — итог
/// <see cref="ActDoorRules.Resolve"/> рассылается всем. Переход в акт (<c>MoveToNextAct</c>) у каждого игрока ждёт итога
/// (<see cref="DoorMovePatch"/>), а вход в акт подменяет вариант (<see cref="DoorEnterActPatch"/>) — у всех одинаково.
/// </para>
/// Настройка «Случайно» — экрана нет, игрок воздерживается; если воздержались все, остаётся вариант, выпавший при создании
/// забега (прежнее поведение <see cref="ActListPatch"/>). Выбранный акт попадает в сохранение как обычный акт забега.
/// </summary>
public static class ActDoors
{
    /// <summary>Сколько хост ждёт недостающие голоса, прежде чем решить по имеющимся (выпавший из сети игрок).</summary>
    public const double HostWaitSeconds = 15;

    private static readonly Dictionary<int, Dictionary<ulong, int>> Votes = new();
    private static readonly Dictionary<int, int> Decisions = new();
    private static ActChangeSynchronizer? _deferred;
    private static bool _choosing;

    private static readonly MethodInfo? MoveToNextAct = AccessTools.Method(typeof(ActChangeSynchronizer), "MoveToNextAct");
    private static readonly MethodInfo? ActsSetter = AccessTools.PropertySetter(typeof(RunState), nameof(RunState.Acts));
    private static readonly FieldInfo? SharedAncients = AccessTools.Field(typeof(ActModel), "_sharedAncientSubset");

    /// <summary>Голоса или итог изменились (экран обновляет отметки).</summary>
    public static event Action<int>? Changed;

    public static void Reset()
    {
        Votes.Clear();
        Decisions.Clear();
        _deferred = null;
        _choosing = false;
    }

    /// <summary>Индекс акта за дверями, если следующий переход — через две двери; иначе −1.</summary>
    public static int PendingIndex(IRunState? run)
    {
        if (run == null || !ActListPatch.FiveActsEnabled) return -1;
        var next = run.CurrentActIndex + 1;
        return next < run.Acts.Count && ActDoorRules.HasDoors(next, run.Acts.Count) ? next : -1;
    }

    /// <summary>Дверь варианта, выпавшего при создании забега (0, если акт в слоте почему-то не из пары).</summary>
    public static int RolledDoor(IRunState run, int index) =>
        Math.Max(0, ActDoorRules.DoorOf(index, ActKinds.Of(run.Acts[index])));

    public static IReadOnlyDictionary<ulong, int> VotesFor(int index) =>
        Votes.TryGetValue(index, out var votes) ? votes : new Dictionary<ulong, int>();

    public static int? DecisionFor(int index) => Decisions.TryGetValue(index, out var door) ? door : null;

    public static bool LocalVoted(int index) => VotesFor(index).ContainsKey(RequiemNet.LocalNetId);

    public static ActModel ModelOf(ActKind kind) => kind switch
    {
        ActKind.FrozenBarrows => ModelDb.Act<FrozenBarrows>(),
        ActKind.ClockworkDepths => ModelDb.Act<ClockworkDepths>(),
        ActKind.BloodCourt => ModelDb.Act<BloodCourt>(),
        _ => ModelDb.Act<StarTower>(),
    };

    // ───── готовность: сначала дверь, потом ванильный голос «дальше» ─────

    /// <summary>false — готовность отложена до выбора двери.</summary>
    internal static bool BeforeLocalReady()
    {
        var run = RequiemNet.State;
        var index = PendingIndex(run);
        if (run == null || index < 0 || LocalVoted(index)) return true;
        if (RequiemConfig.ActVariant == ActVariantMode.Random)
        {
            Cast(index, ActDoorRules.Abstain);
            return true;
        }
        if (!_choosing) TaskHelper.RunSafely(ChooseThenReady(run, index));
        return false;
    }

    private static async Task ChooseThenReady(RunState run, int index)
    {
        _choosing = true;
        var door = ActDoorRules.Abstain;
        try
        {
            door = await NActChoiceScreen.Show(run, index);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Две двери: экран не открылся ({e.Message}) — голос «судьба».");
        }
        finally
        {
            _choosing = false;
        }
        Cast(index, door);
        RunManager.Instance.ActChangeSynchronizer.SetLocalPlayerReady();
    }

    /// <summary>Свой голос: записать, разослать, у хоста (и в одиночной игре) — попробовать решить.</summary>
    private static void Cast(int index, int door)
    {
        Record(index, RequiemNet.LocalNetId, door);
        RequiemNet.Send(new ActDoorMessage { phase = (int)DoorPhase.Vote, act = index, value = door });
        if (!RequiemNet.IsClient) TryDecide(index, force: false);
    }

    private static void Record(int index, ulong netId, int door)
    {
        if (!Votes.TryGetValue(index, out var votes)) Votes[index] = votes = new Dictionary<ulong, int>();
        votes[netId] = door;
        Changed?.Invoke(index);
    }

    internal static void OnMessage(DoorPhase phase, int index, int value, ulong senderId)
    {
        switch (phase)
        {
            case DoorPhase.Vote:
                if (senderId == RequiemNet.LocalNetId) return;
                Record(index, senderId, value);
                if (RequiemNet.IsHost) TryDecide(index, force: false);
                break;
            case DoorPhase.Decision when !RequiemNet.IsHost:
                Decide(index, value);
                break;
        }
    }

    /// <summary>Хост/одиночная игра: итог, когда проголосовали все (или по таймеру — по имеющимся голосам).</summary>
    private static void TryDecide(int index, bool force)
    {
        var run = RequiemNet.State;
        if (run == null || Decisions.ContainsKey(index) || index >= run.Acts.Count) return;
        var votes = VotesFor(index);
        if (!force && !ActDoorRules.AllVoted(votes.Count, run.Players.Count)) return;
        var door = ActDoorRules.Resolve(votes.Values, RolledDoor(run, index), ActDoorRules.TieBreak(run.Rng.StringSeed, index));
        RequiemNet.Send(new ActDoorMessage { phase = (int)DoorPhase.Decision, act = index, value = door });
        Decide(index, door);
    }

    private static void Decide(int index, int door)
    {
        Decisions[index] = door;
        MainFile.Logger.Info($"[SlayTheRequiem] Две двери: акт {index + 1} — {ActDoorRules.Options(index)[door]}.");
        Changed?.Invoke(index);
        if (_deferred is { } sync && MoveToNextAct != null)
        {
            _deferred = null;
            MoveToNextAct.Invoke(sync, null);
        }
    }

    // ───── переход в акт ждёт итога ─────

    /// <summary>false — переход отложен до итога (сработает из <see cref="Decide"/>).</summary>
    internal static bool BeforeMoveToNextAct(ActChangeSynchronizer sync)
    {
        var index = PendingIndex(RequiemNet.State);
        if (index < 0 || Decisions.ContainsKey(index)) return true;
        if (!RequiemNet.IsClient)
        {
            TryDecide(index, force: false);
            if (Decisions.ContainsKey(index)) return true;
        }
        _deferred = sync;
        if (RequiemNet.IsHost && Engine.GetMainLoop() is SceneTree tree)
            tree.CreateTimer(HostWaitSeconds).Timeout += () => TryDecide(index, force: true);
        MainFile.Logger.Info($"[SlayTheRequiem] Две двери: переход в акт {index + 1} ждёт итога голосования.");
        return false;
    }

    // ───── подмена варианта при входе ─────

    internal static void BeforeEnterAct(RunManager manager, int actIndex)
    {
        var state = manager.DebugOnlyGetState();
        if (state == null || ActsSetter == null || !Decisions.TryGetValue(actIndex, out var door)) return;
        if (actIndex >= state.Acts.Count || !ActDoorRules.HasDoors(actIndex, state.Acts.Count)) return;
        var want = ActDoorRules.Options(actIndex)[door];
        var old = state.Acts[actIndex];
        if (ActKinds.Of(old) == want) return;

        var act = ModelOf(want).ToMutable();
        if (SharedAncients?.GetValue(old) is List<AncientEventModel> shared) act.SetSharedAncientSubset(shared);
        act.GenerateRooms(new Rng(state.Rng.Seed, $"requiem_door_{actIndex}"), state.UnlockState, state.Players.Count > 1);
        if (RequiemConfig.EnableRequiemSystems && RequiemConfig.Superbosses != SuperbossMode.Off && ActRules.SuperbossFor(want) is { } boss)
            act.SetSecondBossEncounter(SuperbossEncounter.For(boss));

        var acts = state.Acts.ToList();
        acts[actIndex] = act;
        ActsSetter.Invoke(state, [acts]);
        MainFile.Logger.Info($"[SlayTheRequiem] Две двери: акт {actIndex + 1} заменён на {want}.");
    }
}

/// <summary>Готовность к следующему акту — после выбора двери (кнопка «Дальше», отказ от портала, награда супербосса).</summary>
[HarmonyPatch(typeof(ActChangeSynchronizer), nameof(ActChangeSynchronizer.SetLocalPlayerReady))]
public static class DoorReadyPatch
{
    [HarmonyPrefix]
    public static bool Prefix() => ActDoors.BeforeLocalReady();
}

/// <summary>Все готовы — но вход в акт ждёт итога голосования дверей.</summary>
[HarmonyPatch(typeof(ActChangeSynchronizer), "MoveToNextAct")]
public static class DoorMovePatch
{
    [HarmonyPrefix]
    public static bool Prefix(ActChangeSynchronizer __instance) => ActDoors.BeforeMoveToNextAct(__instance);
}

/// <summary>Вход в акт за дверями: вариант заменяется на выбранный до генерации карты.</summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterAct))]
public static class DoorEnterActPatch
{
    [HarmonyPrefix]
    public static void Prefix(RunManager __instance, int currentActIndex) => ActDoors.BeforeEnterAct(__instance, currentActIndex);
}
