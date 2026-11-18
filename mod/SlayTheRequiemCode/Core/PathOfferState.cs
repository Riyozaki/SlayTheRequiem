namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>A saved boss entitlement, including the original RNG result. Cancel/Continue never rerolls it.</summary>
public static class PathOfferState
{
    public static string Write(IEnumerable<HeroPath> paths)
    {
        var encoded = "1|" + string.Join(",", paths.Select(p => p.ToString()));
        _ = Read(encoded); return encoded;
    }
    public static IReadOnlyList<HeroPath> Read(string encoded)
    {
        if (string.IsNullOrEmpty(encoded) || encoded.Length > 256 || !encoded.StartsWith("1|", StringComparison.Ordinal))
            throw new FormatException("Unsupported saved Path offer");
        var names = encoded[2..].Split(',');
        var paths = new List<HeroPath>();
        foreach (var name in names)
        {
            if (!Enum.TryParse<HeroPath>(name, out var path) || !Enum.IsDefined(path) || path == HeroPath.None
                || path.ToString() != name || paths.Contains(path)) throw new FormatException("Invalid/duplicate saved Path choice");
            paths.Add(path);
        }
        if (paths.Count is < 1 or > TransformationRules.PathOffers) throw new FormatException("Invalid saved Path offer count");
        return paths.AsReadOnly();
    }
}
