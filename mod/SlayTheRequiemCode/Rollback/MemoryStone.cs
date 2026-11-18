using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Rollback;

/// <summary>
/// Камень Памяти (docs/design/15): Отпечаток забега в начале каждого акта и возврат к нему с экрана поражения.
///
/// Отпечаток — обычный <see cref="SerializableRun"/> (тот же, что пишет «Сохранить и выйти»), снятый на хуке
/// подтверждённой границе после всех стартовых/событийных задач; хранится в памяти (как JSON) и копией в папке сохранений профиля, чтобы пережить перезапуск
/// игры. В коопе Отпечаток снимает только хост — у него же сохранение кооп-забега.
///
/// Возврат: Отпечаток дополняется итогами погибшей попытки (Свечи, Шрамы, Месть, «перестроить карту») прямо в
/// свойствах реликвии <see cref="MemoryStoneRelic"/> каждого игрока, карта текущего акта из Отпечатка удаляется — и
/// забег загружается штатным путём. Шрамы накладываются после загрузки (<see cref="MemoryStoneRelic.AfterMapGenerated"/>),
/// новая карта строится в <see cref="MemoryStoneRelic.ModifyGeneratedMap"/>. Так одиночный и кооп-возврат идут одной дорогой:
/// одиночный — через «Продолжить» (<c>SetUpSavedSingleplayer → LoadRun</c>), кооп — через лобби загрузки
/// (<see cref="CoopMemoryStone"/>).
/// </summary>
public static class MemoryStone
{
    private const string FileName = "requiem_memory_stone.save";
    private const string CoopFileName = "requiem_memory_stone_coop.save";
    public const string LocKey = "SLAYTHEREQUIEM-MEMORY_STONE_RELIC";

    /// <summary>Отпечаток текущего забега (JSON SerializableRun) и его опознавательные признаки.</summary>
    internal sealed record Imprint(string Json, string Seed, long StartTime, int ActIndex, int Schema);

    private static Imprint? _imprint;
    private static bool _imprintIsCoop;

    internal static bool Restoring { get; set; }

    public static bool Enabled => RunSettings.EnableMemoryStone && RunSettings.EnableRequiemSystems;

    // ───── Отпечаток ─────

    private static WeakReference<RunState>? _waitingState;
    private static int _waitingAct;
    private static bool _actEntrySettled;
    private static EventRoom? _settledAncient;
    private static EventRoom? _checkingAncient;
    private static IReadOnlyList<Task> PendingEventTasks(EventSynchronizer synchronizer) =>
        AccessTools.Field(typeof(EventSynchronizer), "_pendingOptionTasks")?.GetValue(synchronizer) is List<Task> tasks
            ? tasks.ToArray() : throw new MissingFieldException("Event task adapter incompatible; no Memory Stone imprint will be taken");

    /// <summary>Arm, do not assume that entering the room completed its Ancient/startup decisions.</summary>
    public static void RequestImprint()
    {
        if (!Enabled || RequiemNet.IsClient || RequiemNet.State is not { } state) return;
        _waitingState = new(state);
        _waitingAct = state.CurrentActIndex;
        _actEntrySettled = false; // Hook.AfterActEntered has not finished its later listeners yet
        if (!ReferenceEquals(_settledAncient, state.CurrentRoom)) _settledAncient = null;
    }

    internal static void OnLaunch(RunState state)
    {
        _waitingState = null;
        _settledAncient = _checkingAncient = null;
        _actEntrySettled = Debugging.RunDiagnostics.LaunchKind != "new";
        // Continue of an unfinished act start can finish its pending checkpoint. Never invent a mid-act imprint.
        if (!RequiemNet.IsClient && state.VisitedMapCoords.All(c => c.row <= 0))
        {
            var save = RunManager.Instance.ToSave(null);
            var existing = ImprintFor(state, save);
            if (existing?.ActIndex != state.CurrentActIndex)
            { _waitingState = new(state); _waitingAct = state.CurrentActIndex; }
        }
    }

