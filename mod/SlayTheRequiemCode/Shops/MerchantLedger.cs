using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Potions;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rollback;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

/// <summary>
/// Книга торговца (docs/design/14) — служебная стартовая реликвия: вид текущей лавки, доверие по видам лавок
/// (сколько золота потрачено), купленные Трактаты; цены Реквиема и скидка доверия (хук ModifyMerchantPrice),
/// рост доверия (AfterItemPurchased), золото за бои ×1.3 (ModifyGoldGained).
/// Счётчик на иконке — доверие лавки, в которой вы сейчас стоите.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class MerchantLedger : RequiemRelic
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => false;
    public override bool ShowCounter => true;
    public override int DisplayAmount => TrustOf(CurrentShop);

    public static MerchantLedger? Of(Player? player) => player?.Relics.OfType<MerchantLedger>().FirstOrDefault();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Shop", 0),
        new DynamicVar("Trust", 0),
        new DynamicVar("Discount", 0),
    ];

    // ───── сохраняемое состояние ─────

    private int[] _spent = new int[ShopRules.ShopTypeCount];
    private int _currentShop;
    private int _currentFloor = -1;
    private int _shopAct = -1;
    private int _shopsThisAct;
    private int _previousShop = -1;
    private int _treatisesBought;
    private int _orderShop;      // вид лавки заказа + 1; 0 — заказа нет
    private int _orderCategory;
    private int _orderFloor;     // этаж, где заказ выполняется, + 1; 0 — нигде
    private int _refillsUsed;
    private int _theftFloor;     // этаж последней попытки кражи + 1
    private int _theftTries;     // попыток на этом этаже (Кольцо Безликого Вора даёт вторую)
    private int _caravanAct;     // акт, где уже был Караван, + 1 (14 §2.8: не больше одного за акт)
    private int _tradeFloor;     // этаж, где уже был Обмен Каравана, + 1 (раз за визит)

    /// <summary>Золото, потраченное в лавках каждого вида (индекс = <see cref="ShopType"/>).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] Spent
    {
        get => _spent;
        set
        {
            AssertMutable();
            _spent = new int[ShopRules.ShopTypeCount];
            if (value != null) Array.Copy(value, _spent, Math.Min(value.Length, _spent.Length));
            SyncVars();
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CurrentShopIndex { get => _currentShop; set { AssertMutable(); _currentShop = value; SyncVars(); } }

    /// <summary>Этаж (TotalFloor), для которого уже выбран вид лавки; −1 — ни для какого.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CurrentFloor { get => _currentFloor; set { AssertMutable(); _currentFloor = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ShopAct { get => _shopAct; set { AssertMutable(); _shopAct = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ShopsThisAct { get => _shopsThisAct; set { AssertMutable(); _shopsThisAct = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PreviousShop { get => _previousShop; set { AssertMutable(); _previousShop = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int TreatisesBought { get => _treatisesBought; set { AssertMutable(); _treatisesBought = value; } }

    /// <summary>Заказ (14 §1): вид лавки + 1 (0 — нет заказа).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int OrderShop { get => _orderShop; set { AssertMutable(); _orderShop = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int OrderCategoryIndex { get => _orderCategory; set { AssertMutable(); _orderCategory = value; } }

    /// <summary>Этаж выполнения заказа + 1 — чтобы перезагрузка в той же лавке не теряла заказанный товар.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int OrderFloor { get => _orderFloor; set { AssertMutable(); _orderFloor = value; } }

    /// <summary>
    /// Пополнений витрины за этот визит. Не сохраняется: хук пополнения срабатывает только на машине покупателя,
    /// и сохранённое значение расходилось бы между игроками коопа.
    /// </summary>
    public int RefillsUsed => _refillsUsed;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int TheftFloor { get => _theftFloor; set { AssertMutable(); _theftFloor = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int TheftTries { get => _theftTries; set { AssertMutable(); _theftTries = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CaravanAct { get => _caravanAct; set { AssertMutable(); _caravanAct = value; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int TradeFloor { get => _tradeFloor; set { AssertMutable(); _tradeFloor = value; } }

    public bool TradedHere(IRunState runState) => _tradeFloor == runState.TotalFloor + 1;

    public void MarkTrade(IRunState runState)
    {
        AssertMutable();
        _tradeFloor = runState.TotalFloor + 1;
    }

    public ShopType CurrentShop => (ShopType)Math.Clamp(_currentShop, 0, ShopRules.ShopTypeCount - 1);
    public int TrustOf(ShopType shop) => ShopRules.Trust(_spent[(int)shop]);

    /// <summary>Лавки Реквиема включены для этого героя.</summary>
    public static bool Active(Player? player) => RequiemConfig.ShopVariety && RequiemMode.IsInSystem(player);

    // ───── вход в лавку ─────

    /// <summary>
    /// Выбрать вид лавки для текущего узла (до генерации витрины). Повторный вызов на том же этаже ничего не меняет.
    /// Вид зависит только от зерна забега, этажа и счётчиков книги — поэтому одинаков у всех игроков коопа.
    /// </summary>
    /// <summary>Отладка (<c>str_shop</c>): вид следующей лавки вместо броска. Сбрасывается при входе.</summary>
    internal static ShopType? DebugNextShop { get; set; }

    public ShopType EnterShop(IRunState runState)
    {
        AssertMutable();
        var floor = runState.TotalFloor;
        if (_currentFloor == floor && DebugNextShop == null) return CurrentShop;
        if (_shopAct != runState.CurrentActIndex)
        {
            _shopAct = runState.CurrentActIndex;
            _shopsThisAct = 0;
            _previousShop = -1;
        }
        var hash = ShopRules.Fnv1a(runState.Rng.StringSeed ?? string.Empty, floor * 31 + runState.CurrentActIndex);
        ShopType? previous = _previousShop >= 0 ? (ShopType)_previousShop : null;
        var kind = Acts.ActKinds.Current(runState);
        var caravanSeen = _caravanAct == runState.CurrentActIndex + 1;
        var shop = DebugNextShop ?? ShopRules.RollShopType(hash, kind, Acts.ActKinds.Tier(runState), _shopsThisAct, previous,
            outlaw: IsOutlaw(Owner), caravanSeen: caravanSeen);
        if (shop == ShopType.Caravan) _caravanAct = runState.CurrentActIndex + 1;
        DebugNextShop = null;
        _currentFloor = floor;
        _refillsUsed = 0;
        // Заказ выполняется в первой следующей лавке того же вида.
        if (_orderShop == (int)shop + 1)
        {
            _orderShop = 0;
            _orderFloor = floor + 1;
        }
        _shopsThisAct++;
        _previousShop = (int)shop;
        CurrentShopIndex = (int)shop;
        return shop;
    }

    /// <summary>
    /// «Вне закона» (14 §2.7): Запятнанный/Отступник или под Знамением Ловкача — Скупщик краденого встречается в любом акте.
    /// </summary>
    public static bool IsOutlaw(Player player) =>
        Pantheon.Faith.Effective(player) != Piety.Righteous || player.Relics.OfType<Pantheon.OmenOfTheTrickster>().Any();

    /// <summary>Бог Храмовой лавки на этом этаже (по зерну и этажу — одинаков у всех игроков).</summary>
    public static Celestial TempleGod(IRunState runState) =>
        (Celestial)(int)(ShopRules.Fnv1a($"{runState.Rng.StringSeed}|temple", runState.TotalFloor) % (uint)Pantheon.CelestialBlessing.Blessings.Length);

    // ───── доверие, Заказ, кража ─────

    public int CurrentTrust => TrustOf(CurrentShop);

    public void AddSpent(ShopType shop, int gold)
    {
        if (gold <= 0) return;
        AssertMutable();
        _spent[(int)shop] += gold;
        SyncVars();
    }

    /// <summary>Провал кражи: доверие этого вида лавок → 0.</summary>
    public void ResetTrust(ShopType shop)
    {
        AssertMutable();
        _spent[(int)shop] = 0;
        SyncVars();
    }

    /// <summary>«Отмыть имя» у Скупщика (14 §2.7): доверие лавки — не ниже 1.</summary>
    public void ClearName(ShopType shop)
    {
        AssertMutable();
        _spent[(int)shop] = Math.Max(_spent[(int)shop], ShopRules.ClearNameSpent);
        SyncVars();
    }

    public bool HasOrder => _orderShop > 0;
    public ShopType? OrderedShop => _orderShop > 0 ? (ShopType)(_orderShop - 1) : null;
    public OrderCategory OrderedCategory => (OrderCategory)_orderCategory;

    public void PlaceOrder(ShopType shop, OrderCategory category)
    {
        AssertMutable();
        _orderShop = (int)shop + 1;
        _orderCategory = (int)category;
    }

    /// <summary>Заказ, который выполняется на витрине текущего этажа (null — нет).</summary>
    public OrderCategory? ActiveOrder(IRunState runState) =>
        _orderFloor > 0 && _orderFloor - 1 == runState.TotalFloor ? (OrderCategory)_orderCategory : null;

    /// <summary>Попытки кражи на этом этаже исчерпаны (<paramref name="allowed"/> — сколько можно за визит).</summary>
    public bool TheftTriedHere(IRunState runState, int allowed = 1) =>
        _theftFloor == runState.TotalFloor + 1 && Math.Max(1, _theftTries) >= Math.Max(1, allowed);

    public void MarkTheftAttempt(IRunState runState)
    {
        AssertMutable();
        var floor = runState.TotalFloor + 1;
        _theftTries = _theftFloor == floor ? Math.Max(1, _theftTries) + 1 : 1;
        _theftFloor = floor;
    }

    // ───── цены ─────

    /// <summary>Цена Реквиема для товара (null — оставить ванильную).</summary>
    private int? RequiemPrice(MerchantEntry entry) => entry switch
    {
        MerchantCardEntry { CreationResult.Card: SpellCard spell } => ShopRules.SpellPrice(spell.Tier, single: true),
        MerchantRelicEntry { Model: TreatiseRelic } => ShopRules.TreatisePrice(_treatisesBought),
        MerchantPotionEntry { Model: RequiemPotion potion } =>
            ShopRules.BrewPrice(potion.Tier, AlchemyRules.IsPoison(potion.Kind)),
        MerchantPotionEntry { Model: RequiemScroll scroll } => ShopRules.ScrollPrice(scroll.Tier),
        MerchantRelicEntry { Model: Pantheon.CelestialAmulet } when CurrentShop == ShopType.Temple => ShopRules.TempleAmuletPrice,
        MerchantRelicEntry { Model: Pantheon.CelestialRobe } when CurrentShop == ShopType.Temple => ShopRules.TempleRobePrice,
        _ => null,
    };

    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal cost)
    {
        if (player != Owner || !Active(Owner)) return cost;
        var price = RequiemPrice(entry) ?? (int)Math.Round(cost);
        return ShopRules.ApplyTrust(price, TrustOf(CurrentShop));
    }

    /// <summary>
    /// Покупка у ванильного прилавка выполняется только на машине покупателя (остальным приходят лишь «получил
    /// предмет / потерял золото»), поэтому рост доверия рассылается отдельным действием.
    /// </summary>
    public override async Task AfterItemPurchased(Player player, MerchantEntry itemPurchased, int goldSpent)
    {
        if (player != Owner || !Active(Owner) || goldSpent <= 0 || !LocalContext.IsMe(Owner)) return;
        await ShopActions.Perform(Owner, ShopAction.TrustSpent, (int)CurrentShop, goldSpent);
    }

    /// <summary>
    /// «+1 товар на витрине за уровень доверия» (14 §1): слоты сцены фиксированы, поэтому купленный товар
    /// замещается новым — до «доверие» раз за визит. Удаление карты не пополняется.
    /// </summary>
    public override bool ShouldRefillMerchantEntry(MerchantEntry entry, Player player)
    {
        if (player != Owner || !Active(Owner) || entry is MerchantCardRemovalEntry) return false;
        if (_refillsUsed >= ShopRules.RefillsPerVisit(CurrentTrust)) return false;
        AssertMutable();
        _refillsUsed++;
        return true;
    }

    /// <summary>
    /// Удаление «Предсмертного эха» у торговца стоит вдвое (15 §3): после оплаты удаления списывается ещё одна его цена
    /// (сколько есть). Удаление выполняется у всех игроков коопа, значит и доплата — у всех одинаково.
    /// </summary>
    public override async Task BeforeCardRemoved(CardModel card)
    {
        if (card is not DeathEcho || card.Owner != Owner || Owner.RunState.CurrentRoom is not MerchantRoom) return;
        var price = Active(Owner)
            ? ShopRules.ApplyTrust(ShopRules.RemovalPrice(Owner.ExtraFields.CardShopRemovalsUsed), CurrentTrust)
            : ShopRules.RemovalPrice(Owner.ExtraFields.CardShopRemovalsUsed);
        var surcharge = ShopRules.DeathEchoSurcharge(price, Owner.Gold);
        if (surcharge > 0) await PlayerCmd.LoseGold(surcharge, Owner, GoldLossType.Spent);
    }

    /// <summary>
    /// Трактат куплен. Ванильная запись реликвии обнуляет Model ещё до AfterItemPurchased, поэтому о покупке
    /// сообщает сам <see cref="TreatiseRelic"/> при получении.
    /// </summary>
    public void NoteTreatiseBought()
    {
        AssertMutable();
        _treatisesBought++;
    }

    // ───── золото ─────

    public override decimal ModifyGoldGained(Player player, decimal amount)
    {
        if (player != Owner || !RequiemMode.IsInSystem(Owner) || amount <= 0) return amount;
        if (Owner.RunState.CurrentRoom is not CombatRoom) return amount;
        return ShopRules.CombatGold((int)amount);
    }

    private void SyncVars()
    {
        if (DynamicVars.TryGetValue("Shop", out var s)) s.BaseValue = (int)CurrentShop;
        var trust = TrustOf(CurrentShop);
        if (DynamicVars.TryGetValue("Trust", out var t)) t.BaseValue = trust;
        if (DynamicVars.TryGetValue("Discount", out var d)) d.BaseValue = 5 * trust;
        InvokeDisplayAmountChanged();
    }
}
