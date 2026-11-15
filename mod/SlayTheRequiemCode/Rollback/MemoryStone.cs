using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using FileAccess = Godot.FileAccess;

namespace SlayTheRequiem.SlayTheRequiemCode.Rollback;

/// <summary>
/// Камень Памяти (docs/design/15): Отпечаток забега в начале каждого акта и возврат к нему с экрана поражения.
///
/// Отпечаток — это обычный <see cref="SerializableRun"/> (тот же, что пишет «Сохранить и выйти»), снятый на хуке
/// <c>AfterActEntered</c>; хранится в памяти (как JSON) и копией в папке сохранений профиля
/// (<c>saves/requiem_memory_stone.save</c>), чтобы пережить перезапуск игры. Возврат повторяет штатный путь «Продолжить»
/// главного меню: <c>RunState.FromSerializable → SetUpSavedSingleplayer → NGame.LoadRun</c>.
///
/// Ограничения MVP (см. docs/design/15 «Статус»): только одиночная игра; карта акта та же (сейв хранит карту, новое зерно —
/// позже); «Опыт поражения» (Слава) и «Знание» (намерения на ход вперёд) — после этапа 5.
/// </summary>
public static class MemoryStone
{
    private const string FileName = "requiem_memory_stone.save";

    /// <summary>Отпечаток текущего забега (JSON SerializableRun) и его опознавательные признаки.</summary>
    private sealed record Imprint(string Json, string Seed, long StartTime, int ActIndex, int Schema);

    private static Imprint? _imprint;

    /// <summary>Что перенести в восстановленный забег (Свечи, Шрам, Месть) — живёт между CleanUp и LoadRun.</summary>
    private sealed record PendingReturn(Scar Scar, int CandlesUsed, int BonusCandles, int Rollbacks,
        int FleshScars, int SoulScars, int PurseScars, string RevengeTargets, int ActIndex);

    private static bool _restoring;

    public static bool Enabled => RequiemConfig.EnableMemoryStone && RequiemConfig.EnableRequiemSystems;

    private static bool IsSingleplayer => RunManager.Instance.NetService?.Type == NetGameType.Singleplayer;

    // ───── Отпечаток ─────

    /// <summary>Снять Отпечаток (вызывается после входа в акт — стартовый узел уже закрыт).</summary>
    public static void TakeImprint()
    {
        if (!Enabled || !IsSingleplayer || !RunManager.Instance.ShouldSave) return;
        var state = RunManager.Instance.DebugOnlyGetState();
        if (state == null || !state.Players.Any(p => RequiemMode.IsInSystem(p))) return;
        try
        {
            var save = RunManager.Instance.ToSave(null);
            var json = JsonSerializationUtility.ToJson(save);
            _imprint = new Imprint(json, save.SerializableRng?.Seed ?? string.Empty, save.StartTime, save.CurrentActIndex, save.SchemaVersion);
            WriteToDisk(json);
            MainFile.Logger.Info($"[SlayTheRequiem] Камень Памяти: Отпечаток акта {save.CurrentActIndex + 1} сохранён.");
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Камень Памяти: Отпечаток не снят ({e.Message}).");
        }
    }

    private static string? DiskPath()
    {
        try { return SaveManager.Instance.GetProfileScopedPath(Path.Combine(UserDataPathProvider.SavesDir, FileName)); }
        catch { return null; }
    }

    private static void WriteToDisk(string json)
    {
        var path = DiskPath();
        if (path == null) return;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(json);
    }

    private static Imprint? ReadFromDisk()
    {
        var path = DiskPath();
        if (path == null || !FileAccess.FileExists(path)) return null;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return null;
        var json = file.GetAsText();
        var result = JsonSerializationUtility.FromJson<SerializableRun>(json);
        if (!result.Success || result.SaveData == null) return null;
        var save = result.SaveData;
        return new Imprint(json, save.SerializableRng?.Seed ?? string.Empty, save.StartTime, save.CurrentActIndex, save.SchemaVersion);
    }

    /// <summary>Отпечаток именно этого забега (по зерну и времени старта) и совместимой версии схемы.</summary>
    private static Imprint? ImprintFor(RunState state, SerializableRun ended)
    {
        _imprint ??= ReadFromDisk();
        var imprint = _imprint;
        if (imprint == null) return null;
        var seed = ended.SerializableRng?.Seed ?? state.Rng.StringSeed;
        if (imprint.Seed != seed || imprint.StartTime != ended.StartTime) return null;
        if (!MemoryStoneRules.IsSnapshotCompatible(imprint.Schema, SaveManager.Instance.GetLatestSchemaVersion<SerializableRun>())) return null;
        return imprint;
    }

    // ───── экран поражения ─────

    /// <summary>Можно ли предложить возврат: поражение, одиночная игра, есть Отпечаток и Свечи.</summary>
    public static bool CanOffer(RunState state, SerializableRun ended)
    {
        if (!Enabled || _restoring || !IsSingleplayer) return false;
        if (state.CurrentRoom?.IsVictoryRoom ?? false) return false;
        var me = state.Players.FirstOrDefault();
        if (me == null || !RequiemMode.IsInSystem(me)) return false;
        var stone = MemoryStoneRelic.Of(me);
        if (stone == null || stone.CandlesLeft <= 0) return false;
        return ImprintFor(state, ended) != null;
    }

