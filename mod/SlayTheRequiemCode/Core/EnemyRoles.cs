namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Role of an individual enemy, NOT the reward/difficulty rank of its room.</summary>
public enum EnemyRole { Normal, Elite, Boss, Minion, Prop }

/// <summary>Explicit role for mod monsters; vanilla models use the shipped monster table plus native secondary status.</summary>
public interface IEnemyRoleSource
{
    EnemyRole CombatRole { get; }
}

public static class EnemyRoleRules
{
    public static EnemyRole Resolve(EnemyRole declared, bool secondary) =>
        declared == EnemyRole.Prop ? EnemyRole.Prop : secondary ? EnemyRole.Minion : declared;

    public static EnemyRank Rank(EnemyRole role) => role switch
    {
        EnemyRole.Boss => EnemyRank.Boss,
        EnemyRole.Elite => EnemyRank.Elite,
        _ => EnemyRank.Normal,
    };

    public static bool UsesResources(EnemyRole role) => role != EnemyRole.Prop;
    public static bool ScalesHp(EnemyRole role) => role != EnemyRole.Prop;
    public static bool CanControl(EnemyRole role) => role is not (EnemyRole.Boss or EnemyRole.Prop);
    public static bool CanExecute(EnemyRole role) => role is not (EnemyRole.Boss or EnemyRole.Prop);
    public static bool CanRaise(EnemyRole role) => role is not (EnemyRole.Boss or EnemyRole.Prop);
    public static bool RollsAffixes(EnemyRole role) => role is EnemyRole.Normal or EnemyRole.Elite;

    public static EnemyRole Parse(string value) => value switch
    {
        "" or "normal" => EnemyRole.Normal, // old tables stay readable, but never inherit a boss room's rank
        "elite" => EnemyRole.Elite,
        "boss" => EnemyRole.Boss,
        "minion" => EnemyRole.Minion,
        "prop" => EnemyRole.Prop,
        _ => throw new FormatException("Unknown enemy role: " + value),
    };
}
