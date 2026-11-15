using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Events;

// ═════════════ Акт 4а — Кровавый Двор: бархат, кровь, интриги (13 §3) ═════════════

/// <summary>«Маскарад»: надеть маску (превращение и золото) или танцевать с хозяйкой (редкая реликвия ценой макс. ♥).</summary>
public sealed class MasqueradeEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.BloodCourt;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Gold", 60), ("MaxHp", 5)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("MASK", Mask),
        Choice("DANCE", Dance),
        LeaveChoice(),
    ];

    private async Task Mask()
    {
        await TransformChosen(1);
        await Gold(N("Gold"));
        Finish("MASKED");
    }

    private async Task Dance()
    {
        await LoseMaxHp(N("MaxHp"));
        await Relic(RelicRarity.Rare);
        Finish("DANCED");
    }
}

/// <summary>«Кровавый фонтан»: испить (+макс. ♥, грех) или омыть оружие (улучшение случайных карт).</summary>
public sealed class BloodFountainEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.BloodCourt;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("MaxHp", 9), ("Upgrades", 2)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("DRINK", Drink),
        Choice("WASH", Wash),
        LeaveChoice(),
    ];

    private async Task Drink()
    {
        await GainMaxHp(N("MaxHp"));
        Sin();
        Finish("DRANK");
    }

    private Task Wash()
    {
        UpgradeRandom(N("Upgrades"));
        Finish("WASHED");
        return Task.CompletedTask;
    }
}

/// <summary>«Кости графа»: ставка золотом (удвоение) или кровью (реликвия) — шанс 50 %, жребий события.</summary>
public sealed class CountsDiceEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.BloodCourt;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Bet", 60), ("Win", 130), ("Hp", 12)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("BET_GOLD", BetGold, CanPay(N("Bet"))),
        Choice("BET_BLOOD", BetBlood),
        LeaveChoice(),
    ];

    private async Task BetGold()
    {
        await Pay(N("Bet"));
        if (Roll(2) == 0)
        {
            await Gold(N("Win"));
            Finish("WON");
        }
        else Finish("LOST");
    }

    private async Task BetBlood()
    {
        await Hurt(N("Hp"));
        if (Roll(2) == 0)
        {
            await Relic();
            Finish("BLOOD_WON");
        }
        else Finish("BLOOD_LOST");
    }
}

/// <summary>«Рынок трэллов»: выкупить пленников (лечение, улучшение, искупление) или донести страже (золото, грех).</summary>
public sealed class ThrallMarketEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.BloodCourt;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Cost", 80), ("Heal", 20), ("Gold", 55)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("FREE", Free, CanPay(N("Cost"))),
        Choice("INFORM", Inform),
        LeaveChoice(),
    ];

    private async Task Free()
    {
        await Pay(N("Cost"));
        await Heal(N("Heal"));
        if (CanRedeem) Redeem();
        await UpgradeChosen(1);
        Finish("FREED");
    }

    private async Task Inform()
    {
        await Gold(N("Gold"));
        Sin();
        Finish("INFORMED");
    }
}

/// <summary>«Зеркало без отражения»: разбить (удалить карту ценой ♥) или вглядеться (+1 Иллюзии и Сомнение).</summary>
public sealed class BlindMirrorEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.BloodCourt;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Hp", 6)];

    private bool CanGaze => Mastery.GrimoireOf(Owner) is { } g && g.Level(SpellSchool.Illusion) < SpellAffinity.MaxMastery;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("SHATTER", Shatter),
        Choice("GAZE", Gaze, CanGaze),
        LeaveChoice(),
    ];

    private async Task Shatter()
    {
        await Hurt(N("Hp"));
        await RemoveChosen(1);
        Finish("SHATTERED");
    }

    private async Task Gaze()
    {
        RaiseMastery(SpellSchool.Illusion);
        await Curse<Doubt>();
        Finish("GAZED");
    }
}

/// <summary>«Ночная карета»: доехать (лечение за плату) или ограбить (золото ценой ♥ и греха).</summary>
public sealed class NightCarriageEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.BloodCourt;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Fare", 40), ("Heal", 25), ("Loot", 90), ("Hp", 14)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("RIDE", Ride, CanPay(N("Fare"))),
        Choice("ROB", Rob),
        LeaveChoice(),
    ];

    private async Task Ride()
    {
        await Pay(N("Fare"));
        await Heal(N("Heal"));
        Finish("RODE");
    }

    private async Task Rob()
    {
        await Hurt(N("Hp"));
        await Gold(N("Loot"));
        Sin();
        Finish("ROBBED");
    }
}