    /// <summary>Окно «Вернуться к Камню Памяти?», выбор Шрама и возврат.</summary>
    public static async Task Offer(RunState state, SerializableRun ended)
    {
        var me = state.Players.FirstOrDefault();
        var stone = MemoryStoneRelic.Of(me);
        var imprint = ImprintFor(state, ended);
        if (me == null || stone == null || imprint == null) return;

        var popup = NGenericPopup.Create();
        if (popup == null || NModalContainer.Instance == null) return;
        NModalContainer.Instance.Add(popup);
        const string key = "SLAYTHEREQUIEM-MEMORY_STONE_RELIC";
        var body = new LocString("relics", $"{key}.offerBody");
        body.Add("Act", imprint.ActIndex + 1);
        body.Add("Candles", stone.CandlesLeft == MemoryStoneRules.Infinite ? -1 : stone.CandlesLeft);
        if (!await popup.WaitForConfirmation(body, new LocString("relics", $"{key}.offerHeader"),
                new LocString("relics", $"{key}.offerNo"), new LocString("relics", $"{key}.offerYes")))
            return;

        // Шрам — выбор из трёх карт (видны заранее, docs/design/15 §3). Пропуск = отказ от возврата.
        var choices = new List<CardModel>
        {
            state.CreateCard<ScarOfFlesh>(me),
            state.CreateCard<ScarOfSoul>(me),
            state.CreateCard<ScarOfPurse>(me),
        };
        var picked = await CardSelectCmd.FromChooseACardScreen(new ThrowingPlayerChoiceContext(), choices, me, canSkip: true);
        if (picked is not ScarChoiceCard scar) return;

        var pending = new PendingReturn(
            scar.Scar,
            stone.CandlesUsed + 1,
            stone.BonusCandles,
            stone.Rollbacks + 1,
            stone.FleshScars,
            stone.SoulScars,
            stone.PurseScars,
            RevengeTargetsOf(state),
            imprint.ActIndex);
        await Return(imprint, pending);
    }

    /// <summary>Кто убил: монстры текущего боя и их фракции.</summary>
    private static string RevengeTargetsOf(RunState state)
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

    // ───── возврат ─────

    private static async Task Return(Imprint imprint, PendingReturn pending)
    {
        var parsed = JsonSerializationUtility.FromJson<SerializableRun>(imprint.Json);
        if (!parsed.Success || parsed.SaveData == null)
        {
            MainFile.Logger.Warn("[SlayTheRequiem] Камень Памяти: Отпечаток не читается — возврат отменён.");
            return;
        }
        var save = parsed.SaveData;
        _restoring = true;
        try
        {
            await NGame.Instance.Transition.FadeOut();
            RunManager.Instance.CleanUp();
            // Штатный путь «Продолжить» (NMainMenu.OnContinueButtonPressedAsync).
            var runState = RunState.FromSerializable(save);
            await RunManager.Instance.SetUpSavedSingleplayer(runState, save);
            NGame.Instance.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
            await NGame.Instance.LoadRun(runState, save.PreFinishedRoom);
            await ApplyPending(runState, pending);
            await NGame.Instance.Transition.FadeIn();
            // Сразу записать забег: «Сохранить и выйти» после возврата не должен терять Шрам.
            await SaveManager.Instance.SaveRun(null);
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"[SlayTheRequiem] Камень Памяти: возврат не удался ({e.Message}).");
            await NGame.Instance.ReturnToMainMenuWithInternalError(e);
        }
        finally
        {
            _restoring = false;
        }
    }

    /// <summary>Перенести Свечи/Шрамы из погибшей попытки и наложить выбранный Шрам.</summary>
    private static async Task ApplyPending(RunState state, PendingReturn pending)
    {
        var me = state.Players.FirstOrDefault();
        var stone = MemoryStoneRelic.Of(me);
        if (me == null || stone == null) return;

        // Шрамы, наложенные ПОСЛЕ снятия Отпечатка (прошлые возвраты к этому же Камню + новый), в Отпечатке отсутствуют:
        // накладываем разницу между счётчиками погибшей попытки (+ новый Шрам) и счётчиками из Отпечатка.
        var applyFlesh = MemoryStoneRules.ScarsToApply(pending.FleshScars, pending.Scar == Scar.Flesh, stone.FleshScars);
        var applySoul = MemoryStoneRules.ScarsToApply(pending.SoulScars, pending.Scar == Scar.Soul, stone.SoulScars);
        var applyPurse = MemoryStoneRules.ScarsToApply(pending.PurseScars, pending.Scar == Scar.Purse, stone.PurseScars);

        stone.CandlesUsed = pending.CandlesUsed;
        stone.BonusCandles = pending.BonusCandles;
        stone.Rollbacks = pending.Rollbacks;
        stone.FleshScars = pending.FleshScars + (pending.Scar == Scar.Flesh ? 1 : 0);
        stone.SoulScars = pending.SoulScars + (pending.Scar == Scar.Soul ? 1 : 0);
        stone.PurseScars = pending.PurseScars + (pending.Scar == Scar.Purse ? 1 : 0);
        stone.RevengeTargets = pending.RevengeTargets;
        stone.RevengeAct = pending.ActIndex;

        var creature = me.Creature;
        for (var i = 0; i < applyFlesh; i++)
            await CreatureCmd.SetMaxHp(creature, MemoryStoneRules.FleshScarMaxHp(creature.MaxHp));
        if (creature.CurrentHp > creature.MaxHp) await CreatureCmd.SetCurrentHp(creature, creature.MaxHp);

        for (var i = 0; i < applySoul; i++)
            await CardPileCmd.Add(state.CreateCard<DeathEcho>(me), PileType.Deck);

        for (var i = 0; i < applyPurse; i++)
            await PlayerCmd.SetGold(MemoryStoneRules.PurseScarGold(me.Gold), me);
    }
}
