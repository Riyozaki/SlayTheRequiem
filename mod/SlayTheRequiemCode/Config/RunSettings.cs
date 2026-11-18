using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Net;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Config;

/// <summary>
/// Игровые настройки мода, которые действуют в текущем забеге (ревью F-03). <see cref="RequiemConfig"/> — это то, что
/// игрок выставил в меню своей машины; здесь — снимок на забег:
/// <list type="bullet">
/// <item>новый забег: хост (в одиночной игре — игрок) снимает настройки в момент старта, клиенты коопа получают снимок
/// хоста сообщением <see cref="RunSettingsMessage"/> до того, как игра строит список актов;</item>
/// <item>личный выбор (происхождения, предыстория) каждый клиент отправляет хосту, нажимая «Готов»
/// (<see cref="PersonalChoicesMessage"/>); у хоста он попадает в снимок;</item>
/// <item>снимок хранится в Гримуаре каждого героя (<see cref="Grimoire.RunSettingsCode"/>), поэтому загрузка, возврат
/// Камнем Памяти и переподключение берут правила самого забега, а не текущие настройки машины.</item>
/// </list>
/// Вне забега (меню, лобби) свойства читают <see cref="RequiemConfig"/>. Сохранения без восстанавливаемого снимка
/// отклоняются до записи; забег без служебных реликвий остаётся выключенным, а не берёт правила меню.
/// Оформление (эффекты, звуки, кукла, цвет названий) не синхронизируется: оно не меняет состояние игры.
/// </summary>
public static class RunSettings
{
    private static RunSettingsData? _active;
    private static RunSettingsData? _fromHost;
    private static readonly Dictionary<ulong, PersonalChoices> LobbyChoices = new();
    private static readonly Dictionary<ulong, int> LobbyStartupProtocols = new();
    private static int _hostStartupProtocol;
    private static WeakReference<StartRunLobby>? _lobby;
    private static StartRunLobby? Lobby => _lobby != null && _lobby.TryGetTarget(out var lobby) ? lobby : null;

    /// <summary>Защитная копия снимка текущего забега; null — забега нет и действуют настройки меню.</summary>
    public static RunSettingsData? Active => _active?.Clone();
    public static string SourceName { get; private set; } = "menu (no run snapshot)";

    // ───── настройки, которые читает игровая логика ─────

    /// <summary>Главный выключатель. Ложь и при сбое несущего патча (<see cref="PatchReport.Degraded"/>).</summary>
    public static bool EnableRequiemSystems => !PatchReport.Degraded && (_active?.EnableRequiemSystems ?? RequiemConfig.EnableRequiemSystems);

    public static bool EnableOverstrain => _active?.EnableOverstrain ?? RequiemConfig.EnableOverstrain;
    public static int EnemyHpPercent => _active?.EnemyHpPercent ?? RequiemConfig.EnemyHpPercent;
    public static bool AutoDodge => _active?.AutoDodge ?? RequiemConfig.AutoDodge;
    public static bool ShowVanillaOnlyHeroes => _active?.ShowVanillaOnlyHeroes ?? RequiemConfig.ShowVanillaOnlyHeroes;
    public static bool FiveActs => _active?.FiveActs ?? RequiemConfig.FiveActs;
    public static bool ShortRun => _active?.ShortRun ?? RequiemConfig.ShortRun;
    public static ActVariantMode ActVariant => _active != null ? (ActVariantMode)_active.ActVariant : RequiemConfig.ActVariant;
    public static int RequiemLevelSetting => _active?.RequiemLevelSetting ?? RequiemConfig.RequiemLevelSetting;
    public static SuperbossMode Superbosses => _active != null ? (SuperbossMode)_active.Superbosses : RequiemConfig.Superbosses;
    public static bool WideMaps => _active?.WideMaps ?? RequiemConfig.WideMaps;
    public static bool ShopVariety => _active?.ShopVariety ?? RequiemConfig.ShopVariety;
    public static bool AllowTheft => _active?.AllowTheft ?? RequiemConfig.AllowTheft;
    public static bool EnablePantheon => _active?.EnablePantheon ?? RequiemConfig.EnablePantheon;
    public static bool EnableCampMeta => _active?.EnableCampMeta ?? RequiemConfig.EnableCampMeta;
    public static int CampBuildingsUnlocked => _active?.CampBuildingsUnlocked ?? RequiemConfig.CampBuildingsUnlocked;
    public static bool EnableMemoryStone => _active?.EnableMemoryStone ?? RequiemConfig.EnableMemoryStone;
    public static int MemoryStoneCandles => _active?.MemoryStoneCandles ?? RequiemConfig.MemoryStoneCandles;

