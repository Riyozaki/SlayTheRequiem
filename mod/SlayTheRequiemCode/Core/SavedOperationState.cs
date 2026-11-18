namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Do not silently import a foreign/legacy permanent Block with no compatible recovery contract.</summary>
public static class SavedOperationState
{
    public static void Validate(int schema, int protocol, bool blocked, string? pending, int currentSchema, int currentProtocol)
    {
        if (schema < 0 || schema > currentSchema || protocol < 0 || protocol > currentProtocol)
            throw new FormatException("Unsupported Requiem save/protocol; use the matching build, original retained");
        if ((schema < currentSchema || protocol < currentProtocol) && (blocked || !string.IsNullOrEmpty(pending)))
            throw new FormatException("Legacy save has an unresolved inventory operation without this build's recovery contract. "
                + "No automatic unlock or permanent-block import: retain the original/raw backup and restore a known-safe copy with its matching build");
    }
}
