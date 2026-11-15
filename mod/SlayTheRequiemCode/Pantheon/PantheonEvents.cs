using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Extensions;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Pantheon;

/// <summary>
/// Общая основа событий Пантеона. В пул «?» сами не попадают (<see cref="IsAllowed"/> = false):
/// их подставляет Знак веры через ModifyNextEvent с долями из <see cref="PantheonRules.UnknownRoomRoll"/>.
/// </summary>
public abstract class PantheonEvent : CustomEventModel
{
    public override bool IsAllowed(IRunState runState) => false;

    /// <summary>Свой портрет images/events/&lt;id&gt;.png, иначе ванильный по таблице tools/art/vanilla_art.csv.</summary>
    public override string? CustomInitialPortraitPath =>
        $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".EventImagePath(Art.VanillaArt.EventPortrait(GetType().Name));

    protected LocString Text(string key) => L10NLookup($"{Id.Entry}.{key}");

    protected EventOption Choice(Func<Task> onChosen, string key, Action<LocString>? vars = null)
    {
        var title = Text($"pages.INITIAL.options.{key}.title");
        var description = Text($"pages.INITIAL.options.{key}.description");
        vars?.Invoke(title);
        vars?.Invoke(description);
        return Option(onChosen, title, description);
    }

    protected EventOption Locked(string key, Action<LocString>? vars = null)
    {
        var title = Text($"pages.INITIAL.options.{key}.title");
        var description = Text($"pages.INITIAL.options.{key}.description");
        vars?.Invoke(title);
        vars?.Invoke(description);
        return Option((Func<Task>?)null, title, description);
    }

    protected void Finish(string page) => SetEventFinished(PageDescription(page));

    protected Task Leave()
    {
        Finish("LEAVE");
        return Task.CompletedTask;
    }

    /// <summary>Случайное число события [0, n) — у каждого игрока своё событие, ГСЧ события детерминирован.</summary>
    protected int Next(int n) => n <= 0 ? 0 : Rng.NextInt(n);
}

// ═════════════ Шесть камней у воды ═════════════

/// <summary>
/// «Шесть камней у воды» (docs/design/05 §1): три Осколка отлива на выбор; взятый действует до конца акта.
/// На повторной встрече — заменить. Акты 2–3.
/// </summary>
public sealed class EbbStonesEvent : PantheonEvent
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var current = EbbShardRelic.Of(Owner);
        var exclude = current == null ? null : new List<int> { (int)current.Kind };
        var picks = OmenRules.PickDistinct(OmenRules.ShardCount, OmenRules.ShardChoices, Next, exclude);
        var options = new List<EventOption>();
        foreach (var index in picks)
        {
            var shard = EbbShardRelic.Create((EbbShard)index);
            options.Add(RelicOption(shard, () => Take(shard)));
        }
        options.Add(Choice(Leave, "LEAVE"));
        return options;
    }

    private async Task Take(RelicModel shard)
    {
        if (EbbShardRelic.Of(Owner) is { } old) await old.RemoveSelf();
        await RelicCmd.Obtain(shard, Owner!);
        Finish("TAKEN");
    }
}

// ═════════════ Святилище ═════════════

/// <summary>
/// Святилище одного из Небесных (docs/design/05 §3): благословение (смена прежнего), Искупление за золото
/// (раз в акт), Амулет бога за 120 и Облачение бога за 150, или ограбление — золото ценой праведности.
/// Покупки и благословение возвращают к выбору; ограбление и уход завершают событие.
/// </summary>
public sealed class ShrineEvent : PantheonEvent
{
    private Celestial _god;
    private bool _godChosen;

