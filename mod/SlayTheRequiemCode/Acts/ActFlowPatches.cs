using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Helpers;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Net;

namespace SlayTheRequiem.SlayTheRequiemCode.Acts;

// Ход забега из пяти актов (docs/design/13 §2, 11 §6): список актов, ванильные таблицы «на три акта», редкие элиты,
// Багровый портал и Безымянный предел.

/// <summary>
/// Пять актов: Подножие → (2а | 2б) → Улей → (4а | 4б) → Слава. Ванильный выбор (по акту на индекс 0..2) сохраняется,
/// новые акты добавляются тем же генератором «act_selection» — у всех игроков коопа список совпадает.
/// «Короткий забег» и выключенные пять актов — ванильные три акта (с оверлеями мода).
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.GetRandomList))]
public static class ActListPatch
{
    public static bool FiveActsEnabled => RequiemConfig.EnableRequiemSystems && RequiemConfig.FiveActs && !RequiemConfig.ShortRun;

    [HarmonyPostfix]
    public static void Postfix(Rng rng, ref IEnumerable<ActModel> __result)
    {
        if (!FiveActsEnabled) return;
        var vanilla = __result.ToList();
        if (vanilla.Count != ActRules.ShortRunActs) return;
        var second = rng.NextItem(new ActModel[] { ModelDb.Act<FrozenBarrows>(), ModelDb.Act<ClockworkDepths>() })!;
        var fourth = rng.NextItem(new ActModel[] { ModelDb.Act<BloodCourt>(), ModelDb.Act<StarTower>() })!;
        __result = new List<ActModel> { vanilla[0], second, vanilla[1], fourth, vanilla[2] };
    }
}

/// <summary>Ступень ванильного кода (0..2) для текущего акта — таблицы игры знают только три акта.</summary>
public static class ActIndexShim
{
    public static int ActCount => RequiemNet.State?.Acts.Count ?? ActRules.ShortRunActs;

    public static int Tier(int actIndex) => ActRules.VanillaTier(actIndex, ActCount);

    /// <summary>Подменяет <c>IRunState.CurrentActIndex</c> в транспайлерах.</summary>
    public static int Tier(IRunState run) => ActRules.VanillaTier(run.CurrentActIndex, run.Acts.Count);

    private static readonly MethodInfo ShimMethod = AccessTools.Method(typeof(ActIndexShim), nameof(Tier), [typeof(IRunState)]);

    /// <summary>Сколько чтений <c>CurrentActIndex</c> заменил каждый транспайлер (0 = IL игры изменился, патч не сработал).</summary>
    public static IReadOnlyDictionary<string, int> Replacements => _replacements;

    private static readonly Dictionary<string, int> _replacements = new();

    /// <summary>
    /// Все чтения <c>CurrentActIndex</c> (через интерфейс или класс) — в <see cref="Tier(IRunState)"/>.
    /// Число замен пишется в лог; ноль — предупреждение: после обновления игры метод читает индекс иначе,
    /// и в актах 4–5 ванильные таблицы получат индекс вне 0..2 (ревью R-04, smoke-проверка M0).
    /// </summary>
    public static IEnumerable<CodeInstruction> ReplaceActIndex(IEnumerable<CodeInstruction> instructions, string patch)
    {
        var result = new List<CodeInstruction>();
        var replaced = 0;
        foreach (var code in instructions)
        {
            if ((code.opcode == OpCodes.Callvirt || code.opcode == OpCodes.Call) && code.operand is MethodInfo
                {
                    Name: "get_CurrentActIndex",
                } method && typeof(IRunState).IsAssignableFrom(method.DeclaringType))
            {
                result.Add(new CodeInstruction(OpCodes.Call, ShimMethod).WithLabels(code.labels).WithBlocks(code.blocks));
                replaced++;
                continue;
            }
            result.Add(code);
        }
        _replacements[patch] = replaced;
        if (replaced == 0)
            MainFile.Logger.Warn($"ActIndexShim: {patch} — не найдено ни одного чтения CurrentActIndex. " +
                                 "IL игры изменился; в актах 4–5 метод получит индекс вне 0..2.");
        else
            MainFile.Logger.Info($"ActIndexShim: {patch} — заменено чтений CurrentActIndex: {replaced}.");
        return result;
    }
}

