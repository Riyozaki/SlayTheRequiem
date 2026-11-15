using MegaCrit.Sts2.Core.Events;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Events;

// ═════════════ Акт 2б — Заводные Глубины: медь, пар, механизмы (13 §3) ═════════════

/// <summary>«Сломанный голем просит ключ» (13 §5): завести его за золото (снаряжение) или разобрать (золото и превращение).</summary>
public sealed class BrokenGolemEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.ClockworkDepths;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Cost", 50), ("Gold", 35)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("WIND", Wind, CanPay(N("Cost"))),
        Choice("SCRAP", Scrap),
        LeaveChoice(),
    ];

    private async Task Wind()
    {
        await Pay(N("Cost"));
        await Equipment(2, 3);
        Finish("WOUND");
    }

    private async Task Scrap()
    {
        await Gold(N("Gold"));
        await TransformChosen(1);
        Finish("SCRAPPED");
    }
}

/// <summary>«Паровой вентиль»: перекрыть руками (♥ за бои без Перегрева) или погреться в пару (лечение).</summary>
public sealed class SteamValveEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.ClockworkDepths;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Hp", 7), ("Relief", 2), ("Heal", 10)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("CLOSE", Close),
        Choice("BATHE", Bathe),
        LeaveChoice(),
    ];

    private async Task Close()
    {
        await Hurt(N("Hp"));
        if (Ledger is { } ledger) ledger.ValveRelief += N("Relief");
        Finish("CLOSED");
    }

    private async Task Bathe()
    {
        await Heal(N("Heal"));
        Finish("BATHED");
    }
}

/// <summary>«Латунный архив»: изучить чертежи (+1 владения Изменением ценой ♥) или скопировать (улучшение карты).</summary>
public sealed class BrassArchiveEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.ClockworkDepths;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Hp", 6)];

    private bool CanStudy => Mastery.GrimoireOf(Owner) is { } g && g.Level(SpellSchool.Alteration) < SpellAffinity.MaxMastery;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("STUDY", Study, CanStudy),
        Choice("COPY", Copy),
        LeaveChoice(),
    ];

    private async Task Study()
    {
        await Hurt(N("Hp"));
        RaiseMastery(SpellSchool.Alteration);
        Finish("STUDIED");
    }

    private async Task Copy()
    {
        await UpgradeChosen(1);
        Finish("COPIED");
    }
}

/// <summary>«Береговые грабители» (фракция 2б): заплатить пошлину, отбиться (♥ и их кошель) или перекупить добычу (реликвия).</summary>
public sealed class CoastRaidersEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.ClockworkDepths;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Toll", 40), ("Hp", 11), ("Loot", 30), ("Bribe", 80)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("PAY", PayToll, CanPay(N("Toll"))),
        Choice("FIGHT", Fight),
        Choice("BRIBE", Bribe, CanPay(N("Bribe"))),
    ];

    private async Task PayToll()
    {
        await Pay(N("Toll"));
        Finish("PAID");
    }

    private async Task Fight()
    {
        await Hurt(N("Hp"));
        await Gold(N("Loot"));
        Finish("FOUGHT");
    }

    private async Task Bribe()
    {
        await Pay(N("Bribe"));
        await Relic();
        Finish("BRIBED");
    }
}

/// <summary>«Кузня автоматонов»: закалить атаку (Дыхательные чары ценой ♥) или перековать карту (превращение).</summary>
public sealed class AutomatonForgeEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.ClockworkDepths;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Hp", 6)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("TEMPER", Temper, HasEnchantable(attacksOnly: true)),
        Choice("REFORGE", Reforge),
        LeaveChoice(),
    ];

    private async Task Temper()
    {
        await Hurt(N("Hp"));
        await Enchant<BreathingEnchantment>(attacksOnly: true);
        Finish("TEMPERED");
    }

    private async Task Reforge()
    {
        await TransformChosen(1);
        Finish("REFORGED");
    }
}

/// <summary>«Заводной оракул»: монета за предсказание (награда картой) или разбить автомат (♥ за золото и Грозовые кристаллы).</summary>
public sealed class ClockworkOracleEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.ClockworkDepths;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Coin", 25), ("Hp", 6), ("Gold", 45), ("Crystal", 2)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("COIN", Coin, CanPay(N("Coin"))),
        Choice("SMASH", Smash),
        LeaveChoice(),
    ];

    private async Task Coin()
    {
        await Pay(N("Coin"));
        await CardReward();
        Finish("ASKED");
    }

    private async Task Smash()
    {
        await Hurt(N("Hp"));
        await Gold(N("Gold"));
        Reagents(Reagent.StormCrystal, N("Crystal"));
        Finish("SMASHED");
    }
}
