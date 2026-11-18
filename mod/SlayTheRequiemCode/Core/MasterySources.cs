using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>A positive contribution, not an already capped display value. Only stat purchases are refundable.</summary>
public sealed record MasteryGrant(string Source, SpellSchool School, int Levels, int PaidPoints = 0);

/// <summary>
/// Provenance for permanent school mastery. Identity/skill contributions may exceed the visible cap together;
/// removing a stat investment must never erase a treatise, a learned node or another source hidden by that cap.
/// Legacy counters are preserved as opaque, non-refundable values: their original allocation cannot be guessed.
/// Immutable encoded snapshots are suitable for a SavedProperty and do not share mutable collections on clone.
/// </summary>
public sealed class MasterySources
{
    public const int Version = 1;
    public const int MaxEntries = 96;
    public const int MaxEncodedLength = 65536;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12,
        RespectRequiredConstructorParameters = true,
    };
    private sealed record Envelope(int Version, bool IdentityKnown, List<MasteryGrant> Grants);
    private readonly List<MasteryGrant> _grants;
    public bool IdentityKnown { get; }
    public IReadOnlyList<MasteryGrant> Grants => _grants.AsReadOnly();
    public int StatPoints => _grants.Sum(g => g.PaidPoints);

    private MasterySources(bool identityKnown, IEnumerable<MasteryGrant> grants)
    {
        IdentityKnown = identityKnown;
        _grants = grants.ToList();
        Validate();
        _grants.Sort((a, b) =>
        {
            var order = StringComparer.Ordinal.Compare(a.Source, b.Source);
            return order != 0 ? order : a.School.CompareTo(b.School);
        });
    }

    public static MasterySources ForNewHero(string hero) => hero is "Ironclad" or "Silent" or "Regent"
        ? new(true, Enum.GetValues<SpellSchool>().Select(s => new MasteryGrant("base", s, SpellAffinity.StartingMastery(hero, s))).Where(g => g.Levels > 0))
        : throw new ArgumentException("Unsupported Requiem hero", nameof(hero));

    public static MasterySources PreserveLegacy(IReadOnlyList<int> levels)
    {
        ValidateVector(levels);
        return new(false, Enum.GetValues<SpellSchool>().Where(s => levels[(int)s] > 0)
            .Select(s => new MasteryGrant("legacy", s, levels[(int)s])));
    }

    public int Level(SpellSchool school)
    {
        ValidateSchool(school);
        return Math.Min(SpellAffinity.MaxMastery, _grants.Where(g => g.School == school).Sum(g => g.Levels));
    }

    public void RequireCounters(IReadOnlyList<int> levels)
    {
        ValidateVector(levels);
        if (Enum.GetValues<SpellSchool>().Any(s => Level(s) != levels[(int)s]))
            throw new FormatException("Saved mastery counters disagree with their source journal; no inferred refund");
    }

    /// <summary>Check accounting before a refund; no reconstruction from the already capped counters.</summary>
    public void RequireHero(string hero, IReadOnlySet<Skill> learned, int statMastery)
    {
        if (statMastery < 0 || (IdentityKnown ? StatPoints != statMastery : StatPoints > statMastery))
            throw new FormatException("Paid mastery journal disagrees with invested stat points");
        if (IdentityKnown)
        {
            var basis = ForNewHero(hero);
            foreach (var school in Enum.GetValues<SpellSchool>())
                if (_grants.Where(g => g.Source == "base" && g.School == school).Sum(g => g.Levels) != basis.Level(school))
                    throw new FormatException("Mastery journal has a different hero base");
        }
        foreach (var skill in Enum.GetValues<Skill>())
        {
            var present = _grants.Any(g => g.Source == "skill:" + skill);
            var expected = learned.Contains(skill) && SkillTree.SchoolOf(SkillTree.Node(skill).Branch) != null;
            if (present && !expected || IdentityKnown && present != expected)
                throw new FormatException("Mastery journal disagrees with retained learned nodes");
        }
    }

    public bool IsIdentitySchool(SpellSchool school)
    {
        ValidateSchool(school);
        return _grants.Any(g => g.School == school && g.Source is "base" or "origin" or "background");
    }

    public MasterySources WithIdentity(IReadOnlyList<int> origin, IReadOnlyList<int> background)
    {
        if (!IdentityKnown) throw new InvalidOperationException("Legacy mastery has no reconstructible identity sources");
        ValidateVector(origin, 1); ValidateVector(background, 1);
        var next = _grants.Where(g => g.Source is not ("origin" or "background")).ToList();
        foreach (var school in Enum.GetValues<SpellSchool>())
        {
            if (origin[(int)school] > 0) next.Add(new("origin", school, origin[(int)school]));
            if (background[(int)school] > 0) next.Add(new("background", school, background[(int)school]));
        }
        return new(true, next);
    }

    public bool TryRaiseAcquired(SpellSchool school, out MasterySources next) => TryRaise("acquired", school, 0, out next);
    public bool TryBuyStat(SpellSchool school, int paidPoints, out MasterySources next)
    {
        if (paidPoints is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(paidPoints));
        return TryRaise("stat", school, paidPoints, out next);
    }

    private bool TryRaise(string source, SpellSchool school, int paidPoints, out MasterySources next)
    {
        next = this;
        if (Level(school) >= SpellAffinity.MaxMastery) return false;
        var prior = _grants.SingleOrDefault(g => g.Source == source && g.School == school);
        var grants = _grants.Where(g => !ReferenceEquals(g, prior)).ToList();
        grants.Add(new(source, school, (prior?.Levels ?? 0) + 1, (prior?.PaidPoints ?? 0) + paidPoints));
        next = new(IdentityKnown, grants);
        return true;
    }

    /// <summary>A node is its own idempotent source, even when its mastery is temporarily hidden by the cap.</summary>
    public MasterySources WithSkill(Skill skill)
    {
        var node = SkillTree.Node(skill);
        if (SkillTree.SchoolOf(node.Branch) is not { } school) return this;
        var source = "skill:" + skill;
        return _grants.Any(g => g.Source == source) ? this : new(IdentityKnown, _grants.Append(new(source, school, 1)));
    }

    public MasterySources WithoutStatPurchases() => new(IdentityKnown, _grants.Where(g => g.Source != "stat"));

    public string Encode() => JsonSerializer.Serialize(new Envelope(Version, IdentityKnown, _grants), JsonOptions);

    public static MasterySources Read(string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > MaxEncodedLength)
            throw new FormatException("Missing or oversized mastery source journal");
        try
        {
            using var document = JsonDocument.Parse(encoded, new JsonDocumentOptions { MaxDepth = 12 });
            RequireUniqueFields(document.RootElement);
            var envelope = JsonSerializer.Deserialize<Envelope>(encoded, JsonOptions);
            if (envelope == null || envelope.Version != Version || envelope.Grants == null)
                throw new FormatException("Unsupported mastery source journal");
            return new(envelope.IdentityKnown, envelope.Grants);
        }
        catch (JsonException e) { throw new FormatException("Invalid mastery source journal", e); }
    }

    private void Validate()
    {
        if (_grants.Count > MaxEntries || _grants.Any(g => g == null)) throw new FormatException("Invalid mastery source count");
        var keys = new HashSet<(string, SpellSchool)>();
        foreach (var grant in _grants)
        {
            ValidateSchool(grant.School);
            if (grant.Levels is < 1 or > SpellAffinity.MaxMastery || !keys.Add((grant.Source, grant.School)))
                throw new FormatException("Invalid/duplicate mastery contribution");
            if (grant.Source == "stat")
            {
                if (grant.PaidPoints < grant.Levels || grant.PaidPoints > grant.Levels * 2)
                    throw new FormatException("Invalid paid mastery contribution");
            }
            else if (grant.PaidPoints != 0) throw new FormatException("Only stat purchases can refund points");
            if (grant.Source is "origin" or "background" && grant.Levels != 1)
                throw new FormatException("Invalid identity mastery contribution");
            if (grant.Source?.StartsWith("skill:", StringComparison.Ordinal) == true)
            {
                var name = grant.Source[6..];
                if (!Enum.TryParse<Skill>(name, out var skill) || !Enum.IsDefined(skill) || skill.ToString() != name
                    || SkillTree.SchoolOf(SkillTree.Node(skill).Branch) != grant.School || grant.Levels != 1)
                    throw new FormatException("Invalid skill mastery contribution");
            }
            else if (grant.Source is not ("base" or "origin" or "background" or "stat" or "acquired" or "legacy"))
                throw new FormatException("Unknown mastery contribution source");
        }
        if (IdentityKnown && _grants.Any(g => g.Source == "legacy")
            || !IdentityKnown && _grants.Any(g => g.Source is "base" or "origin" or "background"))
            throw new FormatException("Ambiguous mastery identity provenance");
    }

    private static void ValidateSchool(SpellSchool school)
    {
        if (!Enum.IsDefined(school)) throw new FormatException("Invalid mastery school");
    }
    private static void ValidateVector(IReadOnlyList<int> levels, int maximum = SpellAffinity.MaxMastery)
    {
        if (levels == null || levels.Count != Enum.GetValues<SpellSchool>().Length || levels.Any(n => n < 0 || n > maximum))
            throw new FormatException("Invalid mastery counter vector");
    }
    private static void RequireUniqueFields(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new FormatException("Duplicate mastery JSON field");
                RequireUniqueFields(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RequireUniqueFields(item);
    }
}
