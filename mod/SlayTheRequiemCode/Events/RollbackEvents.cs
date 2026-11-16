using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rollback;
using SlayTheRequiem.SlayTheRequiemCode.Shops;

namespace SlayTheRequiem.SlayTheRequiemCode.Events;

// ═════════════ Камень Памяти (15 §2, completion-plan M5 п. 6) ═════════════

/// <summary>
/// «Свеча в склепе» — редкое событие актов 2–5: зажечь Свечу Реквиема у Камня Памяти (+1 Свеча, не больше одной за забег)
/// или забрать воск (золото). Есть в пуле «?» акта лишь с шансом <see cref="MemoryStoneRules.CryptCandleChancePercent"/> %
/// (бросок от зерна и акта) и только если Свеча хоть кому-то в отряде пригодится: Камень включён, Свечи конечны,
/// уровень не «Проклятый», Свечу из склепа ещё не брали. Остальным в коопе остаётся воск.
/// </summary>
public sealed class CryptCandleEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.FrozenBarrows;

    public override IReadOnlyList<ActKind> AlsoIn =>
        [ActKind.ClockworkDepths, ActKind.Hive, ActKind.BloodCourt, ActKind.StarTower, ActKind.Glory];

    protected override IEnumerable<(string Name, int Value)> Numbers => [("Candles", 1), ("Gold", 45)];

    protected override bool Allowed(IRunState runState)
    {
        var roll = ShopRules.Fnv1a((runState.Rng.StringSeed ?? string.Empty) + "|crypt_candle", runState.CurrentActIndex);
        return MemoryStoneRules.CryptCandleInPool(roll) && runState.Players.Any(CanLight);
    }

    private static bool CanLight(Player? player) => MemoryStoneRelic.Of(player) is { CanTakeCryptCandle: true };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("LIGHT", Light, CanLight(Owner)),
        Choice("WAX", Wax),
        LeaveChoice(),
    ];

    private Task Light()
    {
        MemoryStoneRelic.Of(Owner)?.TakeCryptCandle();
        Finish("LIT");
        return Task.CompletedTask;
    }

    private async Task Wax()
    {
        await Gold(N("Gold"));
        Finish("MELTED");
    }
}