    // ───── личный выбор ─────

    public static PlatebearerOriginChoice PlatebearerOrigin(Player player) => (PlatebearerOriginChoice)ChoicesFor(player).PlatebearerOrigin;
    public static SilentOriginChoice SilentOrigin(Player player) => (SilentOriginChoice)ChoicesFor(player).SilentOrigin;
    public static RegentOriginChoice RegentOrigin(Player player) => (RegentOriginChoice)ChoicesFor(player).RegentOrigin;
    public static BackgroundChoice Background(Player player) => (BackgroundChoice)ChoicesFor(player).Background;

    /// <summary>
    /// Личный выбор игрока. Без снимка (старое сохранение): свой — из меню, чужой — «Случайно», одинаково на всех машинах.
    /// </summary>
    public static PersonalChoices ChoicesFor(Player player)
    {
        if (_active != null) return _active.ChoicesFor(player.NetId);
        var isLocal = !RequiemNet.IsMultiplayer || player.NetId == MegaCrit.Sts2.Core.Context.LocalContext.NetId;
        return isLocal ? LocalChoices() : PersonalChoices.Random;
    }

    public static PersonalChoices LocalChoices() => new(
        (int)RequiemConfig.PlatebearerOrigin, (int)RequiemConfig.SilentOrigin, (int)RequiemConfig.RegentOrigin, (int)RequiemConfig.Background);

    // ───── снимок ─────

    /// <summary>Снимок настроек меню этой машины (с её личным выбором под <paramref name="localNetId"/>).</summary>
    public static RunSettingsData FromConfig(ulong? localNetId)
    {
        var data = new RunSettingsData
        {
            EnableRequiemSystems = RequiemConfig.EnableRequiemSystems,
            EnableOverstrain = RequiemConfig.EnableOverstrain,
            EnemyHpPercent = RequiemConfig.EnemyHpPercent,
            AutoDodge = RequiemConfig.AutoDodge,
            ShowVanillaOnlyHeroes = RequiemConfig.ShowVanillaOnlyHeroes,
            FiveActs = RequiemConfig.FiveActs,
            ShortRun = RequiemConfig.ShortRun,
            ActVariant = (int)RequiemConfig.ActVariant,
            RequiemLevelSetting = RequiemConfig.RequiemLevelSetting,
            Superbosses = (int)RequiemConfig.Superbosses,
            WideMaps = RequiemConfig.WideMaps,
            ShopVariety = RequiemConfig.ShopVariety,
            AllowTheft = RequiemConfig.AllowTheft,
            EnablePantheon = RequiemConfig.EnablePantheon,
            EnableCampMeta = RequiemConfig.EnableCampMeta,
            CampBuildingsUnlocked = RequiemConfig.CampBuildingsUnlocked,
            EnableMemoryStone = RequiemConfig.EnableMemoryStone,
            MemoryStoneCandles = RequiemConfig.MemoryStoneCandles,
        };
        StartupState.StampNewRules(data);
        if (localNetId is { } id) data.Personal[id] = LocalChoices();
        return data;
    }

    /// <summary>Строка снимка для сохранения в Гримуаре; без снимка — снимок меню (забег стартовал не через лобби).</summary>
    public static string CodeForNewRun()
    {
        if (_active == null)
        {
            if (RequiemNet.IsMultiplayer)
                throw new InvalidOperationException("Multiplayer Requiem run requires the host lobby settings snapshot; local fallback is not safe");
            _active = FromConfig(MegaCrit.Sts2.Core.Context.LocalContext.NetId ?? RequiemNet.LocalNetId);
            if (!RequiemNet.IsMultiplayer) _active.RunId = Guid.NewGuid().ToString("N");
            SourceName = "local fallback (run started outside lobby)";
            Debugging.RunDiagnostics.LaunchKind = "new";
            MainFile.Logger.Info("RunSettings: забег начат не из лобби — снимок взят из настроек этой машины.");
        }
        return _active.Encode();
    }