    internal static void AfterActHooksCompleted(IRunState state)
    {
        if (!ReferenceEquals(state, RequiemNet.State)) return;
        _actEntrySettled = true;
        OnRoomEntered();
    }

    internal static void OnRoomEntered()
    {
        if (RequiemNet.State?.CurrentRoom is EventRoom { IsPreFinished: true } room) QueueAncientBoundary(room);
        else TryTakeImprint();
    }

    internal static void QueueAncientBoundary(EventRoom room)
    {
        if (_waitingState == null || RequiemNet.IsClient || ReferenceEquals(_checkingAncient, room)) return;
        _checkingAncient = room;
        TaskHelper.RunSafely(AfterAncientTasks(room));
    }

    private static async Task AfterAncientTasks(EventRoom room)
    {
        try
        {
            // StateChanged fires INSIDE EventOption.Chosen, before task registration/history. Cleanup can
            // append another task; stabilize the full set without clearing or swallowing native bookkeeping.
            var state = RequiemNet.State;
            bool Current() => state != null && ReferenceEquals(state, RequiemNet.State) && ReferenceEquals(state.CurrentRoom, room);
            if (!await AsyncTaskBoundary.Settle(() => PendingEventTasks(RunManager.Instance.EventSynchronizer), Current)
                || !room.IsPreFinished || !RunManager.Instance.EventSynchronizer.Events.All(e => e.IsFinished)) return;
            _settledAncient = room;
            TryTakeImprint();
        }
        catch (Exception e) { MainFile.Logger.Error($"[MemoryStone] Ancient task did not settle; no imprint: {e}"); }
        finally { if (ReferenceEquals(_checkingAncient, room)) _checkingAncient = null; }
    }

    public static void TryTakeImprint()
    {
        if (!_actEntrySettled || !Enabled || RequiemNet.IsClient || !RunManager.Instance.ShouldSave || Restoring
            || _waitingState == null || !_waitingState.TryGetTarget(out var pending)
            || !ReferenceEquals(pending, RequiemNet.State) || pending.CurrentActIndex != _waitingAct) return;
        var state = pending;
        if (state.CurrentRoom is not (MapRoom or EventRoom) || CombatManager.Instance.IsInProgress) return;
        var ancientUnfinished = state.CurrentRoom is EventRoom eventRoom
            && (!eventRoom.IsPreFinished || !ReferenceEquals(eventRoom, _settledAncient));
        var startersReady = state.Players.SelectMany(p => p.Relics.OfType<Grimoire>()).All(g => g.StartingChoicesComplete);
        var mutating = state.Players.Select(TravelPack.Of).OfType<TravelPack>()
            .Any(p => p.OperationPending.Length > 0 || p.OperationBlocked || InventoryGate.IsMutating(p.Owner));
        if (!MemoryStoneRules.CanCaptureImprint(ancientUnfinished, startersReady, mutating)) return;
        if (!state.Players.Any(p => RequiemMode.IsInSystem(p))) return;
        try
        {
            // Preserve the finished Ancient room in the native snapshot, otherwise Continue offers it again.
            var save = RunManager.Instance.ToSave(state.CurrentRoom is EventRoom ? state.CurrentRoom : null);
            var json = JsonSerializationUtility.ToJson(save);
            var imprint = new Imprint(json, save.SerializableRng?.Seed ?? string.Empty, save.StartTime, save.CurrentActIndex, save.SchemaVersion);
            WriteToDisk(json, RequiemNet.IsMultiplayer); // a failed write must not claim a durable checkpoint
            _imprint = imprint;
            _imprintIsCoop = RequiemNet.IsMultiplayer;
            _waitingState = null;
            _settledAncient = null;
            MainFile.Logger.Info($"[MemoryStone] boundary=confirmed-start act={save.CurrentActIndex + 1} preFinished={save.PreFinishedRoom != null}");
        }
        catch (Exception e) { MainFile.Logger.Warn($"[MemoryStone] Capture delayed; previous imprint retained: {e}"); }
    }

