using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Alchemy;

/// <summary>
/// Походный мешок (docs/design/02 §3, 10) — служебная стартовая реликвия Груза:
/// счётчик = текущий вес (в описании — вес/вместимость/перегруз); при перегрузе &gt; +10 новые карты не берутся;
/// «Пёрышко» добавляет вместимость до следующего лагеря; хранит содержимое Сундука лагеря (карты и снаряжение).
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class TravelPack : RequiemRelic, IResourceSource
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => false;
    public override bool ShowCounter => true;
    public override int DisplayAmount => Loadout.TotalWeight(Owner);

    public static TravelPack? Of(Player? player) => player?.Relics.OfType<TravelPack>().FirstOrDefault();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Weight", 0), new DynamicVar("Capacity", 0), new DynamicVar("Excess", 0), new DynamicVar("Stored", 0)];

    // ───── «Пёрышко» ─────

    private int _featherBonus;

    /// <summary>Вместимость от зелья «Пёрышко» — до следующего лагеря.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FeatherBonus { get => _featherBonus; set { AssertMutable(); _featherBonus = value; } }

    public int BurdenBonus => FeatherBonus;

    // ───── Сундук ─────

    private List<SerializableCard> _storedCards = new();
    private string _storedItems = "";

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public List<SerializableCard> StoredCards { get => _storedCards; set { AssertMutable(); _storedCards = value ?? new(); } }

    /// <summary>Снаряжение в сундуке — имена классов через запятую (у предметов ступень задана классом).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string StoredItems { get => _storedItems; set { AssertMutable(); _storedItems = value ?? ""; } }

    public IReadOnlyList<string> StoredItemNames => _storedItems.Split(',', StringSplitOptions.RemoveEmptyEntries);
    public int StoredCount => _storedCards.Count + StoredItemNames.Count;

    /// <summary>Убрать карты из колоды в сундук.</summary>
    public async Task StoreCards(IReadOnlyList<CardModel> cards)
    {
        AssertMutable();
        if (cards.Count == 0) return;
        foreach (var card in cards)
        {
            _storedCards.Add(card.ToSerializable());
            await CardPileCmd.RemoveFromDeck(card);
        }
        Refresh();
    }

    /// <summary>Достать карты из сундука в колоду (индексы в <see cref="StoredCards"/>).</summary>
    public async Task TakeCards(IReadOnlyList<int> indices)
    {
        AssertMutable();
        foreach (var i in indices.Distinct().OrderByDescending(i => i))
        {
            if (i < 0 || i >= _storedCards.Count) continue;
            var card = CardModel.FromSerializable(_storedCards[i]);
            _storedCards.RemoveAt(i);
            Owner.RunState.AddCard(card, Owner);
            await CardPileCmd.Add(card, PileType.Deck);
        }
        Refresh();
    }

    /// <summary>Мутабельные копии карт сундука — для экрана выбора (порядок = индексы).</summary>
    public List<CardModel> PreviewStoredCards() => _storedCards.Select(CardModel.FromSerializable).ToList();

    private static Type? EquipmentType(string name) =>
        typeof(EquipmentRelic).Assembly.GetTypes().FirstOrDefault(t => t.Name == name && t.IsSubclassOf(typeof(EquipmentRelic)) && !t.IsAbstract);

    /// <summary>Канонические модели вещей сундука — для экрана выбора.</summary>
    public List<RelicModel> StoredItemRelics() =>
        StoredItemNames.Select(EquipmentType).Where(t => t != null)
            .Select(t => ModelDb.GetByIdOrNull<RelicModel>(ModelDb.GetId(t!))).OfType<RelicModel>().ToList();

    /// <summary>Снятую вещь из рюкзака — в сундук (перестаёт весить). Надетое не кладётся.</summary>
    public async Task StoreItem(EquipmentRelic item)
    {
        AssertMutable();
        if (!item.IsStowed || item.Owner != Owner) return;
        await RelicCmd.Remove(item);
        _storedItems = _storedItems.Length == 0 ? item.GetType().Name : _storedItems + "," + item.GetType().Name;
        Refresh();
    }

    /// <summary>Вещь из сундука — в рюкзак (снятой: надетое не меняется).</summary>
    public async Task TakeItem(string name)
    {
        AssertMutable();
        var names = StoredItemNames.ToList();
        if (!names.Remove(name) || EquipmentType(name) is not { } type) return;
        _storedItems = string.Join(",", names);
        var relic = (EquipmentRelic)ModelDb.GetById<RelicModel>(ModelDb.GetId(type)).ToMutable();
        // AfterObtained надевает новую вещь и снимает мешающие — вернуть как было.
        var wasEquipped = Loadout.Equipped(Owner).ToList();
        await RelicCmd.Obtain(relic, Owner);
        relic.IsStowed = true;
        foreach (var r in wasEquipped) r.IsStowed = false;
        Refresh();
    }

    // ───── лагерь ─────

    private bool _freeActionPending;

    /// <summary>Отметить, что выбранное действие лагеря — свободное (Варка, Сундук).</summary>
    public void MarkFreeAction() => _freeActionPending = true;

    public override bool ShouldDisableRemainingRestSiteOptions(Player player)
    {
        if (player != Owner) return true;
        var free = _freeActionPending;
        _freeActionPending = false;
        return !free;
    }

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner || !RequiemMode.IsInSystem(Owner)) return false;
        options.Add(new BrewRestSiteOption(player));
        options.Add(new ChestRestSiteOption(player));
        return true;
    }

    // ───── Груз ─────

    /// <summary>Перегруз &gt; +10: новые карты не подбираются, пока не выбросите лишнее (docs/design/02 §3).</summary>
    public override bool ShouldAddToDeck(CardModel card)
    {
        if (card.Owner != Owner || !RequiemMode.IsInSystem(Owner)) return true;
        if (card.Type is CardType.Curse or CardType.Status) return true; // проклятия навязываются
        return Loadout.Overload(Owner).CanPickUp;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        // «Пёрышко» действует до следующего лагеря.
        if (room.RoomType == RoomType.RestSite && FeatherBonus > 0) FeatherBonus = 0;
        Refresh();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        Refresh();
        return Task.CompletedTask;
    }

    public void Refresh()
    {
        if (Owner == null) return;
        var weight = Loadout.TotalWeight(Owner);
        var capacity = Loadout.Capacity(Owner);
        Set("Weight", weight);
        Set("Capacity", capacity);
        Set("Excess", Math.Max(0, weight - capacity));
        Set("Stored", StoredCount);
        InvokeDisplayAmountChanged();
    }

    private void Set(string name, int value)
    {
        if (DynamicVars.TryGetValue(name, out var v)) v.BaseValue = value;
    }
}
