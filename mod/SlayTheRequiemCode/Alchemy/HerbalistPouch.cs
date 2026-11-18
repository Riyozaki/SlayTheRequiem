using System.Globalization;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Alchemy;

/// <summary>
/// Сумка травника (docs/design/09 §2) — служебная стартовая реликвия: хранит реагенты (до 12, вес 0),
/// после победы добавляет реагенты с побеждённых врагов (по фракции и типу атаки), списывает реагенты за
/// сваренное в лагере зелье. Счётчик — сколько реагентов в сумке; в описании — разбивка по видам.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class HerbalistPouch : RequiemRelic
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => false;
    public override bool ShowCounter => true;
    public override int DisplayAmount => Total;

    private int[] _reagents = new int[AlchemyRules.ReagentCount];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _reagents = (int[])_reagents.Clone();
        _pendingBrews = new();
        BrewTaken = false;
    }

    /// <summary>Сохраняется массивом: индекс = <see cref="Reagent"/>.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] Reagents
    {
        get => _reagents;
        set
        {
            AssertMutable();
            _reagents = new int[AlchemyRules.ReagentCount];
            if (value != null) Array.Copy(value, _reagents, Math.Min(value.Length, _reagents.Length));
            SyncVars();
        }
    }

    public int Total => _reagents.Sum();
    public int Count(Reagent reagent) => _reagents[(int)reagent];

    public static HerbalistPouch? Of(Player? player) => player?.Relics.OfType<HerbalistPouch>().FirstOrDefault();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        Enum.GetValues<Reagent>().Select(r => new DynamicVar(r.ToString(), 0)).ToList();

    /// <summary>Добавить реагенты (лишнее сверх 12 пропадает). Возвращает, сколько поместилось.</summary>
    public int Add(Reagent reagent, int amount)
    {
        AssertMutable();
        var fit = AlchemyRules.Fit(Total, amount);
        if (fit <= 0) return 0;
        _reagents[(int)reagent] += fit;
        SyncVars();
        return fit;
    }

    /// <summary>One kit mutation before any display callback; a notification cannot leave half a purchased kit.</summary>
    internal bool TryAddKit(IReadOnlyList<Reagent> reagents)
    {
        AssertMutable();
        if (reagents.Count == 0 || reagents.Any(r => !Enum.IsDefined(r)) || AlchemyRules.Fit(Total, reagents.Count) != reagents.Count) return false;
        foreach (var reagent in reagents) _reagents[(int)reagent]++;
        SyncVars();
        return true;
    }

    private bool TrySpendCore(IReadOnlyList<Reagent> ingredients)
    {
        AssertMutable();
        if (!AlchemyRules.CanAfford(_reagents, ingredients)) return false;
        foreach (var r in ingredients) _reagents[(int)r]--;
        return true; // no callbacks/await between validation and all material mutations
    }

    public bool TrySpend(IReadOnlyList<Reagent> ingredients)
    {
        if (!TrySpendCore(ingredients)) return false;
        SyncVars();
        return true;
    }

    private void SyncVars()
    {
        foreach (var r in Enum.GetValues<Reagent>())
            if (DynamicVars.TryGetValue(r.ToString(), out var v)) v.BaseValue = _reagents[(int)r];
        InvokeDisplayAmountChanged();
    }

    // ───── Пояс ─────

    /// <summary>Пояс для ядов (docs/design/09 §1): +2 слота. Пока ядам не выделены отдельные слоты — общие слоты зелий.</summary>
    public const int BeltSlots = 2;
    private bool _beltGranted;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool BeltGranted { get => _beltGranted; set { AssertMutable(); _beltGranted = value; } }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        await CompleteSavedBrews();
        if (BeltGranted || !RequiemMode.IsInSystem(Owner)) return;
        BeltGranted = true;
        await PlayerCmd.GainMaxPotionCount(BeltSlots, Owner);
    }

    // ───── выпадение ─────

    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (!RequiemMode.IsInSystem(Owner)) return Task.CompletedTask;
        var rank = room.RoomType switch
        {
            RoomType.Boss => EnemyRank.Boss,
            RoomType.Elite => EnemyRank.Elite,
            _ => EnemyRank.Normal,
        };
        var candidates = room.Encounter.AllPossibleMonsters
            .SelectMany(m => { var e = RequiemData.MonsterEntry(m.GetType().Name); return AlchemyRules.DropCandidates(e.Faction, e.AttackType); })
            .ToList();
        if (candidates.Count == 0) candidates.Add(Reagent.Bloodmoss);

        var rng = Owner.PlayerRng.Rewards;
        var gained = 0;
        // «Знаток трав» (Выживание III): реагенты с врагов ×2.
        var drops = AlchemyRules.DropCount(rank) * (Progression.HeroRecord.Of(Owner)?.Has(Skill.HerbLore) == true ? 2 : 1);
        for (var i = 0; i < drops; i++)
            gained += Add(rng.NextItem(candidates), 1);
        // Элиты и боссы — шанс на Драконий порошок (сопротивления).
        if (rank == EnemyRank.Boss || (rank == EnemyRank.Elite && rng.NextInt(2) == 0))
            gained += Add(Reagent.DragonScale, 1);
        if (gained > 0) Flash();
        return Task.CompletedTask;
    }

    // ───── варка ─────

    // The recipe travels WITH the offered potion. A save made by an insertion callback before our obtain hook
    // can therefore reconcile materials after Continue. Unselected previews are not serialized into the player.
    private static readonly SavedSpireField<PotionModel, string> BrewCost = new(() => "", "SlayTheRequiem_BrewCost");
    internal static void Initialize() { _ = BrewCost; }
    private Dictionary<PotionModel, Reagent[]> _pendingBrews = new();
    public bool BrewTaken { get; private set; }

    internal static void MarkBrewCost(PotionModel potion, IReadOnlyList<Reagent> ingredients)
    {
        if (ingredients.Count is < 1 or > 3 || ingredients.Any(r => !Enum.IsDefined(r)))
            throw new ArgumentException("Invalid brew recipe", nameof(ingredients));
        BrewCost[potion] = string.Join(",", ingredients.Select(r => ((int)r).ToString(CultureInfo.InvariantCulture)));
    }

    internal static bool HasBrewCost(PotionModel potion) => !string.IsNullOrEmpty(BrewCost[potion]);
    internal static void CancelBrewCost(PotionModel potion) => BrewCost[potion] = "";

    public void BeginBrew(IEnumerable<(PotionModel potion, Reagent[] ingredients)> offers)
    {
        if (_pendingBrews.Count > 0) throw new InvalidOperationException("Another camp brew is already active");
        var staged = offers.ToDictionary(o => o.potion, o => (Reagent[])o.ingredients.Clone());
        if (staged.Values.Any(a => a.Length is < 1 or > 3 || a.Any(r => !Enum.IsDefined(r))))
            throw new ArgumentException("Invalid camp brew recipe", nameof(offers));
        _pendingBrews = staged;
        BrewTaken = false;
        foreach (var (potion, ingredients) in staged) MarkBrewCost(potion, ingredients);
    }

    public void EndBrew()
    {
        foreach (var potion in _pendingBrews.Keys.Where(p => !Owner.PotionSlots.Contains(p))) CancelBrewCost(potion);
        _pendingBrews.Clear(); // never clear a committed potion's unpaid saved obligation
    }

    /// <summary>Из одной Варки берётся одно зелье: остальные варианты после выбора недоступны.</summary>
    public override bool ShouldProcurePotion(PotionModel potion, Player player) =>
        player != Owner || !_pendingBrews.TryGetValue(potion, out var ingredients)
            || !BrewTaken && AlchemyRules.CanAfford(_reagents, ingredients);

    internal bool HasBrewCommit => BrewTaken || _pendingBrews.Keys.Any(p => Owner.PotionSlots.Contains(p));

    internal void CompleteBrew()
    {
        // Do not early-return on BrewTaken: insufficient materials keep a recoverable debt, NOT a paid flag.
        foreach (var potion in _pendingBrews.Keys.Where(p => Owner.PotionSlots.Contains(p))) CompleteBrew(potion);
    }

    internal void CompleteBrew(PotionModel potion)
    {
        if (potion.Owner != Owner || !Owner.PotionSlots.Contains(potion)) return;
        if (_pendingBrews.ContainsKey(potion)) BrewTaken = true;
        if (!HasBrewCost(potion)) return; // already paid, including a late notification/animation exception
        var pieces = BrewCost[potion]!.Split(',');
        if (pieces.Length is < 1 or > 3) throw new InvalidOperationException("Unknown saved brew-cost schema; debt retained");
        var ingredients = pieces.Select(s => int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            && Enum.IsDefined(typeof(Reagent), n) ? (Reagent)n : throw new InvalidOperationException("Invalid saved brew ingredient; debt retained")).ToArray();
        if (!TrySpendCore(ingredients)) throw new InvalidOperationException("Brew was obtained but its materials changed; saved debt retained");
        CancelBrewCost(potion); // raw material mutation + paid marker precede EVERY notification/await
        SyncVars();
        Flash();
    }

    internal async Task CompleteSavedBrews()
    {
        if (!Owner.PotionSlots.OfType<PotionModel>().Any(HasBrewCost)) return;
        using var lease = await InventoryGate.For(Owner).Enter(allowBlocked: true);
        foreach (var potion in Owner.PotionSlots.OfType<PotionModel>().Where(HasBrewCost).ToList())
        {
            try { CompleteBrew(potion); }
            catch (Exception e) { InventoryGate.Block(Owner, $"Saved brew {potion.Id}: {e}"); }
        }
    }

    public override Task AfterPotionProcured(PotionModel potion)
    {
        CompleteBrew(potion); // actual procure, not the later reward-completion flag; also used by the shop service
        return Task.CompletedTask;
    }

    public override Task AfterRewardTaken(Player player, Reward reward)
    {
        if (player == Owner && reward is PotionReward { Potion: { } potion }) CompleteBrew(potion);
        return Task.CompletedTask;
    }

}