    private static string? DiskPath(bool coop)
    {
        try { return SaveManager.Instance.GetProfileScopedPath(Path.Combine(UserDataPathProvider.SavesDir, coop ? CoopFileName : FileName)); }
        catch { return null; }
    }

    private static void WriteToDisk(string json, bool coop)
    {
        var path = DiskPath(coop) ?? throw new IOException("Memory Stone save path unavailable");
        AtomicSaveFile.Write(Godot.ProjectSettings.GlobalizePath(path), json);
    }

    private static Imprint? ReadFromDisk(bool coop, string seed, long startTime, int act)
    {
        var path = DiskPath(coop);
        if (path == null) return null;
        return AtomicSaveFile.ReadValidated(Godot.ProjectSettings.GlobalizePath(path), json =>
        {
            var parsed = JsonSerializationUtility.FromJson<SerializableRun>(json);
            var save = parsed.SaveData;
            if (!parsed.Success || save == null || save.SerializableRng?.Seed != seed || save.StartTime != startTime
                || save.CurrentActIndex != act || !MemoryStoneRules.IsSnapshotCompatible(save.SchemaVersion,
                    SaveManager.Instance.GetLatestSchemaVersion<SerializableRun>())) return null;
            return new Imprint(json, seed, startTime, save.CurrentActIndex, save.SchemaVersion);
        });
    }

    /// <summary>Only this run AND this act. Never silently return to a previous act when capture was unavailable.</summary>
    internal static Imprint? ImprintFor(RunState state, SerializableRun ended)
    {
        var coop = RequiemNet.IsMultiplayer;
        var seed = ended.SerializableRng?.Seed ?? state.Rng.StringSeed;
        if (_imprint == null || _imprintIsCoop != coop || _imprint.Seed != seed || _imprint.StartTime != ended.StartTime
            || _imprint.ActIndex != state.CurrentActIndex)
        {
            _imprint = ReadFromDisk(coop, seed, ended.StartTime, state.CurrentActIndex);
            _imprintIsCoop = coop;
        }
        var imprint = _imprint;
        if (imprint == null || imprint.ActIndex != state.CurrentActIndex) return null;
        return MemoryStoneRules.IsSnapshotCompatible(imprint.Schema, SaveManager.Instance.GetLatestSchemaVersion<SerializableRun>()) ? imprint : null;
    }

    // ───── Свечи отряда ─────

    public static IReadOnlyList<MemoryStoneRelic> Stones(RunState state) =>
        state.Players.Select(MemoryStoneRelic.Of).OfType<MemoryStoneRelic>().ToList();

    /// <summary>Свечи отряда (15 §5): в коопе общие — сколько осталось у самого «бедного» Камня.</summary>
    public static int CandlesLeft(RunState state) =>
        MemoryStoneRules.SharedCandlesLeft(Stones(state).Select(s => s.CandlesLeft).ToList());

    // ───── экран поражения ─────

    /// <summary>
    /// Можно ли предложить возврат: поражение, есть Свечи; в одиночной игре и у хоста — есть Отпечаток.
    /// Клиент коопа сам не предлагает: он отвечает на предложение хоста.
    /// </summary>
    public static bool CanOffer(RunState state, SerializableRun ended)
    {
        if (!Enabled || Restoring || RequiemNet.IsClient) return false;
        if (state.CurrentRoom?.IsVictoryRoom ?? false) return false;
        if (!state.Players.Any(p => RequiemMode.IsInSystem(p))) return false;
        if (Stones(state).Count == 0 || CandlesLeft(state) <= 0) return false;
        return ImprintFor(state, ended) != null;
    }

    public static Task Offer(RunState state, SerializableRun ended) =>
        RequiemNet.IsHost ? CoopMemoryStone.Propose(state, ended) : OfferSingleplayer(state, ended);

