using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlayTheRequiem.SlayTheRequiemCode.Core;

public enum CreationMode { Start, Rebirth }
public enum CreationStage { Origin, Background, Bonus, Review, Applying, Receipt, Complete }
public enum StartingBonus { None, Gold, Potion, RemoveBasic }
public sealed record CreationBaseline(int MaxHp, int Stamina, int Mana, int Capacity, int Gold, int[] Mastery, string[] Deck,
    int StatPointsSpent = 0, int UnspentStatPoints = 0);

/// <summary>
/// Durable decisions, not a claim that native callbacks are atomic. Selectors/back/confirmation do not issue
/// gifts. Each native effect has a separate write-ahead adapter scope. A selector save is resumable; a save
/// inside an interrupted effect retains B1's checkpoint/fail-closed contract. Legacy gifts are never inferred.
/// </summary>
public sealed record HeroCreationPlan(int Version, CreationMode Mode, CreationStage Stage, string Origin,
    string Background, StartingBonus Bonus, string BonusSource, bool BonusApplied, bool IdentityApplied,
    CreationBaseline? Baseline, string SourceFingerprint, string BackgroundSource = "")
{
    public const int CurrentVersion = 1;
    public const int GoldGift = 60;
    public const int MaxLength = 131072;
    private static readonly JsonSerializerOptions Options = new()
    {
        MaxDepth = 16,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
    };

    public static HeroCreationPlan Empty(CreationMode mode) =>
        new(CurrentVersion, mode, CreationStage.Origin, "", "", StartingBonus.None, "", false, false, null, "");

    public HeroCreationPlan ChooseOrigin(string id)
    {
        RequireStage(CreationStage.Origin);
        RequireId(id);
        return this with { Origin = id, Background = "", Bonus = StartingBonus.None, BonusSource = "", BackgroundSource = "", Stage = CreationStage.Background };
    }
    public HeroCreationPlan ChooseBackground(string id)
    {
        RequireStage(CreationStage.Background);
        RequireId(id);
        return this with { Background = id, Bonus = StartingBonus.None, BonusSource = "", BackgroundSource = "",
            Stage = Mode == CreationMode.Start ? CreationStage.Bonus : CreationStage.Review };
    }
    public HeroCreationPlan ChooseBonus(StartingBonus bonus)
    {
        RequireStage(CreationStage.Bonus);
        if (Mode != CreationMode.Start || bonus == StartingBonus.None || !Enum.IsDefined(bonus))
            throw new InvalidOperationException("Invalid starter bonus choice");
        return this with { Bonus = bonus, BonusSource = "", Stage = CreationStage.Review };
    }
    public HeroCreationPlan Back() => Stage switch
    {
        CreationStage.Background when !IdentityApplied => this with { Stage = CreationStage.Origin },
        CreationStage.Bonus when !IdentityApplied => this with { Stage = CreationStage.Background },
        CreationStage.Review => this with { Stage = Mode == CreationMode.Start ? CreationStage.Bonus : CreationStage.Background },
        _ => throw new InvalidOperationException("Committed creation decisions cannot go back"),
    };
    public HeroCreationPlan Confirm(CreationBaseline baseline, string fingerprint = "")
    {
        RequireStage(CreationStage.Review);
        var next = this with { Stage = CreationStage.Applying, Baseline = Baseline ?? baseline, SourceFingerprint = fingerprint };
        next.Validate(); return next;
    }
    /// <summary>Optional background gift selected BEFORE identity effects; "none" is an explicit skip.</summary>
    public HeroCreationPlan PrepareBackgroundSource(string payload)
    {
        RequireStage(CreationStage.Applying);
        if (Mode != CreationMode.Start || IdentityApplied || BackgroundSource.Length != 0 || payload.Length == 0)
            throw new InvalidOperationException("Background source already prepared or identity already committed");
        var next = this with { BackgroundSource = payload }; next.Validate(); return next;
    }
    public HeroCreationPlan IdentityComplete()
    {
        RequireStage(CreationStage.Applying);
        return this with { IdentityApplied = true };
    }
    public HeroCreationPlan PrepareBonusSource(string payload)
    {
        RequireStage(CreationStage.Applying);
        if (Mode != CreationMode.Start || !IdentityApplied || Bonus is StartingBonus.None or StartingBonus.Gold || BonusSource.Length != 0)
            throw new InvalidOperationException("Starter source already prepared or not required");
        var next = this with { BonusSource = payload }; next.Validate(); return next;
    }
    public HeroCreationPlan RetryBonusChoice()
    {
        RequireStage(CreationStage.Applying);
        if (Mode != CreationMode.Start || !IdentityApplied || BonusApplied)
            throw new InvalidOperationException("A committed bonus cannot be exchanged");
        return this with { Stage = CreationStage.Bonus, Bonus = StartingBonus.None, BonusSource = "" };
    }
    public HeroCreationPlan EffectsComplete()
    {
        RequireStage(CreationStage.Applying);
        if (!IdentityApplied) throw new InvalidOperationException("Identity effects are not complete");
        var next = this with { Stage = CreationStage.Receipt, BonusApplied = Mode == CreationMode.Start };
        next.Validate(); return next;
    }
    public HeroCreationPlan Acknowledge()
    {
        RequireStage(CreationStage.Receipt);
        return this with { Stage = CreationStage.Complete };
    }
    public string Encode()
    {
        Validate();
        var encoded = JsonSerializer.Serialize(this, Options);
        if (encoded.Length > MaxLength) throw new FormatException("Oversized hero creation plan");
        return encoded;
    }
    public static HeroCreationPlan Read(string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > MaxLength)
            throw new FormatException("Missing/oversized hero creation plan");
        try
        {
            using var document = JsonDocument.Parse(encoded, new JsonDocumentOptions { MaxDepth = 16 });
            Unique(document.RootElement);
            var plan = JsonSerializer.Deserialize<HeroCreationPlan>(encoded, Options) ?? throw new FormatException("Empty hero creation plan");
            plan.Validate(); return plan;
        }
        catch (JsonException e) { throw new FormatException("Invalid hero creation plan", e); }
    }
    public void Validate()
    {
        if (Version != CurrentVersion || !Enum.IsDefined(Mode) || !Enum.IsDefined(Stage) || !Enum.IsDefined(Bonus)
            || Origin == null || Background == null || BonusSource == null || SourceFingerprint == null || BackgroundSource == null)
            throw new FormatException("Unsupported hero creation plan");
        if (Origin.Length > 0) RequireId(Origin);
        if (Background.Length > 0) RequireId(Background);
        if (Stage >= CreationStage.Background && Origin.Length == 0 || Stage >= CreationStage.Bonus && Background.Length == 0)
            throw new FormatException("Hero creation decision missing");
        if (Mode == CreationMode.Rebirth && (Stage == CreationStage.Bonus || Bonus != StartingBonus.None || BonusApplied || BonusSource.Length > 0 || BackgroundSource.Length > 0))
            throw new FormatException("Rebirth cannot issue another startup gift");
        if (Mode == CreationMode.Start && Stage >= CreationStage.Review && Bonus == StartingBonus.None)
            throw new FormatException("Starter bonus missing");
        if (BonusApplied != (Mode == CreationMode.Start && Stage >= CreationStage.Receipt))
            throw new FormatException("Starter receipt phase disagrees");
        if ((Stage >= CreationStage.Applying || IdentityApplied) && Baseline == null)
            throw new FormatException("Confirmed creation baseline missing");
        if (IdentityApplied && Stage < CreationStage.Bonus || Stage >= CreationStage.Receipt && !IdentityApplied
            || Mode == CreationMode.Rebirth && IdentityApplied && Stage < CreationStage.Applying)
            throw new FormatException("Identity receipt phase disagrees");
        if (Mode == CreationMode.Rebirth && Stage >= CreationStage.Applying
            && (SourceFingerprint.Length != 64 || SourceFingerprint.Any(c => !Uri.IsHexDigit(c)))
            || Mode == CreationMode.Start && SourceFingerprint.Length > 0)
            throw new FormatException("Invalid creation source fingerprint");
        if (BonusSource.Length > 0)
        {
            if (Stage < CreationStage.Applying || !IdentityApplied || Bonus is StartingBonus.None or StartingBonus.Gold || BonusSource.Length > 65536)
                throw new FormatException("Unexpected starter source payload");
            try
            {
                using var document = JsonDocument.Parse(BonusSource, new JsonDocumentOptions { MaxDepth = 12 });
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new FormatException("Starter source must be a complete native object");
                Unique(document.RootElement);
            }
            catch (JsonException e) { throw new FormatException("Invalid starter source payload", e); }
        }
        if (BackgroundSource.Length > 0)
        {
            if (Mode != CreationMode.Start || Stage < CreationStage.Applying && !IdentityApplied || BackgroundSource.Length > 65536)
                throw new FormatException("Unexpected background source payload");
            if (BackgroundSource != "none")
            {
                try
                {
                    using var document = JsonDocument.Parse(BackgroundSource, new JsonDocumentOptions { MaxDepth = 12 });
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                        throw new FormatException("Background source must be an exact native object or an explicit skip");
                    Unique(document.RootElement);
                }
                catch (JsonException e) { throw new FormatException("Invalid background source payload", e); }
            }
        }
        if (Mode == CreationMode.Start && Stage >= CreationStage.Receipt && Bonus != StartingBonus.Gold && BonusSource.Length == 0)
            throw new FormatException("Starter receipt has no prepared source");
        if (Baseline is { } before && (before.MaxHp < 1 || before.Stamina < 0 || before.Mana < 0 || before.Capacity < 0 || before.Gold < 0 || before.StatPointsSpent < 0 || before.UnspentStatPoints < 0
            || before.Mastery == null || before.Mastery.Length != 5 || before.Mastery.Any(n => n is < 0 or > SpellAffinity.MaxMastery)
            || before.Deck == null || before.Deck.Length > 1024 || before.Deck.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 256)))
            throw new FormatException("Invalid hero creation baseline");
    }
    private void RequireStage(CreationStage stage)
    {
        if (Stage != stage) throw new InvalidOperationException("Unexpected hero creation stage");
    }
    private static void RequireId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || id.Any(c => !char.IsLetterOrDigit(c) && c is not ('_' or '-' or '.')))
            throw new FormatException("Invalid saved identity choice ID");
    }
    private static void Unique(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new FormatException("Duplicate hero creation JSON field");
                Unique(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) Unique(item);
    }
}
