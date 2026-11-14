using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Кто участвует в системах ◆/✦/⚖. Некробиндер и Дефект — только облики превращений
/// (docs/design/06 §4), поэтому как персонажи забега они в систему не входят.
/// </summary>
public static class RequiemMode
{
    private static readonly HashSet<string> Participants = new(StringComparer.Ordinal)
    {
        "Ironclad", // Латоносец
        "Silent",   // Безмолвная
        "Regent",   // Регент
    };

    public static bool IsInSystem(Player? player) =>
        RequiemConfig.EnableRequiemSystems && player != null && Participants.Contains(player.Character.GetType().Name);

    public static bool IsInSystem(Creature? creature) => IsInSystem(creature?.Player);

    public static HeroStats StatsFor(Player? player) => HeroStats.ForCharacterTypeName(player?.Character.GetType().Name);
}
