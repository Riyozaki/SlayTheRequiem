using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Pantheon;

/// <summary>
/// Осколок отлива (docs/design/05 §1): берётся у «Шести камней у воды» в актах 2–3, действует до конца акта.
/// Держать можно один; на повторной встрече — заменить. Плюс и минус вместе — <see cref="OmenRules.ShardBoon"/>.
/// </summary>
public abstract class EbbShardRelic : BoonRelic
{
    public abstract EbbShard Kind { get; }
    protected override Boon ComputeBoon() => OmenRules.ShardBoon(Kind);

    private int _act;  // акт получения + 1

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ObtainedAct
    {
        get => _act;
        set
        {
            AssertMutable();
            _act = value;
        }
    }

    protected override Task OnObtained()
    {
        ObtainedAct = Owner.RunState.CurrentActIndex + 1;
        return Task.CompletedTask;
    }

    /// <summary>Новый акт — осколок рассыпается.</summary>
    public override async Task AfterActEntered()
    {
        if (_act > 0 && Owner.RunState.CurrentActIndex + 1 != _act) await RemoveSelf();
    }

    public static EbbShardRelic? Of(Player? player) => player?.Relics.OfType<EbbShardRelic>().FirstOrDefault();

    public static readonly Type[] All =
    [
        typeof(EarthShard), typeof(RootShard), typeof(BeastShard), typeof(WaterShard), typeof(WindShard), typeof(SunShard),
    ];

    public static RelicModel Create(EbbShard shard) => ModelDb.GetById<RelicModel>(ModelDb.GetId(All[(int)shard])).ToMutable();
}

/// <summary>Земли: +1 пул ◆, Латы +1; −1 карта в первой руке.</summary>
[Pool(typeof(SharedRelicPool))] public sealed class EarthShard : EbbShardRelic { public override EbbShard Kind => EbbShard.Earth; }

/// <summary>Корня: сопр. магии I, ядам II; уязвимость к огню.</summary>
[Pool(typeof(SharedRelicPool))] public sealed class RootShard : EbbShardRelic { public override EbbShard Kind => EbbShard.Root; }

/// <summary>Зверя: атаки +2, Пробитие 1; физический урон по вам +1.</summary>
[Pool(typeof(SharedRelicPool))] public sealed class BeastShard : EbbShardRelic { public override EbbShard Kind => EbbShard.Beast; }

/// <summary>Воды: +3 пул ✦, +1 прирост ✦; уязвимость к разряду.</summary>
[Pool(typeof(SharedRelicPool))] public sealed class WaterShard : EbbShardRelic { public override EbbShard Kind => EbbShard.Water; }

/// <summary>Ветра: +1 карта в первой руке, +1 прирост ◆; Латы −1.</summary>
[Pool(typeof(SharedRelicPool))] public sealed class WindShard : EbbShardRelic { public override EbbShard Kind => EbbShard.Wind; }

/// <summary>Солнца: +8 ♥, Регенерация 1; уязвимость к холоду.</summary>
[Pool(typeof(SharedRelicPool))] public sealed class SunShard : EbbShardRelic { public override EbbShard Kind => EbbShard.Sun; }
