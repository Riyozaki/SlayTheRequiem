using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Shops;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

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

    private RequiemScroll? Scroll => Owner?.PotionSlots.OfType<RequiemScroll>().FirstOrDefault(s => !s.RewriteCommitted);

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

    private bool _copyInFlight, _copyCompleted;
    private async Task Copy()
    {
        if (_copyInFlight || _copyCompleted || Scroll is not { } scroll) return;
        _copyInFlight = true;
        var owner = Owner!;
        var room = owner.RunState.CurrentRoom;
        try
        {
            var result = await CopyScroll(owner, scroll, N("Copy"),
                () => !_copyCompleted && Owner == owner && ReferenceEquals(room, owner.RunState.CurrentRoom));
            if (!result.Succeeded && MegaCrit.Sts2.Core.Context.LocalContext.IsMe(owner))
                UI.NRequiemToast.Show(UI.RequiemUi.TipText("OPERATION_RESULT.title", ("Status", UI.RequiemUi.TipText($"OP_STATUS_{result.Status.ToString().ToUpperInvariant()}.title"))));
            if (result.Changed) { _copyCompleted = true; Finish("COPIED"); }
        }
        finally { _copyInFlight = false; }
    }

    internal static async Task<OperationResult> CopyScroll(Player owner, RequiemScroll scroll, int price, Func<bool> available)
    {
        using var lease = await InventoryGate.For(owner).Enter();
        if (lease == null) return new(OperationStatus.Busy, scroll.Id.Entry, "Inventory synchronization blocked");
        if (price <= 0 || owner.Gold < price || !available()) return OperationResult.Reject(scroll.Id.Entry, "Library offer/price changed");
        return await RequiemScroll.Rewrite(owner, scroll, async () =>
        {
            if (owner.Gold < price || !available()) throw new InvalidOperationException("Library payment changed before commit");
            var before = owner.Gold;
            await PlayerCmd.LoseGold(price, owner);
            if (OperationAccounting.Paid(before, owner.Gold) != price) throw new InvalidOperationException("Library payment amount mismatch");
        }, lease);
    }

    private async Task Treatise()
    {
        await Pay(N("Treatise"));
        if (ShopStock.PickTreatise(Owner!) is { } treatise) await RelicCmd.Obtain(treatise.ToMutable(), Owner!);
        Finish("BOUGHT");
    }
}
