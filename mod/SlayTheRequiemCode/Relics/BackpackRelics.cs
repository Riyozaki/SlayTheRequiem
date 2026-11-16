using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Potions;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Relics;

// Новые реликвии рюкзака (docs/design/07 §7 «Прочее», M4 п. 5). Не снаряжение: слотов нет, лежат в рюкзаке,
// вес Груза — по умолчанию 1 (Мешок-Перышко сам ничего не весит).

/// <summary>Мешок-Перышко: +8 к вместимости Груза.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class FeatherSack : RequiemRelic, IResourceSource, IWeightless
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public int BurdenBonus => BackpackRules.FeatherBurden;
}

/// <summary>Походный котелок: еда даёт +1 к каждому своему действию (<see cref="FoodRules.Boosted"/>).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class CampKettle : RequiemRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
}

/// <summary>
/// Карта Курганов: видно, какие элиты ждут в этом акте — подсказка реликвии перечисляет ближайшие бои элит
/// в порядке, в котором игра их выдаст (<c>RoomSet.eliteEncounters</c>).
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class BarrowMap : RequiemRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    private static readonly AccessTools.FieldRef<ActModel, RoomSet> Rooms = AccessTools.FieldRefAccess<ActModel, RoomSet>("_rooms");

    /// <summary>Названия следующих элит акта (пусто — вне забега или элит нет).</summary>
    public IReadOnlyList<string> UpcomingElites()
    {
        if (!IsMutable || Owner?.RunState?.Act is not { } act) return [];
        var rooms = Rooms(act);
        if (rooms == null || rooms.eliteEncounters.Count == 0) return [];
        var list = rooms.eliteEncounters;
        return BackpackRules.UpcomingIndices(rooms.eliteEncountersVisited, list.Count)
            .Select(i => list[i].Title.GetFormattedText())
            .ToList();
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var elites = UpcomingElites();
            if (elites.Count == 0) return [];
            return [new HoverTip(UI.RequiemUi.Tip("BARROW_MAP.title"), string.Join("\n", elites.Select((e, i) => $"{i + 1}. {e}")))];
        }
    }
}

/// <summary>
/// Факел (13 §3, M6 п. 1): Ночное зрение — Тьма курганов отступает до 4 рядов, Засады и Тайные тропы видны.
/// Действует на весь отряд в коопе (<see cref="Acts.MapGraph.NightSight"/>).
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class Torch : RequiemRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;
}

/// <summary>Точильный камень: Оружейные атаки +1 урона на первом ходу боя.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class SharpeningStone : RequiemRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target != null && cardSource != null && dealer == Owner.Creature && props.IsPoweredAttack()
        && Owner.PlayerCombatState?.TurnNumber == 1 && EquipmentCombatRules.IsWeaponAttack(cardSource)
            ? BackpackRules.SharpeningDamage
            : 0m;
}

/// <summary>Бутыль Хмельного: в начале каждого боя — бесплатное зелье «Похмелье?» (если есть свободный слот).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class BottleOfMead : RequiemRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || combatState.RoundNumber != 1) return;
        if (Owner.PotionSlots.Any(p => p is HangoverPotion) || !Owner.HasOpenPotionSlots) return;
        Flash();
        await PotionCmd.TryToProcure<HangoverPotion>(Owner);
    }

    /// <summary>Неиспользованное «Похмелье?» выдыхается после боя — бутыль наливает новое.</summary>
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        foreach (var potion in Owner.PotionSlots.OfType<HangoverPotion>().ToList())
            await PotionCmd.Discard(potion);
    }
}
