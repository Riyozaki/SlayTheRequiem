using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Progression;

namespace SlayTheRequiem.SlayTheRequiemCode.Events;

// ═════════════ Оверлеи: Улей — «Трещины Бездны», Слава — «Вторжение Бездны» (13 §3, §5) ═════════════

/// <summary>«Шепчущая трещина» (Улей): протянуть руку (реликвия и проклятие) или запечатать (удалить карту ценой ♥).</summary>
public sealed class WhisperingRiftEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.Hive;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Hp", 8)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("REACH", Reach),
        Choice("SEAL", Seal),
        LeaveChoice(),
    ];

    private async Task Reach()
    {
        await Relic();
        await Curse<Regret>();
        Finish("REACHED");
    }

    private async Task Seal()
    {
        await Hurt(N("Hp"));
        await RemoveChosen(1);
        Finish("SEALED");
    }
}

/// <summary>«Янтарь Улья»: расколоть (награда картой) или собрать смолу ({Resin} случайных реагентов).</summary>
public sealed class HiveAmberEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.Hive;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Resin", 3)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("CRACK", Crack),
        Choice("GATHER", Gather),
        LeaveChoice(),
    ];

    private async Task Crack()
    {
        await CardReward();
        Finish("CRACKED");
    }

    private Task Gather()
    {
        var reagents = Enum.GetValues<Reagent>();
        for (var i = 0; i < N("Resin"); i++) Reagents(reagents[Roll(reagents.Length)], 1);
        Finish("GATHERED");
        return Task.CompletedTask;
    }
}

/// <summary>«Пепельный паломник» (13 §5): подать милостыню (лечение, искупление) или пойти с ним сквозь пепел (Пламенные чары).</summary>
public sealed class AshPilgrimEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.Hive;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Alms", 30), ("Heal", 15), ("Hp", 6), ("Salt", 2)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("ALMS", Alms, CanPay(N("Alms"))),
        Choice("FOLLOW", Follow, HasEnchantable(attacksOnly: true)),
        LeaveChoice(),
    ];

    private async Task Alms()
    {
        await Pay(N("Alms"));
        await Heal(N("Heal"));
        if (CanRedeem) Redeem();
        Finish("GAVE");
    }

    private async Task Follow()
    {
        await Hurt(N("Hp"));
        Reagents(Reagent.AshSalt, N("Salt"));
        await Enchant<FlamingEnchantment>(attacksOnly: true);
        Finish("FOLLOWED");
    }
}

/// <summary>«Павший герольд» (Слава): принять знамя (+макс. ♥) или снять с него доспех (снаряжение высокой ступени, грех).</summary>
public sealed class FallenHeraldEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.Glory;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("MaxHp", 7)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("BANNER", Banner),
        Choice("ARMOR", Armor),
        LeaveChoice(),
    ];

    private async Task Banner()
    {
        await GainMaxHp(N("MaxHp"));
        Finish("BANNER");
    }

    private async Task Armor()
    {
        await Equipment(4, 5);
        Sin();
        Finish("ARMOR");
    }
}

/// <summary>«Торговец пустоты» (Слава): продать плоть (редкая реликвия за макс. ♥) или память (удалить карту за золото).</summary>
public sealed class VoidMerchantEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.Glory;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("MaxHp", 6), ("Gold", 90)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("FLESH", Flesh),
        Choice("MEMORY", Memory),
        LeaveChoice(),
    ];

    private async Task Flesh()
    {
        await LoseMaxHp(N("MaxHp"));
        await Relic(RelicRarity.Rare);
        Finish("FLESH");
    }

    private async Task Memory()
    {
        await RemoveChosen(1);
        await Gold(N("Gold"));
        Finish("MEMORY");
    }
}

/// <summary>«Последний костёр» (Слава): отдохнуть (как в лагере) или наточить клинок (улучшить карту).</summary>
public sealed class LastCampfireEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.Glory;
    protected override IEnumerable<(string Name, int Value)> Numbers => [];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("REST", Rest),
        Choice("SHARPEN", Sharpen),
        LeaveChoice(),
    ];

    private async Task Rest()
    {
        await PlayerCmd.MimicRestSiteHeal(Owner!);
        Finish("RESTED");
    }

    private async Task Sharpen()
    {
        await UpgradeChosen(1);
        Finish("SHARPENED");
    }
}