    // ───── старт, загрузка, конец забега ─────

    /// <summary>Хост: снимок с личным выбором всех игроков лобби — рассылается до сообщения о старте забега.</summary>
    private static RunSettingsData BuildHostSnapshot(StartRunLobby lobby)
    {
        if (lobby.NetService.Type == NetGameType.Host)
            StartupState.RequirePeers(lobby.Players.Where(p => p.id != lobby.NetService.NetId).Select(p => p.id), LobbyStartupProtocols);
        var data = FromConfig(lobby.NetService.NetId);
        data.RunId = Guid.NewGuid().ToString("N");
        foreach (var player in lobby.Players)
            if (player.id != lobby.NetService.NetId)
                data.Personal[player.id] = LobbyChoices.TryGetValue(player.id, out var c) ? c : PersonalChoices.Random;
        return data;
    }

    /// <summary>Старт нового забега на этой машине (хост, клиент или одиночная игра).</summary>
    private static void BeginRun(StartRunLobby lobby)
    {
        var type = lobby.NetService.Type;
        Debugging.RunDiagnostics.LaunchKind = "new";
        SourceName = type == NetGameType.Client ? "host snapshot" : "host/local snapshot";
        if (type == NetGameType.Client)
        {
            if (_fromHost == null || string.IsNullOrEmpty(_fromHost.RunId))
            {
                SourceName = "MISSING HOST SNAPSHOT: launch rejected";
                // Starting with local rules would already desynchronize combat, before operation guards can help.
                throw new InvalidOperationException("Requiem host settings/run identity missing; update the same mod version on all peers and restart the lobby");
            }
            StartupState.RequireHost(_hostStartupProtocol);
            StartupState.RequireNewRules(_fromHost);
            _active = _fromHost;
        }
        else
        {
            _active = _fromHost ?? BuildHostSnapshot(lobby);
        }
        _fromHost = null;
        LobbyChoices.Clear();
        LobbyStartupProtocols.Clear();
        MainFile.Logger.Info($"RunSettings: забег начат с настройками {_active.Encode()}");
    }

    /// <summary>No model setters, menu fallback, disk write or RNG use. Runs BEFORE native reload-counter writes.</summary>
    internal static SavedRulesSnapshot ValidateSaved(SerializableRun save)
    {
        var grimoireId = ModelDb.Relic<Grimoire>().Id;
        var packId = ModelDb.Relic<Alchemy.TravelPack>().Id;
        var codes = new List<string?>();
        foreach (var player in save.Players)
        {
            var grimoires = player.Relics.Where(r => r.Id == grimoireId).ToList();
            if (grimoires.Count > 1) throw new FormatException("Duplicate saved Grimoire; no save rewrite");
            if (grimoires.Count == 1) codes.Add(SavedString(grimoires[0].Props, nameof(Grimoire.RunSettingsCode)));
            var packs = player.Relics.Where(r => r.Id == packId).ToList();
            if (packs.Count > 1) throw new FormatException("Duplicate saved TravelPack; no save rewrite");
            foreach (var pack in packs)
                SavedOperationState.Validate(SavedInt(pack.Props, nameof(Alchemy.TravelPack.DataSchema)),
                    SavedInt(pack.Props, nameof(Alchemy.TravelPack.ShopProtocol)), SavedBool(pack.Props, nameof(Alchemy.TravelPack.OperationBlocked)),
                    SavedString(pack.Props, nameof(Alchemy.TravelPack.OperationPending)), Debugging.RunDiagnostics.SaveSchema, Shops.ShopOperations.Protocol);
            foreach (var path in player.Relics.Where(r => ModelDb.GetByIdOrNull<RelicModel>(r.Id) is Transformations.PathEssence))
                _ = RelicGrantState.Read(SavedString(path.Props, nameof(Transformations.PathEssence.SignGrant)));
            HeroProgressionSave.Validate(player);
        }
        var timelines = save.Players.SelectMany(p => p.Relics).Where(r => r.Id == packId)
            .Select(r => SavedString(r.Props, nameof(Alchemy.TravelPack.OperationRunId)))
            .Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        if (timelines.Count > 1) throw new FormatException("Conflicting saved Requiem timelines; original save retained");
        return SavedRulesSnapshot.Read(codes);
    }