    /// <summary>Окно «Вернуться к Камню Памяти?», выбор Шрама и возврат.</summary>
    private static async Task OfferSingleplayer(RunState state, SerializableRun ended)
    {
        var me = state.Players.FirstOrDefault();
        var imprint = ImprintFor(state, ended);
        if (me == null || imprint == null) return;

        var candles = CandlesLeft(state);
        if (!await RequiemNet.Confirm(LocKey, "offer", body =>
            {
                body.Add("Act", imprint.ActIndex + 1);
                body.Add("Candles", candles == MemoryStoneRules.Infinite ? -1 : candles);
            }))
            return;

        // Пропуск выбора Шрама = отказ от возврата.
        var scar = await ChooseScar(state, me, canSkip: true);
        if (scar == null) return;

        var save = BuildReturnSave(imprint, state, new Dictionary<ulong, Scar> { [me.NetId] = scar.Value });
        if (save == null) return;
        await ReturnSingleplayer(save);
    }

    /// <summary>Шрам — выбор из трёх карт (видны заранее, docs/design/15 §3). Локальный экран, без синхронизатора выбора.</summary>
    internal static async Task<Scar?> ChooseScar(RunState state, Player me, bool canSkip)
    {
        var choices = new List<CardModel>
        {
            state.CreateCard<ScarOfFlesh>(me),
            state.CreateCard<ScarOfSoul>(me),
            state.CreateCard<ScarOfPurse>(me),
        };
        var picked = await RequiemNet.ChooseCard(choices, canSkip);
        return picked is ScarChoiceCard scar ? scar.Scar : null;
    }

    /// <summary>Кто убил: монстры текущего боя и их фракции.</summary>
    internal static string RevengeTargetsOf(RunState state)
    {
        if (state.CurrentRoom is not CombatRoom combat) return string.Empty;
        var tokens = new List<string>();
        foreach (var (monster, _) in combat.Encounter.MonstersWithSlots)
        {
            var name = monster.GetType().Name;
            tokens.Add("Monster:" + name);
            var faction = RequiemData.MonsterEntry(name).Faction;
            if (!string.IsNullOrEmpty(faction)) tokens.Add("Faction:" + faction);
        }
        return string.Join(",", tokens.Distinct());
    }

    // ───── сборка сохранения для возврата ─────

