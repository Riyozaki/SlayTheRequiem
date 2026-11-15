using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using FileAccess = Godot.FileAccess;

namespace SlayTheRequiem.SlayTheRequiemCode.Rollback;

/// <summary>
/// Камень Памяти (docs/design/15): Отпечаток забега в начале каждого акта и возврат к нему с экрана поражения.
///
/// Отпечаток — обычный <see cref="SerializableRun"/> (тот же, что пишет «Сохранить и выйти»), снятый на хуке
/// <c>AfterActEntered</c>; хранится в памяти (как JSON) и копией в папке сохранений профиля, чтобы пережить перезапуск
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

    public static bool Enabled => RequiemConfig.EnableMemoryStone && RequiemConfig.EnableRequiemSystems;

    // ───── Отпечаток ─────

    /// <summary>Снять Отпечаток (после входа в акт — стартовый узел уже закрыт). В коопе — только хост.</summary>
    public static void TakeImprint()
    {
        if (!Enabled || RequiemNet.IsClient || !RunManager.Instance.ShouldSave) return;
        var state = RunManager.Instance.DebugOnlyGetState();
        if (state == null || !state.Players.Any(p => RequiemMode.IsInSystem(p))) return;
        try
        {
            var save = RunManager.Instance.ToSave(null);
            var json = JsonSerializationUtility.ToJson(save);
            _imprint = new Imprint(json, save.SerializableRng?.Seed ?? string.Empty, save.StartTime, save.CurrentActIndex, save.SchemaVersion);
            _imprintIsCoop = RequiemNet.IsMultiplayer;
            WriteToDisk(json, _imprintIsCoop);
            MainFile.Logger.Info($"[SlayTheRequiem] Камень Памяти: Отпечаток акта {save.CurrentActIndex + 1} сохранён.");
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SlayTheRequiem] Камень Памяти: Отпечаток не снят ({e.Message}).");
        }
    }

    private static string? DiskPath(bool coop)
    {
        try { return SaveManager.Instance.GetProfileScopedPath(Path.Combine(UserDataPathProvider.SavesDir, coop ? CoopFileName : FileName)); }
        catch { return null; }
    }

    private static void WriteToDisk(string json, bool coop)
    {
        var path = DiskPath(coop);
        if (path == null) return;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(json);
    }

    private static Imprint? ReadFromDisk(bool coop)
    {
        var path = DiskPath(coop);
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
    internal static Imprint? ImprintFor(RunState state, SerializableRun ended)
    {
        var coop = RequiemNet.IsMultiplayer;
        if (_imprint == null || _imprintIsCoop != coop)
        {
            _imprint = ReadFromDisk(coop);
            _imprintIsCoop = coop;
        }
        var imprint = _imprint;
        if (imprint == null) return null;
        var seed = ended.SerializableRng?.Seed ?? state.Rng.StringSeed;
        if (imprint.Seed != seed || imprint.StartTime != ended.StartTime) return null;
        if (!MemoryStoneRules.IsSnapshotCompatible(imprint.Schema, SaveManager.Instance.GetLatestSchemaVersion<SerializableRun>())) return null;
        return imprint;
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
                stone.Rollbacks = rollbacks;
                stone.FleshScars = deadStone.FleshScars + (scar == Scar.Flesh ? 1 : 0);
                stone.SoulScars = deadStone.SoulScars + (scar == Scar.Soul ? 1 : 0);
                stone.PurseScars = deadStone.PurseScars + (scar == Scar.Purse ? 1 : 0);
                stone.RevengeTargets = revenge;
                stone.RevengeAct1 = imprint.ActIndex + 1;
                stone.MapRerollAct1 = imprint.ActIndex + 1;

                relicSave.Props = SavedProperties.From(stone);
            }

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

    private static async Task ReturnSingleplayer(SerializableRun save)
    {
        Restoring = true;
        try
        {
            await NGame.Instance.Transition.FadeOut();
            RunManager.Instance.CleanUp();
            // Штатный путь «Продолжить» (NMainMenu.OnContinueButtonPressedAsync).
            var runState = RunState.FromSerializable(save);
            await RunManager.Instance.SetUpSavedSingleplayer(runState, save);
            NGame.Instance.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
            await NGame.Instance.LoadRun(runState, save.PreFinishedRoom);
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
            await CardPileCmd.Add(player.RunState.CreateCard<DeathEcho>(player), PileType.Deck);

        for (var i = 0; i < purse; i++)
            await PlayerCmd.SetGold(MemoryStoneRules.PurseScarGold(player.Gold), player);
    }
}
