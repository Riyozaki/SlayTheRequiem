using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

/// <summary>The stock, restock, UI quote and authoritative executor use the SAME saved route policy.
/// Native tier remains appropriate for vanilla map/encounter code, not for a new five-stage shop curve.</summary>
public static class ShopEconomy
{
    public static EconomyRoute Context(IRunState run) => new(RunSettings.EconomyRevision,
        run.CurrentActIndex, run.Acts.Count, RunSettings.FiveActs, RunSettings.ShortRun);
}