    private Celestial God
    {
        get
        {
            if (!_godChosen)
            {
                _god = (Celestial)Next(PantheonRules.CelestialCount);
                _godChosen = true;
            }
            return _god;
        }
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => BuildOptions();

    private List<EventOption> BuildOptions()
    {
        var player = Owner!;
        var god = God;
        var options = new List<EventOption>();
        var current = CelestialBlessing.Of(player);

        // Благословение.
        if (current?.God == god) options.Add(Locked("BLESSED_ALREADY"));
        else if (Faith.Forbidden(player) || Faith.Effective(player) == Piety.Apostate) options.Add(Locked("BLESSING_DENIED"));
        else
        {
            var blessing = CelestialBlessing.Create(CelestialBlessing.Blessings[(int)god]);
            options.Add(RelicOption(blessing, () => Bless(god)));
        }

        // Искупление.
        var cost = PietyRules.RedemptionCost(global::SlayTheRequiem.SlayTheRequiemCode.Acts.ActKinds.Tier(player.RunState));
        void CostVar(LocString s) => s.Add("Cost", cost);
        if (Faith.Recorded(player) != Piety.Righteous)
        {
            if (Faith.CanRedeem(player) && player.Gold >= cost) options.Add(Choice(Redeem, "REDEEM", CostVar));
            else options.Add(Locked("REDEEM_LOCKED", CostVar));
        }

        // Комплект бога.
        AddGarment(options, CelestialBlessing.Amulets[(int)god], PantheonRules.AmuletPrice, BuyAmulet);
        AddGarment(options, CelestialBlessing.Robes[(int)god], PantheonRules.RobePrice, BuyRobe);

        // Ограбление.
        var loot = PietyRules.RobberyGold(global::SlayTheRequiem.SlayTheRequiemCode.Acts.ActKinds.Tier(player.RunState));
        options.Add(Choice(Rob, "ROB", s => s.Add("Gold", loot)));
        options.Add(Choice(Leave, "LEAVE"));
        return options;
    }

    private void AddGarment(List<EventOption> options, Type type, int price, Func<Task> buy)
    {
        var player = Owner!;
        if (player.Relics.Any(r => r.GetType() == type)) return;
        var item = CelestialBlessing.Create(type);
        if (player.Gold < price)
        {
            options.Add(Locked("GARMENT_POOR", s => s.Add("Price", price)));
            return;
        }
        options.Add(RelicOption(item, buy));
    }

    private void Refresh(string page) => SetEventState(PageDescription(page), BuildOptions());

    private async Task Bless(Celestial god)
    {
        await CelestialBlessing.Grant(Owner!, god);
        Refresh("BLESSED");
    }

    private async Task Redeem()
    {
        var player = Owner!;
        var cost = PietyRules.RedemptionCost(global::SlayTheRequiem.SlayTheRequiemCode.Acts.ActKinds.Tier(player.RunState));
        if (player.Gold < cost || !Faith.CanRedeem(player))
        {
            Refresh("INITIAL");
            return;
        }
        await PlayerCmd.LoseGold(cost, player, GoldLossType.Spent);
        Faith.Redeem(player);
        if (CelestialBlessing.Of(player) is { } blessing) await blessing.SyncMaxHp();
        Refresh("REDEEMED");
    }

    private Task BuyAmulet() => Buy(CelestialBlessing.Amulets[(int)God], PantheonRules.AmuletPrice);
    private Task BuyRobe() => Buy(CelestialBlessing.Robes[(int)God], PantheonRules.RobePrice);

    private async Task Buy(Type type, int price)
    {
        var player = Owner!;
        if (player.Gold < price)
        {
            Refresh("INITIAL");
            return;
        }
        await PlayerCmd.LoseGold(price, player, GoldLossType.Spent);
        await RelicCmd.Obtain(CelestialBlessing.Create(type), player);
        if (CelestialBlessing.Of(player) is { } blessing) await blessing.SyncMaxHp();
        Refresh("BOUGHT");
    }

    private async Task Rob()
    {
        var player = Owner!;
        await PlayerCmd.GainGold(PietyRules.RobberyGold(global::SlayTheRequiem.SlayTheRequiemCode.Acts.ActKinds.Tier(player.RunState)), player);
        Faith.Sin(player);
        if (CelestialBlessing.Of(player) is { } blessing) await blessing.SyncMaxHp();
        Finish("ROBBED");
    }
}

// ═════════════ Тёмное святилище ═════════════

/// <summary>
/// Тёмное святилище (docs/design/05 §4): два Князя Бездны предлагают дар с Ценой. Принять — договор (Отступник,
/// кроме Тройственных у Наследника); прежний дар уходит. Можно отказаться.
/// </summary>
public sealed class DarkShrineEvent : PantheonEvent
{
    public const int Offers = 2;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var current = AbyssGift.Of(Owner);
        var exclude = current == null ? null : new List<int> { (int)current.Prince };
        var picks = OmenRules.PickDistinct(PantheonRules.PrinceCount, Offers, Next, exclude);
        var options = new List<EventOption>();
        foreach (var index in picks)
        {
            var prince = (AbyssPrince)index;
            options.Add(RelicOption(AbyssGift.Create(prince), () => Accept(prince)));
        }
        options.Add(Choice(Leave, "REFUSE"));
        return options;
    }

    private async Task Accept(AbyssPrince prince)
    {
        await AbyssGift.Accept(Owner!, prince);
        Finish("ACCEPTED");
    }
}