    internal static string? SavedString(SavedProperties? props, string name)
    {
        RequireSavedKind(props, name, "strings");
        var values = props?.strings?.Where(p => p.name == name).ToList();
        if (values is { Count: > 1 }) throw new FormatException($"Ambiguous saved property {name}");
        return values is { Count: 1 } ? values[0].value : null;
    }
    internal static bool SavedBool(SavedProperties? props, string name)
    {
        RequireSavedKind(props, name, "bools");
        var values = props?.bools?.Where(p => p.name == name).ToList();
        if (values is { Count: > 1 }) throw new FormatException($"Ambiguous saved property {name}");
        return values is { Count: 1 } && values[0].value;
    }
    internal static int SavedInt(SavedProperties? props, string name)
    {
        RequireSavedKind(props, name, "ints");
        var values = props?.ints?.Where(p => p.name == name).ToList();
        if (values is { Count: > 1 }) throw new FormatException($"Ambiguous saved property {name}");
        return values is { Count: 1 } ? values[0].value : 0;
    }

    private static void RequireSavedKind(SavedProperties? props, string name, string kind)
    {
        if (props == null) return;
        if (kind != "ints" && props.ints?.Any(p => p.name == name) == true
            || kind != "bools" && props.bools?.Any(p => p.name == name) == true
            || kind != "strings" && props.strings?.Any(p => p.name == name) == true
            || props.intArrays?.Any(p => p.name == name) == true || props.modelIds?.Any(p => p.name == name) == true
            || props.cards?.Any(p => p.name == name) == true || props.cardArrays?.Any(p => p.name == name) == true)
            throw new FormatException($"Saved field {name} is in an unexpected property bucket; no silent default/rewrite");
    }

    /// <summary>Loading a disabled/vanilla run never enables Requiem from the local menu.</summary>
    private static void LoadRun(RunState? state)
    {
        _active = null;
        SourceName = "saved rules not validated";
        Debugging.RunDiagnostics.LaunchKind = "continue";
        if (state == null) return;
        var result = SavedRulesSnapshot.Read(state.Players.SelectMany(p => p.Relics.OfType<Grimoire>())
            .Select(g => (string?)g.RunSettingsCode).ToList());
        _active = result.Rules ?? new RunSettingsData { EnableRequiemSystems = false };
        SourceName = result.Rules == null ? "saved run without Requiem systems" : "saved run snapshot";
        MainFile.Logger.Info($"RunSettings: {SourceName} {_active.Encode()}");
    }

    /// <summary>Backup precedes migration AND the game's IncrementNumReloads. Ordinary Continue preserves timeline/revision.</summary>
    internal static void PrepareSavedRun(RunState state, SerializableRun save, bool coop, bool keepBackup)
    {
        var validation = ValidateSaved(save);
        LoadRun(state);
        var grimoires = state.Players.SelectMany(p => p.Relics.OfType<Grimoire>()).ToList();
        var packs = state.Players.Select(Alchemy.TravelPack.Of).OfType<Alchemy.TravelPack>().ToList();
        var timelines = packs.Select(p => p.OperationRunId).Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (timelines.Count > 1) throw new FormatException("Saved Requiem operation timelines disagree; original save retained");
        var records = state.Players.Select(Progression.HeroRecord.Of).OfType<Progression.HeroRecord>().ToList();
        var needsMigration = validation.MissingCopies || grimoires.Any(g => StartupState.NeedsLegacyMigration(g.StartProtocol) || g.MasterySourceData.Length == 0)
            || records.Any(r => r.FatigueVisitKey.Length == 0) || packs.Any(p => p.OperationRunId.Length == 0
                || p.DataSchema < Debugging.RunDiagnostics.SaveSchema || p.ShopProtocol < Shops.ShopOperations.Protocol)
            || validation.Rules is { RunId.Length: 0 };
        if (needsMigration && keepBackup) OperationRecovery.BackupBeforeMigration(save, coop);
        if (validation.Rules != null && _active is { RunId.Length: 0 })
            _active.RunId = OperationIdentity.LegacyNonce(state.Rng.StringSeed, save.StartTime)[..32].ToLowerInvariant();
        if (validation.Rules != null && _active != null)
        {
            var code = _active.Encode();
            foreach (var grimoire in grimoires) grimoire.RunSettingsCode = code;
        }
        foreach (var grimoire in grimoires)
        {
            grimoire.MigrateLegacyMastery(); // opaque exact counters, no reconstruction of historical paid ranks
            grimoire.MigrateLegacyStart();
        }
        foreach (var record in records.Where(r => !InventoryGate.For(r.Owner).Blocked)) record.RecordNativeSchools();
        foreach (var record in records.Where(r => r.FatigueVisitKey.Length == 0))
            record.FatigueVisitKey = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{state.CurrentActIndex}:{state.TotalFloor}");
        var timeline = timelines.FirstOrDefault() ?? (_active?.RunId is { Length: > 0 } id ? id
            : OperationIdentity.LegacyNonce(state.Rng.StringSeed, save.StartTime));
        foreach (var pack in packs)
        {
            if (pack.OperationRunId.Length == 0) pack.OperationRunId = timeline;
            pack.DataSchema = Debugging.RunDiagnostics.SaveSchema;
            pack.ShopProtocol = Shops.ShopOperations.Protocol;
        }
        if (needsMigration) SourceName = "saved rules + backed-up legacy metadata migration";
    }