    /// <summary>
    /// Отпечаток + итоги погибшей попытки. Свечи общие (потрачено = максимум по отряду + 1), откаты — тоже;
    /// Шрамы у каждого свои. Игрок без выбранного Шрама получает <see cref="MemoryStoneRules.DefaultScar"/>.
    /// </summary>
    internal static SerializableRun? BuildReturnSave(Imprint imprint, RunState dead, IReadOnlyDictionary<ulong, Scar> scars)
    {
        var parsed = JsonSerializationUtility.FromJson<SerializableRun>(imprint.Json);
        if (!parsed.Success || parsed.SaveData == null)
        {
            MainFile.Logger.Warn("[SlayTheRequiem] Камень Памяти: Отпечаток не читается — возврат отменён.");
            return null;
        }
        var save = parsed.SaveData;
        var deadStones = Stones(dead);
        if (deadStones.Count == 0) return null;

        var candlesUsed = MemoryStoneRules.SharedCandlesUsed(deadStones.Select(s => s.CandlesUsed).ToList()) + 1;
        var rollbacks = deadStones.Max(s => s.Rollbacks) + 1;
        var revenge = RevengeTargetsOf(dead);
        var stoneId = ModelDb.Relic<MemoryStoneRelic>().Id;

        try
        {
            foreach (var sp in save.Players)
            {
                var relicSave = sp.Relics.FirstOrDefault(r => r.Id == stoneId);
                var deadStone = MemoryStoneRelic.Of(dead.GetPlayer(sp.NetId));
                if (relicSave == null || deadStone == null) continue;

                var stone = (MemoryStoneRelic)ModelDb.Relic<MemoryStoneRelic>().ToMutable();
                relicSave.Props?.Fill(stone);
                var scar = scars.TryGetValue(sp.NetId, out var s) ? s : MemoryStoneRules.DefaultScar;

                // Шрамы, наложенные ПОСЛЕ снятия Отпечатка (прошлые возвраты к этому же Камню + новый), в Отпечатке
                // отсутствуют: накладываем разницу между погибшей попыткой (+ новый Шрам) и Отпечатком.
                stone.PendingFlesh = MemoryStoneRules.ScarsToApply(deadStone.FleshScars, scar == Scar.Flesh, stone.FleshScars);
                stone.PendingSoul = MemoryStoneRules.ScarsToApply(deadStone.SoulScars, scar == Scar.Soul, stone.SoulScars);
                stone.PendingPurse = MemoryStoneRules.ScarsToApply(deadStone.PurseScars, scar == Scar.Purse, stone.PurseScars);

                stone.CandlesUsed = candlesUsed;
                stone.BonusCandles = deadStone.BonusCandles;
                stone.CryptCandles = deadStone.CryptCandles;
                stone.Rollbacks = rollbacks;
                stone.FleshScars = deadStone.FleshScars + (scar == Scar.Flesh ? 1 : 0);
                stone.SoulScars = deadStone.SoulScars + (scar == Scar.Soul ? 1 : 0);
                stone.PurseScars = deadStone.PurseScars + (scar == Scar.Purse ? 1 : 0);
                stone.RevengeTargets = revenge;
                stone.RevengeAct1 = imprint.ActIndex + 1;
                stone.MapRerollAct1 = imprint.ActIndex + 1;

                // Do not round-trip unrelated/unknown properties through this version's mutable model.
                var props = relicSave.Props ??= new SavedProperties();
                SavedPropertyWrite.Int(props, nameof(MemoryStoneRelic.PendingFlesh), stone.PendingFlesh);
                SavedPropertyWrite.Int(props, nameof(MemoryStoneRelic.PendingSoul), stone.PendingSoul);
                SavedPropertyWrite.Int(props, nameof(MemoryStoneRelic.PendingPurse), stone.PendingPurse);
                SavedPropertyWrite.Int(props, nameof(MemoryStoneRelic.CandlesUsed), stone.CandlesUsed);
                SavedPropertyWrite.Int(props, nameof(MemoryStoneRelic.BonusCandles), stone.BonusCandles);
                SavedPropertyWrite.Int(props, nameof(MemoryStoneRelic.CryptCandles), stone.CryptCandles);
                SavedPropertyWrite.Int(props, nameof(MemoryStoneRelic.Rollbacks), stone.Rollbacks);
                SavedPropertyWrite.Int(props, nameof(MemoryStoneRelic.FleshScars), stone.FleshScars);
                SavedPropertyWrite.Int(props, nameof(MemoryStoneRelic.SoulScars), stone.SoulScars);
                SavedPropertyWrite.Int(props, nameof(MemoryStoneRelic.PurseScars), stone.PurseScars);
                SavedPropertyWrite.Int(props, nameof(MemoryStoneRelic.RevengeAct1), stone.RevengeAct1);
                SavedPropertyWrite.Int(props, nameof(MemoryStoneRelic.MapRerollAct1), stone.MapRerollAct1);
                SavedPropertyWrite.Text(props, nameof(MemoryStoneRelic.RevengeTargets), stone.RevengeTargets);
            }

            // Слава (02 §5, 15): половина Славы, набранной после Отпечатка, сохраняется.
            var recordId = ModelDb.Relic<Progression.HeroRecord>().Id;
            foreach (var sp in save.Players)
            {
                var recordSave = sp.Relics.FirstOrDefault(r => r.Id == recordId);
                var deadRecord = Progression.HeroRecord.Of(dead.GetPlayer(sp.NetId));
                if (recordSave == null || deadRecord == null) continue;
                var record = (Progression.HeroRecord)ModelDb.Relic<Progression.HeroRecord>().ToMutable();
                recordSave.Props?.Fill(record);
                var retained = ProgressionRules.RetainedRenown(record.Renown, deadRecord.Renown);
                if (retained == record.Renown) continue;
                record.Renown = retained;
                SavedPropertyWrite.Int(recordSave.Props ??= new SavedProperties(), nameof(Progression.HeroRecord.Renown), record.Renown);
            }

            // A rollback is a NEW operation timeline. Reusing the imprint's nonce/revision would let an old message
            // from the abandoned attempt become valid again. Generated ONCE by the host/save builder and persisted
            // for all peers; an ordinary Continue keeps its existing nonce and high-water revision unchanged.
            OperationRecovery.RotateTimeline(save);

            // Новая карта (15 §1.3): без сохранённой карты акт строит её заново, а Камень подменяет зерно.
            if (imprint.ActIndex >= 0 && imprint.ActIndex < save.Acts.Count) save.Acts[imprint.ActIndex].SavedMap = null;
            return save;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Камень Памяти: снимок для возврата не собран ({e.Message}).");
            return null;
        }
    }

