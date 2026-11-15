using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Progression;

namespace SlayTheRequiem.SlayTheRequiemCode.Events;

// ═════════════ Акт 2а — Ледяные Курганы: лёд, склепы, мёртвые (13 §3) ═════════════

/// <summary>«Замёрзший паломник»: обобрать мертвеца (золото ценой греха) или похоронить (лечение и искупление).</summary>
public sealed class FrozenPilgrimEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.FrozenBarrows;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Gold", 55), ("Heal", 12)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("LOOT", Loot),
        Choice("BURY", Bury),
        LeaveChoice(),
    ];

    private async Task Loot()
    {
        await Gold(N("Gold"));
        Sin();
        Finish("LOOTED");
    }

    private async Task Bury()
    {
        await Heal(N("Heal"));
        if (CanRedeem) Redeem();
        Finish("BURIED");
    }
}

/// <summary>«Печать кургана»: дверь с драконьим замком — Когтистый ключ (реликвия) или взлом (♥ за золото).</summary>
public sealed class BarrowSealEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.FrozenBarrows;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Hp", 9), ("Gold", 65)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("UNLOCK", Unlock, Ledger is { ClawKeys: > 0 }),
        Choice("FORCE", Force),
        LeaveChoice(),
    ];

    private async Task Unlock()
    {
        if (Ledger is { } ledger) ledger.ClawKeys--;
        await Relic();
        Finish("UNLOCKED");
    }

    private async Task Force()
    {
        await Hurt(N("Hp"));
        await Gold(N("Gold"));
        Finish("FORCED");
    }
}

/// <summary>«Алтарь стужи»: Морозные чары на атаку ценой макс. ♥ или растопить лёд — бои без Стужи и лечение.</summary>
public sealed class FrostAltarEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.FrozenBarrows;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("MaxHp", 4), ("Warmth", 2), ("Heal", 8)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("BLESS", Bless, HasEnchantable(attacksOnly: true)),
        Choice("MELT", Melt),
        LeaveChoice(),
    ];

    private async Task Bless()
    {
        await LoseMaxHp(N("MaxHp"));
        await Enchant<FrostyEnchantment>(attacksOnly: true);
        Finish("BLESSED");
    }

    private async Task Melt()
    {
        if (Ledger is { } ledger) ledger.WarmthFights += N("Warmth");
        await Heal(N("Heal"));
        Finish("MELTED");
    }
}

/// <summary>«Тризна драугров»: сесть за стол мертвецов (+макс. ♥ и проклятие) или поднять кубок за павших (улучшения).</summary>
public sealed class DraugrWakeEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.FrozenBarrows;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("MaxHp", 7), ("Upgrades", 2)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("FEAST", Feast),
        Choice("TOAST", Toast),
        LeaveChoice(),
    ];

    private async Task Feast()
    {
        await GainMaxHp(N("MaxHp"));
        await Curse<Decay>();
        Finish("FEASTED");
    }

    private Task Toast()
    {
        UpgradeRandom(N("Upgrades"));
        Finish("TOASTED");
        return Task.CompletedTask;
    }
}

/// <summary>«Погребальные урны»: разбить одну (золото, Прах мертвеца или ловушка) или все (наверняка, но с проклятием).</summary>
public sealed class BurialUrnsEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.FrozenBarrows;
    protected override IEnumerable<(string Name, int Value)> Numbers =>
        [("Gold", 70), ("Dust", 2), ("Hp", 8), ("AllGold", 40), ("AllDust", 3)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("ONE", BreakOne),
        Choice("ALL", BreakAll),
        LeaveChoice(),
    ];

    private async Task BreakOne()
    {
        var roll = Roll(10);
        if (roll < 5)
        {
            await Gold(N("Gold"));
            Finish("ONE_GOLD");
        }
        else if (roll < 8)
        {
            Reagents(Reagent.GraveDust, N("Dust"));
            Finish("ONE_DUST");
        }
        else
        {
            await Hurt(N("Hp"));
            Finish("ONE_TRAP");
        }
    }

    private async Task BreakAll()
    {
        await Gold(N("AllGold"));
        Reagents(Reagent.GraveDust, N("AllDust"));
        await Curse<Regret>();
        Finish("ALL");
    }
}

/// <summary>«Занесённые сани»: забрать припасы (зелье и Жёлтая кислица) или сжечь сани (лечение и бой без Стужи).</summary>
public sealed class SnowboundSledEvent : RequiemEvent
{
    public override ActKind HomeAct => ActKind.FrozenBarrows;
    protected override IEnumerable<(string Name, int Value)> Numbers => [("Sorrel", 2), ("Heal", 14), ("Warmth", 1)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Choice("SUPPLIES", Supplies),
        Choice("BURN", Burn),
        LeaveChoice(),
    ];

    private async Task Supplies()
    {
        Reagents(Reagent.Yellowsorrel, N("Sorrel"));
        await Potion();
        Finish("SUPPLIES");
    }

    private async Task Burn()
    {
        await Heal(N("Heal"));
        if (Ledger is { } ledger) ledger.WarmthFights += N("Warmth");
        Finish("BURNED");
    }
}
