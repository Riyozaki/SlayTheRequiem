namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Persisted startup decisions are separate from the declaration of completed legacy grants.</summary>
public static class StartupState
{
    public const int Protocol = 1;
    public const int Complete = 4;
    public static bool IsComplete(int phase, bool originGranted) => phase == Complete && originGranted;
    public static bool NeedsLegacyMigration(int protocol) => protocol == 0;
    // Old builds recorded only OriginGranted. Honor their declared completion without replaying one-shot
    // HP/mastery/removal effects. A saved pending operation is independently blocked by InventoryGate.
    public static int LegacyPhase(bool originGranted) => originGranted ? Complete : 0;
    public static bool ShouldCountVisit(string recorded, string current, bool countsAsNode) =>
        recorded != current && countsAsNode;
}