/// <summary>Кооп-надбавка ♥ (бросает исключение на индексе &gt; 2).</summary>
[HarmonyPatch(typeof(MultiplayerScalingModel), nameof(MultiplayerScalingModel.GetMultiplayerScaling))]
public static class MultiplayerScalingTierPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref int actIndex) => actIndex = ActIndexShim.Tier(actIndex);
}

/// <summary>Сундук акта (бросает исключение вне 0..2).</summary>
[HarmonyPatch(typeof(TreasureRoom), MethodType.Constructor, typeof(int))]
public static class TreasureRoomTierPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref int actIndex) => actIndex = ActIndexShim.Tier(actIndex);
}

/// <summary>Поза героя в лагере выбирается по индексу акта (0..2, иначе исключение).</summary>
[HarmonyPatch(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter._Ready))]
public static class RestSiteCharacterTierPatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        ActIndexShim.ReplaceActIndex(instructions, "NRestSiteCharacter._Ready");
}

/// <summary>
/// Шанс улучшенной карты в награде растёт с актом: в пяти актах — по ступени, а не по индексу.
/// У RollForUpgrade две перегрузки (без Rng и с Rng) — без типов аргументов Harmony бросит AmbiguousMatchException
/// и сорвёт PatchAll. CurrentActIndex читает только перегрузка с Rng (игра 0.107.1).
/// </summary>
[HarmonyPatch(typeof(CardFactory), "RollForUpgrade", typeof(Player), typeof(CardModel), typeof(decimal), typeof(Rng))]
public static class CardUpgradeTierPatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        ActIndexShim.ReplaceActIndex(instructions, "CardFactory.RollForUpgrade");
}

/// <summary>
/// Редкая элита акта (11 §5): на каждом элитном узле шанс 15 %, не больше одной за акт. Бросок — от сида забега, акта и
/// этажа, а не от общего генератора: одинаков у всех игроков и не сдвигает ванильные случайности.
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEncounter))]
public static class RareElitePatch
{
    public static EncounterModel? RareFor(ActModel act) => act switch
    {
        RequiemActModel r => r.RareElite,
        _ => ActKinds.Of(act) switch
        {
            ActKind.Foothills => ModelDb.Encounter<CauldronMistressElite>(),
            ActKind.Hive => ModelDb.Encounter<SwarmDevourerElite>(),
            ActKind.Glory => ModelDb.Encounter<AbyssBlackKnightElite>(),
            _ => null,
        },
    };

    [HarmonyPostfix]
    public static void Postfix(ActModel __instance, RoomType roomType, ref EncounterModel __result)
    {
        if (roomType != RoomType.Elite || !RequiemConfig.EnableRequiemSystems) return;
        var run = RequiemNet.State;
        if (run == null || RareFor(__instance) is not { } rare) return;
        var actIndex = run.CurrentActIndex;
        var roll = new Rng(run.Rng.Seed, $"requiem_rare_elite_{actIndex}_{run.ActFloor}").NextInt(100);
        if (!ActRules.RollRareElite(roll, ActRunState.RareEliteMet(run, actIndex))) return;
        foreach (var ledger in ActRunState.Records(run)) ledger.RareEliteMask |= 1 << actIndex;
        __result = rare;
    }
}

/// <summary>
/// Раскладка боссов при создании забега: (1) боссы не повторяются между актами (новые акты берут ванильных боссов);
/// (2) Багровый портал — супербосс акта ставится вторым боссом (узел за боссом акта), если супербоссы включены.
/// Комнаты сохраняются в забеге, так что на загрузке это не пересчитывается.
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
public static class ActRoomsPatch
{
    [HarmonyPostfix]
    public static void Postfix(RunManager __instance)
    {
        var state = __instance.DebugOnlyGetState();
        if (state == null || !RequiemConfig.EnableRequiemSystems) return;
        var rng = new Rng(state.Rng.Seed, "requiem_bosses");
        var used = new HashSet<ModelId>();
        foreach (var act in state.Acts)
        {
            if (used.Contains(act.BossEncounter.Id))
            {
                var fresh = act.AllBossEncounters.Where(e => !used.Contains(e.Id) && e.Id != act.SecondBossEncounter?.Id).ToList();
                if (fresh.Count > 0) act.SetBossEncounter(rng.NextItem(fresh)!);
            }
            used.Add(act.BossEncounter.Id);
            if (act.SecondBossEncounter != null) used.Add(act.SecondBossEncounter.Id);
        }

        if (RequiemConfig.Superbosses == SuperbossMode.Off) return;
        foreach (var act in state.Acts)
            if (ActRules.SuperbossFor(ActKinds.Of(act)) is { } boss)
                act.SetSecondBossEncounter(SuperbossEncounter.For(boss));
    }
}

