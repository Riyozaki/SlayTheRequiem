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

    public bool TrySpend(IReadOnlyList<Reagent> ingredients)
    {
        AssertMutable();
        if (!AlchemyRules.CanAfford(_reagents, ingredients)) return false;
        foreach (var r in ingredients) _reagents[(int)r]--;
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
        for (var i = 0; i < AlchemyRules.DropCount(rank); i++)
            gained += Add(rng.NextItem(candidates), 1);
        // Элиты и боссы — шанс на Драконий порошок (сопротивления).
        if (rank == EnemyRank.Boss || (rank == EnemyRank.Elite && rng.NextInt(2) == 0))
            gained += Add(Reagent.DragonScale, 1);
        if (gained > 0) Flash();
        return Task.CompletedTask;
    }

    // ───── варка ─────

    /// <summary>Предложенные в лагере варианты: зелье → его рецепт (списывается, когда зелье взято).</summary>
    private readonly Dictionary<PotionModel, Reagent[]> _pendingBrews = new();
    public bool BrewTaken { get; private set; }

    public void BeginBrew(IEnumerable<(PotionModel potion, Reagent[] ingredients)> offers)
    {
        _pendingBrews.Clear();
        BrewTaken = false;
        foreach (var (potion, ingredients) in offers) _pendingBrews[potion] = ingredients;
    }

    public void EndBrew() => _pendingBrews.Clear();

    /// <summary>Из одной Варки берётся одно зелье: остальные варианты после выбора недоступны.</summary>
    public override bool ShouldProcurePotion(PotionModel potion, Player player) =>
        player != Owner || !BrewTaken || !_pendingBrews.ContainsKey(potion);

    public override Task AfterRewardTaken(Player player, Reward reward)
    {
        if (player != Owner || BrewTaken || reward is not PotionReward { Potion: { } potion }) return Task.CompletedTask;
        if (!_pendingBrews.TryGetValue(potion, out var ingredients)) return Task.CompletedTask;
        TrySpend(ingredients);
        BrewTaken = true;
        Flash();
        return Task.CompletedTask;
    }
}
