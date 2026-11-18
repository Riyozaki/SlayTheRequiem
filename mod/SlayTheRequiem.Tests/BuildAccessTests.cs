using SlayTheRequiem.SlayTheRequiemCode.Core;
using Xunit;

namespace SlayTheRequiem.Tests;

public sealed class BuildAccessTests
{
    [Fact]
    public void New_regent_has_three_links_without_growing_or_reordering_the_deck()
    {
        string[] native = ["StrikeRegent", "StrikeRegent", "StrikeRegent", "StrikeRegent",
            "DefendRegent", "DefendRegent", "DefendRegent", "DefendRegent", "FallingStar", "Venerate"];
        var occurrences = new Dictionary<string, int>();
        var result = native.Select(name =>
        {
            var n = occurrences.GetValueOrDefault(name); occurrences[name] = n + 1;
            return BuildAccessRules.StarterReplacement("Regent", name, n);
        }).ToArray();
        Assert.Equal(new[] { "Firebolt", "ConjureSpiritWolf", "StrikeRegent", "StrikeRegent", "ArcaneFocus",
            "DefendRegent", "DefendRegent", "DefendRegent", "FallingStar", "Venerate" }, result);
        Assert.Equal(native.Length, result.Length);
        Assert.Equal("Firebolt", BuildAccessRules.StarterReplacement("Regent", "Firebolt", 0));
        Assert.Equal("OtherModCard", BuildAccessRules.StarterReplacement("Regent", "OtherModCard", 0));
        Assert.Equal("StrikeRegent", BuildAccessRules.StarterReplacement("Silent", "StrikeRegent", 0));
    }

    [Theory]
    [InlineData(true, false, WeaponKind.None, true)]
    [InlineData(false, true, WeaponKind.Bow, true)]
    [InlineData(false, true, WeaponKind.Crossbow, true)]
    [InlineData(false, true, WeaponKind.Dagger, false)]
    [InlineData(false, false, WeaponKind.Bow, false)]
    public void Hunter_keeps_Shivs_but_needs_a_weapon_hit_for_bow_bonus(bool shiv, bool weapon, WeaponKind kind, bool expected) =>
        Assert.Equal(expected, BuildAccessRules.HunterBonus(shiv, weapon, kind));

    [Theory]
    [InlineData(false, 0, false)]
    [InlineData(false, 1, false)]
    [InlineData(false, 2, true)]
    [InlineData(true, 1, true)]
    public void Herald_is_not_locked_to_a_random_door(bool tomb, int elites, bool expected) =>
        Assert.Equal(expected, BuildAccessRules.HeraldDue(tomb, elites));

    [Theory]
    [InlineData(FormKind.DarkPact, 0, 5, false, true)]
    [InlineData(FormKind.Werewolf, 4, 5, false, true)]
    [InlineData(FormKind.Vampire, 0, 5, false, false)]
    [InlineData(FormKind.Vampire, 1, 5, false, true)]
    [InlineData(FormKind.Clockwork, 1, 3, true, true)]
    [InlineData(FormKind.Righteous, 1, 3, true, true)]
    [InlineData(FormKind.Lich, 2, 5, false, false)]
    [InlineData(FormKind.Lich, 3, 5, false, true)]
    [InlineData(FormKind.Lich, 1, 3, true, false)]
    [InlineData(FormKind.Lich, 2, 3, true, true)]
    [InlineData(FormKind.None, 0, 5, false, false)]
    [InlineData(FormKind.Herald, -1, 5, false, false)]
    public void Camp_windows_remain_optional_and_do_not_expire(FormKind kind, int act, int total, bool shortRun, bool expected) =>
        Assert.Equal(expected, BuildAccessRules.FormAvailableAtCamp(kind, act, total, shortRun));

    [Theory]
    [InlineData(true, true, 3, true)]
    [InlineData(false, true, 3, false)]
    [InlineData(true, false, 3, false)]
    [InlineData(true, true, 5, false)]
    public void Vanilla_three_acts_are_not_an_explicit_short_route(bool fiveActs, bool shortRun, int count, bool expected) =>
        Assert.Equal(expected, BuildAccessRules.IsShortRoute(fiveActs, shortRun, count));

    [Fact]
    public void Signature_teaching_is_scoped_not_free_refundable_mastery()
    {
        Assert.Equal(2, BuildAccessRules.SpellMastery(2, false, SpellTier.Master));
        Assert.Equal(5, BuildAccessRules.SpellMastery(2, true, SpellTier.Master));
        var signature = SpellAffinity.Evaluate(7, SpellTier.Master, BuildAccessRules.SpellMastery(2, true, SpellTier.Master));
        Assert.True(signature.Castable); Assert.Equal(7, signature.ManaCost); Assert.Equal(1m, signature.EffectMultiplier);
        Assert.False(SpellAffinity.Evaluate(7, SpellTier.Master, 2).Castable);
        Assert.Equal(10, BuildAccessRules.LichWinsRequired(false));
        Assert.Equal(6, BuildAccessRules.LichWinsRequired(true));
        Assert.Equal(1, CampRules.Cost(CampAction.FormRite, false));
        Assert.True(CampRules.CanAfford(CampRules.BasePoints, CampRules.Cost(CampAction.FormRite, false)));
    }
}
