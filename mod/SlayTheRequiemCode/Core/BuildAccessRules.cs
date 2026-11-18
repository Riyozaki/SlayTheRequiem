namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Small, explicit access policies for the nine release build directions. Not a combat simulator.
/// Starting-deck mapping is used only by new inventory construction; Continue/Reborn never apply it.</summary>
public static class BuildAccessRules
{
    public const int PactEliteWinsForHerald = 2;
    public const int ShortLichWinsRequired = 6;

    /// <summary>Occurrence is zero-based among cards of the same native type. Preserve count/order/other mods' cards.</summary>
    public static string StarterReplacement(string? hero, string nativeType, int occurrence) =>
        (hero, nativeType, occurrence) switch
        {
            ("Regent", "StrikeRegent", 0) => "Firebolt",
            ("Regent", "StrikeRegent", 1) => "ConjureSpiritWolf",
            ("Regent", "DefendRegent", 0) => "ArcaneFocus",
            _ => nativeType,
        };

    public static bool HunterBonus(bool shiv, bool weaponAttack, WeaponKind equipped) =>
        shiv || weaponAttack && equipped is WeaponKind.Bow or WeaponKind.Crossbow;

    public static bool HeraldDue(bool tombGuardian, int eliteWins) =>
        tombGuardian || eliteWins >= PactEliteWinsForHerald;

    public static bool IsShortRoute(bool fiveActsEnabled, bool shortRun, int actCount) =>
        fiveActsEnabled && shortRun && actCount == ActRules.ShortRunActs;

    public static int LichWinsRequired(bool shortRun) =>
        shortRun ? ShortLichWinsRequired : TransformationRules.LichWinsRequired;

    /// <summary>No expiry on an optional camp opportunity. Compatibility/piety/earned chain steps are checked separately.
    /// Explicit short 1→3→5 uses route slots; a vanilla three-act run is not silently treated as short.</summary>
    public static bool FormAvailableAtCamp(FormKind kind, int actIndex, int totalActs, bool shortRun)
    {
        if (actIndex < 0 || totalActs <= 0 || actIndex >= totalActs) return false;
        var slot = shortRun ? actIndex * 2 : actIndex;
        return kind switch
        {
            FormKind.Werewolf or FormKind.DarkPact or FormKind.Herald => true,
            FormKind.Righteous or FormKind.Clockwork or FormKind.Vampire => slot >= 1,
            FormKind.Lich => TransformationRules.IsLateAct(actIndex, totalActs),
            _ => false,
        };
    }

    /// <summary>The chosen Element Lord signature is taught by its essence, not a refundable school/stat grant.
    /// No other Master spell, school, scroll, Reborn or signature from another element inherits this competency.</summary>
    public static int SpellMastery(int regular, bool chosenElementSignature, SpellTier tier) =>
        Math.Clamp(chosenElementSignature ? Math.Max(regular, (int)tier) : regular, 0, SpellAffinity.MaxMastery);
}
