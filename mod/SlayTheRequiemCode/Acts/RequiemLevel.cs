using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Acts;

/// <summary>Уровень «Реквием» забега (<see cref="RequiemLevelRules"/>): настройка или Возвышение + 1.</summary>
public static class RequiemLevel
{
    public static int Of(IRunState? run) =>
        RequiemLevelRules.Resolve(RequiemConfig.RequiemLevelSetting, run?.AscensionLevel ?? 0);

    public static int Of(Player? player) => Of(player?.RunState);
}

/// <summary>Общее для отряда состояние актов (хранится в Дорожных картах героев; берётся объединение по игрокам).</summary>
public static class ActRunState
{
    public static IEnumerable<ActLedger> Records(IRunState? run) =>
        run?.Players.Select(ActLedger.Of).OfType<ActLedger>() ?? [];

    /// <summary>Врата Бездны / открытые склепы / повёрнутые вентили — у кого-нибудь из отряда.</summary>
    public static bool AnyHas(IRunState? run, Func<ActLedger, int[]> list, int code) => Records(run).Any(r => r.Has(list(r), code));

    /// <summary>Побеждённые супербоссы (бит <see cref="Superboss"/>).</summary>
    public static int SuperbossMask(IRunState? run) => Records(run).Aggregate(0, (m, r) => m | r.SuperbossMask);

    /// <summary>Портал этого акта принят хотя бы одним героем.</summary>
    public static bool PortalAccepted(IRunState? run, int actIndex) => Records(run).Any(r => (r.PortalMask & (1 << actIndex)) != 0);

    /// <summary>Портал этого акта уже предлагался (ответ получен).</summary>
    public static bool PortalAsked(IRunState? run, int actIndex) => Records(run).Any(r => (r.PortalAskedMask & (1 << actIndex)) != 0);

    /// <summary>Редкая элита этого акта уже встречена.</summary>
    public static bool RareEliteMet(IRunState? run, int actIndex) => Records(run).Any(r => (r.RareEliteMask & (1 << actIndex)) != 0);

    /// <summary>Невеста Владыки побеждена в этом акте (−10 % ♥ Владыке).</summary>
    public static bool BrideDefeated(IRunState? run, int actIndex) => Records(run).Any(r => (r.BrideMask & (1 << actIndex)) != 0);
}
