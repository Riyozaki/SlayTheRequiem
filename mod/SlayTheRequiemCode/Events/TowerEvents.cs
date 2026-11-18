using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Shops;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Events;

// ═════════════ Акт 4б — Звёздная Башня: звёзды, стекло, магия (13 §3) ═════════════

/// <summary>
/// «Звёздный экзамен» (13 §5: тест владения школой): экзамен по сильнейшей школе героя — при владении не ниже {Need}
/// +1 к нему и золото; иначе вариант недоступен. «Зубрёжка» — +1 к слабейшей школе за золото и грех (списал).
/// </summary>
public sealed class StarExamEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.StarTower;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Need", 2), ("Gold", 50), ("Bribe", 70)];

    private SpellSchool Strongest => Mastery.Schools.OrderByDescending(s => Mastery.Of(Owner, s)).First();

    private SpellSchool? Weakest => Mastery.Schools.Where(s => Mastery.Of(Owner, s) < SpellAffinity.MaxMastery)
        .OrderBy(s => Mastery.Of(Owner, s)).Cast<SpellSchool?>().FirstOrDefault();

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var hasGrimoire = Mastery.GrimoireOf(Owner) != null;
        var school = Strongest;
        var level = Mastery.Of(Owner, school);
        return
        [
            Choice($"PASS_{school.ToString().ToUpperInvariant()}", () => Pass(school),
                hasGrimoire && level >= N("Need") && level < SpellAffinity.MaxMastery),
            Choice("CRAM", Cram, hasGrimoire && Weakest != null && CanPay(N("Bribe"))),
            LeaveChoice(),
        ];
    }

    private async Task Pass(SpellSchool school)
    {
        RaiseMastery(school);
        await Gold(N("Gold"));
        Finish("PASSED");
    }

    private async Task Cram()
    {
        await Pay(N("Bribe"));
        if (Weakest is { } school) RaiseMastery(school);
        Sin();
        Finish("CRAMMED");
    }
}

/// <summary>«Колодец лей-линии»: испить (Грозовые чары на атаку) или наполнить флягу (зелье и Синецвет).</summary>
public sealed class LeyWellEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.StarTower;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Bloom", 3)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("DRINK", Drink, HasEnchantable(attacksOnly: true)),
        Choice("FILL", Fill),
        LeaveChoice(),
    ];

    private async Task Drink()
    {
        await Enchant<StormEnchantment>(attacksOnly: true);
        Finish("DRANK");
    }

    private async Task Fill()
    {
        Reagents(Reagent.Bluebloom, N("Bloom"));
        await Potion();
        Finish("FILLED");
    }
}

/// <summary>«Разбитый планетарий»: собрать механизм (реликвия ценой ♥) или продать линзы (золото).</summary>
public sealed class BrokenOrreryEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.StarTower;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Hp", 8), ("Gold", 75)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("ASSEMBLE", Assemble),
        Choice("SELL", Sell),
        LeaveChoice(),
    ];

    private async Task Assemble()
    {
        await Hurt(N("Hp"));
        await Relic();
        Finish("ASSEMBLED");
    }

    private async Task Sell()
    {
        await Gold(N("Gold"));
        Finish("SOLD");
    }
}

/// <summary>«Ученик-отступник»: купить заклинание (1 из {Spells} по владению) или отнять гримуар силой (♥ и грех).</summary>
public sealed class RenegadeApprenticeEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.StarTower;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Cost", 40), ("Hp", 10), ("Spells", 3)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("TRADE", Trade, CanPay(N("Cost"))),
        Choice("SEIZE", Seize),
        LeaveChoice(),
    ];

    private async Task Trade()
    {
        if (Owner!.Gold < N("Cost") || !await OfferSpells(N("Spells"), N("Cost"))) return;
        Finish("TRADED");
    }

    private async Task Seize()
    {
        if (!await OfferSpells(N("Spells"))) return;
        await Hurt(N("Hp"));
        Sin();
        Finish("SEIZED");
    }
}

/// <summary>«Зеркальная библиотека»: читать (улучшить карты ценой ♥) или сжечь страницу (удалить карту, Синецвет).</summary>
public sealed class MirrorLibraryEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.StarTower;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Hp", 7), ("Upgrades", 2), ("Bloom", 1)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("READ", Read),
        Choice("BURN", Burn),
        LeaveChoice(),
    ];

    private async Task Read()
    {
        await Hurt(N("Hp"));
        await UpgradeChosen(N("Upgrades"));
        Finish("READ");
    }

    private async Task Burn()
    {
        await RemoveChosen(1);
        Reagents(Reagent.Bluebloom, N("Bloom"));
        Finish("BURNED");
    }
}

/// <summary>«Звёздный картограф»: купить трактат владения со скидкой или помочь с расчётами (золото и улучшение).</summary>
public sealed class StarCartographerEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.StarTower;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Cost", 90), ("Gold", 45)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("BUY", Buy, CanPay(N("Cost"))),
        Choice("HELP", Help),
        LeaveChoice(),
    ];

    private async Task Buy()
    {
        await Pay(N("Cost"));
        if (ShopStock.PickTreatise(Owner!) is { } treatise) await RelicCmd.Obtain(treatise.ToMutable(), Owner!);
        else await Relic();
        Finish("BOUGHT");
    }

    private async Task Help()
    {
        await Gold(N("Gold"));
        await UpgradeChosen(1);
        Finish("HELPED");
    }
}
