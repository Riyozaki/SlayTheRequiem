using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Unlocks;
using SlayTheRequiem.SlayTheRequiemCode.Config;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>
/// Некробиндер и Дефект — облики превращений, а не герои мода (docs/design/06 §4, ревью R-23). При включённых системах
/// мода (и выключенной настройке <see cref="RequiemConfig.ShowVanillaOnlyHeroes"/>) они скрыты на экране выбора,
/// не выпадают «случайным героем», а их карты не попадают в награды и случайную генерацию (Калейдоскоп, Призматический
/// самоцвет, Разноцветные философы, Брызги, Морское стекло Оробаса). Их пулы реликвий доступны только им самим —
/// без героя они не выпадают и отдельного фильтра не требуют.
/// </summary>
public static class HiddenHeroes
{
    public static bool Active => RequiemConfig.EnableRequiemSystems && !RequiemConfig.ShowVanillaOnlyHeroes;

    public static bool IsHidden(CharacterModel? character) => Active && character is Necrobinder or Defect;

    public static bool IsHiddenPool(CardPoolModel? pool) => Active && pool is NecrobinderCardPool or DefectCardPool;

    /// <summary>Герои мода — замена скрытому при выборе «случайного героя» (детерминированно у всех игроков коопа).</summary>
    public static CharacterModel Replacement(CharacterModel hidden)
    {
        var heroes = new CharacterModel[] { ModelDb.Character<Ironclad>(), ModelDb.Character<Silent>(), ModelDb.Character<Regent>() };
        var index = ModelDb.AllCharacters.ToList().IndexOf(hidden);
        return heroes[Math.Max(0, index) % heroes.Length];
    }

    /// <summary>Пока true — фильтр пулов отключён (проверка Калейдоскопа «открыты все герои»).</summary>
    [ThreadStatic] private static int _bypass;

    public static bool Bypassed => _bypass > 0;

    [HarmonyPatch(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Init))]
    public static class HideButton
    {
        [HarmonyPostfix]
        public static void Postfix(NCharacterSelectButton __instance, CharacterModel character)
        {
            if (IsHidden(character)) __instance.Visible = false;
        }
    }

    [HarmonyPatch(typeof(StartRunLobby), "ChangeCharacter")]
    public static class NoRandomHidden
    {
        [HarmonyPrefix]
        public static void Prefix(ref CharacterModel character, bool isRandomCharacterResolution)
        {
            if (isRandomCharacterResolution && IsHidden(character)) character = Replacement(character);
        }
    }

    [HarmonyPatch(typeof(UnlockState), nameof(UnlockState.CharacterCardPools), MethodType.Getter)]
    public static class FilterCardPools
    {
        [HarmonyPostfix]
        public static void Postfix(ref IEnumerable<CardPoolModel> __result)
        {
            if (Active && !Bypassed) __result = __result.Where(p => !IsHiddenPool(p)).ToList();
        }
    }

    [HarmonyPatch(typeof(Kaleidoscope), nameof(Kaleidoscope.IsAllowedAtNeow))]
    public static class KaleidoscopeAllHeroes
    {
        [HarmonyPrefix]
        public static void Prefix() => _bypass++;

        [HarmonyFinalizer]
        public static void Finalizer() => _bypass = Math.Max(0, _bypass - 1);
    }

    [HarmonyPatch(typeof(Orobas), "SeaGlassOptions", MethodType.Getter)]
    public static class FilterSeaGlass
    {
        [HarmonyPostfix]
        public static void Postfix(ref IEnumerable<EventOption> __result)
        {
            if (!Active) return;
            __result = __result.Where(o => o.Relic is not SeaGlass glass || glass.CharacterId == null
                                           || !IsHidden(ModelDb.GetById<CharacterModel>(glass.CharacterId))).ToList();
        }
    }
}

/// <summary>
/// Ironclad в моде — Латоносец / Platebearer (глоссарий 12, ревью R-11). Ванильные строки не перезаписываются:
/// при включённых системах мода имя, имя в косвенном падеже и текст экрана выбора берутся из ключей мода
/// в characters.json (SLAYTHEREQUIEM-PLATEBEARER.*), выключили системы — снова «Железный».
/// </summary>
public static class PlatebearerNames
{
    public const string Key = "SLAYTHEREQUIEM-PLATEBEARER";

    private static bool Applies(CharacterModel model) => RequiemConfig.EnableRequiemSystems && model is Ironclad;

    [HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.Title), MethodType.Getter)]
    public static class Title
    {
        [HarmonyPostfix]
        public static void Postfix(CharacterModel __instance, ref LocString __result)
        {
            if (Applies(__instance)) __result = new LocString(CharacterModel.locTable, $"{Key}.title");
        }
    }

    [HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.TitleObject), MethodType.Getter)]
    public static class TitleObject
    {
        [HarmonyPostfix]
        public static void Postfix(CharacterModel __instance, ref LocString __result)
        {
            if (Applies(__instance)) __result = new LocString(CharacterModel.locTable, $"{Key}.titleObject");
        }
    }

    [HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.CharacterSelectTitle), MethodType.Getter)]
    public static class SelectTitle
    {
        [HarmonyPostfix]
        public static void Postfix(CharacterModel __instance, ref string __result)
        {
            if (Applies(__instance)) __result = $"{Key}.title";
        }
    }

    [HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.CharacterSelectDesc), MethodType.Getter)]
    public static class SelectDesc
    {
        [HarmonyPostfix]
        public static void Postfix(CharacterModel __instance, ref string __result)
        {
            if (Applies(__instance)) __result = $"{Key}.description";
        }
    }
}
