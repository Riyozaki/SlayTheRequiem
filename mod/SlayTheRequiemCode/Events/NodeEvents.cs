using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Shops;

namespace SlayTheRequiem.SlayTheRequiemCode.Events;

// ═════════════ События подтипов узлов (M5, 13 §4.2) ═════════════

/// <summary>
/// Событие подтипа узла «?» (Библиотека): в пул актов само не попадает (<see cref="IsAllowed"/> = false) — его подставляет
/// <see cref="Acts.ActLedger.ModifyNextEvent"/> по броску <see cref="NodeRules.RollLibrary"/>. Тексты, числа и картинка —
/// как у событий актов (tools/ci/check_events.py проверяет и их).
/// </summary>
public abstract class NodeEvent : RequiemEvent
{
    public sealed override ActKind HomeAct => ActKind.Other;

    public override bool IsAllowed(IRunState runState) => false;
}

/// <summary>
/// 📖 «Библиотека» (акты 2–5): изучить заклинание (1 из 3 по владению), переписать свиток в колоду или купить трактат владения.
/// </summary>
public sealed class LibraryEvent : NodeEvent
{
    protected override IEnumerable<(string Name, int Value)> Numbers =>
        [("Spells", 3), ("Copy", 30), ("Treatise", 95)];

    private RequiemScroll? Scroll => Owner?.PotionSlots.OfType<RequiemScroll>().FirstOrDefault();

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("STUDY", Study),
        Choice("COPY", Copy, Scroll != null && CanPay(N("Copy"))),
        Choice("TREATISE", Treatise, CanPay(N("Treatise"))),
        LeaveChoice(),
    ];

    private async Task Study()
    {
        await OfferSpells(N("Spells"));
        Finish("STUDIED");
    }

    private async Task Copy()
    {
        if (Scroll is { } scroll)
        {
            await Pay(N("Copy"));
            await RequiemScroll.Rewrite(Owner!, scroll);
        }
        Finish("COPIED");
    }

    private async Task Treatise()
    {
        await Pay(N("Treatise"));
        if (ShopStock.PickTreatise(Owner!) is { } treatise) await RelicCmd.Obtain(treatise.ToMutable(), Owner!);
        Finish("BOUGHT");
    }
}