    // ───── возврат в одиночной игре ─────

    internal static async Task ReturnSingleplayer(SerializableRun save, string operation = "memory-stone-return")
    {
        Restoring = true;
        try
        {
            OperationRecovery.CapturePreparedRun(save, operation, coop: false);
            await UI.RequiemVfx.Rewind(save.GetHashCode());
            await NGame.Instance.Transition.FadeOut();
            RunManager.Instance.CleanUp();
            // Штатный путь «Продолжить» (NMainMenu.OnContinueButtonPressedAsync).
            var runState = RunState.FromSerializable(save);
            await RunManager.Instance.SetUpSavedSingleplayer(runState, save);
            NGame.Instance.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
            await NGame.Instance.LoadRun(runState, save.PreFinishedRoom);
            await NGame.Instance.Transition.FadeIn();
            // Сразу записать забег: «Сохранить и выйти» после возврата не должен терять Шрам.
            await SaveManager.Instance.SaveRun(runState.CurrentRoom?.IsPreFinished == true ? runState.CurrentRoom : null);
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"[SlayTheRequiem] Камень Памяти: возврат не удался ({e.Message}).");
            await NGame.Instance.ReturnToMainMenuWithInternalError(e);
        }
        finally
        {
            Restoring = false;
        }
    }

    // ───── Шрамы после загрузки ─────

    /// <summary>
    /// Наложить Шрамы (15 §3) — вызывается Камнем игрока при генерации карты после загрузки, на всех машинах коопа
    /// одинаково: Плоть −6 % макс. ♥, Душа — «Предсмертное эхо» в колоду, Кошелёк −40 % золота.
    /// </summary>
    internal static async Task ApplyScars(Player player, int flesh, int soul, int purse)
    {
        var creature = player.Creature;
        for (var i = 0; i < flesh; i++)
            await CreatureCmd.SetMaxHp(creature, MemoryStoneRules.FleshScarMaxHp(creature.MaxHp));
        if (creature.CurrentHp > creature.MaxHp) await CreatureCmd.SetCurrentHp(creature, creature.MaxHp);

        for (var i = 0; i < soul; i++)
            await CardAcquisition.AddOrDefer(player.RunState.CreateCard<DeathEcho>(player));

        for (var i = 0; i < purse; i++)
            await PlayerCmd.SetGold(MemoryStoneRules.PurseScarGold(player.Gold), player);
    }
}


/// <summary>Called only after ALL synchronized Ancient events have finished, not when the room merely opens.</summary>
[HarmonyPatch(typeof(EventRoom), "OnEventStateChanged")]
public static class MemoryStoneAncientBoundaryPatch
{
    [HarmonyPostfix]
    public static void Postfix(EventRoom __instance)
    {
        if (__instance.IsPreFinished) MemoryStone.QueueAncientBoundary(__instance);
    }
}