/// <summary>
/// Вопрос Багрового портала (режим «На выбор», одиночная игра, не «Проклятый»): после награды за босса — войти ли к супербоссу.
/// «Нет» — сразу к следующему акту, как без второго босса. В коопе и при обязательном портале — ванильный переход к нему.
/// </summary>
[HarmonyPatch(typeof(NRewardsScreen), "OnProceedButtonPressed")]
public static class PortalPromptPatch
{
    private static readonly AccessTools.FieldRef<NRewardsScreen, bool> IsTerminal = AccessTools.FieldRefAccess<NRewardsScreen, bool>("_isTerminal");
    private static readonly AccessTools.FieldRef<NRewardsScreen, IRunState> RunStateRef = AccessTools.FieldRefAccess<NRewardsScreen, IRunState>("_runState");
    private static readonly AccessTools.FieldRef<NRewardsScreen, NProceedButton> ProceedRef = AccessTools.FieldRefAccess<NRewardsScreen, NProceedButton>("_proceedButton");

    private static bool _asking;

    [HarmonyPrefix]
    public static bool Prefix(NRewardsScreen __instance)
    {
        if (_asking) return false;
        if (!IsTerminal(__instance) || RequiemConfig.Superbosses != SuperbossMode.Optional || !RequiemNet.IsSingleplayer) return true;
        var run = RunStateRef(__instance);
        if (run?.CurrentRoom?.RoomType != RoomType.Boss || run.Map.SecondBossMapPoint == null
            || run.CurrentMapCoord != run.Map.BossMapPoint.coord || run.Act.SecondBossEncounter is not SuperbossEncounter) return true;
        if (ActRules.PortalMandatory(false, RequiemLevel.Of(run))) return true;
        TaskHelper.RunSafely(Ask(__instance, run));
        return false;
    }

    private static async Task Ask(NRewardsScreen screen, IRunState run)
    {
        _asking = true;
        try
        {
            var enter = await RequiemNet.Confirm(ActLedger.LocKey, "portal");
            foreach (var ledger in ActRunState.Records(run))
            {
                ledger.PortalAskedMask |= 1 << run.CurrentActIndex;
                if (enter) ledger.PortalMask |= 1 << run.CurrentActIndex;
            }
            if (enter)
            {
                await RunManager.Instance.ProceedFromTerminalRewardsScreen();
                return;
            }
            ProceedRef(screen).Disable();
            RunManager.Instance.ActChangeSynchronizer.SetLocalPlayerReady();
        }
        finally
        {
            _asking = false;
        }
    }
}

/// <summary>
/// ★ Безымянный предел: в полном забеге после пяти побеждённых супербоссов переход «за последний акт» дописывает к забегу
/// шестой акт и входит в него вместо финала. Комнаты предела создаются здесь же (свой генератор от сида).
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterNextAct))]
public static class NamelessLimitPatch
{
    private static readonly MethodInfo? ActsSetter = AccessTools.PropertySetter(typeof(RunState), nameof(RunState.Acts));

    [HarmonyPrefix]
    public static void Prefix(RunManager __instance)
    {
        var state = __instance.DebugOnlyGetState();
        if (state == null || !RequiemConfig.EnableRequiemSystems || ActsSetter == null) return;
        if (state.CurrentActIndex < state.Acts.Count - 1 || state.CurrentRoom is { IsVictoryRoom: true }) return;
        if (state.Act is NamelessLimit || !ActKinds.IsFullRun(state)) return;
        var records = ActRunState.Records(state).ToList();
        if (records.Count == 0 || records.Any(r => r.NamelessAdded)) return;
        if (!ActRules.NamelessUnlocked(ActRunState.SuperbossMask(state), fullRun: true)) return;

        var nameless = ModelDb.Act<NamelessLimit>().ToMutable();
        nameless.GenerateRooms(new Rng(state.Rng.Seed, "requiem_nameless_limit"), state.UnlockState, state.Players.Count > 1);
        ActsSetter.Invoke(state, [state.Acts.Append(nameless).ToList()]);
        foreach (var ledger in records) ledger.NamelessAdded = true;
        MainFile.Logger.Info("[SlayTheRequiem] Безымянный предел открыт.");
    }
}
