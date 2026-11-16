using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
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
/// Вне забега (меню, лобби) и для старых сохранений без снимка свойства читают <see cref="RequiemConfig"/> напрямую.
/// Оформление (эффекты, звуки, кукла, цвет названий) не синхронизируется: оно не меняет состояние игры.
/// </summary>
public static class RunSettings
{
    private static RunSettingsData? _active;
    private static RunSettingsData? _fromHost;
    private static readonly Dictionary<ulong, PersonalChoices> LobbyChoices = new();

    /// <summary>Снимок текущего забега; null — забега нет (или старое сохранение) и действуют настройки меню.</summary>
    public static RunSettingsData? Active => _active;

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
        if (localNetId is { } id) data.Personal[id] = LocalChoices();
        return data;
    }

    /// <summary>Строка снимка для сохранения в Гримуаре; без снимка — снимок меню (забег стартовал не через лобби).</summary>
    public static string CodeForNewRun()
    {
        if (_active == null)
        {
            _active = FromConfig(MegaCrit.Sts2.Core.Context.LocalContext.NetId ?? RequiemNet.LocalNetId);
            MainFile.Logger.Info("RunSettings: забег начат не из лобби — снимок взят из настроек этой машины.");
        }
        return _active.Encode();
    }

    // ───── старт, загрузка, конец забега ─────

    /// <summary>Хост: снимок с личным выбором всех игроков лобби — рассылается до сообщения о старте забега.</summary>
    private static RunSettingsData BuildHostSnapshot(StartRunLobby lobby)
    {
        var data = FromConfig(lobby.NetService.NetId);
        foreach (var player in lobby.Players)
            if (player.id != lobby.NetService.NetId)
                data.Personal[player.id] = LobbyChoices.TryGetValue(player.id, out var c) ? c : PersonalChoices.Random;
        return data;
    }

    /// <summary>Старт нового забега на этой машине (хост, клиент или одиночная игра).</summary>
    private static void BeginRun(StartRunLobby lobby)
    {
        var type = lobby.NetService.Type;
        if (type == NetGameType.Client)
        {
            if (_fromHost == null)
                MainFile.Logger.Warn("RunSettings: снимок настроек от хоста не пришёл (у хоста нет мода или старая версия?) — " +
                                     "берутся настройки этой машины, возможен рассинхрон.");
            _active = _fromHost ?? FromConfig(lobby.NetService.NetId);
        }
        else
        {
            _active = _fromHost ?? BuildHostSnapshot(lobby);
        }
        _fromHost = null;
        LobbyChoices.Clear();
        MainFile.Logger.Info($"RunSettings: забег начат с настройками {_active.Encode()}");
    }

    /// <summary>Загрузка забега: снимок из Гримуаров. Нет Гримуара у героя мода — забег начат с выключенными системами.</summary>
    private static void LoadRun(RunState? state)
    {
        _active = null;
        if (state == null) return;
        foreach (var player in state.Players)
        {
            var code = player.Relics.OfType<Grimoire>().FirstOrDefault()?.RunSettingsCode;
            if (RunSettingsData.Decode(code) is { } data)
            {
                _active = data;
                MainFile.Logger.Info($"RunSettings: загружен снимок забега {code}");
                return;
            }
        }
        if (state.Players.Any(p => RequiemMode.IsParticipant(p.Character)) && !state.Players.Any(p => p.Relics.OfType<Grimoire>().Any()))
        {
            // Герои мода без Гримуара: забег шёл по ванильным правилам — не включать системы посреди забега.
            _active = FromConfig(null);
            _active.EnableRequiemSystems = false;
            MainFile.Logger.Info("RunSettings: в сохранении нет Гримуара — забег продолжается без систем Реквиема.");
            return;
        }
        MainFile.Logger.Info("RunSettings: в сохранении нет снимка (старая версия мода) — действуют настройки этой машины.");
    }

    /// <summary>Клиент: снимок хоста (до сообщения о старте забега).</summary>
    public static void OnHostSettings(string code, ulong senderId)
    {
        if (RunSettingsData.Decode(code) is not { } data)
        {
            MainFile.Logger.Warn($"RunSettings: не разобран снимок от {senderId}: «{code}»");
            return;
        }
        _fromHost = data;
        MainFile.Logger.Info($"RunSettings: получен снимок хоста {code}");
    }

    /// <summary>Хост: личный выбор клиента (при «Готов»).</summary>
    public static void OnPersonalChoices(ulong senderId, PersonalChoices choices) => LobbyChoices[senderId] = choices;

    // ───── патчи лобби и забега ─────

    /// <summary>Хост рассылает снимок до <c>LobbyBeginRunMessage</c>: сообщения надёжные и упорядоченные.</summary>
    [CriticalPatch]
    [HarmonyPatch(typeof(StartRunLobby), "BeginRunForAllPlayers")]
    public static class HostSendsSnapshot
    {
        private static readonly AccessTools.FieldRef<StartRunLobby, bool> IsBeginning =
            AccessTools.FieldRefAccess<StartRunLobby, bool>("_isBeginningRun");

        [HarmonyPrefix]
        public static void Prefix(StartRunLobby __instance)
        {
            if (__instance.NetService.Type != NetGameType.Host || IsBeginning(__instance)) return;
            var data = BuildHostSnapshot(__instance);
            _fromHost = data;
            try { CustomMessageWrapper.Send(new RunSettingsMessage { data = data.Encode() }, __instance.NetService); }
            catch (Exception e) { MainFile.Logger.Error($"RunSettings: снимок не разослан: {e}"); }
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
    [HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.SetReady))]
    public static class ClientSendsChoices
    {
        [HarmonyPrefix]
        public static void Prefix(StartRunLobby __instance, bool ready)
        {
            if (!ready || __instance.NetService.Type != NetGameType.Client) return;
            var c = LocalChoices();
            try
            {
                CustomMessageWrapper.Send(new PersonalChoicesMessage
                {
                    platebearer = c.PlatebearerOrigin, silent = c.SilentOrigin, regent = c.RegentOrigin, background = c.Background,
                }, __instance.NetService);
            }
            catch (Exception e) { MainFile.Logger.Warn($"RunSettings: личный выбор не отправлен: {e.Message}"); }
        }
    }

    /// <summary>Загрузка (одиночная, кооп, возврат Камнем Памяти): снимок из сохранения.</summary>
    [CriticalPatch]
    [HarmonyPatch(typeof(RunManager), "InitializeSavedRun")]
    public static class ApplyOnLoad
    {
        [HarmonyPrefix]
        public static void Prefix(RunManager __instance, SerializableRun save) => LoadRun(__instance.DebugOnlyGetState());
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
        }
    }
}
