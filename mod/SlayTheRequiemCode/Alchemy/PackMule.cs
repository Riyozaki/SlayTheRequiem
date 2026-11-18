using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Alchemy;

/// <summary>
/// Вьючный зверь (docs/design/10 §6, completion-plan M5 п. 5) — «реликвия-компаньон» без слота и веса:
/// +10 груза («Подкованный» — ещё +5: Кузня в лагере или Оружейник); Сундук доступен не только у костра, но и у
/// прилавка любой лавки. В бою не участвует. Где взять: событие «Брошенный мул» (акты 1–2) или торговец (Странник,
/// Караван — 120). В событии «Засада» (акты 2–4) зверя можно не защитить — тогда он погибает и половина Сундука теряется.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class PackMule : RequiemRelic, IResourceSource, IWeightless
{
    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsAllowedInShops => false;
    protected override bool IsAllowedInternal(IRunState runState) => false;
    /// <summary>Счётчик — сколько груза несёт зверь (10, подкованный — 15).</summary>
    public override bool ShowCounter => true;
    public override int DisplayAmount => BurdenBonus;

    public static PackMule? Of(Player? player) => player?.Relics.OfType<PackMule>().FirstOrDefault();

    /// <summary>Можно завести зверя: герой в системе Груза и зверя ещё нет.</summary>
    public static bool CanAdopt(Player? player) => player != null && TravelPack.Of(player) != null && Of(player) == null;

    public static async Task Adopt(Player player)
    {
        if (!CanAdopt(player)) return;
        await RelicCmd.Obtain(ModelDb.Relic<PackMule>().ToMutable(), player);
        TravelPack.Of(player)?.Refresh();
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Burden", CampRules.MuleBurden), new DynamicVar("Shod", CampRules.ShodBurden)];

    private bool _shod;

    /// <summary>«Подкованный»: +5 груза.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool Shod { get => _shod; set { AssertMutable(); _shod = value; } }

    public int BurdenBonus => CampRules.MuleCapacity(Shod);

    /// <summary>Подковать (Кузня, Оружейник). false — уже подкован.</summary>
    public bool Shoe()
    {
        if (Shod) return false;
        Shod = true;
        Flash();
        InvokeDisplayAmountChanged();
        TravelPack.Of(Owner)?.Refresh();
        return true;
    }

    /// <summary>«Засада»: зверь погибает, с ним пропадает половина Сундука (<paramref name="roll"/> — ГСЧ события).</summary>
    public async Task<int> Perish(Func<int, int> roll)
    {
        var owner = Owner;
        var lost = TravelPack.Of(owner) is { } pack ? pack.LoseHalf(roll) : 0;
        await RelicCmd.Remove(this);
        TravelPack.Of(owner)?.Refresh();
        return lost;
    }
}