    /// <summary>Клиент: снимок хоста (до сообщения о старте забега).</summary>
    public static void OnHostSettings(string code, ulong senderId)
    {
        if (Lobby is { } hostLobby && hostLobby.NetService.Type == NetGameType.Host)
        {
            if (senderId == hostLobby.NetService.NetId || !hostLobby.Players.Any(p => p.id == senderId)) return;
            if (code == StartupState.LobbyQuery) OfferStartupProtocol(hostLobby, senderId);
            else if (code == StartupState.LobbyAgreement) OnStartupProtocol(senderId, StartupState.Protocol);
            else if (code != null && code.StartsWith(StartupState.LobbyPrefix, StringComparison.Ordinal)) LobbyStartupProtocols.Remove(senderId);
            return; // a client can never install rules on the host
        }
        if (Lobby?.NetService is not INetClientGameService { NetClient: { } client } || senderId != client.HostNetId)
        {
            MainFile.Logger.Warn($"RunSettings: ignored non-host/out-of-lobby settings from {senderId}");
            return;
        }
        if (code == StartupState.LobbyOffer)
        {
            _hostStartupProtocol = StartupState.Protocol;
            CustomMessageWrapper.Send(new RunSettingsMessage { data = StartupState.LobbyAgreement }, Lobby!.NetService);
            return;
        }
        if (code != null && code.StartsWith(StartupState.LobbyPrefix, StringComparison.Ordinal))
        {
            _hostStartupProtocol = 0; _fromHost = null;
            MainFile.Logger.Warn("RunSettings: incompatible host startup capability; use the same mod build");
            return;
        }
        _fromHost = null; // an invalid NEW host snapshot must not silently reuse a previous start attempt
        if (code == null || code.Length > 16384 || RunSettingsData.Decode(code) is not { RunId.Length: > 0 } data)
        {
            MainFile.Logger.Warn($"RunSettings: invalid/missing run identity in host snapshot from {senderId}");
            return;
        }
        try { StartupState.RequireHost(_hostStartupProtocol); StartupState.RequireNewRules(data); }
        catch (Exception e) when (e is FormatException or InvalidOperationException) { MainFile.Logger.Error($"RunSettings: host wizard protocol rejected: {e.Message}"); return; }
        _fromHost = data;
        MainFile.Logger.Info($"RunSettings: получен снимок хоста {code}");
    }

    private static void OfferStartupProtocol(StartRunLobby lobby, ulong peerId) =>
        lobby.NetService.SendMessage(new CustomMessageWrapper { Message = new RunSettingsMessage { data = StartupState.LobbyOffer } }, peerId);

    private static void RefuseStartup(string reason)
    {
        MainFile.Logger.Warn("RunSettings: startup refused: " + reason);
        UI.NRequiemToast.Show(UI.RequiemUi.TipText("CREATION.protocolMismatch.title"));
    }

