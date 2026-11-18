namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Permanent identity mastery, separate from one-shot starter cards, potions, items and gifts.</summary>
public static class HeroIdentityRules
{
    public static IReadOnlyList<string> OriginTypes(string hero) => hero switch
    {
        "Ironclad" => ["FrostbornOrigin", "ForgebloodOrigin", "DunebladeOrigin", "CitadelOrigin"],
        "Silent" => ["MoonbornOrigin", "ThicketbornOrigin", "MarshbornOrigin"],
        "Regent" => ["HighbornOrigin", "MistbornOrigin", "AshbornOrigin"],
        _ => [],
    };

    /// <summary>Combine source deltas before clamping once. Separate loss/gain clamps could create max HP.</summary>
    public static int ReconciledHpDelta(int currentMaxHp, int oldBoon, int newBoon, int identityDelta = 0)
    {
        if (currentMaxHp < 1) throw new ArgumentOutOfRangeException(nameof(currentMaxHp));
        return Math.Max(checked(newBoon - oldBoon + identityDelta), 1 - currentMaxHp);
    }

    public static int[] OriginMastery(string? origin) => Bonus(origin switch
    {
        "HighbornOrigin" => SpellSchool.Destruction,
        "MistbornOrigin" => SpellSchool.Restoration,
        _ => null,
    });

    public static int[] BackgroundMastery(string? background) => Bonus(background switch
    {
        "FrostbinderBackground" or "ThunderShamanBackground" or "MushroomTowerBackground" => SpellSchool.Destruction,
        "DawnBrotherBackground" or "DuskSingerBackground" or "VenomweaverBackground" => SpellSchool.Restoration,
        "CitadelMageBackground" => SpellSchool.Alteration,
        "SummonerBackground" => SpellSchool.Conjuration,
        "ShadowSorcererBackground" => SpellSchool.Illusion,
        _ => null,
    });

    /// <summary>Actual pre-B2 hooks, not the broader design table. Do not invent missing old grants on Continue.</summary>
    public static SpellSchool? LegacyBackgroundMastery(string? background) => background switch
    {
        "ThunderShamanBackground" => SpellSchool.Destruction,
        "DawnBrotherBackground" or "DuskSingerBackground" => SpellSchool.Restoration,
        "CitadelMageBackground" => SpellSchool.Alteration,
        "ShadowSorcererBackground" => SpellSchool.Illusion,
        _ => null,
    };

    private static int[] Bonus(SpellSchool? school)
    {
        var values = new int[Enum.GetValues<SpellSchool>().Length];
        if (school is { } s) values[(int)s] = 1;
        return values;
    }
}
