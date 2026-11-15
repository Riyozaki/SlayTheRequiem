using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Shops;

namespace SlayTheRequiem.SlayTheRequiemCode.Pantheon;

/// <summary>
/// Знак веры (docs/design/05 §2) — служебная стартовая реликвия героя в системе Реквиема: записанная праведность
/// (Праведник → Запятнанный → Отступник), акт последнего Искупления, ступень «Небесный щит» (этап 6).
/// Счётчик на иконке — действующая праведность: 0 Праведник, 1 Запятнанный, 2 Отступник.
/// Ещё подменяет часть «?» в актах 2+ Святилищами, Тёмными святилищами и Камнями отлива (хук ModifyNextEvent).
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class FaithSigil : RequiemRelic, IWeightless
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => false;
    public override bool ShowCounter => true;
    public override int DisplayAmount => IsMutable && Owner != null ? (int)Faith.Effective(Owner) : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Piety", 0),
        new DynamicVar("Recorded", 0),
        new DynamicVar("Cost", PietyRules.RedemptionCost(0)),
    ];

    private int _piety;
    private int _redeemedAct;     // акт последнего Искупления + 1; 0 — не было
    private bool _heavenlyShield;
    private int _sins;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int RecordedPiety
    {
        get => _piety;
        set
        {
            AssertMutable();
            _piety = Math.Clamp(value, 0, 2);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int RedeemedAct
    {
        get => _redeemedAct;
        set
        {
            AssertMutable();
            _redeemedAct = value;
        }
    }

    /// <summary>Ступень «Небесный щит» Праведника (этап 6) — открывает Дух божества.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HeavenlyShield
    {
        get => _heavenlyShield;
        set
        {
            AssertMutable();
            _heavenlyShield = value;
        }
    }

    private int _seenFormEvents;

    /// <summary>Встреченные события трансформаций (бит 1 &lt;&lt; FormKind) — каждое один раз за забег (этап 6).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int SeenFormEvents
    {
        get => _seenFormEvents;
        set
        {
            AssertMutable();
            _seenFormEvents = value;
        }
    }

    public void MarkFormEventSeen(FormKind kind)
    {
        if (IsMutable) SeenFormEvents = _seenFormEvents | (1 << (int)kind);
    }

    /// <summary>Сколько раз герой согрешил (для истории и событий).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Sins
    {
        get => _sins;
        set
        {
            AssertMutable();
            _sins = value;
        }
    }

    public Piety Recorded => (Piety)_piety;

    public void Set(Piety piety)
    {
        RecordedPiety = (int)piety;
        Refresh();
    }

    public void Refresh()
    {
        if (!IsMutable || Owner == null) return;
        if (DynamicVars.TryGetValue("Piety", out var p)) p.BaseValue = (int)Faith.Effective(Owner);
        if (DynamicVars.TryGetValue("Recorded", out var r)) r.BaseValue = _piety;
        if (DynamicVars.TryGetValue("Cost", out var c)) c.BaseValue = PietyRules.RedemptionCost(global::SlayTheRequiem.SlayTheRequiemCode.Acts.ActKinds.Tier(Owner.RunState));
        InvokeDisplayAmountChanged();
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        Refresh();
        return Task.CompletedTask;
    }

    // ───── «?» → Святилища ─────

    /// <summary>
    /// В актах 2+ часть «?» становится Святилищем, Тёмным святилищем или (акты 2–3) Камнями отлива.
    /// Решение зависит только от зерна и этажа, поэтому одинаково у всех; меняет событие только Знак первого игрока.
    /// </summary>
    public override EventModel ModifyNextEvent(EventModel currentEvent)
    {
        if (!RequiemConfig.EnablePantheon || !IsMutable || Owner == null || !RequiemMode.IsInSystem(Owner)) return currentEvent;
        if (currentEvent is ShrineEvent or DarkShrineEvent or EbbStonesEvent or Transformations.FormEvent or Events.NodeEvent) return currentEvent;
        var run = Owner.RunState;
        if (run.Players.FirstOrDefault() != Owner) return currentEvent;
        var roll = (int)(ShopRules.Fnv1a((run.Rng.StringSeed ?? string.Empty) + "|pantheon", run.TotalFloor * 17 + run.CurrentActIndex) % 100u);
        EventModel? pantheon = PantheonRules.UnknownRoomRoll(global::SlayTheRequiem.SlayTheRequiemCode.Acts.ActKinds.Tier(run), roll) switch
        {
            1 => ModelDb.Event<ShrineEvent>(),
            2 => ModelDb.Event<DarkShrineEvent>(),
            3 => ModelDb.Event<EbbStonesEvent>(),
            _ => null,
        };
        if (pantheon != null) return pantheon;
        // События трансформаций (этап 6): свой бросок, каждое — один раз за забег.
        var seed = (run.Rng.StringSeed ?? string.Empty) + "|forms";
        var formRoll = (int)(ShopRules.Fnv1a(seed, run.TotalFloor * 31 + run.CurrentActIndex) % 100u);
        var pick = (int)(ShopRules.Fnv1a(seed + "|pick", run.TotalFloor) % 1000u);
        var form = TransformationRules.UnknownRoomEvent(run.CurrentActIndex, run.Acts.Count, formRoll, pick, _seenFormEvents);
        return Transformations.FormEvent.For(form) ?? currentEvent;
    }
}

/// <summary>Форма-трансформация (вампир, оборотень, лич — этап 6): пока активна — Отступник.</summary>
public interface ITransformationForm
{
    bool IsActiveForm { get; }
}

/// <summary>Реликвия, запрещающая праведность (дар Отверженного, предыстория Ординатора).</summary>
public interface IPietyForbidder { }

/// <summary>Реликвия вечного отступничества (дар Разрушителя Врат).</summary>
public interface IPermanentApostasy { }

/// <summary>Праведность героя: чтение и изменение (docs/design/05 §2).</summary>
public static class Faith
{
    public static FaithSigil? SigilOf(Player? player) => player?.Relics.OfType<FaithSigil>().FirstOrDefault();

    public static Piety Recorded(Player? player) => SigilOf(player)?.Recorded ?? Piety.Righteous;

    /// <summary>Бездонная вещь или дар Князя (кроме Тройственных у Наследника) — не выше «Запятнанного».</summary>
    public static bool HoldsAbyssal(Player player) =>
        player.Relics.Any(r => r is IAbyssalItem && !(r is AbyssGift { IsExempt: true }));

    public static bool Transformed(Player player) =>
        player.Relics.OfType<ITransformationForm>().Any(f => f.IsActiveForm)
        || player.Creature.Powers.OfType<ITransformationForm>().Any(f => f.IsActiveForm);

    public static bool PermanentApostate(Player player) => player.Relics.Any(r => r is IPermanentApostasy);

    /// <summary>Праведность недоступна: благословений нет, Искупление закрыто.</summary>
    public static bool Forbidden(Player? player) => player != null && player.Relics.Any(r => r is IPietyForbidder);

    public static Piety Effective(Player? player)
    {
        if (player == null) return Piety.Righteous;
        return PietyRules.Effective(Recorded(player), HoldsAbyssal(player), Transformed(player), PermanentApostate(player));
    }

    public static BlessingStrength Strength(Player? player) => PietyRules.Strength(Effective(player), Forbidden(player));

    public static bool HasHeavenlyShield(Player? player) => SigilOf(player)?.HeavenlyShield == true;

    /// <summary>Кража в лавке, грабёж в событии, дар Князя: −1 ступень.</summary>
    public static void Sin(Player player)
    {
        var sigil = SigilOf(player);
        if (sigil == null) return;
        sigil.Sins++;
        sigil.Set(PietyRules.Sin(sigil.Recorded));
        sigil.Flash();
    }

    /// <summary>Убийство мирного, Тёмный договор: сразу Отступник.</summary>
    public static void GraveSin(Player player)
    {
        var sigil = SigilOf(player);
        if (sigil == null) return;
        sigil.Sins++;
        sigil.Set(PietyRules.GraveSin(sigil.Recorded));
        sigil.Flash();
    }

    public static bool CanRedeem(Player player)
    {
        var sigil = SigilOf(player);
        return sigil != null && PietyRules.CanRedeem(sigil.Recorded, sigil.RedeemedAct - 1, player.RunState.CurrentActIndex, Forbidden(player) || PermanentApostate(player));
    }

    /// <summary>Искупление (пожертвование в Святилище): +1 ступень, раз в акт. Золото списывает вызывающий.</summary>
    public static bool Redeem(Player player)
    {
        if (!CanRedeem(player)) return false;
        var sigil = SigilOf(player)!;
        sigil.RedeemedAct = player.RunState.CurrentActIndex + 1;
        sigil.Set(PietyRules.Redeem(sigil.Recorded));
        sigil.Flash();
        return true;
    }

    /// <summary>Обновить описания реликвий веры (после смены праведности или снаряжения).</summary>
    public static void RefreshAll(Player player)
    {
        SigilOf(player)?.Refresh();
    }
}