    /// <summary>Authenticated ready-time agreement; old/missing/future clients cannot start a different choice stream.</summary>
    public static void OnStartupProtocol(ulong senderId, int protocol)
    {
        if (Lobby is not { } lobby || lobby.NetService.Type != NetGameType.Host || senderId == lobby.NetService.NetId
            || !lobby.Players.Any(p => p.id == senderId)) return;
        LobbyStartupProtocols.Remove(senderId);
        if (protocol == StartupState.Protocol) LobbyStartupProtocols[senderId] = protocol;
        else MainFile.Logger.Error($"RunSettings: incompatible hero creation protocol {protocol} from {senderId}");
    }

    /// <summary>Хост: личный выбор клиента (при «Готов»).</summary>
    public static void OnPersonalChoices(ulong senderId, PersonalChoices choices)
    {
        if (Lobby is not { } lobby || lobby.NetService.Type != NetGameType.Host
            || senderId == lobby.NetService.NetId || !lobby.Players.Any(p => p.id == senderId)) return;
        if (!Enum.IsDefined(typeof(PlatebearerOriginChoice), choices.PlatebearerOrigin)
            || !Enum.IsDefined(typeof(SilentOriginChoice), choices.SilentOrigin)
            || !Enum.IsDefined(typeof(RegentOriginChoice), choices.RegentOrigin)
            || !Enum.IsDefined(typeof(BackgroundChoice), choices.Background))
        {
            MainFile.Logger.Warn($"RunSettings: invalid personal choices from {senderId}");
            return;
        }
        LobbyChoices[senderId] = choices;
    }

    // ───── патчи лобби и забега ─────

    [CriticalPatch]
    [HarmonyPatch(typeof(StartRunLobby), MethodType.Constructor, typeof(GameMode), typeof(INetGameService), typeof(IStartRunLobbyListener), typeof(int))]
    public static class TrackStartLobby
    {
        [HarmonyPostfix]
        public static void Postfix(StartRunLobby __instance)
        {
            LobbyMessageBridge.Ensure(__instance.NetService);
            _lobby = new(__instance);
            _fromHost = null;
            _hostStartupProtocol = 0;
            LobbyChoices.Clear(); // no choices/nonce from a previously cancelled lobby
            LobbyStartupProtocols.Clear();
            __instance.PlayerConnected += player =>
            {
                if (!ReferenceEquals(Lobby, __instance)) return;
                if (__instance.NetService.Type == NetGameType.Host && player.id != __instance.NetService.NetId)
                    OfferStartupProtocol(__instance, player.id);
                else if (__instance.NetService.Type == NetGameType.Client && player.id == __instance.NetService.NetId)
                    CustomMessageWrapper.Send(new RunSettingsMessage { data = StartupState.LobbyQuery }, __instance.NetService);
            };
            __instance.PlayerDisconnected += player =>
            {
                if (!ReferenceEquals(Lobby, __instance)) return;
                LobbyChoices.Remove(player.id); LobbyStartupProtocols.Remove(player.id); // no stale agreement on rejoin
            };
        }
    }

