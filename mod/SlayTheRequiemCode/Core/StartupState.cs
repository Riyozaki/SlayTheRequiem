namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Persisted startup decisions are separate from the declaration of completed legacy grants.</summary>
public static class StartupState
{
    public const int Protocol = 2;
    public const int Complete = 4;
    public const string RulesKey = "hero_creation";
    // Control strings in the EXISTING RunSettingsMessage wire layout. Old BaseLib builds cannot decode an
    // unknown custom-message type, so negotiate before Ready without adding/sending a new message class.
    public const string LobbyPrefix = "!STR/hero-creation/";
    public const string LobbyQuery = LobbyPrefix + "query";
    public static string LobbyOffer => LobbyPrefix + "host/" + Protocol.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public static string LobbyAgreement => LobbyPrefix + "peer/" + Protocol.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public static void RequireHost(int protocol)
    {
        if (protocol != Protocol) throw new InvalidOperationException("Host did not advertise the current hero creation protocol; update the same mod build on every peer");
    }

    // The rules envelope remains v1 for saved-run compatibility. A NEW lobby must explicitly agree to the
    // wizard's choice-stream protocol on both sides; legacy Random clients cannot silently execute another stream.
    public static void StampNewRules(RunSettingsData data) => data.Extensions[RulesKey] = Protocol.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public static void RequireNewRules(RunSettingsData data)
    {
        if (!data.Extensions.TryGetValue(RulesKey, out var value) || value != Protocol.ToString(System.Globalization.CultureInfo.InvariantCulture))
            throw new FormatException("Incompatible hero creation protocol; use the same mod build on every peer");
    }
    public static void RequirePeers(IEnumerable<ulong> peers, IReadOnlyDictionary<ulong, int> protocols)
    {
        if (peers.Any(id => !protocols.TryGetValue(id, out var protocol) || protocol != Protocol))
            throw new InvalidOperationException("A peer has not agreed to this hero creation protocol; update the same mod build and ready again");
    }
    public static bool IsComplete(int phase, bool originGranted) => phase == Complete && originGranted;
    public static bool NeedsLegacyMigration(int protocol) => protocol < Protocol;
    // Old builds recorded only OriginGranted. Honor their declared completion without replaying one-shot
    // HP/mastery/removal effects. A saved pending operation is independently blocked by InventoryGate.
    public static int LegacyPhase(bool originGranted) => originGranted ? Complete : 0;
    public static bool ShouldCountVisit(string recorded, string current, bool countsAsNode) =>
        recorded != current && countsAsNode;
}
