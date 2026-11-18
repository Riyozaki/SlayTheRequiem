namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Cross-field save contract, checked before native setters or reload-counter writes.</summary>
public sealed record HeroCreationSnapshot(int Protocol, int Phase, bool DeclaredComplete, bool Legacy,
    string Mastery, string Start, string Rebirth, HeroPath Path, bool Interrupted = false)
{
    public void Validate(int saveSchema)
    {
        if (Protocol is < 0 or > StartupState.Protocol || Phase is < 0 or > StartupState.Complete
            || !Enum.IsDefined(Path) || Mastery == null || Start == null || Rebirth == null)
            throw new FormatException("Unsupported saved hero creation metadata");
        if (Protocol < StartupState.Protocol)
        {
            if (saveSchema >= 4 || Mastery.Length > 0 || Start.Length > 0 || Rebirth.Length > 0 || Legacy)
                throw new FormatException("Mixed legacy/current hero creation schemas");
            if (!DeclaredComplete || Protocol == 1 && Phase != StartupState.Complete)
                throw new FormatException("Unfinished legacy identity has no safe one-shot receipt. Use the retained original with its matching build/checkpoint; no gifts guessed or replayed");
            return;
        }
        if (saveSchema < 4) throw new FormatException("Hero creation protocol requires Requiem save schema 4 or newer");
        var sources = MasterySources.Read(Mastery);
        if (Legacy)
        {
            if (sources.IdentityKnown || Start.Length > 0 || Rebirth.Length > 0 || !DeclaredComplete || Phase != StartupState.Complete)
                throw new FormatException("Invalid legacy startup compatibility receipt");
            return;
        }
        if (!sources.IdentityKnown) throw new FormatException("A current wizard cannot use opaque legacy identity accounting");
        var start = HeroCreationPlan.Read(Start);
        if (start.Mode != CreationMode.Start || (start.Stage == CreationStage.Complete) != DeclaredComplete
            || Phase != (DeclaredComplete ? StartupState.Complete : 0))
            throw new FormatException("Startup phase/declaration disagrees with its saved wizard");
        if (Rebirth.Length == 0)
        {
            if (Path == HeroPath.Reborn) throw new FormatException("Source-accounted Reborn Path has no decision/receipt");
            return;
        }
        var rebirth = HeroCreationPlan.Read(Rebirth);
        if (!DeclaredComplete || rebirth.Mode != CreationMode.Rebirth || !sources.IdentityKnown)
            throw new FormatException("Rebirth without a source-accounted completed hero");
        if (rebirth.Stage >= CreationStage.Receipt && Path != HeroPath.Reborn
            || rebirth.Stage < CreationStage.Receipt && Path != HeroPath.None && !(Interrupted && Path == HeroPath.Reborn))
            throw new FormatException("Rebirth receipt and Path ownership disagree");
    }
}