    [HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.CleanUp))]
    public static class ForgetStartLobby
    {
        [HarmonyPostfix]
        public static void Postfix(StartRunLobby __instance)
        {
            if (ReferenceEquals(Lobby, __instance))
            {
                _lobby = null; _hostStartupProtocol = 0;
                LobbyChoices.Clear(); LobbyStartupProtocols.Clear();
            }
        }
    }

    /// <summary>Хост рассылает снимок до <c>LobbyBeginRunMessage</c>: сообщения надёжные и упорядоченные.</summary>
    [CriticalPatch]
    [HarmonyPatch(typeof(StartRunLobby), "BeginRunForAllPlayers")]
    public static class HostSendsSnapshot
    {
        private static readonly AccessTools.FieldRef<StartRunLobby, bool> IsBeginning =
            AccessTools.FieldRefAccess<StartRunLobby, bool>("_isBeginningRun");

        [HarmonyPrefix]
        public static bool Prefix(StartRunLobby __instance)
        {
            if (__instance.NetService.Type != NetGameType.Host || IsBeginning(__instance)) return true;
            RunSettingsData data;
            try { data = BuildHostSnapshot(__instance); }
            catch (InvalidOperationException e) { _fromHost = null; RefuseStartup(e.Message); return false; }
            _fromHost = data;
            try { CustomMessageWrapper.Send(new RunSettingsMessage { data = data.Encode() }, __instance.NetService); }
            catch (Exception e)
            {
                _fromHost = null;
                SourceName = "HOST SNAPSHOT SEND FAILED: launch rejected";
                MainFile.Logger.Error($"RunSettings: снимок не разослан, старт отменён: {e}");
                throw; // fail before LobbyBeginRunMessage/local mutation, not a silent host-only start
            }
            return true;
        }
    }

    /// <summary>Старт забега на любой машине: снимок становится правилами забега (до генерации актов и героев).</summary>
    [CriticalPatch]
    [HarmonyPatch(typeof(StartRunLobby), "BeginRunLocally")]
    public static class ApplyOnBegin
    {
        [HarmonyPrefix]
        public static void Prefix(StartRunLobby __instance) => BeginRun(__instance);
    }

    /// <summary>Клиент отправляет хосту свой личный выбор, нажимая «Готов» (до сообщения о готовности).</summary>
    [CriticalPatch]
    [HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.SetReady))]
    public static class ClientSendsChoices
    {
        [HarmonyPrefix]
        public static bool Prefix(StartRunLobby __instance, bool ready)
        {
            if (!ready || __instance.NetService.Type != NetGameType.Client) return true;
            if (_hostStartupProtocol != StartupState.Protocol)
            {
                CustomMessageWrapper.Send(new RunSettingsMessage { data = StartupState.LobbyQuery }, __instance.NetService);
                RefuseStartup("No compatible host capability yet; ready again after all peers use the same build");
                return false; // do not let an old host start before the modern client can reject its rules
            }
            var c = LocalChoices();
            try
            {
                CustomMessageWrapper.Send(new RunSettingsMessage { data = StartupState.LobbyAgreement }, __instance.NetService);
                CustomMessageWrapper.Send(new PersonalChoicesMessage
                {
                    platebearer = c.PlatebearerOrigin, silent = c.SilentOrigin, regent = c.RegentOrigin, background = c.Background,
                }, __instance.NetService);
            }
            catch (Exception e)
            {
                MainFile.Logger.Error($"RunSettings: protocol/choices not sent; ready rejected: {e}");
                throw;
            }
            return true;
        }
    }

    // Validation at InitializeSavedRun alone is TOO LATE: SetUpSaved* writes NumReloads first.
    [CriticalPatch]
    [HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
    public static class ValidateBeforeDeserialization
    {
        [HarmonyPrefix]
        public static void Prefix(SerializableRun save) => ValidateSaved(save);
    }

    [CriticalPatch]
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedSingleplayer))]
    public static class PrepareSingleplayerLoad
    {
        [HarmonyPrefix]
        public static void Prefix(RunState state, SerializableRun save) => PrepareSavedRun(state, save, coop: false, keepBackup: true);
    }

    [CriticalPatch]
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer))]
    public static class PrepareMultiplayerLoad
    {
        [HarmonyPrefix]
        public static void Prefix(RunState state, LoadRunLobby lobby) =>
            PrepareSavedRun(state, lobby.Run, coop: true, keepBackup: lobby.NetService.Type != NetGameType.Client);
    }

    /// <summary>Reassert saved rules for replay/native load paths as well. All migrating load paths ran earlier.</summary>
    [CriticalPatch]
    [HarmonyPatch(typeof(RunManager), "InitializeSavedRun")]
    public static class ApplyOnLoad
    {
        [HarmonyPrefix]
        public static void Prefix(RunManager __instance) => LoadRun(__instance.DebugOnlyGetState());
    }

    /// <summary>Конец забега: снова действуют настройки меню.</summary>
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
    public static class ClearOnCleanUp
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            _active = null;
            _fromHost = null;
            _hostStartupProtocol = 0;
            LobbyStartupProtocols.Clear();
            SourceName = "menu (no run snapshot)";
            Debugging.RunDiagnostics.LaunchKind = "unknown";
        }
    }
}
