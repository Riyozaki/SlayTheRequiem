namespace SlayTheRequiem.SlayTheRequiemCode.Core;

public enum ReaperMove { Scythe, Raise, Cry }

/// <summary>Approved D-07 combat schedule/state, shared by real move tables and pure regressions.</summary>
public static class BossTimingRules
{
    public const int WandererGuardPlating = 3;
    public const int WandererGuardResistance = 1;
    public const int ReaperCycleLength = 4;

    public static bool ThresholdReached(int hp, int maxHp, int percent) =>
        maxHp > 0 && hp > 0 && percent > 0 && (long)hp * 100 <= (long)maxHp * percent;

    /// <summary>Waiting twice never stacks a second stance; any Attack played by the party ends it after that play.</summary>
    public static bool WandererGuardsAfterTurn(bool partyPlayedAttack) => !partyPlayedAttack;

    /// <summary>One-based turn: scythe, scythe, raise, cry. Raise 3/7/11, cry 4/8/12; no stealth difficulty increase.</summary>
    public static ReaperMove ReaperMoveAt(int turn)
    {
        if (turn < 1) throw new ArgumentOutOfRangeException(nameof(turn));
        return ((turn - 1) % ReaperCycleLength) switch
        {
            2 => ReaperMove.Raise,
            3 => ReaperMove.Cry,
            _ => ReaperMove.Scythe,
        };
    }
}
