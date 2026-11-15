using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Events;

// ═════════════ Вьючный зверь (10 §6, completion-plan M5 п. 5) ═════════════

/// <summary>
/// «Брошенный мул» (акты 1–2): взять зверя с собой (+10 груза, Сундук у прилавка) или обобрать поклажу
/// (золото и Кровяной мох). Встречается, пока у кого-то из отряда нет зверя.
/// </summary>
public sealed class AbandonedMuleEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.FrozenBarrows;
    public override IReadOnlyList<ActKind> AlsoIn => [ActKind.Foothills, ActKind.ClockworkDepths];
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Burden", 10), ("Gold", 35), ("Moss", 2)];

    protected override bool Allowed(IRunState runState) => runState.Players.Any(PackMule.CanAdopt);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("TAKE", Take, PackMule.CanAdopt(Owner)),
        Choice("UNLOAD", Unload),
        LeaveChoice(),
    ];

    private async Task Take()
    {
        await PackMule.Adopt(Owner!);
        Finish("TAKEN");
    }

    private async Task Unload()
    {
        await Gold(N("Gold"));
        Reagents(Reagent.Bloodmoss, N("Moss"));
        Finish("UNLOADED");
    }
}

/// <summary>
/// «Засада» (акты 2–4, только со вьючным зверем): защитить зверя (♥), откупиться (золото) или бросить его — зверь
/// погибает, с ним пропадает половина Сундука (10 §6).
/// </summary>
public sealed class MuleAmbushEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.FrozenBarrows;
    public override IReadOnlyList<ActKind> AlsoIn => [ActKind.ClockworkDepths, ActKind.Hive, ActKind.BloodCourt, ActKind.StarTower];
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Hp", 12), ("Bribe", 75)];

    protected override bool Allowed(IRunState runState) => runState.Players.Any(p => PackMule.Of(p) != null);

    private bool HasMule => PackMule.Of(Owner) != null;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new List<EventOption>
        {
            Choice("DEFEND", Defend),
            Choice("BRIBE", Bribe, CanPay(N("Bribe"))),
            Choice("ABANDON", Abandon, HasMule),
        };
        if (!HasMule) options.Add(LeaveChoice());
        return options;
    }

    private async Task Defend()
    {
        await Hurt(N("Hp"));
        Finish("DEFENDED");
    }

    private async Task Bribe()
    {
        await Pay(N("Bribe"));
        Finish("BRIBED");
    }

    private async Task Abandon()
    {
        if (PackMule.Of(Owner) is { } mule) await mule.Perish(Roll);
        Finish("ABANDONED");
    }
}
