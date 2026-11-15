using SlayTheRequiem.SlayTheRequiemCode.Core;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Patches;

/// <summary>Герою в системе Реквиема к стартовым реликвиям добавляются Гримуар (владение школами), Сумка травника, Походный мешок,
/// Книга торговца, Свечи Реквиема и Знак веры.</summary>
[HarmonyPatch(typeof(Player), "PopulateStartingRelics")]
public static class StartingRelicsPatch
{
    [HarmonyPostfix]
    public static void AddGrimoire(Player __instance)
    {
        if (!RequiemMode.IsInSystem(__instance)) return;
        // Сумка травника (реагенты) и Походный мешок (Груз, Сундук лагеря) — служебные, у всех героев системы.
        if (!__instance.Relics.OfType<Alchemy.HerbalistPouch>().Any())
        {
            var pouch = ModelDb.Relic<Alchemy.HerbalistPouch>().ToMutable();
            pouch.FloorAddedToDeck = 1;
            __instance.AddRelicInternal(pouch, silent: true);
        }
        if (!__instance.Relics.OfType<Alchemy.TravelPack>().Any())
        {
            var pack = ModelDb.Relic<Alchemy.TravelPack>().ToMutable();
            pack.FloorAddedToDeck = 1;
            __instance.AddRelicInternal(pack, silent: true);
        }
        // Книга торговца (лавки, доверие — docs/design/14) и Свечи Реквиема (Камень Памяти — docs/design/15).
        if (!__instance.Relics.OfType<Shops.MerchantLedger>().Any())
        {
            var ledger = ModelDb.Relic<Shops.MerchantLedger>().ToMutable();
            ledger.FloorAddedToDeck = 1;
            __instance.AddRelicInternal(ledger, silent: true);
        }
        if (!__instance.Relics.OfType<Rollback.MemoryStoneRelic>().Any())
        {
            var stone = ModelDb.Relic<Rollback.MemoryStoneRelic>().ToMutable();
            stone.FloorAddedToDeck = 1;
            __instance.AddRelicInternal(stone, silent: true);
        }
        // Знак веры — праведность и Святилища (docs/design/05 §2). «Реквием 10» начинает Запятнанным (docs/design/11 §7).
        if (!__instance.Relics.OfType<Pantheon.FaithSigil>().Any())
        {
            var sigil = (Pantheon.FaithSigil)ModelDb.Relic<Pantheon.FaithSigil>().ToMutable();
            sigil.FloorAddedToDeck = 1;
            if (RequiemLevelRules.StartsTainted(Acts.RequiemLevel.Of(__instance.RunState)))
                sigil.RecordedPiety = (int)Piety.Tainted;
            __instance.AddRelicInternal(sigil, silent: true);
        }
        // Дорожная карта — состояние актов: супербоссы, Врата Бездны, ключи, Стужа/Перегрев (docs/design/13).
        if (!__instance.Relics.OfType<Acts.ActLedger>().Any())
        {
            var actLedger = ModelDb.Relic<Acts.ActLedger>().ToMutable();
            actLedger.FloorAddedToDeck = 1;
            __instance.AddRelicInternal(actLedger, silent: true);
        }
        // Летопись героя — Слава, уровни, навыки, Сытость, Камни душ, очки лагеря (docs/design/02 §5, 04, 10).
        if (!__instance.Relics.OfType<Progression.HeroRecord>().Any())
        {
            var record = ModelDb.Relic<Progression.HeroRecord>().ToMutable();
            record.FloorAddedToDeck = 1;
            __instance.AddRelicInternal(record, silent: true);
        }
        if (__instance.Relics.OfType<Grimoire>().Any()) return;
        var grimoire = (Grimoire)ModelDb.Relic<Grimoire>().ToMutable();
        grimoire.InitFor(__instance.Character.GetType().Name);
        grimoire.FloorAddedToDeck = 1;
        __instance.AddRelicInternal(grimoire, silent: true);
        // Происхождение выдаёт сам Гримуар при входе в первую комнату: здесь ГСЧ игрока ещё не засеян,
        // а AddRelicInternal не вызывает AfterObtained (правка ♥).
    }
}

/// <summary>Счётчик звёзд = счётчик ✦: показывать у всех героев в системе, а не только у Регента.</summary>
[HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.ShouldAlwaysShowStarCounter), MethodType.Getter)]
public static class StarCounterPatch
{
    [HarmonyPostfix]
    public static void ShowForEveryone(CharacterModel __instance, ref bool __result)
    {
        if (!__result && RequiemMode.IsInSystem(__instance)) __result = true;
    }
}

/// <summary>
/// Лечение Реквиема (docs/design/03, 06): Хворь запрещает его целиком; формы и Дух божества меняют проценты
/// (вампир −50 %/голод, оборотень, Заводное сердце −50 %, Матерь Очага-Дух +50 %), Лич и голодный вампир лечатся только
/// кормлением (см. <see cref="Transformations.Healing"/>).
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Commands.CreatureCmd), nameof(MegaCrit.Sts2.Core.Commands.CreatureCmd.Heal))]
public static class BlightHealPatch
{
    [HarmonyPrefix]
    public static bool BlockHealing(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature, ref decimal amount, ref Task __result)
    {
        if (!creature.HasPower<Powers.BlightPower>())
        {
            amount = Transformations.Healing.Modify(creature, amount);
            if (amount > 0) return true;
        }
        __result = Task.CompletedTask;
        return false;
    }
}
