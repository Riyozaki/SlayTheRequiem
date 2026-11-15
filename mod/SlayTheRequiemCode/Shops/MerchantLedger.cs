using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Potions;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
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

    public ShopType CurrentShop => (ShopType)Math.Clamp(_currentShop, 0, ShopRules.ShopTypeCount - 1);
    public int TrustOf(ShopType shop) => ShopRules.Trust(_spent[(int)shop]);

    /// <summary>Лавки Реквиема включены для этого героя.</summary>
    public static bool Active(Player? player) => RequiemConfig.ShopVariety && RequiemMode.IsInSystem(player);

    // ───── вход в лавку ─────

    /// <summary>
    /// Выбрать вид лавки для текущего узла (до генерации витрины). Повторный вызов на том же этаже ничего не меняет.
    /// Вид зависит только от зерна забега, этажа и счётчиков книги — поэтому одинаков у всех игроков коопа.
    /// </summary>
    public ShopType EnterShop(IRunState runState)
    {
        AssertMutable();
        var floor = runState.TotalFloor;
        if (_currentFloor == floor) return CurrentShop;
        if (_shopAct != runState.CurrentActIndex)
        {
            _shopAct = runState.CurrentActIndex;
            _shopsThisAct = 0;
            _previousShop = -1;
        }
        var hash = ShopRules.Fnv1a(runState.Rng.StringSeed ?? string.Empty, floor * 31 + runState.CurrentActIndex);
        ShopType? previous = _previousShop >= 0 ? (ShopType)_previousShop : null;
        var shop = ShopRules.RollShopType(hash, runState.CurrentActIndex, _shopsThisAct, previous);
        _currentFloor = floor;
        _shopsThisAct++;
        _previousShop = (int)shop;
        CurrentShopIndex = (int)shop;
        return shop;
    }

    // ───── цены ─────

    /// <summary>Цена Реквиема для товара (null — оставить ванильную).</summary>
    private int? RequiemPrice(MerchantEntry entry) => entry switch
    {
        MerchantCardEntry { CreationResult.Card: SpellCard spell } => ShopRules.SpellPrice(spell.Tier, single: true),
        MerchantRelicEntry { Model: TreatiseRelic } => ShopRules.TreatisePrice(_treatisesBought),
        MerchantPotionEntry { Model: RequiemPotion potion } =>
            ShopRules.BrewPrice(potion.Tier, AlchemyRules.IsPoison(potion.Kind)),
        _ => null,
    };

    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal cost)
    {
        if (player != Owner || !Active(Owner)) return cost;
        var price = RequiemPrice(entry) ?? (int)Math.Round(cost);
        return ShopRules.ApplyTrust(price, TrustOf(CurrentShop));
    }

    public override Task AfterItemPurchased(Player player, MerchantEntry itemPurchased, int goldSpent)
    {
        if (player != Owner || !Active(Owner)) return Task.CompletedTask;
        AssertMutable();
        if (goldSpent > 0)
        {
            _spent[(int)CurrentShop] += goldSpent;
            SyncVars();
        }
        return Task.CompletedTask;
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

    // ───── продажа ─────

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not MerchantRoom || !Active(Owner)) return;
        await SellService.OfferBackpackSale(Owner, this);
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
